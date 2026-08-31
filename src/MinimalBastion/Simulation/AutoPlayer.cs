using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Enemies;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Simulation;

public sealed class AutoPlayer
{
    private readonly AutoPlayerStrategy _strategy;
    private readonly Random _random;
    private readonly List<Vector2> _placementCandidates;
    private readonly string? _forcedTowerId;
    private readonly string? _forcedDoctrineId;
    private readonly string? _forcedSpecializationId;
    private readonly bool _useProtocols;
    private readonly bool _useApexUpgrades;
    private readonly bool _holdBuild;
    private readonly bool _holdFootprint;
    private readonly WavePlan? _wavePlan;
    private int _salesWave = -1;
    private int _salesThisWave;
    private int _directEmergencyPurchasesThisWave;

    public AutoPlayer(GameSession session, AutoPlayerStrategy strategy, int seed, SimulationOptions? options = null)
    {
        _strategy = strategy;
        _random = new Random(seed);
        _placementCandidates = BuildPlacementCandidates(session);
        _forcedTowerId = options?.ForcedTowerId;
        _forcedDoctrineId = options?.ForcedDoctrineId;
        _forcedSpecializationId = options?.ForcedSpecializationId;
        _useProtocols = options?.UseProtocols ?? true;
        _useApexUpgrades = options?.UseApexUpgrades ?? true;
        _holdBuild = options?.HoldBuild ?? false;
        _holdFootprint = options?.HoldFootprint ?? false;
        _wavePlan = options?.WavePlan;
    }

    public void PrepareForWave(GameSession session)
    {
        _directEmergencyPurchasesThisWave = 0;
        if (_holdBuild) return;
        var threat = ThreatProfile.From(session.Waves.NextWave, session.Content.Enemies);
        if (_strategy == AutoPlayerStrategy.Experienced)
            ConfigureExperiencedTargeting(session, threat, duringWave: false);
        if (!_holdFootprint)
        {
            TryRebalance(session, threat);
            ManageGenerator(session);
        }
        Spend(session, threat, duringWave: false, session.IsFinalCampaignAct ? 48 : 24);
    }

    public void ReactDuringWave(GameSession session)
    {
        if (_holdBuild) return;
        var threat = ThreatProfile.From(session.Waves.ActiveWave, session.Content.Enemies);
        if (_strategy == AutoPlayerStrategy.Experienced)
            ConfigureExperiencedTargeting(session, threat, duringWave: true);
        if (_useProtocols) TryUseOverdrive(session, threat);
        if (_strategy == AutoPlayerStrategy.Experienced && ExperiencedPursuesApex(session))
            TryRebalance(session, threat);
        TryUseEmergencyDefense(session);
        Spend(session, threat, duringWave: true, 2);
    }

    private void Spend(GameSession session, ThreatProfile threat, bool duringWave, int actionLimit)
    {
        for (var action = 0; action < actionLimit; action++)
        {
            var reserve = ReserveCredits(session, duringWave);
            if (_strategy == AutoPlayerStrategy.Experienced && duringWave && IsFinalCampaignWave(session) &&
                PlanParameter("finalPlateReserve", 1f, 0f, 1f) >= 0.5f &&
                session.Towers.Any(tower => tower.IsApex) && session.EmergencyInventory <= 0)
                reserve = Math.Max(reserve, session.CurrentEmergencyDirectPurchaseCost);
            var spendable = session.Economy.Credits - reserve;
            if (spendable < 50) return;

            var foundation = FoundationSize();
            var combatTowerCount = session.Towers.Count(x => !x.IsSupport);
            if (!_holdFootprint && session.CurrentWave == 0 && combatTowerCount < foundation &&
                TryBuyFoundation(session, threat, spendable, duringWave))
                continue;

            var awaitingExperiencedApex = _strategy == AutoPlayerStrategy.Experienced &&
                                           ExperiencedPursuesApex(session) &&
                                           session.Towers.Count(tower => tower.IsApex) < ExperiencedApexLimit();
            if (awaitingExperiencedApex)
            {
                if (TryExperiencedLateInvestment(session, threat, spendable, duringWave)) continue;
                if (session.Towers.Any(session.CanApexUpgrade)) return;
            }

            if (_strategy == AutoPlayerStrategy.Experienced && !IsFinalCampaignWave(session) &&
                TryExperiencedMilestoneUpgrade(session, threat, spendable, duringWave))
                continue;

            if (!_holdFootprint && (!session.IsFinalCampaignAct || _strategy == AutoPlayerStrategy.Experienced))
            {
                if (TryBuyStrategicPriority(session, threat, spendable, duringWave, out var savingForPriority))
                    continue;
                if (savingForPriority) return;
            }

            var suppressFinalFill = _strategy == AutoPlayerStrategy.Experienced && duringWave &&
                                    IsFinalCampaignWave(session) && session.Towers.Any(tower => tower.IsApex) &&
                                    PlanParameter("finalRoleFill", 1f, 0f, 1f) < 0.5f;
            var purchase = _holdFootprint || suppressFinalFill ? null : BestPurchase(session, threat, spendable);
            var upgrade = BestUpgrade(session, threat, spendable);
            var targetWave = ActiveOrNextWave(session);
            var purchaseBias = combatTowerCount < DesiredTowerCount(targetWave)
                ? 1.45f
                : IsFinalCampaignWave(session) ? 0.16f : 0.38f;
            purchaseBias *= EconomyProfileMultiplier(purchasing: true);
            purchaseBias *= PlanParameter("purchaseBias", 1f, 0.2f, 3f);
            var buyScore = purchase?.Score * purchaseBias ?? float.MinValue;
            var upgradeScore = upgrade?.Score * UpgradeBias() * EconomyProfileMultiplier(purchasing: false) *
                               PlanParameter("upgradeBias", 1f, 0.2f, 3f) ?? float.MinValue;

            if (buyScore <= 0 && upgradeScore <= 0) return;
            if (purchase is { } buy && buyScore >= upgradeScore)
            {
                if (!session.TryPlaceTower(buy.Definition.Id, buy.Position)) return;
                ConfigureTargeting(session, session.Towers[^1], threat, duringWave);
                continue;
            }

            if (upgrade is { } up)
            {
                var upgraded = up.DoctrineId is not null
                    ? session.TryChooseTowerDoctrine(up.Tower.Id, up.DoctrineId)
                    : up.SpecializationId is not null
                        ? session.TrySpecializeTower(up.Tower.Id, up.SpecializationId)
                        : session.TryUpgradeTower(up.Tower.Id);
                if (upgraded)
                {
                    ConfigureTargeting(session, up.Tower, threat, duringWave);
                    continue;
                }
            }
            return;
        }
    }

    private bool TryBuyFoundation(GameSession session, ThreatProfile threat, int spendable, bool duringWave)
    {
        var ids = _strategy switch
        {
            AutoPlayerStrategy.AntiSwarm => new[] { "needle_turret", "needle_turret", "shard_fan" },
            AutoPlayerStrategy.AntiArmor => new[] { "needle_turret", "needle_turret", "needle_turret" },
            AutoPlayerStrategy.LongRange => new[] { "needle_turret", "needle_turret", "watchtower" },
            AutoPlayerStrategy.Control => new[] { "needle_turret", "needle_turret", "needle_turret", "frost_spire" },
            AutoPlayerStrategy.Synergy => new[] { "needle_turret", "needle_turret", "needle_turret", "frost_spire" },
            AutoPlayerStrategy.Experienced => new[] { "needle_turret", "needle_turret", "needle_turret" },
            AutoPlayerStrategy.Randomized => session.Content.Towers.Values.Where(x => session.IsTowerAvailable(x.Id) && !x.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && x.PurchaseCost <= 250).OrderBy(_ => _random.Next()).Select(x => x.Id).ToArray(),
            _ => new[] { "needle_turret", "shard_fan" }
        };

        var combatTowerCount = session.Towers.Count(x => !x.IsSupport);
        for (var offset = 0; offset < ids.Length; offset++)
        {
            var id = ids[(combatTowerCount + offset) % ids.Length];
            if (!session.Content.Towers.TryGetValue(id, out var definition) || definition.PurchaseCost > spendable) continue;
            var position = FindBestPosition(session, definition, threat);
            if (position is null || !session.TryPlaceTower(id, position.Value)) continue;
            ConfigureTargeting(session, session.Towers[^1], threat, duringWave);
            return true;
        }
        return false;
    }

