using MinimalBastion.Core;
using MinimalBastion.Data;
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
    private int _lastRebalanceWave = -1;
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
    }

    public void PrepareForWave(GameSession session)
    {
        _directEmergencyPurchasesThisWave = 0;
        if (_holdBuild) return;
        var threat = ThreatProfile.From(session.Waves.NextWave, session.Content.Enemies);
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
        if (_useProtocols) TryUseOverdrive(session, threat);
        TryUseEmergencyDefense(session);
        Spend(session, threat, duringWave: true, 2);
    }

    private void Spend(GameSession session, ThreatProfile threat, bool duringWave, int actionLimit)
    {
        for (var action = 0; action < actionLimit; action++)
        {
            var reserve = ReserveCredits(session, duringWave);
            var spendable = session.Economy.Credits - reserve;
            if (spendable < 50) return;

            var foundation = FoundationSize();
            var combatTowerCount = session.Towers.Count(x => !x.IsSupport);
            if (!_holdFootprint && session.CurrentWave == 0 && combatTowerCount < foundation && TryBuyFoundation(session, threat, spendable))
                continue;
            if (!_holdFootprint && !session.IsFinalCampaignAct)
            {
                if (TryBuyStrategicPriority(session, threat, spendable, out var savingForPriority))
                    continue;
                if (savingForPriority) return;
            }

            if (_strategy == AutoPlayerStrategy.Experienced &&
                TryExperiencedMilestoneUpgrade(session, threat, spendable))
                continue;

            var purchase = _holdFootprint ? null : BestPurchase(session, threat, spendable);
            var upgrade = BestUpgrade(session, threat, spendable);
            var purchaseBias = combatTowerCount < DesiredTowerCount(session.CurrentWave + (duringWave ? 0 : 1))
                ? 1.45f
                : session.IsFinalCampaignAct ? 0.16f : 0.38f;
            var buyScore = purchase?.Score * purchaseBias ?? float.MinValue;
            var upgradeScore = upgrade?.Score * UpgradeBias() ?? float.MinValue;

            if (buyScore <= 0 && upgradeScore <= 0) return;
            if (purchase is { } buy && buyScore >= upgradeScore)
            {
                if (!session.TryPlaceTower(buy.Definition.Id, buy.Position)) return;
                ConfigureTargeting(session, session.Towers[^1], threat);
                continue;
            }

            if (upgrade is { } up)
            {
                var upgraded = up.DoctrineId is not null
                    ? session.TryChooseTowerDoctrine(up.Tower.Id, up.DoctrineId)
                    : up.SpecializationId is not null
                        ? session.TrySpecializeTower(up.Tower.Id, up.SpecializationId)
                        : session.TryUpgradeTower(up.Tower.Id);
                if (upgraded) continue;
            }
            return;
        }
    }

    private bool TryBuyFoundation(GameSession session, ThreatProfile threat, int spendable)
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
            ConfigureTargeting(session, session.Towers[^1], threat);
            return true;
        }
        return false;
    }

    private bool TryBuyStrategicPriority(GameSession session, ThreatProfile threat, int spendable, out bool savingForPriority)
    {
        savingForPriority = false;
        var wave = Math.Max(1, session.Waves.ActiveWave?.Number ?? session.Waves.NextWave?.Number ?? session.CurrentWave + 1);
        if (_strategy == AutoPlayerStrategy.Experienced)
            return TryBuyExperiencedPriority(session, threat, spendable, wave, out savingForPriority);

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
                    if (TryBuyFromPool(session, threat, spendable, missingIdentity)) return true;
                    savingForPriority = MustSaveForPool(session, missingIdentity);
                    return false;
                }

                if (session.Towers.Count(x => ids.Contains(x.Definition.Id, StringComparer.OrdinalIgnoreCase)) >= desired)
                    goto SupportPriority;
                var underrepresented = UnderrepresentedPool(session, ids);
                if (TryBuyFromPool(session, threat, spendable, underrepresented)) return true;
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
            if (TryBuyFromPool(session, threat, spendable, supportPool)) return true;
            savingForPriority = MustSaveForPool(session, supportPool);
        }

        return false;
    }

    private bool TryBuyExperiencedPriority(GameSession session, ThreatProfile threat, int spendable, int wave,
        out bool savingForPriority)
    {
        savingForPriority = false;
        var priorities = new List<(string Id, bool Urgent)>();

        void Require(string id, bool condition, bool urgent = false)
        {
            if (condition && session.IsTowerAvailable(id) && session.Towers.All(tower => tower.Definition.Id != id))
                priorities.Add((id, urgent));
        }

        Require("frost_spire", wave >= 2 || threat.Fast >= 0.12f, threat.Fast >= 0.22f);
        Require("shard_fan", wave >= 3, threat.Swarm >= 0.55f);
        Require("breaker_cannon", wave >= 5, threat.Armored >= 0.15f || threat.HasBoss);
        Require("arc_relay", wave >= 6, threat.Swarm >= 0.55f);
        Require("prism_beam", wave >= 8, wave >= 9 || threat.Shielded >= 0.10f || threat.HasBoss);
        Require("ember_coil", wave >= 8 && (threat.Durable > 0 || threat.HasElite || threat.HasBoss),
            threat.Durable >= 0.15f);
        Require("siege_mortar", wave >= 9 && threat.Swarm >= 0.38f, threat.Swarm >= 0.62f);
        Require("watchtower", wave >= 10 && (threat.HasElite || threat.HasBoss || threat.Durable >= 0.12f),
            threat.HasBoss);

        foreach (var priority in priorities)
        {
            if (!session.Content.Towers.TryGetValue(priority.Id, out var definition)) continue;
            if (definition.PurchaseCost <= spendable)
            {
                var position = FindBestPosition(session, definition, threat);
                if (position is not null && session.TryPlaceTower(definition.Id, position.Value))
                {
                    ConfigureTargeting(session, session.Towers[^1], threat);
                    return true;
                }
            }

            // Preserve a counter reserve only when the incoming wave makes that
            // missing role immediately important. Otherwise improve the existing grid.
            if (priority.Urgent && session.Economy.Credits < definition.PurchaseCost)
            {
                savingForPriority = true;
                return false;
            }
        }

        var combatTowers = session.Towers.Count(tower => !tower.IsSupport);
        var desiredBeacons = combatTowers >= 15 ? 2 : combatTowers >= 7 ? 1 : 0;
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

    private static bool MustSaveForPool(GameSession session, IReadOnlyList<string> ids)
    {
        var costs = ids.Where(id => session.IsTowerAvailable(id) && session.Content.Towers.ContainsKey(id)).Select(id => session.Content.Towers[id].PurchaseCost).ToArray();
        return costs.Length > 0 && session.Economy.Credits < costs.Min();
    }

    private bool TryBuyFromPool(GameSession session, ThreatProfile threat, int spendable, IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            if (!session.IsTowerAvailable(id) || !session.Content.Towers.TryGetValue(id, out var definition) || definition.PurchaseCost > spendable) continue;
            var position = FindBestPosition(session, definition, threat);
            if (position is null) continue;
            if (!session.TryPlaceTower(definition.Id, position.Value)) continue;
            ConfigureTargeting(session, session.Towers[^1], threat);
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
            var current = UpgradeValue(session, tower, tower.Level, threat);
            if (tower.RequiresDoctrine)
            {
                TowerDoctrineDefinition? selectedDoctrine = null;
                var selectedFit = float.MinValue;
                var upgradePace = 0f;
                var doctrineCandidates = tower.Definition.Tier2Doctrines.Where(x => x.UpgradeCost <= spendable);
                if (IsForcedTower(tower) && _forcedDoctrineId is not null)
                    doctrineCandidates = doctrineCandidates.Where(doctrine => doctrine.Id.Equals(_forcedDoctrineId, StringComparison.OrdinalIgnoreCase));
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
                    var fit = doctrineValue * doctrineWeight;
                    if (fit > selectedFit)
                    {
                        selectedFit = fit;
                        selectedDoctrine = doctrine;
                    }

                    // Branch foresight must not make the tower jump ahead of unrelated
                    // purchases. Retain the immediate-value upgrade cadence that the
                    // baseline balance matrix was tuned against.
                    upgradePace = MathF.Max(upgradePace, immediateGainPerCredit * doctrineWeight);
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
                foreach (var specialization in specializationCandidates)
                {
                    var next = UpgradeValue(session, tower, specialization.Level.WithDoctrine(tower.Doctrine), threat);
                    Consider(new UpgradeOption(tower, null, specialization.Id,
                        MathF.Max(0.01f, next - current) * StrategyWeight(tower.Definition.Id, threat) *
                        SpecializationWeight(tower.Definition.Id, specialization.Id, threat) / specialization.UpgradeCost));
                }
                continue;
            }
            if (_useApexUpgrades && session.CanApexUpgrade(tower) && tower.ApexUpgradeCost <= spendable)
            {
                var apexNext = UpgradeValue(session, tower, tower.ApexPreviewLevel, threat);
                Consider(new UpgradeOption(tower, null, null,
                    MathF.Max(0.01f, apexNext - current) * StrategyWeight(tower.Definition.Id, threat) /
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

    private bool TryExperiencedMilestoneUpgrade(GameSession session, ThreatProfile threat, int spendable)
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
            return TryUpgradeByFit(session, tower, threat, spendable);
        }

        return false;
    }

    private bool TryUpgradeByFit(GameSession session, TowerInstance tower, ThreatProfile threat, int spendable)
    {
        if (tower.RequiresDoctrine)
        {
            var doctrine = tower.Definition.Tier2Doctrines
                .Where(candidate => candidate.UpgradeCost <= spendable)
                .Select(candidate => new
                {
                    Definition = candidate,
                    Score = UpgradeValue(session, tower, tower.Definition.Levels[1].WithDoctrine(candidate), threat) *
                            DoctrineWeight(candidate, threat) / Math.Max(1, candidate.UpgradeCost)
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Definition.Id)
                .FirstOrDefault();
            return doctrine is not null && session.TryChooseTowerDoctrine(tower.Id, doctrine.Definition.Id);
        }

        if (tower.RequiresSpecialization)
        {
            var specialization = tower.Definition.Specializations
                .Where(candidate => candidate.UpgradeCost <= spendable)
                .Select(candidate => new
                {
                    Definition = candidate,
                    Score = UpgradeValue(session, tower, candidate.Level.WithDoctrine(tower.Doctrine), threat) *
                            SpecializationWeight(tower.Definition.Id, candidate.Id, threat) /
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
            var nearBest = eligible
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Position.Y)
                .ThenBy(candidate => candidate.Position.X)
                .Take(3)
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

        var progress = PathProgressNear(session, position);
        score *= 1f + (1f - MathF.Abs(progress - 0.58f) * 2f) * 0.035f;
        return score;
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

    private static int ExperiencedCopyLimit(string towerId, int wave) => towerId switch
    {
        "needle_turret" => wave < 8 ? 4 : wave < 15 ? 6 : 9,
        "frost_spire" => 4,
        "shard_fan" => 5,
        "watchtower" => 5,
        "ember_coil" => 5,
        "breaker_cannon" => 6,
        "arc_relay" => 6,
        "siege_mortar" => 5,
        "prism_beam" => 5,
        "signal_beacon" => 3,
        _ => 6
    };

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
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
            return 16f + level.AuraAttackSpeedBonus * 90f + level.AuraRangeBonus * 55f + level.AuraRange * 0.03f;

        var directDps = level.Damage * level.AttacksPerSecond;
        var value = directDps;
        value += level.BurnDamagePerSecond * MathF.Min(1f, level.BurnDuration * level.AttacksPerSecond);
        value *= 1f + MathF.Max(0, level.PelletCount - 1) * (0.25f + threat.Swarm * 0.65f);
        if (level.RicochetRange > 0)
            value += directDps * level.RicochetDamageMultiplier * (0.25f + threat.Swarm * 0.75f);
        if (level.ChainCount > 0)
            value += level.ChainDamage * level.ChainCount * level.AttacksPerSecond * (0.35f + threat.Swarm * 0.65f);
        if (level.SplashRadius > 0) value *= 1.12f + threat.Swarm * MathF.Min(2.4f, level.SplashRadius / 24f);
        value += level.ArmorPierce * level.AttacksPerSecond * threat.Armored * 1.8f;
        value += level.ArmorReduction * threat.Armored * 2.2f;
        value += level.SlowPercent * level.SlowDuration * (8f + threat.Fast * 20f);
        value += level.ExposePercent * (12f + threat.Durable * 30f);
        value += level.StunDuration * (4f + threat.Fast * 12f);
        var rankedThreat = threat.HasBoss ? 1f : threat.HasElite ? 0.35f : 0f;
        value += rankedThreat * (level.Damage * 0.35f + level.ArmorPierce * 2.5f + level.ExposePercent * 45f);
        if (threat.HasBoss && (level.PelletCount > 1 || level.SplashRadius > 0 || level.RicochetRange > 0)) value *= 0.94f;
        value *= 1f + MathHelper.Clamp((level.Range - 115f) / 650f, 0, 0.32f);
        if (threat.Armored > 0.3f && level.Damage < 12 && level.ArmorPierce <= 0) value *= 0.72f;
        if (threat.Shielded > 0.2f && level.IgnoreShield) value *= 1.25f;
        return MathF.Max(0.1f, value);
    }

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

    private void ConfigureTargeting(GameSession session, TowerInstance tower, ThreatProfile threat)
    {
        if (_strategy == AutoPlayerStrategy.Experienced)
        {
            var experiencedMode = tower.Definition.Id switch
            {
                "frost_spire" => TargetMode.Fastest,
                "breaker_cannon" => session.Challenge.CounterPressureEnabled ? TargetMode.Support : TargetMode.Armored,
                "watchtower" or "prism_beam" => session.Challenge.CounterPressureEnabled ? TargetMode.Support : TargetMode.Strongest,
                "siege_mortar" => session.Challenge.CounterPressureEnabled ? TargetMode.Support : TargetMode.First,
                "ember_coil" => TargetMode.Strongest,
                _ => TargetMode.First
            };
            session.TrySetTargetMode(tower.Id, experiencedMode);
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

    private void TryRebalance(GameSession session, ThreatProfile threat)
    {
        if (_strategy != AutoPlayerStrategy.Adaptive || session.CurrentWave < 9 || _lastRebalanceWave == session.CurrentWave) return;
        TowerInstance? mismatch = null;
        if (threat.Armored > 0.55f)
            mismatch = session.Towers.FirstOrDefault(x => x.LevelIndex == 0 && x.Definition.Id == "shard_fan");
        else if (threat.Swarm > 0.65f)
            mismatch = session.Towers.FirstOrDefault(x => x.LevelIndex == 0 && x.Definition.Id == "breaker_cannon");
        if (mismatch is null || session.Towers.Count <= 6) return;
        if (session.TrySellTower(mismatch.Id)) _lastRebalanceWave = session.CurrentWave;
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
            AutoPlayerStrategy.Experienced => 35,
            _ => 25
        };
        if (duringWave && session.Economy.Lives < session.Economy.StartingLives * 0.6f) return 0;
        return baseReserve;
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
        AutoPlayerStrategy.Experienced => 4 + wave,
        _ => 3 + wave / 2
    };

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
                AutoPlayerStrategy.Experienced => nextWave >= 15,
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
        var urgent = lead.PathProgress >= 0.82f || session.Economy.Lives <= session.Economy.StartingLives / 2;
        if (!urgent && !(tactical && session.Enemies.Count >= 7 && lead.PathProgress >= 0.55f)) return;
        var activeLimit = tactical ? 3 : 1;
        if (session.EmergencyDefenses.Count >= activeLimit) return;
        if (session.EmergencyInventory <= 0)
        {
            var directLimit = 2;
            var directPurchaseAllowed = session.Economy.Lives <= session.Economy.StartingLives / 2;
            if (_directEmergencyPurchasesThisWave >= directLimit) return;
            if (!directPurchaseAllowed || session.Economy.Credits < session.CurrentEmergencyDirectPurchaseCost + ReserveCredits(session, true)) return;
        }

        var total = session.Map.Path.TotalLength;
        var leadDistance = lead.DistanceAlongPath;
        var candidateDistances = new[]
        {
            MathF.Min(total - 85, leadDistance + 38),
            MathF.Min(total - 85, leadDistance + 90),
            total * 0.88f,
            total * 0.74f,
            total * 0.60f
        };
        var directPurchase = session.EmergencyInventory <= 0;
        foreach (var distance in candidateDistances)
        {
            if (!session.TryDeployEmergencyDefense(session.Map.Path.GetPosition(MathF.Max(85, distance)))) continue;
            if (directPurchase) _directEmergencyPurchasesThisWave++;
            return;
        }
    }

    private static void TryUseOverdrive(GameSession session, ThreatProfile threat)
    {
        if (!session.ProtocolsEnabled || session.OverdriveCooldownRemaining > 0 || session.Enemies.Count == 0) return;
        var pressure = threat.HasBoss || threat.HasElite || session.Enemies.Count >= 5 ||
                       session.Economy.Lives <= session.Economy.StartingLives * 0.6f;
        if (!pressure) return;
        var candidate = session.Towers
            .Where(tower => !tower.IsOverdriven)
            .Select(tower => new
            {
                Tower = tower,
                Targets = session.GetProtocolTargets(tower).Count
            })
            .Where(x => x.Targets > 0)
            .OrderByDescending(x => x.Targets * 20 + x.Tower.InvestedCredits)
            .ThenBy(x => x.Tower.Id)
            .FirstOrDefault();
        if (candidate is not null) session.TryOverdriveTower(candidate.Tower.Id);
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
}
