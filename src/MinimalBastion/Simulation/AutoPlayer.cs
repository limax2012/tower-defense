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
    private int _lastRebalanceWave = -1;
    private int _directEmergencyPurchasesThisWave;

    public AutoPlayer(GameSession session, AutoPlayerStrategy strategy, int seed)
    {
        _strategy = strategy;
        _random = new Random(seed);
        _placementCandidates = BuildPlacementCandidates(session);
    }

    public void PrepareForWave(GameSession session)
    {
        _directEmergencyPurchasesThisWave = 0;
        var threat = ThreatProfile.From(session.Waves.NextWave, session.Content.Enemies);
        TryRebalance(session, threat);
        ManageGenerator(session);
        Spend(session, threat, duringWave: false, 24);
    }

    public void ReactDuringWave(GameSession session)
    {
        var threat = ThreatProfile.From(session.Waves.ActiveWave, session.Content.Enemies);
        TryUseOverdrive(session, threat);
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
            if (session.CurrentWave == 0 && combatTowerCount < foundation && TryBuyFoundation(session, threat, spendable))
                continue;
            if (TryBuyStrategicPriority(session, threat, spendable, out var savingForPriority))
                continue;
            if (savingForPriority) return;

            var purchase = BestPurchase(session, threat, spendable);
            var upgrade = BestUpgrade(session, threat, spendable);
            var purchaseBias = combatTowerCount < DesiredTowerCount(session.CurrentWave + (duringWave ? 0 : 1)) ? 1.45f : 0.38f;
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
                var upgraded = up.SpecializationId is null
                    ? session.TryUpgradeTower(up.Tower.Id)
                    : session.TrySpecializeTower(up.Tower.Id, up.SpecializationId);
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
            AutoPlayerStrategy.Randomized => session.Content.Towers.Values.Where(x => !x.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && x.PurchaseCost <= 250).OrderBy(_ => _random.Next()).Select(x => x.Id).ToArray(),
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
            ids = StrategicPoolForWave(ids, wave);
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
        var wantsSupport = _strategy is AutoPlayerStrategy.Conservative or AutoPlayerStrategy.UpgradeFocused or AutoPlayerStrategy.LongRange or AutoPlayerStrategy.Control or AutoPlayerStrategy.Adaptive;
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

    private static bool MustSaveForPool(GameSession session, IReadOnlyList<string> ids)
    {
        var costs = ids.Where(session.Content.Towers.ContainsKey).Select(id => session.Content.Towers[id].PurchaseCost).ToArray();
        return costs.Length > 0 && session.Economy.Credits < costs.Min();
    }

    private bool TryBuyFromPool(GameSession session, ThreatProfile threat, int spendable, IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            if (!session.Content.Towers.TryGetValue(id, out var definition) || definition.PurchaseCost > spendable) continue;
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
            if (!IsAllowedPurchase(definition.Id)) continue;
            if (definition.PurchaseCost > spendable) continue;
            if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && session.Towers.Count(x => !x.IsSupport) < 4) continue;
            var existingCopies = session.Towers.Count(x => x.Definition.Id == definition.Id);
            var copyLimit = _strategy == AutoPlayerStrategy.Spam ? 30 : definition.Id switch
            {
                "needle_turret" => 7,
                "signal_beacon" => 3,
                _ => 10
            };
            if (existingCopies >= copyLimit) continue;
            var position = FindBestPosition(session, definition, threat);
            if (position is null) continue;
            var positionScore = PositionScore(session, definition, position.Value);
            var score = TowerValue(definition, 0, threat) * StrategyWeight(definition.Id, threat) / definition.PurchaseCost;
            score *= 0.65f + MathF.Min(1.4f, positionScore / 14f);
            var repetitionPenalty = _strategy == AutoPlayerStrategy.Spam ? 0.08f : 0.35f;
            score /= 1f + existingCopies * repetitionPenalty;
            score *= 0.96f + (float)_random.NextDouble() * 0.08f;
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
            AutoPlayerStrategy.Tactical => towerId is "needle_turret" or "frost_spire" or "watchtower" or "breaker_cannon" or "signal_beacon",
            _ => true
        };
    }

    private UpgradeOption? BestUpgrade(GameSession session, ThreatProfile threat, int spendable)
    {
        UpgradeOption? best = null;
        foreach (var tower in session.Towers)
        {
            var current = TowerValue(tower.Definition, tower.Level, threat);
            if (tower.RequiresSpecialization)
            {
                foreach (var specialization in tower.Definition.Specializations.Where(x => x.UpgradeCost <= spendable))
                {
                    var next = TowerValue(tower.Definition, specialization.Level, threat);
                    Consider(new UpgradeOption(tower, specialization.Id,
                        MathF.Max(0.01f, next - current) * StrategyWeight(tower.Definition.Id, threat) *
                        SpecializationWeight(tower.Definition.Id, specialization.Id, threat) / specialization.UpgradeCost));
                }
                continue;
            }
            if (!tower.CanUpgrade || tower.UpgradeCost > spendable) continue;
            var linearNext = TowerValue(tower.Definition, tower.Definition.Levels[tower.LevelIndex + 1], threat);
            Consider(new UpgradeOption(tower, null,
                MathF.Max(0.01f, linearNext - current) * StrategyWeight(tower.Definition.Id, threat) / tower.UpgradeCost));
        }
        return best;

        void Consider(UpgradeOption option)
        {
            var score = option.Score * (0.98f + (float)_random.NextDouble() * 0.04f);
            if (_strategy == AutoPlayerStrategy.Randomized) score *= 0.6f + (float)_random.NextDouble();
            option = option with { Score = score };
            if (best is null || score > best.Value.Score) best = option;
        }
    }

    private Vector2? FindBestPosition(GameSession session, TowerDefinition definition, ThreatProfile threat)
    {
        var eligible = new List<(Vector2 Position, float Score)>();
        foreach (var position in _placementCandidates)
        {
            if (session.ValidatePlacement(definition.Id, position) != PlacementFailure.None) continue;
            var score = PositionScore(session, definition, position);
            if (_strategy == AutoPlayerStrategy.Conservative) score *= 1f + PathProgressNear(session, position) * 0.18f;
            if (threat.Fast > 0.30f) score *= 1f + PathProgressNear(session, position) * 0.12f;
            eligible.Add((position, score));
        }
        if (eligible.Count == 0) return null;
        var ordered = eligible.OrderByDescending(x => x.Score).Take(_strategy == AutoPlayerStrategy.Randomized ? 8 : 3).ToArray();
        return ordered[_random.Next(ordered.Length)].Position;
    }

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
        if (threat.HasBoss && (level.PelletCount > 1 || level.SplashRadius > 0)) value *= 0.94f;
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
            AutoPlayerStrategy.Tactical => towerId switch { "needle_turret" => 1.30f, "frost_spire" => 1.22f, "watchtower" => 1.16f, "breaker_cannon" => 1.08f, _ => 0.82f },
            AutoPlayerStrategy.Adaptive => AdaptiveWeight(towerId, threat),
            AutoPlayerStrategy.Randomized => 0.75f + (float)_random.NextDouble() * 0.5f,
            _ => 1f
        };
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

    private void ConfigureTargeting(GameSession session, TowerInstance tower, ThreatProfile threat)
    {
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
            AutoPlayerStrategy.Tactical => session.Generator is null && session.CurrentWave >= 4
                ? session.Content.Tactics.Generator.PurchaseCost
                : 70,
            AutoPlayerStrategy.Aggressive or AutoPlayerStrategy.Spam => 0,
            _ => 25
        };
        if (duringWave && session.Economy.Lives < session.Economy.StartingLives * 0.6f) return 0;
        return baseReserve;
    }

    private int FoundationSize() => _strategy switch
    {
        AutoPlayerStrategy.Conservative or AutoPlayerStrategy.Spam or AutoPlayerStrategy.Control => 4,
        _ => 3
    };

    private int DesiredTowerCount(int wave) => _strategy switch
    {
        AutoPlayerStrategy.Spam => 4 + wave,
        AutoPlayerStrategy.UpgradeFocused => 3 + wave / 4,
        AutoPlayerStrategy.Economy => 3 + wave / 3,
        _ => 3 + wave / 2
    };

    private void ManageGenerator(GameSession session)
    {
        var nextWave = session.CurrentWave + 1;
        if (session.Generator is null)
        {
            var wantsGenerator = _strategy switch
            {
                AutoPlayerStrategy.Tactical => nextWave >= 5,
                AutoPlayerStrategy.Economy => nextWave >= 7,
                _ => false
            };
            var requiredCombatTowers = _strategy == AutoPlayerStrategy.Tactical ? 4 : 6;
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
        var upgradeWave = generator.LevelIndex == 0 ? 11 : 16;
        if (nextWave >= upgradeWave && generator.CanUpgrade && session.Economy.Credits >= generator.UpgradeCost + ReserveCredits(session, false))
            session.TryUpgradeGenerator();
    }

    private void TryUseEmergencyDefense(GameSession session)
    {
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
        if (session.OverdriveCooldownRemaining > 0 || session.Enemies.Count == 0) return;
        var pressure = threat.HasBoss || threat.HasElite || session.Enemies.Count >= 5 ||
                       session.Economy.Lives <= session.Economy.StartingLives * 0.6f;
        if (!pressure) return;
        var candidate = session.Towers
            .Where(tower => !tower.IsSupport && !tower.IsOverdriven)
            .Select(tower => new
            {
                Tower = tower,
                Targets = session.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                    Vector2.DistanceSquared(tower.Position, enemy.Position) <= session.GetEffectiveRange(tower) * session.GetEffectiveRange(tower))
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
        return points.Select(x => new Vector2(x.X, x.Y)).OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
    }
}