    private bool TryBuyStrategicPriority(
        GameSession session,
        ThreatProfile threat,
        int spendable,
        bool duringWave,
        out bool savingForPriority)
    {
        savingForPriority = false;
        var wave = Math.Max(1, session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);
        if (_strategy == AutoPlayerStrategy.Experienced)
            return TryBuyExperiencedPriority(session, threat, spendable, wave, duringWave, out savingForPriority);

        string[]? ids = null;
        var desired = 0;

        switch (_strategy)
        {
            case AutoPlayerStrategy.Conservative:
                ids = new[] { "watchtower", "frost_spire", "breaker_cannon" };
                desired = 1 + wave / 3;
                break;
            case AutoPlayerStrategy.Economy:
                ids = new[] { "watchtower" };
                desired = 1 + wave / 4;
                break;
            case AutoPlayerStrategy.Aggressive:
                ids = new[] { "shard_fan", "watchtower", "prism_beam" };
                desired = 1 + wave / 3;
                break;
            case AutoPlayerStrategy.UpgradeFocused:
                ids = new[] { "watchtower", "breaker_cannon", "prism_beam" };
                desired = 1 + wave / 5;
                break;
            case AutoPlayerStrategy.AntiSwarm:
                ids = new[] { "shard_fan", "arc_relay", "siege_mortar", "breaker_cannon" };
                desired = 2 + wave / 2;
                break;
            case AutoPlayerStrategy.AntiArmor:
                ids = new[] { "breaker_cannon", "watchtower", "prism_beam" };
                desired = 2 + wave / 2;
                break;
            case AutoPlayerStrategy.LongRange:
                ids = new[] { "watchtower", "siege_mortar", "prism_beam" };
                desired = 2 + wave / 2;
                break;
            case AutoPlayerStrategy.Control:
                ids = new[] { "frost_spire", "ember_coil", "arc_relay", "breaker_cannon" };
                desired = 2 + wave / 2;
                break;
            case AutoPlayerStrategy.Synergy:
                ids = wave switch
                {
                    < 6 => new[] { "needle_turret", "frost_spire" },
                    < 8 => new[] { "needle_turret", "frost_spire", "arc_relay" },
                    < 10 => new[] { "needle_turret", "frost_spire", "arc_relay", "breaker_cannon" },
                    < 14 => new[] { "needle_turret", "frost_spire", "arc_relay", "breaker_cannon", "prism_beam" },
                    < 18 => new[] { "needle_turret", "frost_spire", "arc_relay", "breaker_cannon", "prism_beam", "ember_coil", "siege_mortar" },
                    _ => new[] { "needle_turret", "frost_spire", "arc_relay", "breaker_cannon", "prism_beam", "ember_coil", "siege_mortar", "watchtower" }
                };
                desired = 4 + wave * 3 / 4;
                break;
            case AutoPlayerStrategy.Tactical:
                ids = new[] { "frost_spire", "watchtower", "breaker_cannon" };
                desired = 1 + wave / 3;
                break;
            case AutoPlayerStrategy.Adaptive:
                if (threat.Armored >= 0.18f)
                {
                    ids = new[] { "breaker_cannon", "watchtower" };
                    desired = 2 + wave / 3;
                }
                else if (threat.Fast >= 0.22f)
                {
                    ids = new[] { "watchtower", "frost_spire" };
                    desired = 2 + wave / 3;
                }
                else if (threat.Swarm >= 0.55f)
                {
                    ids = new[] { "shard_fan", "arc_relay", "siege_mortar" };
                    desired = 2 + wave / 3;
                }
                break;
        }

        if (ids is not null)
        {
            ids = StrategicPoolForWave(ids, wave).Where(session.IsTowerAvailable).ToArray();
            if (ids.Length > 0)
            {
                var missingIdentity = ids.Where(id => session.Towers.All(x => !x.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (missingIdentity.Length > 0)
                {
                    if (TryBuyFromPool(session, threat, spendable, missingIdentity, duringWave)) return true;
                    savingForPriority = MustSaveForPool(session, missingIdentity);
                    return false;
                }

                if (session.Towers.Count(x => ids.Contains(x.Definition.Id, StringComparer.OrdinalIgnoreCase)) >= desired)
                    goto SupportPriority;
                var underrepresented = UnderrepresentedPool(session, ids);
                if (TryBuyFromPool(session, threat, spendable, underrepresented, duringWave)) return true;
                savingForPriority = MustSaveForPool(session, underrepresented);
                return false;
            }
        }

    SupportPriority:
        var wantsSupport = _strategy is AutoPlayerStrategy.Conservative or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.Control or AutoPlayerStrategy.Synergy or AutoPlayerStrategy.Adaptive;
        var combatTowers = session.Towers.Count(x => !x.IsSupport);
        var desiredBeacons = combatTowers >= 12 ? 2 : combatTowers >= 6 ? 1 : 0;
        if (wantsSupport && session.Towers.Count(x => x.Definition.Id == "signal_beacon") < desiredBeacons)
        {
            var supportPool = new[] { "signal_beacon" };
            if (TryBuyFromPool(session, threat, spendable, supportPool, duringWave)) return true;
            savingForPriority = MustSaveForPool(session, supportPool);
        }

        return false;
    }

    private bool TryBuyExperiencedPriority(
        GameSession session,
        ThreatProfile threat,
        int spendable,
        int wave,
        bool duringWave,
        out bool savingForPriority)
    {
        savingForPriority = false;
        foreach (var plan in ExperiencedRolePlan(wave))
        {
            var existingRole = session.Towers.Where(tower => tower.Definition.Id == plan.Id).ToArray();
            if (session.Waves.IsActive && IsFinalCampaignWave(session) && session.Towers.Any(tower => tower.IsApex) &&
                PlanParameter("finalRoleFill", 1f, 0f, 1f) < 0.5f && existingRole.Length < plan.Count)
                continue;
            if (!session.IsTowerAvailable(plan.Id) || existingRole.Length >= plan.Count ||
                existingRole.Any(tower => tower.LevelIndex < 2) ||
                !session.Content.Towers.TryGetValue(plan.Id, out var definition)) continue;
            if (definition.PurchaseCost <= spendable)
            {
                var position = FindBestPosition(session, definition, threat);
                if (position is not null && session.TryPlaceTower(definition.Id, position.Value))
                {
                    ConfigureTargeting(session, session.Towers[^1], threat, duringWave);
                    return true;
                }
            }

            if (plan.Urgent && session.Economy.Credits < definition.PurchaseCost)
            {
                savingForPriority = true;
                return false;
            }
        }

        var combatTowers = session.Towers.Count(tower => !tower.IsSupport);
        var desiredBeacons = Math.Min(
            ExperiencedRoleCount("signal_beacon", wave),
            combatTowers >= 15 ? 2 : combatTowers >= 7 ? 1 : 0);
        var beaconCount = session.Towers.Count(tower => tower.Definition.Id == "signal_beacon");
        if (beaconCount < desiredBeacons && session.IsTowerAvailable("signal_beacon") &&
            session.Content.Towers.TryGetValue("signal_beacon", out var beacon))
        {
            var position = FindBestPosition(session, beacon, threat);
            if (position is not null && ExperiencedSupportPositionScore(session, beacon, position.Value) >= 12f)
            {
                if (beacon.PurchaseCost <= spendable && session.TryPlaceTower(beacon.Id, position.Value)) return true;
                if (session.Economy.Credits < beacon.PurchaseCost) savingForPriority = true;
            }
        }

        return false;
    }

    private static IReadOnlyList<(string Id, int Count, bool Urgent)> ExperiencedRolePlan(int wave)
    {
        var order = wave switch
        {
            <= 10 => new[] { "shard_fan", "breaker_cannon", "ember_coil", "prism_beam", "needle_turret" },
            <= 13 => new[] { "frost_spire", "needle_turret", "shard_fan", "breaker_cannon", "ember_coil", "prism_beam" },
            <= 19 => new[] { "prism_beam", "frost_spire", "breaker_cannon", "ember_coil", "shard_fan", "siege_mortar", "needle_turret" },
            _ => new[] { "prism_beam", "frost_spire", "breaker_cannon", "siege_mortar", "needle_turret", "ember_coil", "shard_fan" }
        };
        return order
            .Select(id => (Id: id, Count: ExperiencedRoleCount(id, wave), Urgent: false))
            .Where(plan => plan.Count > 0)
            .ToArray();
    }

    private static int ExperiencedRoleCount(string towerId, int wave) => towerId switch
    {
        "needle_turret" => wave switch
        {
            >= 30 => 14,
            >= 29 => 13,
            >= 28 => 12,
            >= 12 => 11,
            >= 11 => 10,
            >= 10 => 9,
            >= 9 => 8,
            >= 8 => 7,
            >= 7 => 6,
            >= 6 => 5,
            >= 5 => 4,
            _ => 3
        },
        "shard_fan" => wave >= 15 ? 2 : wave >= 3 ? 1 : 0,
        "breaker_cannon" => wave >= 28 ? 5 : wave >= 25 ? 4 : wave >= 21 ? 3 : wave >= 16 ? 2 : wave >= 5 ? 1 : 0,
        "ember_coil" => wave >= 17 ? 2 : wave >= 8 ? 1 : 0,
        "frost_spire" => wave >= 28 ? 7 : wave >= 25 ? 6 : wave >= 23 ? 5 : wave >= 21 ? 4 : wave >= 19 ? 3 : wave >= 16 ? 2 : wave >= 12 ? 1 : 0,
        "prism_beam" => wave >= 25 ? 5 : wave >= 22 ? 4 : wave >= 18 ? 3 : wave >= 15 ? 2 : wave >= 9 ? 1 : 0,
        "siege_mortar" => wave >= 23 ? 3 : wave >= 21 ? 2 : wave >= 15 ? 1 : 0,
        "signal_beacon" => wave >= 18 ? 2 : wave >= 12 ? 1 : 0,
        _ => 0
    };

    private static bool MustSaveForPool(GameSession session, IReadOnlyList<string> ids)
    {
        var costs = ids.Where(id => session.IsTowerAvailable(id) && session.Content.Towers.ContainsKey(id)).Select(id => session.Content.Towers[id].PurchaseCost).ToArray();
        return costs.Length > 0 && session.Economy.Credits < costs.Min();
    }

    private bool TryBuyFromPool(
        GameSession session,
        ThreatProfile threat,
        int spendable,
        IReadOnlyList<string> ids,
        bool duringWave)
    {
        foreach (var id in ids)
        {
            if (!session.IsTowerAvailable(id) || !session.Content.Towers.TryGetValue(id, out var definition) || definition.PurchaseCost > spendable) continue;
            var position = FindBestPosition(session, definition, threat);
            if (position is null) continue;
            if (!session.TryPlaceTower(definition.Id, position.Value)) continue;
            ConfigureTargeting(session, session.Towers[^1], threat, duringWave);
            return true;
        }
        return false;
    }

    private static string[] UnderrepresentedPool(GameSession session, IReadOnlyList<string> ids)
    {
        var counts = ids.Select(id => (Id: id, Count: session.Towers.Count(x => x.Definition.Id == id))).ToArray();
        var minimum = counts.Min(x => x.Count);
        return counts.Where(x => x.Count == minimum).Select(x => x.Id).ToArray();
    }

    private PurchaseOption? BestPurchase(GameSession session, ThreatProfile threat, int spendable)
    {
        PurchaseOption? best = null;
        foreach (var definition in session.Content.Towers.Values)
        {
            if (!session.IsTowerAvailable(definition.Id) || !IsAllowedPurchase(definition.Id)) continue;
            if (definition.PurchaseCost > spendable) continue;
            if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && session.Towers.Count(x => !x.IsSupport) < 4) continue;
            var existingCopies = session.Towers.Count(x => x.Definition.Id == definition.Id);
            if (_strategy == AutoPlayerStrategy.Experienced && existingCopies > 0 &&
                session.Towers.Any(tower => tower.Definition.Id == definition.Id && tower.LevelIndex < 2))
                continue;
            var copyLimit = _strategy == AutoPlayerStrategy.Spam ? 30 : definition.Id switch
            {
                "needle_turret" => 7,
                "signal_beacon" => 3,
                _ => 10
            };
            if (_strategy == AutoPlayerStrategy.Experienced)
                copyLimit = ExperiencedCopyLimit(definition.Id,
                    Math.Max(1, session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1));
            if (existingCopies >= copyLimit) continue;
            var position = FindBestPosition(session, definition, threat);
            if (position is null) continue;
            var positionScore = PlacementScore(session, definition, position.Value);
            if (_strategy == AutoPlayerStrategy.Experienced && definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) &&
                positionScore < 12f)
                continue;
            var score = TowerValue(definition, 0, threat) * StrategyWeight(definition.Id, threat) / definition.PurchaseCost;
            score *= 0.65f + MathF.Min(1.4f, positionScore / 14f);
            var repetitionPenalty = _strategy == AutoPlayerStrategy.Spam ? 0.08f :
                _strategy == AutoPlayerStrategy.Experienced ? ExperiencedRepetitionPenalty(definition.Id) : 0.35f;
            score /= 1f + existingCopies * repetitionPenalty;
            score *= _strategy == AutoPlayerStrategy.Experienced
                ? 0.9975f + (float)_random.NextDouble() * 0.005f
                : 0.96f + (float)_random.NextDouble() * 0.08f;
            if (_strategy == AutoPlayerStrategy.Randomized) score *= 0.55f + (float)_random.NextDouble();
            if (best is null || score > best.Value.Score) best = new PurchaseOption(definition, position.Value, score);
        }
        return best;
    }

    private string[] StrategicPoolForWave(IEnumerable<string> ids, int wave)
    {
        return ids.Where(id => id switch
        {
            "arc_relay" => wave >= 6,
            "siege_mortar" => wave >= 8,
            "prism_beam" => wave >= 10,
            "breaker_cannon" when _strategy is AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Control or AutoPlayerStrategy.Tactical => wave >= 8,
            _ => true
        }).ToArray();
    }

    private bool IsAllowedPurchase(string towerId)
    {
        return _strategy switch
        {
            AutoPlayerStrategy.Economy => towerId is "needle_turret" or "watchtower" or "signal_beacon",
            AutoPlayerStrategy.Aggressive => towerId is "needle_turret" or "shard_fan" or "watchtower" or "arc_relay" or "prism_beam",
            AutoPlayerStrategy.UpgradeFocused => towerId is "needle_turret" or "watchtower" or "breaker_cannon" or "prism_beam" or "signal_beacon",
            AutoPlayerStrategy.Spam => towerId is "needle_turret" or "shard_fan" or "frost_spire" or "watchtower" or "ember_coil",
            AutoPlayerStrategy.AntiSwarm => towerId is "needle_turret" or "shard_fan" or "frost_spire" or "arc_relay" or "siege_mortar" or "breaker_cannon",
            AutoPlayerStrategy.AntiArmor => towerId is "needle_turret" or "watchtower" or "breaker_cannon" or "prism_beam" or "signal_beacon",
            AutoPlayerStrategy.LongRange => towerId is "needle_turret" or "watchtower" or "siege_mortar" or "prism_beam" or "signal_beacon",
            AutoPlayerStrategy.Control => towerId is "needle_turret" or "frost_spire" or "ember_coil" or "arc_relay" or "breaker_cannon" or "signal_beacon",
            AutoPlayerStrategy.Synergy => towerId is "needle_turret" or "frost_spire" or "arc_relay" or "breaker_cannon" or "prism_beam" or "ember_coil" or "siege_mortar" or "watchtower" or "signal_beacon",
            AutoPlayerStrategy.Tactical => towerId is "needle_turret" or "frost_spire" or "watchtower" or "breaker_cannon" or "signal_beacon",
            AutoPlayerStrategy.Experienced => true,
            _ => true
        };
    }

    private UpgradeOption? BestUpgrade(GameSession session, ThreatProfile threat, int spendable)
    {
        UpgradeOption? best = null;
        foreach (var tower in session.Towers)
        {
            if (_strategy == AutoPlayerStrategy.Experienced && IsFinalCampaignWave(session) &&
                tower.Definition.Id == "needle_turret" && tower.LevelIndex < 2 &&
                session.Towers.Count(candidate => candidate.Definition.Id == "needle_turret") >= 14 &&
                session.Towers.Any(candidate => candidate.IsApex))
                continue;
            var current = UpgradeValue(session, tower, tower.Level, threat);
            if (tower.RequiresDoctrine)
            {
                TowerDoctrineDefinition? selectedDoctrine = null;
                var selectedFit = float.MinValue;
                var upgradePace = 0f;
                var doctrineCandidates = tower.Definition.Tier2Doctrines.Where(x => x.UpgradeCost <= spendable);
                if (IsForcedTower(tower) && _forcedDoctrineId is not null)
                    doctrineCandidates = doctrineCandidates.Where(doctrine => doctrine.Id.Equals(_forcedDoctrineId, StringComparison.OrdinalIgnoreCase));
                if (_strategy == AutoPlayerStrategy.Experienced &&
                    ExperiencedPreferredDoctrine(session, tower) is { } preferredDoctrine)
                    doctrineCandidates = doctrineCandidates.Where(doctrine => doctrine.Id == preferredDoctrine);
                foreach (var doctrine in doctrineCandidates)
                {
                    var next = tower.Definition.Levels[1].WithDoctrine(doctrine);
                    var immediateGain = MathF.Max(0.01f, UpgradeValue(session, tower, next, threat) - current);
                    var immediateGainPerCredit = immediateGain / doctrine.UpgradeCost;
                    var finalGainPerCredit = tower.Definition.Specializations.Count == 0
                        ? immediateGainPerCredit
                        : tower.Definition.Specializations.Max(specialization =>
                            MathF.Max(0.01f,
                                UpgradeValue(session, tower, specialization.Level.WithDoctrine(doctrine), threat) - current) /
                            (doctrine.UpgradeCost + specialization.UpgradeCost));

                    // A doctrine is both an immediate tier-two upgrade and a commitment
                    // to one of several final builds. Preserve short-term discipline while
                    // allowing deliberate sidegrades (for example Frost Bolt) to be chosen
                    // when their completed build fits the current threat profile.
                    var doctrineValue = immediateGainPerCredit * 0.72f + finalGainPerCredit * 0.28f;
                    var doctrineWeight = DoctrineWeight(doctrine, threat);
                    var diversity = _strategy == AutoPlayerStrategy.Experienced
                        ? ExperiencedDoctrineDiversity(session, tower, doctrine.Id)
                        : 1f;
                    var fit = doctrineValue * doctrineWeight * diversity;
                    if (fit > selectedFit)
                    {
                        selectedFit = fit;
                        selectedDoctrine = doctrine;
                    }

                    // Branch foresight must not make the tower jump ahead of unrelated
                    // purchases. Retain the immediate-value upgrade cadence that the
                    // baseline balance matrix was tuned against.
                    upgradePace = MathF.Max(upgradePace, immediateGainPerCredit * doctrineWeight * diversity);
                }
                if (selectedDoctrine is not null)
                    Consider(new UpgradeOption(tower, selectedDoctrine.Id, null,
                        upgradePace * StrategyWeight(tower.Definition.Id, threat)));
                continue;
            }
            if (tower.RequiresSpecialization)
            {
                var specializationCandidates = tower.Definition.Specializations.Where(x => x.UpgradeCost <= spendable);
                if (IsForcedTower(tower) && _forcedSpecializationId is not null)
                    specializationCandidates = specializationCandidates.Where(specialization => specialization.Id.Equals(_forcedSpecializationId, StringComparison.OrdinalIgnoreCase));
                if (_strategy == AutoPlayerStrategy.Experienced &&
                    ExperiencedPreferredSpecialization(session, tower) is { } preferredSpecialization)
                    specializationCandidates = specializationCandidates.Where(specialization => specialization.Id == preferredSpecialization);
                foreach (var specialization in specializationCandidates)
                {
                    var next = UpgradeValue(session, tower, specialization.Level.WithDoctrine(tower.Doctrine), threat);
                    Consider(new UpgradeOption(tower, null, specialization.Id,
                        MathF.Max(0.01f, next - current) * StrategyWeight(tower.Definition.Id, threat) *
                        SpecializationWeight(tower.Definition.Id, specialization.Id, threat) *
                        (_strategy == AutoPlayerStrategy.Experienced
                            ? ExperiencedSpecializationDiversity(session, tower, specialization.Id)
                            : 1f) /
                        specialization.UpgradeCost));
                }
                continue;
            }
            var apexAllowed = _strategy != AutoPlayerStrategy.Experienced ||
                              ExperiencedPursuesApex(session) &&
                              session.Towers.Count(candidate => candidate.IsApex) < ExperiencedApexLimit();
            if (_useApexUpgrades && apexAllowed && session.CanApexUpgrade(tower) &&
                tower.ApexUpgradeCost <= spendable)
            {
                var apexNext = UpgradeValue(session, tower, tower.ApexPreviewLevel, threat);
                Consider(new UpgradeOption(tower, null, null,
                    (MathF.Max(0.01f, apexNext - current) +
                     ApexProtocolValue(session, tower, tower.ApexPreviewLevel, threat)) *
                    StrategyWeight(tower.Definition.Id, threat) /
                    tower.ApexUpgradeCost));
                continue;
            }
            if (!tower.CanUpgrade || tower.UpgradeCost > spendable) continue;
            var linearNext = UpgradeValue(session, tower, tower.Definition.Levels[tower.LevelIndex + 1], threat);
            Consider(new UpgradeOption(tower, null, null,
                MathF.Max(0.01f, linearNext - current) * StrategyWeight(tower.Definition.Id, threat) / tower.UpgradeCost));
        }
        return best;

        void Consider(UpgradeOption option)
        {
            var score = option.Score * (_strategy == AutoPlayerStrategy.Experienced
                ? 0.998f + (float)_random.NextDouble() * 0.004f
                : 0.98f + (float)_random.NextDouble() * 0.04f);
            if (_strategy == AutoPlayerStrategy.Experienced)
                score *= ExperiencedUpgradeBreadth(session, option.Tower);
            if (_strategy == AutoPlayerStrategy.Randomized) score *= 0.6f + (float)_random.NextDouble();
            option = option with { Score = score };
            if (best is null || score > best.Value.Score) best = option;
        }
    }

    private bool TryExperiencedMilestoneUpgrade(
        GameSession session,
        ThreatProfile threat,
        int spendable,
        bool duringWave)
    {
        var wave = Math.Max(1,
            session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);
        var milestones = new (int Wave, string TowerId, int Level)[]
        {
            (3, "frost_spire", 1),
            (4, "shard_fan", 1),
            (6, "breaker_cannon", 1),
            (7, "arc_relay", 1),
            (8, "prism_beam", 1),
            (9, "breaker_cannon", 2),
            (10, "prism_beam", 2),
            (11, "frost_spire", 2),
            (12, "arc_relay", 2)
        };

        foreach (var milestone in milestones)
        {
            if (wave < milestone.Wave) continue;
            var tower = session.Towers
                .Where(candidate => candidate.Definition.Id == milestone.TowerId && candidate.LevelIndex < milestone.Level)
                .OrderByDescending(candidate => PlacementScore(session, candidate.Definition, candidate.Position))
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            if (tower is null) continue;
            var upgraded = TryUpgradeByFit(session, tower, threat, spendable);
            if (upgraded) ConfigureTargeting(session, tower, threat, duringWave);
            return upgraded;
        }

        return false;
    }

    private bool TryExperiencedLateInvestment(
        GameSession session,
        ThreatProfile threat,
        int spendable,
        bool duringWave)
    {
        if (!_useApexUpgrades || !ExperiencedPursuesApex(session)) return false;
        var apexCount = session.Towers.Count(tower => tower.IsApex);
        var apexLimit = ExperiencedApexLimit();
        if (apexCount >= apexLimit) return false;
        var candidate = ExperiencedApexCandidate(session, threat);
        if (candidate is null || candidate.Value.Tower.ApexUpgradeCost > spendable ||
            !session.TryUpgradeTower(candidate.Value.Tower.Id)) return false;
        ConfigureTargeting(session, candidate.Value.Tower, threat, duringWave);
        return true;
    }

    private ApexInvestmentOption? ExperiencedApexCandidate(GameSession session, ThreatProfile threat)
    {
        var rankedCandidates = session.Towers
            .Where(session.CanApexUpgrade)
            .Select(tower => new
            {
                Tower = tower,
                Score = (MathF.Max(0.01f,
                             UpgradeValue(session, tower, tower.ApexPreviewLevel, threat) -
                             UpgradeValue(session, tower, tower.Level, threat)) +
                         ApexProtocolValue(session, tower, tower.ApexPreviewLevel, threat)) *
                        StrategyWeight(tower.Definition.Id, threat) *
                        (0.75f + MathF.Min(1.25f,
                            PlacementScore(session, tower.Definition, tower.Position) / 18f)) *
                         ExperiencedApexSupportMultiplier(session, tower) /
                         Math.Max(1, tower.ApexUpgradeCost)
            })
            .OrderByDescending(choice => choice.Score)
            .ThenBy(choice => choice.Tower.ApexUpgradeCost)
            .ThenBy(choice => choice.Tower.Id)
            .ToArray();
        var apexCandidate = (int)PlanParameter("apexCandidate", 6, 0, 63);
        var candidate = rankedCandidates.Length == 0
            ? null
            : rankedCandidates[Math.Min(apexCandidate, rankedCandidates.Length - 1)];
        return candidate is null ? null : new ApexInvestmentOption(candidate.Tower, candidate.Score);
    }

    private static float ExperiencedApexSupportMultiplier(GameSession session, TowerInstance tower)
    {
        var attackSpeedBonus = 0f;
        var rangeBonus = 0f;
        foreach (var support in session.Towers.Where(candidate => candidate.IsSupport))
        {
            var auraRange = session.GetEffectiveAuraRange(support);
            if (Vector2.DistanceSquared(tower.Position, support.Position) > auraRange * auraRange) continue;
            attackSpeedBonus = MathF.Max(attackSpeedBonus, support.EffectiveAuraAttackSpeedBonus);
            rangeBonus = MathF.Max(rangeBonus, support.EffectiveAuraTowerRangeBonus);
        }
        return 1f + attackSpeedBonus * 5f + rangeBonus * 2.2f;
    }

    private int ExperiencedApexLimit() => (int)PlanParameter("apexLimit",
        _wavePlan is not null && PlanProfile(_wavePlan.EconomyProfileId, "apex") ? 2 : 1,
        0, 4);

    private static string? ExperiencedPreferredDoctrine(GameSession session, TowerInstance tower)
    {
        var completed = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.DoctrineId is not null);
        return tower.Definition.Id switch
        {
            "needle_turret" when completed >= 11 => "needle_cycler",
            "needle_turret" => session.Towers.Count(candidate => candidate.Definition.Id == tower.Definition.Id &&
                                                       candidate.DoctrineId == "needle_cycler") <=
                               session.Towers.Count(candidate => candidate.Definition.Id == tower.Definition.Id &&
                                                       candidate.DoctrineId == "needle_calibrator")
                ? "needle_cycler"
                : "needle_calibrator",
            "shard_fan" => "shard_scatter",
            "breaker_cannon" => completed is 0 or 3 ? "breaker_bored" : "breaker_repeater",
            "ember_coil" => completed == 0 ? "ember_hot_core" : "ember_kindling",
            "frost_spire" => completed == 5 ? "frost_ice_needle" : "frost_deep_chill",
            "prism_beam" => completed < 2 ? "prism_aperture" : "prism_frequency",
            "siege_mortar" => "mortar_survey",
            "signal_beacon" => completed == 0 ? "beacon_amplifier" : "beacon_repeater",
            _ => null
        };
    }

    private static string? ExperiencedPreferredSpecialization(GameSession session, TowerInstance tower)
    {
        var completed = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.SpecializationId is not null);
        var preferred = tower.Definition.Id switch
        {
            "needle_turret" => completed == 0 ? "rapid_array" : "rail_pin",
            "shard_fan" => "lance_fan",
            "breaker_cannon" => completed == 3 ? "breach_round" : "shatter_shell",
            "ember_coil" => completed == 0 ? "searing_brand" : "wildfire_matrix",
            "frost_spire" => completed == 5 ? "hail_lancer" : "permafrost",
            "prism_beam" => completed == 0 ? "core_lance" : "spectrum_split",
            "siege_mortar" => completed == 2 ? "salvo_rack" : "quake_shell",
            "signal_beacon" => completed == 0 ? "tempo_beacon" : "horizon_beacon",
            _ => null
        };
        return preferred;
    }

    private static float ExperiencedDoctrineDiversity(
        GameSession session,
        TowerInstance tower,
        string doctrineId)
    {
        var peers = session.Towers.Where(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.Id != tower.Id).ToArray();
        var same = peers.Count(candidate => candidate.DoctrineId == doctrineId);
        var other = peers.Count(candidate => candidate.DoctrineId is not null && candidate.DoctrineId != doctrineId);
        var diversity = MathHelper.Clamp((1f + other * 0.28f) / (1f + same * 0.38f), 0.55f, 1.55f);
        var completed = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.DoctrineId is not null);
        var chosen = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.DoctrineId == doctrineId);
        var preference = (tower.Definition.Id, doctrineId) switch
        {
            ("shard_fan", "shard_scatter") => 2.2f,
            ("shard_fan", _) => 0.55f,
            ("frost_spire", "frost_ice_needle") => completed >= 5 && chosen == 0 ? 2.8f : 0.35f,
            ("frost_spire", "frost_deep_chill") => completed < 5 ||
                                                       session.Towers.Any(candidate => candidate.Definition.Id == tower.Definition.Id &&
                                                           candidate.DoctrineId == "frost_ice_needle")
                ? 1.8f
                : 0.75f,
            ("prism_beam", "prism_aperture") => chosen < 2 ? 1.9f : 0.45f,
            ("prism_beam", "prism_frequency") => completed >= 2 ? 1.9f : 0.65f,
            ("siege_mortar", "mortar_survey") => 2.2f,
            ("siege_mortar", _) => 0.55f,
            _ => 1f
        };
        return diversity * preference;
    }

    private static float ExperiencedSpecializationDiversity(
        GameSession session,
        TowerInstance tower,
        string specializationId)
    {
        var peers = session.Towers.Where(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.Id != tower.Id).ToArray();
        var same = peers.Count(candidate => candidate.SpecializationId == specializationId);
        var other = peers.Count(candidate => candidate.SpecializationId is not null &&
                                             candidate.SpecializationId != specializationId);
        var diversity = MathHelper.Clamp((1f + other * 0.20f) / (1f + same * 0.30f), 0.60f, 1.45f);
        var completed = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.SpecializationId is not null);
        var chosen = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.SpecializationId == specializationId);
        var preference = (tower.Definition.Id, specializationId) switch
        {
            ("needle_turret", "rapid_array") => completed == 0 ? 3.0f : 0.22f,
            ("needle_turret", "rail_pin") => completed == 0 ? 0.85f : 2.1f,
            ("shard_fan", "lance_fan") => 2.8f,
            ("shard_fan", _) => 0.45f,
            ("breaker_cannon", "breach_round") => completed >= 3 && chosen == 0 ? 2.6f : 0.45f,
            ("breaker_cannon", "shatter_shell") => completed < 3 ||
                                                         session.Towers.Any(candidate => candidate.Definition.Id == tower.Definition.Id &&
                                                             candidate.SpecializationId == "breach_round")
                ? 2.0f
                : 0.70f,
            ("frost_spire", "hail_lancer") => completed >= 5 && chosen == 0 ? 2.8f : 0.30f,
            ("frost_spire", "permafrost") => completed < 5 ||
                                                  session.Towers.Any(candidate => candidate.Definition.Id == tower.Definition.Id &&
                                                      candidate.SpecializationId == "hail_lancer")
                ? 2.0f
                : 0.65f,
            ("prism_beam", "core_lance") => completed == 0 ? 2.6f : 0.35f,
            ("prism_beam", "spectrum_split") => completed == 0 ? 0.80f : 2.2f,
            ("siege_mortar", "quake_shell") => completed < 2 ? 2.4f : 0.65f,
            ("siege_mortar", "salvo_rack") => completed >= 2 && chosen == 0 ? 2.5f : 0.55f,
            ("signal_beacon", "tempo_beacon") => completed == 0 ? 2.4f : 0.75f,
            ("signal_beacon", "horizon_beacon") => completed == 0 ? 0.65f : 2.2f,
            _ => 1f
        };
        return diversity * preference;
    }

    private bool TryUpgradeByFit(GameSession session, TowerInstance tower, ThreatProfile threat, int spendable)
    {
        if (tower.RequiresDoctrine)
        {
            var doctrineCandidates = tower.Definition.Tier2Doctrines
                .Where(candidate => candidate.UpgradeCost <= spendable);
            if (_strategy == AutoPlayerStrategy.Experienced &&
                ExperiencedPreferredDoctrine(session, tower) is { } preferredDoctrine)
                doctrineCandidates = doctrineCandidates.Where(candidate => candidate.Id == preferredDoctrine);
            var doctrine = doctrineCandidates
                .Select(candidate => new
                {
                    Definition = candidate,
                    Score = UpgradeValue(session, tower, tower.Definition.Levels[1].WithDoctrine(candidate), threat) *
                            DoctrineWeight(candidate, threat) *
                            (_strategy == AutoPlayerStrategy.Experienced
                                ? ExperiencedDoctrineDiversity(session, tower, candidate.Id)
                                : 1f) /
                            Math.Max(1, candidate.UpgradeCost)
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Definition.Id)
                .FirstOrDefault();
            return doctrine is not null && session.TryChooseTowerDoctrine(tower.Id, doctrine.Definition.Id);
        }

        if (tower.RequiresSpecialization)
        {
            var specializationCandidates = tower.Definition.Specializations
                .Where(candidate => candidate.UpgradeCost <= spendable);
            if (_strategy == AutoPlayerStrategy.Experienced &&
                ExperiencedPreferredSpecialization(session, tower) is { } preferredSpecialization)
                specializationCandidates = specializationCandidates.Where(candidate => candidate.Id == preferredSpecialization);
            var specialization = specializationCandidates
                .Select(candidate => new
                {
                    Definition = candidate,
                    Score = UpgradeValue(session, tower, candidate.Level.WithDoctrine(tower.Doctrine), threat) *
                            SpecializationWeight(tower.Definition.Id, candidate.Id, threat) *
                            (_strategy == AutoPlayerStrategy.Experienced
                                ? ExperiencedSpecializationDiversity(session, tower, candidate.Id)
                                : 1f) /
                            Math.Max(1, candidate.UpgradeCost)
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Definition.Id)
                .FirstOrDefault();
            return specialization is not null && session.TrySpecializeTower(tower.Id, specialization.Definition.Id);
        }

        if (tower.CanUpgrade && tower.UpgradeCost <= spendable)
            return session.TryUpgradeTower(tower.Id);
        return false;
    }

    private static float ExperiencedUpgradeBreadth(GameSession session, TowerInstance tower)
    {
        var wave = Math.Max(1,
            session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);
        var peersAhead = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.LevelIndex > tower.LevelIndex);
        var roleMaturity = session.Towers.Count(candidate =>
            candidate.Definition.Id == tower.Definition.Id && candidate.LevelIndex >= tower.LevelIndex);

        var breadth = 1f / (1f + peersAhead * 0.34f + MathF.Max(0, roleMaturity - 3) * 0.08f);
        if (tower.LevelIndex >= 2)
        {
            var apexPeers = session.Towers.Count(candidate =>
                candidate.Definition.Id == tower.Definition.Id && candidate.IsApex);
            breadth /= 1f + apexPeers * 1.15f;
            if (tower.Definition.Id == "needle_turret" && apexPeers > 0) breadth *= 0.35f;
        }
        if (tower.Definition.Id == "needle_turret")
        {
            var upgradedNeedles = session.Towers.Count(candidate =>
                candidate.Definition.Id == tower.Definition.Id && candidate.LevelIndex >= tower.LevelIndex + 1);
            var matureCounterRoles = new[] { "frost_spire", "breaker_cannon", "arc_relay", "prism_beam" }
                .Count(id => session.Towers.Any(candidate => candidate.Definition.Id == id && candidate.LevelIndex >= 1));

            if (tower.LevelIndex == 0 && upgradedNeedles >= (wave < 10 ? 3 : 5)) breadth *= 0.18f;
            if (tower.LevelIndex == 1 && upgradedNeedles >= (wave < 15 ? 2 : 4)) breadth *= 0.12f;
            if (matureCounterRoles < 3 && upgradedNeedles >= 3) breadth *= 0.45f;
        }
        if (tower.Definition.Id is "breaker_cannon" or "prism_beam" && tower.LevelIndex < 2) breadth *= 1.18f;
        return breadth;
    }

    private bool IsForcedTower(TowerInstance tower) => _forcedTowerId is not null &&
        tower.Definition.Id.Equals(_forcedTowerId, StringComparison.OrdinalIgnoreCase);

    private float UpgradeValue(GameSession session, TowerInstance tower, TowerLevelDefinition level, ThreatProfile threat)
    {
        if (!tower.IsSupport) return TowerValue(tower.Definition, level, threat);

        // Support upgrades are positional decisions. A wider Horizon aura is
        // valuable only when it actually reaches additional defenses, while a
        // compact cluster should prefer Tempo's stronger rate multiplier.
        var value = 1f;
        foreach (var supported in session.Towers.Where(candidate => !candidate.IsSupport && candidate != tower))
        {
            if (Vector2.DistanceSquared(tower.Position, supported.Position) > level.AuraRange * level.AuraRange) continue;
            var supportedLevel = supported.Level;
            var throughput = supportedLevel.Damage * supportedLevel.AttacksPerSecond;
            throughput *= 1f + MathF.Max(0, supportedLevel.PelletCount - 1) * 0.3f;
            throughput += supportedLevel.ChainDamage * supportedLevel.ChainCount * supportedLevel.AttacksPerSecond * 0.35f;
            throughput += supportedLevel.BurnDamagePerSecond * 0.5f;
            throughput += supported.InvestedCredits * 0.018f;
            value += 3f + throughput * (level.AuraAttackSpeedBonus + level.AuraRangeBonus * 0.42f);
        }
        return value;
    }

    private float ApexProtocolValue(
        GameSession session,
        TowerInstance tower,
        TowerLevelDefinition apexLevel,
        ThreatProfile threat)
    {
        var protocol = tower.Protocol;
        var uptime = MathHelper.Clamp(protocol.DurationSeconds / MathF.Max(
            protocol.DurationSeconds, protocol.CooldownSeconds), 0, 1);
        if (uptime <= 0) return 0;

        if (tower.IsSupport)
        {
            var supportValue = UpgradeValue(session, tower, apexLevel, threat);
            var auraGain = protocol.AuraAttackSpeedBonus * 2f + protocol.AuraRangeBonus * 0.6f;
            return supportValue * uptime * auraGain;
        }

        var activeValue = TowerValue(tower.Definition, apexLevel, threat);
        var throughputGain = (1f + protocol.AttackSpeedBonus) * (1f + protocol.DamageBonus) - 1f;
        var rangeGain = protocol.RangeBonus * 0.35f;
        var armorGain = protocol.ArmorPierceBonus * apexLevel.AttacksPerSecond * threat.Armored * 1.8f;
        var sustainedValue = activeValue * uptime * MathF.Max(0, throughputGain + rangeGain) + armorGain * uptime;

        var expectedTargets = protocol.BurstRadius > 0 ? Math.Clamp(protocol.AutoTriggerCount, 1, 6) : 0;
        var burstValue = protocol.BurstDamage * expectedTargets / MathF.Max(1f, protocol.CooldownSeconds);
        if (protocol.FireOnActivation)
            burstValue += apexLevel.Damage * Math.Max(1, apexLevel.PelletCount) /
                          MathF.Max(1f, protocol.CooldownSeconds);
        return sustainedValue + burstValue;
    }

    private Vector2? FindBestPosition(GameSession session, TowerDefinition definition, ThreatProfile threat)
    {
        var eligible = new List<(Vector2 Position, float Score)>();
        foreach (var position in _placementCandidates)
        {
            if (session.ValidatePlacement(definition.Id, position) != PlacementFailure.None) continue;
            var score = PlacementScore(session, definition, position);
            if (_strategy == AutoPlayerStrategy.Conservative) score *= 1f + PathProgressNear(session, position) * 0.18f;
            if (_strategy == AutoPlayerStrategy.Synergy)
            {
                var power = session.Map.GetPowerBuff(position);
                var nodeValue = power.AttackSpeedBonus + power.RangeBonus + power.DamageBonus + power.ArmorPierceBonus * 0.04f;
                score *= 1f + nodeValue * 2.2f;
            }
            if (threat.Fast > 0.30f) score *= 1f + PathProgressNear(session, position) * 0.12f;
            eligible.Add((position, score));
        }
        if (eligible.Count == 0) return null;
        if (_strategy == AutoPlayerStrategy.Experienced)
        {
            var candidateCount = _wavePlan?.PlacementProfileId.ToLowerInvariant() switch
            {
                "precise" => 1,
                "explore" => 6,
                "clusters" => 4,
                _ => 3
            };
            var nearBest = eligible
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Position.Y)
                .ThenBy(candidate => candidate.Position.X)
                .Take(candidateCount)
                .ToArray();
            return nearBest[_random.Next(nearBest.Length)].Position;
        }
        var ordered = eligible.OrderByDescending(x => x.Score).Take(_strategy == AutoPlayerStrategy.Randomized ? 8 : 3).ToArray();
        return ordered[_random.Next(ordered.Length)].Position;
    }

    private float PlacementScore(GameSession session, TowerDefinition definition, Vector2 position) =>
        _strategy == AutoPlayerStrategy.Experienced
            ? ExperiencedPositionScore(session, definition, position)
            : PositionScore(session, definition, position);

    private float ExperiencedPositionScore(GameSession session, TowerDefinition definition, Vector2 position)
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
            return ExperiencedSupportPositionScore(session, definition, position);

        var score = PositionScore(session, definition, position);
        var power = session.Map.GetPowerBuff(position);
        if (power.IsPowered)
        {
            var level = definition.Levels[0];
            var attackFit = power.AttackSpeedBonus * (level.AttacksPerSecond >= 1f ? 1.25f : 1f);
            var rangeFit = power.RangeBonus * (level.Range <= 190 ? 1.20f : 0.75f);
            var damageFit = power.DamageBonus * (level.Damage >= 20 ? 1.25f : 1f);
            var pierceFit = power.ArmorPierceBonus * (level.ArmorPierce < 6 ? 0.045f : 0.025f);
            score *= 1f + (attackFit + rangeFit + damageFit + pierceFit) * 3.15f;
        }

        var coverageBalance = ExperiencedCoverageBalance(session, definition, position);
        var coverageWeight = _wavePlan?.PlacementProfileId.ToLowerInvariant() switch
        {
            "coverage" => 1.25f,
            "nodes" => 0.60f,
            "clusters" => 0.45f,
            _ => 1f
        };
        coverageWeight *= PlanParameter("coverageWeight", 1f, 0f, 4f);
        var coverageFactor = 0.82f + coverageBalance * 0.38f;
        score *= 1f + (coverageFactor - 1f) * coverageWeight;

        var node = session.Map.Definition.PowerNodes.FirstOrDefault(candidate =>
            Vector2.DistanceSquared(position, candidate.Position.ToVector2()) <= candidate.Radius * candidate.Radius);
        var nodeWeight = _wavePlan?.PlacementProfileId.ToLowerInvariant() switch
        {
            "nodes" => 2.25f,
            "clusters" => 0.60f,
            _ => 1f
        };
        nodeWeight *= PlanParameter("nodeWeight", 1f, 0f, 4f);
        if (node is not null)
        {
            var occupants = session.Towers.Count(tower =>
                Vector2.DistanceSquared(tower.Position, node.Position.ToVector2()) <= node.Radius * node.Radius);
            var nodeFactor = occupants switch
            {
                0 => 1.16f,
                <= 3 => 1.08f,
                _ => 0.90f
            };
            score *= MathF.Pow(nodeFactor, nodeWeight);
        }
        else
        {
            var nearbyCluster = session.Towers.Count(tower => !tower.IsSupport &&
                Vector2.DistanceSquared(position, tower.Position) <= 135f * 135f);
            var clusterWeight = PlanParameter("clusterWeight",
                _wavePlan is not null && PlanProfile(_wavePlan.PlacementProfileId, "clusters") ? 2.4f : 1f,
                0f, 4f);
            score *= 1f + MathF.Min(0.20f, nearbyCluster * 0.012f * clusterWeight);
        }

        var progress = PathProgressNear(session, position);
        score *= 1f + (1f - MathF.Abs(progress - 0.58f) * 2f) * 0.035f;
        return score;
    }

    private static float ExperiencedCoverageBalance(
        GameSession session,
        TowerDefinition definition,
        Vector2 position)
    {
        var power = session.Map.GetPowerBuff(position);
        var range = definition.Levels[0].Range * (1f + power.RangeBonus);
        if (range <= 0) return 1f;

        var coveredSamples = 0;
        var marginalCoverage = 0f;
        var rangeSquared = range * range;
        for (var distance = 0f; distance <= session.Map.Path.TotalLength; distance += 32f)
        {
            var pathPoint = session.Map.Path.GetPosition(distance);
            if (Vector2.DistanceSquared(position, pathPoint) > rangeSquared) continue;

            coveredSamples++;
            var existingCoverage = 0f;
            foreach (var tower in session.Towers.Where(tower => !tower.IsSupport))
            {
                var existingRange = session.GetEffectiveRange(tower);
                if (Vector2.DistanceSquared(tower.Position, pathPoint) > existingRange * existingRange) continue;
                existingCoverage += 0.55f + MathF.Min(2.2f, tower.InvestedCredits / 420f);
            }

            marginalCoverage += 1f / (1f + existingCoverage * 0.32f);
        }

        if (coveredSamples == 0) return 0;
        return marginalCoverage / coveredSamples;
    }

    private static float ExperiencedSupportPositionScore(GameSession session, TowerDefinition definition, Vector2 position)
    {
        var level = definition.Levels[0];
        var auraSquared = level.AuraRange * level.AuraRange;
        var value = 0f;
        var uniqueRecipients = 0;

        foreach (var recipient in session.Towers.Where(tower => !tower.IsSupport &&
                     Vector2.DistanceSquared(position, tower.Position) <= auraSquared))
        {
            var currentAttackBonus = 0f;
            var currentRangeBonus = 0f;
            foreach (var support in session.Towers.Where(tower => tower.IsSupport))
            {
                if (Vector2.DistanceSquared(support.Position, recipient.Position) >
                    support.EffectiveAuraRange * support.EffectiveAuraRange) continue;

                currentAttackBonus = MathF.Max(currentAttackBonus, support.EffectiveAuraAttackSpeedBonus);
                currentRangeBonus = MathF.Max(currentRangeBonus, support.EffectiveAuraTowerRangeBonus);
            }

            var attackGain = MathF.Max(0, level.AuraAttackSpeedBonus - currentAttackBonus);
            var rangeGain = MathF.Max(0, level.AuraRangeBonus - currentRangeBonus);
            if (attackGain <= 0 && rangeGain <= 0) continue;

            uniqueRecipients++;
            var recipientLevel = recipient.Level;
            var throughput = recipientLevel.Damage * MathF.Max(0.25f, recipientLevel.AttacksPerSecond);
            throughput *= 1f + MathF.Max(0, recipientLevel.PelletCount - 1) * 0.28f;
            throughput += recipientLevel.ChainDamage * recipientLevel.ChainCount * recipientLevel.AttacksPerSecond * 0.32f;
            throughput += recipientLevel.BurnDamagePerSecond * 0.45f;
            value += 2.5f + recipient.InvestedCredits / 125f + throughput * (attackGain + rangeGain * 0.45f);
        }

        if (uniqueRecipients < 3) value *= 0.25f;
        else if (uniqueRecipients == 3) value *= 0.72f;
        if (session.Map.GetPowerBuff(position).IsPowered) value *= 0.82f;
        return value;
    }

    private static int ExperiencedCopyLimit(string towerId, int wave) =>
        ExperiencedRoleCount(towerId, wave);

    private static float ExperiencedRepetitionPenalty(string towerId) => towerId switch
    {
        "needle_turret" => 0.14f,
        "frost_spire" => 0.30f,
        "signal_beacon" => 0.55f,
        _ => 0.22f
    };

    private static float PositionScore(GameSession session, TowerDefinition definition, Vector2 position)
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
        {
            var aura = definition.Levels[0].AuraRange;
            var supported = session.Towers.Where(x => !x.IsSupport && Vector2.DistanceSquared(position, x.Position) <= aura * aura).ToArray();
            return supported.Sum(x => 2f + x.InvestedCredits / 100f);
        }

        var power = session.Map.GetPowerBuff(position);
        var range = definition.Levels[0].Range * (1f + power.RangeBonus);
        var score = 0f;
        for (var distance = 0f; distance <= session.Map.Path.TotalLength; distance += 22f)
        {
            var pathPoint = session.Map.Path.GetPosition(distance);
            if (Vector2.DistanceSquared(position, pathPoint) <= range * range)
                score += 1f + session.Map.Path.GetProgress(distance) * 0.08f;
        }
        return score * (1f + power.AttackSpeedBonus * 1.5f + power.RangeBonus * 0.8f +
            power.DamageBonus * 1.2f + power.ArmorPierceBonus * 0.06f);
    }

    private static float PathProgressNear(GameSession session, Vector2 position)
    {
        var bestDistance = float.MaxValue;
        var progress = 0f;
        for (var distance = 0f; distance <= session.Map.Path.TotalLength; distance += 18f)
        {
            var pathPoint = session.Map.Path.GetPosition(distance);
            var candidateDistance = Vector2.DistanceSquared(position, pathPoint);
            if (candidateDistance >= bestDistance) continue;
            bestDistance = candidateDistance;
            progress = session.Map.Path.GetProgress(distance);
        }
        return progress;
    }

    private float TowerValue(TowerDefinition definition, int levelIndex, ThreatProfile threat)
    {
        return TowerValue(definition, definition.Levels[levelIndex], threat);
    }

    private float TowerValue(TowerDefinition definition, TowerLevelDefinition level, ThreatProfile threat)
        => StructuralTowerEvaluator.LevelValue(definition, level, threat);

    private float StrategyWeight(string towerId, ThreatProfile threat)
    {
        var weight = _strategy switch
        {
            AutoPlayerStrategy.Conservative => towerId switch { "needle_turret" => 1.45f, "frost_spire" => 1.15f, "watchtower" => 1.10f, "breaker_cannon" => 1.10f, "signal_beacon" => 0.9f, _ => 0.82f },
            AutoPlayerStrategy.Economy => towerId switch { "needle_turret" => 1.45f, "watchtower" => 1.05f, "signal_beacon" => 1.10f, _ => 0.72f },
            AutoPlayerStrategy.Aggressive => towerId switch { "needle_turret" => 1.25f, "shard_fan" => 1.25f, "watchtower" => 1.15f, "prism_beam" => 1.10f, _ => 0.85f },
            AutoPlayerStrategy.UpgradeFocused => towerId switch { "watchtower" => 1.28f, "breaker_cannon" => 1.20f, "prism_beam" => 1.20f, "needle_turret" => 1.05f, _ => 0.78f },
            AutoPlayerStrategy.Spam => towerId switch { "needle_turret" => 1.60f, "frost_spire" => 1.12f, "shard_fan" => 1.12f, _ => 0.55f },
            AutoPlayerStrategy.AntiSwarm => towerId switch { "shard_fan" => 1.55f, "arc_relay" => 1.40f, "siege_mortar" => 1.35f, "frost_spire" => 1.08f, _ => 0.68f },
            AutoPlayerStrategy.AntiArmor => towerId switch { "breaker_cannon" => 1.65f, "watchtower" => 1.28f, "prism_beam" => 1.18f, _ => 0.65f },
            AutoPlayerStrategy.LongRange => towerId switch { "watchtower" => 1.55f, "siege_mortar" => 1.35f, "prism_beam" => 1.18f, "signal_beacon" => 1.05f, _ => 0.62f },
            AutoPlayerStrategy.Control => towerId switch { "frost_spire" => 1.55f, "ember_coil" => 1.35f, "arc_relay" => 1.35f, "signal_beacon" => 1.08f, _ => 0.68f },
            AutoPlayerStrategy.Synergy => towerId switch { "needle_turret" => 1.28f, "frost_spire" => 1.48f, "arc_relay" => 1.52f, "breaker_cannon" => 1.38f, "prism_beam" => 1.28f, "ember_coil" => 1.04f, "siege_mortar" => 1.12f, "watchtower" => 0.98f, "signal_beacon" => 1.15f, _ => 0.58f },
            AutoPlayerStrategy.Tactical => towerId switch { "needle_turret" => 1.30f, "frost_spire" => 1.22f, "watchtower" => 1.16f, "breaker_cannon" => 1.08f, _ => 0.82f },
            AutoPlayerStrategy.Experienced => ExperiencedWeight(towerId, threat),
            AutoPlayerStrategy.Adaptive => AdaptiveWeight(towerId, threat),
            AutoPlayerStrategy.Randomized => 0.75f + (float)_random.NextDouble() * 0.5f,
            _ => 1f
        };
        return weight;
    }

    private static float ExperiencedWeight(string towerId, ThreatProfile threat)
    {
        var weight = towerId switch
        {
            "needle_turret" => 1.18f,
            "frost_spire" => 1.08f + threat.Fast * 0.75f,
            "shard_fan" => 0.88f + threat.Swarm * 0.78f,
            "watchtower" => 0.84f + threat.Durable * 0.55f,
            "ember_coil" => 0.84f + threat.Durable * 0.85f,
            "breaker_cannon" => 0.86f + threat.Armored * 1.25f,
            "arc_relay" => 0.90f + threat.Swarm * 0.82f,
            "siege_mortar" => 0.78f + threat.Swarm * 0.85f,
            "prism_beam" => 0.82f + threat.Shielded * 1.40f + threat.Durable * 0.40f,
            "signal_beacon" => 1.08f,
            _ => 0.82f
        };
        if (threat.HasElite && towerId is "watchtower" or "breaker_cannon" or "prism_beam" or "ember_coil") weight += 0.18f;
        if (threat.HasBoss && towerId is "watchtower" or "breaker_cannon" or "prism_beam" or "ember_coil") weight += 0.38f;
        if (threat.Armored > 0.30f && towerId is "shard_fan" or "needle_turret") weight *= 0.84f;
        return weight;
    }

    private static float AdaptiveWeight(string towerId, ThreatProfile threat)
    {
        var weight = towerId == "needle_turret" ? 1.05f : 0.85f;
        if (threat.Swarm > 0.45f && towerId is "shard_fan" or "arc_relay" or "siege_mortar") weight += 0.55f;
        if (threat.Fast > 0.25f && towerId is "watchtower" or "frost_spire") weight += 0.45f;
        if (threat.Armored > 0.25f && towerId is "breaker_cannon" or "watchtower") weight += 0.60f;
        if (threat.Shielded > 0.15f && towerId == "prism_beam") weight += 0.45f;
        if (threat.Durable > 0.15f && towerId is "watchtower" or "ember_coil" or "prism_beam") weight += 0.40f;
        if (threat.HasElite && towerId is "watchtower" or "breaker_cannon" or "prism_beam") weight += 0.28f;
        if (threat.HasBoss && towerId is "watchtower" or "breaker_cannon" or "prism_beam") weight += 0.72f;
        if (threat.HasBoss && towerId is "ember_coil" or "signal_beacon") weight += 0.24f;
        return weight;
    }

    private float SpecializationWeight(string towerId, string specializationId, ThreatProfile threat)
    {
        return (towerId, specializationId, _strategy) switch
        {
            ("needle_turret", "rapid_array", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Spam or AutoPlayerStrategy.Aggressive) => 1.35f,
            ("needle_turret", "rail_pin", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange) => 1.45f,
            ("frost_spire", "permafrost", AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.40f,
            ("frost_spire", "hail_lancer", AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam or AutoPlayerStrategy.AntiSwarm) => 2.0f,
            ("breaker_cannon", "breach_round", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange) => 1.85f,
            ("breaker_cannon", "shatter_shell", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Control) => 1.65f,
            ("ember_coil", "wildfire_matrix", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Control) => 1.55f,
            ("ember_coil", "searing_brand", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange) => 1.55f,
            ("shard_fan", "razor_bloom", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Spam or AutoPlayerStrategy.Aggressive) => 1.45f,
            ("shard_fan", "lance_fan", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange) => 1.65f,
            ("watchtower", "sentinel_array", AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Control) => 1.35f,
            ("watchtower", "deadeye_post", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange) => 1.45f,
            ("arc_relay", "storm_lattice", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam) => 1.45f,
            ("arc_relay", "lockdown_coil", AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.70f,
            ("siege_mortar", "salvo_rack", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive) => 1.35f,
            ("siege_mortar", "quake_shell", AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.80f,
            ("prism_beam", "core_lance", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange) => 1.45f,
            ("prism_beam", "spectrum_split", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Control) => 1.45f,
            ("signal_beacon", "tempo_beacon", AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.Tactical) => 1.35f,
            ("signal_beacon", "horizon_beacon", AutoPlayerStrategy.LongRange or AutoPlayerStrategy.Conservative or AutoPlayerStrategy.Control) => 1.70f,
            ("frost_spire", "permafrost", AutoPlayerStrategy.Synergy) => 1.55f,
            ("arc_relay", "storm_lattice", AutoPlayerStrategy.Synergy) => 1.60f,
            ("breaker_cannon", "breach_round", AutoPlayerStrategy.Synergy) => 1.55f,
            ("prism_beam", "core_lance", AutoPlayerStrategy.Synergy) when threat.HasBoss || threat.HasElite || threat.Shielded > 0.2f => 1.50f,
            ("signal_beacon", "tempo_beacon", AutoPlayerStrategy.Synergy) => 1.45f,
            ("ember_coil", "wildfire_matrix", AutoPlayerStrategy.Synergy) => 1.35f,
            ("siege_mortar", "quake_shell", AutoPlayerStrategy.Synergy) => 1.35f,
            ("watchtower", "deadeye_post", AutoPlayerStrategy.Synergy) => 1.25f,
            ("needle_turret", "rapid_array", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.40f => 1.45f,
            ("needle_turret", "rail_pin", AutoPlayerStrategy.Experienced) when threat.Armored > 0.18f || threat.HasElite || threat.HasBoss => 1.50f,
            ("shard_fan", "razor_bloom", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.42f => 1.45f,
            ("shard_fan", "lance_fan", AutoPlayerStrategy.Experienced) when threat.Armored >= 0.22f => 1.35f,
            ("watchtower", "deadeye_post", AutoPlayerStrategy.Experienced) when threat.HasBoss || threat.Durable >= 0.18f => 1.40f,
            ("watchtower", "sentinel_array", AutoPlayerStrategy.Experienced) => 1.18f,
            ("frost_spire", "permafrost", AutoPlayerStrategy.Experienced) => 1.42f,
            ("frost_spire", "hail_lancer", AutoPlayerStrategy.Experienced) when threat.Fast < 0.18f => 1.15f,
            ("ember_coil", "wildfire_matrix", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.35f => 1.35f,
            ("ember_coil", "searing_brand", AutoPlayerStrategy.Experienced) when threat.Durable > 0.12f || threat.Armored > 0.25f => 1.42f,
            ("breaker_cannon", "breach_round", AutoPlayerStrategy.Experienced) when threat.HasBoss || threat.Armored >= 0.30f => 1.48f,
            ("breaker_cannon", "shatter_shell", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.34f => 1.38f,
            ("arc_relay", "storm_lattice", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.38f => 1.48f,
            ("arc_relay", "lockdown_coil", AutoPlayerStrategy.Experienced) when threat.Fast >= 0.22f || threat.HasBoss => 1.36f,
            ("siege_mortar", "salvo_rack", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.48f => 1.36f,
            ("siege_mortar", "quake_shell", AutoPlayerStrategy.Experienced) when threat.Fast >= 0.20f || threat.HasBoss => 1.32f,
            ("prism_beam", "core_lance", AutoPlayerStrategy.Experienced) when threat.Shielded > 0 || threat.HasBoss => 1.48f,
            ("prism_beam", "spectrum_split", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.38f => 1.35f,
            ("signal_beacon", "tempo_beacon", AutoPlayerStrategy.Experienced) => 1.32f,
            ("signal_beacon", "horizon_beacon", AutoPlayerStrategy.Experienced) when threat.Fast > 0.25f || threat.HasBoss => 1.28f,
            (_, "rail_pin" or "breach_round", AutoPlayerStrategy.Adaptive) when threat.HasBoss || threat.HasElite || threat.Armored > 0.35f => 1.55f,
            (_, "rapid_array" or "shatter_shell", AutoPlayerStrategy.Adaptive) when threat.Swarm > 0.45f => 1.45f,
            (_, "permafrost", AutoPlayerStrategy.Adaptive) when threat.Fast > 0.25f => 1.35f,
            ("ember_coil", "searing_brand", AutoPlayerStrategy.Adaptive) when threat.HasBoss || threat.HasElite || threat.Durable > 0.2f => 1.55f,
            ("ember_coil", "wildfire_matrix", AutoPlayerStrategy.Adaptive) when threat.Swarm > 0.4f => 1.45f,
            (_, "lance_fan" or "deadeye_post" or "core_lance", AutoPlayerStrategy.Adaptive) when threat.HasBoss || threat.HasElite || threat.Armored > 0.3f => 1.55f,
            (_, "razor_bloom" or "sentinel_array" or "storm_lattice" or "salvo_rack" or "spectrum_split", AutoPlayerStrategy.Adaptive) when threat.Swarm > 0.4f => 1.45f,
            (_, "lockdown_coil" or "quake_shell", AutoPlayerStrategy.Adaptive) when threat.Fast > 0.25f => 1.55f,
            ("signal_beacon", "horizon_beacon", AutoPlayerStrategy.Adaptive) when threat.Fast > 0.3f || threat.HasBoss => 1.40f,
            _ => 1f
        };
    }

    private float DoctrineWeight(TowerDoctrineDefinition doctrine, ThreatProfile threat)
    {
        var weight = (doctrine.Id, _strategy) switch
        {
            ("needle_cycler", AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam or AutoPlayerStrategy.AntiSwarm) => 1.35f,
            ("needle_calibrator", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.UpgradeFocused) => 1.30f,
            ("shard_scatter", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Spam or AutoPlayerStrategy.Aggressive) => 1.55f,
            ("shard_temper", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused) => 1.35f,
            ("watch_spotter", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Control or AutoPlayerStrategy.Tactical) => 1.45f,
            ("watch_heavy_optics", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.UpgradeFocused) => 1.35f,
            ("frost_deep_chill", AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.35f,
            // The control half of Frost scores very highly in the generic value model.
            // Anti-swarm doctrine runs deliberately exercise the damage/control tradeoff
            // instead of converging on Deep Chill in every simulation.
            ("frost_ice_needle", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam) => 4.00f,
            ("ember_kindling", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Control or AutoPlayerStrategy.Aggressive) => 1.45f,
            ("ember_hot_core", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.UpgradeFocused) => 1.35f,
            ("breaker_repeater", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam) => 1.75f,
            ("breaker_bored", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.UpgradeFocused) => 1.35f,
            ("arc_fork", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam) => 1.65f,
            ("arc_capacitor", AutoPlayerStrategy.Control or AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.Conservative) => 1.40f,
            ("mortar_loader", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive) => 1.95f,
            ("mortar_survey", AutoPlayerStrategy.LongRange or AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.35f,
            ("prism_frequency", AutoPlayerStrategy.AntiSwarm or AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam) => 2.60f,
            ("prism_aperture", AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.UpgradeFocused) => 1.35f,
            ("beacon_amplifier", AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.Tactical) => 1.35f,
            ("beacon_repeater", AutoPlayerStrategy.LongRange or AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative) => 1.45f,
            ("needle_calibrator", AutoPlayerStrategy.Synergy) => 1.25f,
            ("frost_deep_chill", AutoPlayerStrategy.Synergy) => 1.55f,
            ("arc_fork", AutoPlayerStrategy.Synergy) => 1.60f,
            ("breaker_bored", AutoPlayerStrategy.Synergy) => 1.45f,
            ("prism_aperture", AutoPlayerStrategy.Synergy) => 1.40f,
            ("beacon_amplifier", AutoPlayerStrategy.Synergy) => 1.35f,
            ("ember_kindling", AutoPlayerStrategy.Synergy) => 1.25f,
            ("mortar_survey", AutoPlayerStrategy.Synergy) => 1.25f,
            ("watch_heavy_optics", AutoPlayerStrategy.Synergy) => 1.20f,
            ("needle_cycler", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.35f => 1.30f,
            ("needle_calibrator", AutoPlayerStrategy.Experienced) when threat.Armored > 0.15f || threat.HasElite || threat.HasBoss => 1.34f,
            ("shard_scatter", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.40f => 1.42f,
            ("shard_temper", AutoPlayerStrategy.Experienced) when threat.Armored >= 0.20f => 1.34f,
            ("watch_spotter", AutoPlayerStrategy.Experienced) when threat.Fast >= 0.20f || threat.Swarm >= 0.42f => 1.30f,
            ("watch_heavy_optics", AutoPlayerStrategy.Experienced) when threat.Durable > 0.12f || threat.HasBoss => 1.34f,
            ("frost_deep_chill", AutoPlayerStrategy.Experienced) => 1.42f,
            ("ember_kindling", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.35f => 1.32f,
            ("ember_hot_core", AutoPlayerStrategy.Experienced) when threat.Durable > 0.12f || threat.Armored >= 0.22f => 1.36f,
            ("breaker_repeater", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.34f => 1.34f,
            ("breaker_bored", AutoPlayerStrategy.Experienced) when threat.Armored > 0.12f || threat.HasBoss => 1.40f,
            ("arc_fork", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.32f => 1.42f,
            ("arc_capacitor", AutoPlayerStrategy.Experienced) when threat.Fast >= 0.22f || threat.HasBoss => 1.32f,
            ("mortar_loader", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.45f => 1.36f,
            ("mortar_survey", AutoPlayerStrategy.Experienced) when threat.Durable > 0.12f || threat.HasBoss => 1.30f,
            ("prism_frequency", AutoPlayerStrategy.Experienced) when threat.Swarm >= 0.40f => 1.34f,
            ("prism_aperture", AutoPlayerStrategy.Experienced) when threat.Shielded > 0 || threat.HasBoss => 1.42f,
            ("beacon_amplifier", AutoPlayerStrategy.Experienced) => 1.35f,
            ("beacon_repeater", AutoPlayerStrategy.Experienced) when threat.Fast >= 0.28f || threat.HasBoss => 1.22f,
            _ => 1f
        };
        if (_strategy is AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam or AutoPlayerStrategy.AntiSwarm)
            weight *= MathF.Max(0.75f, doctrine.AttackSpeedMultiplier);
        if (_strategy is AutoPlayerStrategy.LongRange)
            weight *= MathF.Max(0.75f, doctrine.RangeMultiplier * 1.08f);
        if (_strategy is AutoPlayerStrategy.Control or AutoPlayerStrategy.Conservative)
            weight *= MathF.Max(0.75f, doctrine.UtilityMultiplier * 1.06f);
        if (_strategy is AutoPlayerStrategy.AntiArmor or AutoPlayerStrategy.UpgradeFocused)
            weight *= MathF.Max(0.75f, doctrine.DamageMultiplier * doctrine.UtilityMultiplier);
        if (_strategy == AutoPlayerStrategy.Adaptive)
        {
            if (doctrine.Id == "frost_ice_needle" && threat.Swarm > 0.4f) weight *= 3.2f;
            if (doctrine.Id == "prism_frequency" && threat.Swarm > 0.4f) weight *= 3.4f;
            if (doctrine.Id == "watch_spotter" && threat.Fast > 0.25f) weight *= 1.8f;
            if (threat.Swarm > 0.4f)
                weight *= doctrine.PelletCountBonus + doctrine.ChainCountBonus + doctrine.SplashTargetLimitBonus > 0
                    ? 1.55f
                    : doctrine.AttackSpeedMultiplier;
            if (threat.Fast > 0.25f) weight *= doctrine.UtilityMultiplier;
            if (threat.Armored > 0.25f || threat.HasElite || threat.HasBoss) weight *= doctrine.DamageMultiplier * doctrine.UtilityMultiplier;
        }
        return weight;
    }

    private void ConfigureTargeting(
        GameSession session,
        TowerInstance tower,
        ThreatProfile threat,
        bool duringWave)
    {
        if (_strategy == AutoPlayerStrategy.Experienced)
        {
            ConfigureExperiencedTargeting(session, threat, duringWave);
            return;
        }

        var mode = tower.Definition.Id switch
        {
            "watchtower" or "breaker_cannon" or "prism_beam" => TargetMode.Strongest,
            "siege_mortar" => threat.Durable > 0.3f ? TargetMode.Strongest : TargetMode.First,
            "frost_spire" => threat.HasBoss ? TargetMode.Strongest : TargetMode.First,
            _ => threat.Fast > 0.35f ? TargetMode.First : tower.TargetMode
        };
        session.TrySetTargetMode(tower.Id, mode);
    }

    private void ConfigureExperiencedTargeting(
        GameSession session,
        ThreatProfile threat,
        bool duringWave)
    {
        foreach (var tower in session.Towers.Where(tower => !tower.IsSupport).OrderBy(tower => tower.Id))
            session.TrySetTargetMode(tower.Id,
                ExperiencedTargetMode(session, tower, threat, duringWave));
    }

    private TargetMode ExperiencedTargetMode(
        GameSession session,
        TowerInstance tower,
        ThreatProfile threat,
        bool duringWave)
    {
        var peers = session.Towers.Where(candidate => candidate.Definition.Id == tower.Definition.Id)
            .OrderBy(candidate => candidate.Id)
            .ToArray();
        var index = Array.IndexOf(peers, tower);
        var live = duringWave
            ? session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray()
            : Array.Empty<EnemyInstance>();
        var signalPresent = session.SupportTargetingEnabled &&
                            (!duringWave || live.Any(enemy => enemy.SignalRole != EnemySignalRole.None) ||
                             session.Waves.CaptureQueuedEnemies(session)
                                 .Any(enemy => enemy.SignalRole != EnemySignalRole.None));
        var cleanup = duringWave && live.Length > 0 && session.Waves.QueuedEnemies == 0 &&
                      live.Max(enemy => enemy.PathProgress) >= 0.72f;
        var wave = Math.Max(1,
            session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);

        if (_wavePlan is not null && !PlanProfile(_wavePlan.TargetingProfileId, "split"))
        {
            if (PlanProfile(_wavePlan.TargetingProfileId, "first"))
            {
                if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
                if (signalPresent && tower.Definition.Id == "needle_turret" && index == 0)
                    return TargetMode.Support;
                if (signalPresent && tower.Definition.Id == "siege_mortar" && index == peers.Length - 1)
                    return TargetMode.Support;
                return TargetMode.First;
            }

            if (PlanProfile(_wavePlan.TargetingProfileId, "support"))
            {
                if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
                if (!signalPresent) return TargetMode.First;
                return tower.Definition.Id switch
                {
                    "breaker_cannon" or "prism_beam" => index % 2 == 1 ? TargetMode.Support : TargetMode.First,
                    "ember_coil" => index == peers.Length - 1 ? TargetMode.Support : TargetMode.First,
                    "needle_turret" => index % 5 == 0 ? TargetMode.Support : TargetMode.First,
                    "siege_mortar" => index == peers.Length - 1 ? TargetMode.Support : TargetMode.First,
                    _ => TargetMode.First
                };
            }

            if (PlanProfile(_wavePlan.TargetingProfileId, "armored"))
            {
                if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
                return tower.Definition.Id switch
                {
                    "breaker_cannon" => TargetMode.Armored,
                    "prism_beam" or "ember_coil" => TargetMode.Strongest,
                    "needle_turret" when signalPresent && index == 0 => TargetMode.Support,
                    "needle_turret" when tower.SpecializationId == "rail_pin" && index % 3 == 1 => TargetMode.Armored,
                    _ => TargetMode.First
                };
            }

            if (PlanProfile(_wavePlan.TargetingProfileId, "strongest"))
            {
                if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
                return tower.Definition.Id switch
                {
                    "breaker_cannon" => TargetMode.Armored,
                    "prism_beam" or "ember_coil" or "siege_mortar" => TargetMode.Strongest,
                    "needle_turret" when signalPresent && index == 0 => TargetMode.Support,
                    _ => TargetMode.First
                };
            }
        }

        if (cleanup && tower.Definition.Id == "frost_spire" && wave < 30)
        {
            var leadEnemy = live.OrderByDescending(enemy => enemy.PathProgress).ThenBy(enemy => enemy.Id)
                .First();
            var escapeProgress = PlanParameter("frostEscapeProgress", 0.86f, 0.65f, 0.98f);
            if (leadEnemy.PathProgress >= escapeProgress)
            {
                var escapeFrost = peers.OrderBy(candidate =>
                        Vector2.DistanceSquared(candidate.Position, leadEnemy.Position))
                    .ThenBy(candidate => candidate.Id)
                    .First();
                return escapeFrost == tower ? TargetMode.First : TargetMode.Fastest;
            }

            return TargetMode.Fastest;
        }

        if (cleanup && wave < 28) return TargetMode.First;

        if (wave >= 30)
        {
            var signalSupportCleanup = duringWave && live.Length > 0 && session.Waves.QueuedEnemies == 0 &&
                                       live.Max(enemy => enemy.PathProgress) >=
                                       PlanParameter("signalSupportExitProgress", 0.72f, 0.5f, 0.98f);
            if (signalPresent && (!duringWave || !signalSupportCleanup))
            {
                var supportTier = (int)PlanParameter("signalSupportTier", 2f, 0f, 6f);
                var openingSupportExit = PlanParameter("openingSupportExitProgress", 0f, 0f, 0.6f);
                if (!duringWave || live.Length == 0 ||
                    live.Max(enemy => enemy.PathProgress) < openingSupportExit)
                    supportTier = Math.Max(supportTier,
                        (int)PlanParameter("openingSignalSupportTier", supportTier, 0f, 6f));
                if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
                return tower.Definition.Id switch
                {
                    "breaker_cannon" when supportTier >= 5 && index is 0 or 2 => TargetMode.Support,
                    "breaker_cannon" when supportTier >= 1 && index == 0 => TargetMode.Support,
                    "prism_beam" when supportTier >= 6 && index is 0 or 2 => TargetMode.Support,
                    "prism_beam" when supportTier >= 2 && index == 0 => TargetMode.Support,
                    "ember_coil" when supportTier >= 4 && index == 0 => TargetMode.Support,
                    "needle_turret" when index == Math.Min(1, peers.Length - 1) ||
                                                supportTier >= 3 && index == Math.Min(8, peers.Length - 1) =>
                        TargetMode.Support,
                    "siege_mortar" => index == peers.Length - 1 ? TargetMode.Support : TargetMode.First,
                    _ => TargetMode.First
                };
            }

            var cleanupArmoredCount = (int)PlanParameter("cleanupArmoredCount", 1f, 0f, 5f);
            if (tower.Definition.Id == "frost_spire")
            {
                var escapeProgress = PlanParameter("frostEscapeProgress", 0.86f, 0.65f, 0.98f);
                var escapeCount = (int)PlanParameter("escapeFrostFirstCount", 1f, 0f, 7f);
                var leadEnemy = live.OrderByDescending(enemy => enemy.PathProgress).ThenBy(enemy => enemy.Id)
                    .FirstOrDefault();
                if (leadEnemy is not null && leadEnemy.PathProgress >= escapeProgress && escapeCount > 0)
                {
                    var escapeFrost = peers.OrderBy(candidate =>
                            Vector2.DistanceSquared(candidate.Position, leadEnemy.Position))
                        .ThenBy(candidate => candidate.Id)
                        .Take(escapeCount);
                    return escapeFrost.Contains(tower) ? TargetMode.First : TargetMode.Fastest;
                }
                return index == 2 ? TargetMode.First : TargetMode.Fastest;
            }
            var cleanupSupportTier = signalPresent
                ? (int)PlanParameter("cleanupSupportTier", 0f, 0f, 6f)
                : 0;
            var cleanupArmoredOffset = (int)PlanParameter("cleanupArmoredOffset", 0f, 0f, 4f);
            return tower.Definition.Id switch
            {
                "breaker_cannon" when cleanupSupportTier >= 5 && index == 2 => TargetMode.Support,
                "breaker_cannon" when cleanupSupportTier >= 1 && index == 0 => TargetMode.Support,
                "prism_beam" when cleanupSupportTier >= 6 && index == 2 => TargetMode.Support,
                "prism_beam" when cleanupSupportTier >= 2 && index == 0 => TargetMode.Support,
                "ember_coil" when cleanupSupportTier >= 4 && index == 0 => TargetMode.Support,
                "needle_turret" when cleanupSupportTier >= 3 && index == Math.Min(8, peers.Length - 1) =>
                    TargetMode.Support,
                "breaker_cannon" when Enumerable.Range(0, cleanupArmoredCount)
                    .Any(slot => (slot + cleanupArmoredOffset) % Math.Max(1, peers.Length) == index) =>
                    TargetMode.Armored,
                "needle_turret" when signalPresent && index == Math.Min(1, peers.Length - 1) => TargetMode.Support,
                "siege_mortar" when signalPresent && index == peers.Length - 1 => TargetMode.Support,
                _ => TargetMode.First
            };
        }

        if (wave >= 28)
        {
            if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;
            if (!signalPresent) return TargetMode.First;
            return tower.Definition.Id switch
            {
                "breaker_cannon" => index is 0 or 2 or 3 ? TargetMode.Support : TargetMode.First,
                "ember_coil" => index == 0 ? TargetMode.Support : TargetMode.First,
                "needle_turret" => index == 1 || index == Math.Min(8, peers.Length - 1) ? TargetMode.Support : TargetMode.First,
                "prism_beam" => index is 0 or 2 or 3 ? TargetMode.Support : TargetMode.First,
                "siege_mortar" => index == peers.Length - 1 ? TargetMode.Support : TargetMode.First,
                _ => TargetMode.First
            };
        }

        if (tower.Definition.Id == "frost_spire") return TargetMode.Fastest;

        return tower.Definition.Id switch
        {
            "breaker_cannon" => TargetMode.Armored,
            "watchtower" => signalPresent && index % 3 == 2 ? TargetMode.Support : TargetMode.Strongest,
            "prism_beam" => signalPresent && index % 2 == 1 ? TargetMode.Support : TargetMode.Strongest,
            "siege_mortar" => signalPresent && index % 2 == 1 ? TargetMode.Support : TargetMode.First,
            "ember_coil" => signalPresent && index % 2 == 1
                ? TargetMode.Support
                : tower.SpecializationId == "searing_brand" || threat.HasBoss
                    ? TargetMode.Strongest
                    : TargetMode.First,
            "needle_turret" => signalPresent && index == 0
                ? TargetMode.Support
                : tower.SpecializationId == "rail_pin" && index % 3 == 1
                    ? TargetMode.Armored
                    : TargetMode.First,
            "arc_relay" => signalPresent && index % 3 == 2 ? TargetMode.Support : TargetMode.First,
            _ => TargetMode.First
        };
    }

    private void TryRebalance(GameSession session, ThreatProfile threat)
    {
        ResetSalesForWave(session);
        if (_strategy == AutoPlayerStrategy.Experienced)
        {
            var saleLimit = (int)PlanParameter("saleLimit", 1, 0, 4);
            if (_salesThisWave >= saleLimit) return;
            if (!ExperiencedPursuesApex(session) || session.CurrentWave < 20 ||
                session.Towers.Count < 24 || !session.SellingEnabled) return;
            var needsApex = session.Towers.Count(tower => tower.IsApex) < ExperiencedApexLimit();
            if (!needsApex) return;
            var apexCandidate = ExperiencedApexCandidate(session, threat);
            var selectedApexCost = apexCandidate?.Tower.ApexUpgradeCost ?? int.MaxValue;
            if (selectedApexCost == int.MaxValue || session.Economy.Credits >= selectedApexCost) return;
            var sales = PlanExperiencedSales(session, threat, selectedApexCost, saleLimit);
            if (sales.Count == 0 || !ExecuteExperiencedSales(session, threat, sales)) return;
            if (session.TryUpgradeTower(apexCandidate!.Value.Tower.Id))
                ConfigureTargeting(session, apexCandidate.Value.Tower, threat, session.Waves.IsActive);
            return;
        }

        if (_salesThisWave > 0) return;
        if (_strategy != AutoPlayerStrategy.Adaptive || session.CurrentWave < 9) return;
        TowerInstance? mismatch = null;
        if (threat.Armored > 0.55f)
            mismatch = session.Towers.FirstOrDefault(x => x.LevelIndex == 0 && x.Definition.Id == "shard_fan");
        else if (threat.Swarm > 0.65f)
            mismatch = session.Towers.FirstOrDefault(x => x.LevelIndex == 0 && x.Definition.Id == "breaker_cannon");
        if (mismatch is null || session.Towers.Count <= 6) return;
        if (session.TrySellTower(mismatch.Id))
        {
            _salesThisWave++;
            RetargetAfterSale(session, threat);
        }
    }

    private void ResetSalesForWave(GameSession session)
    {
        var decisionWave = ActiveOrNextWave(session);
        if (_salesWave == decisionWave) return;
        _salesWave = decisionWave;
        _salesThisWave = 0;
    }

    private IReadOnlyList<TowerInstance> PlanExperiencedSales(
        GameSession session,
        ThreatProfile threat,
        int requiredCredits,
        int saleLimit,
        int maximumLevelIndex = 1)
    {
        var remainingSales = Math.Max(0, saleLimit - _salesThisWave);
        if (remainingSales == 0 || session.Economy.Credits >= requiredCredits)
            return Array.Empty<TowerInstance>();

        var candidates = session.Towers
            .Where(tower => !tower.IsSupport && !tower.IsApex && tower.LevelIndex <= maximumLevelIndex)
            .Select(tower =>
            {
                var forwardValue = StructuralTowerEvaluator.CurrentValue(session, tower, threat);
                return new ExperiencedSaleOption(
                    tower,
                    forwardValue / Math.Max(1, tower.InvestedCredits),
                    forwardValue,
                    PlacementScore(session, tower.Definition, tower.Position));
            })
            .OrderBy(choice => choice.EfficiencyLoss)
            .ThenBy(choice => choice.ForwardValue)
            .ThenBy(choice => choice.Coverage)
            .ThenBy(choice => choice.Tower.Id)
            .ToArray();

        ExperiencedSaleOption[]? best = null;
        var bestEfficiencyLoss = double.PositiveInfinity;
        var bestForwardValue = double.PositiveInfinity;
        var bestCoverage = float.PositiveInfinity;
        var bestOverfunding = int.MaxValue;
        var selection = new List<ExperiencedSaleOption>(remainingSales);
        Search(startIndex: 0, refunds: 0, efficiencyLoss: 0, forwardValue: 0, coverage: 0);
        return best?.Select(choice => choice.Tower).ToArray() ?? Array.Empty<TowerInstance>();

        void Search(
            int startIndex,
            int refunds,
            double efficiencyLoss,
            double forwardValue,
            float coverage)
        {
            var fundedCredits = session.Economy.Credits + refunds;
            if (fundedCredits >= requiredCredits)
            {
                var overfunding = fundedCredits - requiredCredits;
                if (IsBetter(efficiencyLoss, forwardValue, coverage, overfunding, selection, best))
                {
                    best = selection.ToArray();
                    bestEfficiencyLoss = efficiencyLoss;
                    bestForwardValue = forwardValue;
                    bestCoverage = coverage;
                    bestOverfunding = overfunding;
                }
                return;
            }

            if (selection.Count >= remainingSales) return;
            for (var index = startIndex; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                selection.Add(candidate);
                Search(
                    index + 1,
                    refunds + candidate.Tower.SellValue,
                    efficiencyLoss + candidate.EfficiencyLoss,
                    forwardValue + candidate.ForwardValue,
                    coverage + candidate.Coverage);
                selection.RemoveAt(selection.Count - 1);
            }
        }

        bool IsBetter(
            double efficiencyLoss,
            double forwardValue,
            float coverage,
            int overfunding,
            IReadOnlyList<ExperiencedSaleOption> choices,
            IReadOnlyList<ExperiencedSaleOption>? incumbent)
        {
            if (incumbent is null) return true;
            var comparison = forwardValue.CompareTo(bestForwardValue);
            if (comparison != 0) return comparison < 0;
            comparison = coverage.CompareTo(bestCoverage);
            if (comparison != 0) return comparison < 0;
            comparison = efficiencyLoss.CompareTo(bestEfficiencyLoss);
            if (comparison != 0) return comparison < 0;
            comparison = overfunding.CompareTo(bestOverfunding);
            if (comparison != 0) return comparison < 0;
            comparison = choices.Count.CompareTo(incumbent.Count);
            if (comparison != 0) return comparison < 0;
            for (var index = 0; index < choices.Count; index++)
            {
                comparison = choices[index].Tower.Id.CompareTo(incumbent[index].Tower.Id);
                if (comparison != 0) return comparison < 0;
            }
            return false;
        }
    }

    private bool ExecuteExperiencedSales(
        GameSession session,
        ThreatProfile threat,
        IReadOnlyList<TowerInstance> sales)
    {
        foreach (var tower in sales)
        {
            if (!session.TrySellTower(tower.Id)) return false;
            _salesThisWave++;
            RetargetAfterSale(session, threat);
        }

        return true;
    }

    private void RetargetAfterSale(GameSession session, ThreatProfile threat)
    {
        if (_strategy == AutoPlayerStrategy.Experienced)
        {
            ConfigureExperiencedTargeting(session, threat, session.Waves.IsActive);
            return;
        }

        foreach (var tower in session.Towers.Where(tower => !tower.IsSupport).OrderBy(tower => tower.Id))
            ConfigureTargeting(session, tower, threat, session.Waves.IsActive);
    }

    private bool TrySellForEmergencyDefense(GameSession session, float leadProgress, int requiredCredits)
    {
        ResetSalesForWave(session);
        var saleLimit = (int)PlanParameter("saleLimit", 1, 0, 4);
        var minimumDirectPurchases = (int)PlanParameter("plateSaleMinimumDirectPurchases", 0f, 0f, 16f);
        if (_strategy != AutoPlayerStrategy.Experienced || _salesThisWave >= saleLimit ||
            !session.Waves.IsActive || !IsFinalCampaignWave(session) || !session.SellingEnabled ||
            !session.Towers.Any(tower => tower.IsApex) || session.Economy.Credits >= requiredCredits ||
            _directEmergencyPurchasesThisWave < minimumDirectPurchases ||
            leadProgress < PlanParameter("plateSaleProgress", 0.78f, 0.5f, 0.98f))
            return false;

        var maximumLevelIndex = (int)PlanParameter("plateSaleMaxLevel", 1f, 0f, 2f);
        var threat = ThreatProfile.From(session.Waves.ActiveWave, session.Content.Enemies);
        var sales = PlanExperiencedSales(session, threat, requiredCredits, saleLimit, maximumLevelIndex);
        return sales.Count > 0 && ExecuteExperiencedSales(session, threat, sales);
    }

    private int ReserveCredits(GameSession session, bool duringWave)
    {
        var baseReserve = _strategy switch
        {
            AutoPlayerStrategy.Economy => 140 + session.CurrentWave * 12,
            AutoPlayerStrategy.Conservative => 55 + session.CurrentWave * 4,
            AutoPlayerStrategy.UpgradeFocused => 45,
            AutoPlayerStrategy.Tactical when !session.TacticalSystemsEnabled => 25,
            AutoPlayerStrategy.Tactical => session.Generator is null && session.CurrentWave >= 4
                ? session.Content.Tactics.Generator.PurchaseCost
                : 70,
            AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam => 0,
            AutoPlayerStrategy.Experienced => ExperiencedPursuesApex(session)
                ? duringWave ? 0 : 220
                : session.CurrentWave >= 10 ? 75 : 35,
            _ => 25
        };
        if (duringWave && session.Economy.Lives < session.Economy.StartingLives * 0.6f) return 0;
        if (_wavePlan is not null)
        {
            baseReserve = _wavePlan.EconomyProfileId.ToLowerInvariant() switch
            {
                "invest" => (int)MathF.Round(baseReserve * 0.55f),
                "mature" => (int)MathF.Round(baseReserve * 0.85f),
                "apex" => (int)MathF.Round(baseReserve * 1.45f),
                "reserve" => baseReserve + 110,
                _ => baseReserve
            };
            if (duringWave && PlanProfile(_wavePlan.TacticalProfileId, "plates"))
                baseReserve = (int)MathF.Round(baseReserve * 0.35f);
            baseReserve = (int)MathF.Round(baseReserve * PlanParameter("reserveMultiplier", 1f, 0f, 4f));
            if (_wavePlan.Parameters.TryGetValue("reserveCredits", out var reserveCredits) &&
                double.IsFinite(reserveCredits))
                baseReserve = (int)Math.Round(Math.Clamp(reserveCredits, 0, 2_000));
        }
        return Math.Max(0, baseReserve);
    }

    private int FoundationSize() => _strategy switch
    {
        AutoPlayerStrategy.Conservative or AutoPlayerStrategy.Spam or AutoPlayerStrategy.Control or AutoPlayerStrategy.Synergy => 4,
        AutoPlayerStrategy.Experienced => 3,
        _ => 3
    };

    private int DesiredTowerCount(int wave) => _strategy switch
    {
        AutoPlayerStrategy.Spam => 4 + wave,
        AutoPlayerStrategy.UpgradeFocused => 3 + wave / 4,
        AutoPlayerStrategy.Economy => 3 + wave / 3,
        AutoPlayerStrategy.Synergy => 4 + wave * 3 / 4,
        AutoPlayerStrategy.Experienced => ExperiencedCombatTowerCount(wave),
        _ => 3 + wave / 2
    };

    private static int ActiveOrNextWave(GameSession session) => Math.Max(1,
        session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);

    private static bool IsFinalCampaignWave(GameSession session) =>
        !session.IsEndlessMode && ActiveOrNextWave(session) >= session.TotalWaves;

    private bool ExperiencedPursuesApex(GameSession session)
    {
        if (!_useApexUpgrades || !session.IsFinalCampaignAct) return false;
        var apexWave = (int)PlanParameter("apexWave", GameConstants.CampaignWaveCount,
            GameConstants.ApexUnlockWave, GameConstants.CampaignWaveCount);
        return ActiveOrNextWave(session) >= apexWave;
    }

    private static int ExperiencedCombatTowerCount(int wave) =>
        ExperiencedRoleCount("needle_turret", wave) +
        ExperiencedRoleCount("shard_fan", wave) +
        ExperiencedRoleCount("breaker_cannon", wave) +
        ExperiencedRoleCount("ember_coil", wave) +
        ExperiencedRoleCount("frost_spire", wave) +
        ExperiencedRoleCount("prism_beam", wave) +
        ExperiencedRoleCount("siege_mortar", wave);

    private void ManageGenerator(GameSession session)
    {
        if (!session.TacticalSystemsEnabled) return;
        var nextWave = session.CurrentWave + 1;
        if (session.Generator is null)
        {
            var wantsGenerator = _strategy switch
            {
                AutoPlayerStrategy.Tactical => nextWave >= 5,
                AutoPlayerStrategy.Economy => nextWave >= 7,
                AutoPlayerStrategy.Experienced => false,
                _ => false
            };
            var requiredCombatTowers = _strategy switch
            {
                AutoPlayerStrategy.Tactical => 4,
                AutoPlayerStrategy.Experienced => 18,
                _ => 6
            };
            var definition = session.Content.Tactics.Generator;
            if (!wantsGenerator || session.Towers.Count(x => !x.IsSupport) < requiredCombatTowers ||
                session.Economy.Credits < definition.PurchaseCost + 70) return;
            var candidates = _placementCandidates
                .Where(x => session.ValidateTacticalPlacement(TacticalPlacementKind.ChargeForge, x) == PlacementFailure.None)
                .OrderByDescending(x => session.Map.Path.DistanceToPath(x))
                .Take(6)
                .ToArray();
            if (candidates.Length > 0) session.TryPlaceGenerator(candidates[_random.Next(candidates.Length)]);
            return;
        }

        var generator = session.Generator;
        var upgradeWave = _strategy == AutoPlayerStrategy.Experienced
            ? generator.LevelIndex == 0 ? 18 : 24
            : generator.LevelIndex == 0 ? 11 : 16;
        if (nextWave >= upgradeWave && generator.CanUpgrade && session.Economy.Credits >= generator.UpgradeCost + ReserveCredits(session, false))
            session.TryUpgradeGenerator();
    }

    private void TryUseEmergencyDefense(GameSession session)
    {
        if (!session.TacticalSystemsEnabled) return;
        if (session.Enemies.Count == 0) return;
        var lead = session.Enemies.Where(x => !x.IsDead && !x.HasEscaped).OrderByDescending(x => x.PathProgress).FirstOrDefault();
        if (lead is null) return;
        var tactical = _strategy == AutoPlayerStrategy.Tactical;
        var experienced = _strategy == AutoPlayerStrategy.Experienced;
        if (experienced && ExperiencedPursuesApex(session) &&
            session.Towers.Count(tower => tower.IsApex) < ExperiencedApexLimit() &&
            session.Towers.Any(session.CanApexUpgrade))
            return;
        var liveEnemies = session.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped);
        var rankedPressure = session.Enemies.Any(enemy => !enemy.IsDead && !enemy.HasEscaped &&
            enemy.Rank is EnemyRank.Elite or EnemyRank.Boss);
        var plateProgressOffset = PlanParameter("plateProgressOffset",
            _wavePlan?.TacticalProfileId.ToLowerInvariant() switch
            {
                "plates" => -0.08f,
                "conserve" => 0.10f,
                _ => 0f
            }, -0.25f, 0.25f);
        var experiencedPressure = experienced && (session.CurrentWave switch
        {
            < 9 => lead.PathProgress >= 0.82f + plateProgressOffset,
            < 11 => lead.PathProgress >= 0.74f + plateProgressOffset,
            < 15 => lead.PathProgress >= 0.62f + plateProgressOffset ||
                    liveEnemies >= 12 && lead.PathProgress >= 0.54f + plateProgressOffset ||
                    rankedPressure && lead.PathProgress >= 0.50f + plateProgressOffset,
            _ => lead.PathProgress >= 0.58f + plateProgressOffset ||
                 liveEnemies >= 10 && lead.PathProgress >= 0.46f + plateProgressOffset ||
                 rankedPressure && lead.PathProgress >= 0.42f + plateProgressOffset
        });
        var urgent = experiencedPressure || lead.PathProgress >= 0.82f ||
                     session.Economy.Lives <= session.Economy.StartingLives / 2;
        if (!urgent && !(tactical && session.Enemies.Count >= 7 && lead.PathProgress >= 0.55f)) return;
        var activeLimit = tactical ? 3 : experienced ? session.IsFinalCampaignAct ? 6 : 3 : 1;
        if (experienced && _wavePlan is not null)
        {
            if (PlanProfile(_wavePlan.TacticalProfileId, "plates")) activeLimit += 2;
            if (PlanProfile(_wavePlan.TacticalProfileId, "conserve")) activeLimit = Math.Max(1, activeLimit - 1);
            activeLimit = (int)PlanParameter("activePlateLimit", activeLimit, 0, 12);
        }
        if (session.EmergencyDefenses.Count >= activeLimit) return;
        var directPurchase = session.EmergencyInventory <= 0;
        var requiredCredits = 0;
        if (session.EmergencyInventory <= 0)
        {
            var directLimit = experienced
                ? session.IsFinalCampaignAct ? 10 : session.CurrentWave < 9 ? 0 : session.CurrentWave < 11 ? 1 : 4
                : 2;
            if (experienced && _wavePlan is not null)
            {
                if (PlanProfile(_wavePlan.TacticalProfileId, "plates")) directLimit += 2;
                if (PlanProfile(_wavePlan.TacticalProfileId, "conserve")) directLimit = Math.Max(0, directLimit - 2);
                directLimit = (int)PlanParameter("directPlateLimit", directLimit, 0, 16);
            }
            var directPurchaseAllowed = experienced ||
                                        session.Economy.Lives <= session.Economy.StartingLives / 2;
            if (_directEmergencyPurchasesThisWave >= directLimit) return;
            var reserve = experienced && lead.PathProgress >= 0.76f ? 0 : ReserveCredits(session, true);
            if (!directPurchaseAllowed) return;
            requiredCredits = session.CurrentEmergencyDirectPurchaseCost + reserve;
        }

        var position = FindBestEmergencyDefensePosition(session);
        if (position is null) return;
        if (directPurchase && session.Economy.Credits < requiredCredits &&
            !TrySellForEmergencyDefense(session, lead.PathProgress, requiredCredits)) return;
        if (directPurchase && session.Economy.Credits < requiredCredits) return;
        if (!session.TryDeployEmergencyDefense(position.Value)) return;
        if (directPurchase) _directEmergencyPurchasesThisWave++;
    }

    private Vector2? FindBestEmergencyDefensePosition(GameSession session)
    {
        var definition = session.Content.Tactics.EmergencyDefense;
        var path = session.Map.Path;
        var total = path.TotalLength;
        var live = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped)
            .OrderByDescending(enemy => enemy.DistanceAlongPath)
            .ThenBy(enemy => enemy.Id)
            .ToArray();
        if (live.Length == 0) return null;
        var lead = live[0];
        var lateCampaign = ActiveOrNextWave(session) >= 25;
        var clusterWeight = PlanParameter("plateClusterWeight", lateCampaign ? 0.45f : 1f, 0f, 4f);
        var leadWeight = PlanParameter("plateLeadWeight", lateCampaign ? 3f : 1f, 0f, 6f);

        var distances = new HashSet<int>();
        foreach (var enemy in live)
        {
            var armLead = MathF.Max(30f, enemy.CurrentSpeed * (definition.ArmTime + 0.35f));
            distances.Add((int)MathF.Round(enemy.DistanceAlongPath + armLead));
            distances.Add((int)MathF.Round(enemy.DistanceAlongPath + 72f));
            distances.Add((int)MathF.Round(enemy.DistanceAlongPath + 128f));
        }
        foreach (var progress in new[] { 0.58f, 0.66f, 0.74f, 0.81f, 0.87f, 0.92f, 0.96f })
            distances.Add((int)MathF.Round(total * progress));

        var candidates = new List<(Vector2 Position, float Score, float Distance, bool StallsLead)>();
        foreach (var rawDistance in distances.OrderBy(distance => distance))
        {
            var distance = Math.Clamp(rawDistance, 85f, total - 55f);
            var position = path.GetPosition(distance);
            if (session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position,
                    ignoreAvailability: true) != PlacementFailure.None)
                continue;

            var arrivals = live
                .Where(enemy => enemy.CurrentSpeed > 0.01f && distance > enemy.DistanceAlongPath)
                .Select(enemy => new
                {
                    Enemy = enemy,
                    Eta = (distance - enemy.DistanceAlongPath) / enemy.CurrentSpeed
                })
                .Where(arrival => arrival.Eta >= definition.ArmTime + 0.05f)
                .OrderBy(arrival => arrival.Eta)
                .ThenBy(arrival => arrival.Enemy.Id)
                .ToArray();
            if (arrivals.Length == 0 && session.Waves.QueuedEnemies == 0) continue;

            var triggerEta = arrivals.FirstOrDefault()?.Eta ?? 8f;
            var predictedHits = 0;
            var priorityValue = 0f;
            foreach (var arrival in arrivals)
            {
                var predictedDistance = arrival.Enemy.DistanceAlongPath + arrival.Enemy.CurrentSpeed * triggerEta;
                if (MathF.Abs(distance - predictedDistance) > definition.BlastRadius) continue;
                predictedHits++;
                priorityValue += arrival.Enemy.Rank == EnemyRank.Boss ? 5f :
                    arrival.Enemy.Rank == EnemyRank.Elite ? 2.5f : 1f;
                if (arrival.Enemy.SignalRole != EnemySignalRole.None) priorityValue += 1.2f;
                priorityValue += MathF.Min(2f,
                    (arrival.Enemy.Health + arrival.Enemy.Shield) / MathF.Max(1f, arrival.Enemy.MaxHealth));
            }

            var towerCoverage = 0f;
            foreach (var tower in session.Towers.Where(tower => !tower.IsSupport))
            {
                var range = session.GetEffectiveRange(tower);
                if (Vector2.DistanceSquared(tower.Position, position) > range * range) continue;
                towerCoverage += 1f + MathF.Min(4f, tower.InvestedCredits / 260f);
            }

            var queuedValue = session.Waves.QueuedEnemies > 0 ? MathF.Min(8f, session.Waves.QueuedEnemies * 0.15f) : 0f;
            var endpointRisk = distance / total > 0.92f ? 1.5f : 0f;
            var leadArrival = arrivals.FirstOrDefault(arrival => arrival.Enemy.Id == lead.Id);
            var leadStallValue = leadArrival is null
                ? -4f
                : 8f + lead.PathProgress * 10f +
                  (lead.Rank == EnemyRank.Boss ? 8f : lead.Rank == EnemyRank.Elite ? 4f : 0f) +
                  (lead.SignalRole == EnemySignalRole.None ? 0f : 2f);
            var score = predictedHits * 7f * clusterWeight + priorityValue * 2.5f +
                        towerCoverage * 0.65f + queuedValue + leadStallValue * leadWeight -
                        triggerEta * 0.08f - endpointRisk;
            candidates.Add((position, score, distance, leadArrival is not null));
        }

        IEnumerable<(Vector2 Position, float Score, float Distance, bool StallsLead)> ranked = candidates;
        var escapeProgress = PlanParameter("plateEscapeProgress", 0.98f, 0.5f, 0.98f);
        if (lead.PathProgress >= escapeProgress && candidates.Any(candidate => candidate.StallsLead))
            ranked = candidates.Where(candidate => candidate.StallsLead);
        return ranked.OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Distance)
            .Select(candidate => (Vector2?)candidate.Position)
            .FirstOrDefault();
    }

    private void TryUseOverdrive(GameSession session, ThreatProfile threat)
    {
        if (!session.ProtocolsEnabled || session.OverdriveCooldownRemaining > 0 || session.Enemies.Count == 0) return;
        var liveEnemies = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
        if (liveEnemies.Length == 0) return;
        var minimumEnemies = (int)PlanParameter("protocolMinimumEnemies",
            _wavePlan?.TacticalProfileId.ToLowerInvariant() switch
            {
                "protocols" => 2,
                "conserve" => 8,
                _ => 5
            }, 1, 30);
        var rankedPressure = liveEnemies.Any(enemy => enemy.Rank is EnemyRank.Elite or EnemyRank.Boss);
        var pressure = rankedPressure || liveEnemies.Length >= minimumEnemies ||
                       session.Economy.Lives <= session.Economy.StartingLives * 0.6f;
        if (!pressure) return;
        var candidate = session.Towers
            .Where(tower => !tower.IsOverdriven && !tower.IsApex)
            .Select(tower => new
            {
                Tower = tower,
                Targets = session.GetProtocolTargets(tower),
                Protocol = tower.Protocol
            })
            .Where(x => x.Targets.Count > 0)
            .Select(choice => new
            {
                choice.Tower,
                Score = (choice.Targets.Sum(enemy =>
                            8f + enemy.PathProgress * 22f +
                            (enemy.Health + enemy.Shield) / MathF.Max(1f, enemy.MaxHealth) * 12f +
                            (enemy.SignalRole == EnemySignalRole.None ? 0f : 24f) +
                            (enemy.Rank == EnemyRank.Boss ? 32f : enemy.Rank == EnemyRank.Elite ? 14f : 0f)) *
                        (1f + choice.Protocol.AttackSpeedBonus + choice.Protocol.DamageBonus +
                         choice.Protocol.AuraAttackSpeedBonus + choice.Protocol.AuraRangeBonus * 0.55f +
                         choice.Protocol.RangeBonus * 0.35f) +
                        ManualProtocolInvestmentValue(session, choice.Tower)) *
                        (choice.Tower.IsSupport
                            ? PlanParameter("protocolSupportBias",
                                ActiveOrNextWave(session) >= 28 ? 2.4f : 1f, 0.25f, 6f)
                            : 1f)
            })
            .OrderByDescending(choice => choice.Score)
            .ThenBy(x => x.Tower.Id)
            .FirstOrDefault();
        if (candidate is not null) session.TryOverdriveTower(candidate.Tower.Id);
    }

    private static float ManualProtocolInvestmentValue(GameSession session, TowerInstance tower)
    {
        if (!tower.IsSupport) return tower.InvestedCredits * 0.025f;

        var auraRange = session.GetEffectiveAuraRange(tower);
        var auraRangeSquared = auraRange * auraRange;
        var value = 0f;
        foreach (var recipient in session.Towers.Where(candidate => !candidate.IsSupport && !candidate.IsApex &&
                     Vector2.DistanceSquared(candidate.Position, tower.Position) <= auraRangeSquared))
        {
            var level = recipient.Level;
            var throughput = level.Damage * MathF.Max(0.25f, level.AttacksPerSecond) *
                             Math.Max(1, level.PelletCount);
            throughput += level.ChainDamage * level.ChainCount * level.AttacksPerSecond;
            throughput += level.BurnDamagePerSecond;
            value += recipient.InvestedCredits * 0.045f +
                     throughput * (tower.Protocol.AuraAttackSpeedBonus +
                                   tower.Protocol.AuraRangeBonus * 0.45f);
        }
        return value;
    }

    private float UpgradeBias() => _strategy switch
    {
        AutoPlayerStrategy.UpgradeFocused => 2.0f,
        AutoPlayerStrategy.Spam => 0.55f,
        AutoPlayerStrategy.Economy => 0.9f,
        AutoPlayerStrategy.Synergy => 1.35f,
        AutoPlayerStrategy.Experienced => 1.48f,
        _ => 1.12f
    };

    private float EconomyProfileMultiplier(bool purchasing)
    {
        if (_wavePlan is null) return 1f;
        return _wavePlan.EconomyProfileId.ToLowerInvariant() switch
        {
            "invest" => purchasing ? 1.18f : 0.92f,
            "mature" => purchasing ? 0.72f : 1.30f,
            "apex" => purchasing ? 0.55f : 1.12f,
            "reserve" => purchasing ? 0.82f : 0.90f,
            _ => 1f
        };
    }

    private float PlanParameter(string name, float fallback, float minimum, float maximum)
    {
        if (_wavePlan?.Parameters.TryGetValue(name, out var value) != true || !double.IsFinite(value))
            return fallback;
        return Math.Clamp((float)value, minimum, maximum);
    }

    private bool PlanProfile(string value, string profile) =>
        value.Equals(profile, StringComparison.OrdinalIgnoreCase);

    private static List<Vector2> BuildPlacementCandidates(GameSession session)
    {
        var points = new HashSet<(int X, int Y)>();
        foreach (var region in session.Map.BuildableRegions)
        {
            var left = region.Left + (int)GameConstants.TowerRadius;
            var right = region.Right - (int)GameConstants.TowerRadius;
            var top = region.Top + (int)GameConstants.TowerRadius;
            var bottom = region.Bottom - (int)GameConstants.TowerRadius;
            points.Add((region.Center.X, region.Center.Y));
            for (var y = top; y <= bottom; y += 24)
                for (var x = left; x <= right; x += 24)
                    points.Add((x, y));
            if (left <= right && top <= bottom)
            {
                points.Add((right, top));
                points.Add((left, bottom));
                points.Add((right, bottom));
            }
        }
        foreach (var node in session.Map.Definition.PowerNodes)
        {
            var center = node.Position.ToVector2();
            points.Add(((int)MathF.Round(center.X), (int)MathF.Round(center.Y)));
            if (node.Radius >= 34f)
            {
                foreach (var x in new[] { -24f, 24f })
                foreach (var y in new[] { -24f, 24f })
                    points.Add(((int)MathF.Round(center.X + x), (int)MathF.Round(center.Y + y)));
            }

            var ringRadius = MathF.Min(32f, MathF.Max(18f, node.Radius - 1f));
            for (var angleIndex = 0; angleIndex < 8; angleIndex++)
            {
                var angle = MathHelper.TwoPi * angleIndex / 8f;
                points.Add(((int)MathF.Round(center.X + MathF.Cos(angle) * ringRadius),
                    (int)MathF.Round(center.Y + MathF.Sin(angle) * ringRadius)));
            }
        }
        return points.Select(x => new Vector2(x.X, x.Y)).OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
    }

    private readonly record struct ApexInvestmentOption(TowerInstance Tower, float Score);
    private readonly record struct ExperiencedSaleOption(
        TowerInstance Tower,
        double EfficiencyLoss,
        double ForwardValue,
        float Coverage);
}
