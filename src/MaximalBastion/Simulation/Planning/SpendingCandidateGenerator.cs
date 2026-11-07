using System.Globalization;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Tactics;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Simulation.Planning;

public sealed record CandidateGenerationOptions
{
    public int MaximumCandidates { get; init; } = 50;
    public int PositionsPerTower { get; init; } = 3;
    public float SpatialStep { get; init; } = 40;
    public float RefinementStep { get; init; } = 12;
    public bool AllowCombatPurchases { get; init; } = true;
    public bool AllowSaving { get; init; } = true;
    public bool AllowSupport { get; init; } = true;
    public bool UsePlacementHeuristics { get; init; } = true;
    public bool IncludeTactical { get; init; } = true;
    public bool IncludeSales { get; init; } = true;
    public string Family { get; init; } = "unrestricted";
    public string InformationProfile { get; init; } = "oracle";
    public string? ExcludedTowerId { get; init; }
    public int ExplorationOffset { get; init; }
}

public sealed record SpendingCandidate
{
    public string Id { get; init; } = "";
    public string Family { get; init; } = "";
    public IReadOnlyList<GameCommand> Commands { get; init; } = Array.Empty<GameCommand>();
    public int? WaitForCredits { get; init; }
    public int Cost { get; init; }
    public float Score { get; init; }
    public string Reason { get; init; } = "";
    public int ReserveCredits { get; init; }
}

/// <summary>Finite proposals rank legal investments; authoritative rollouts determine their value.</summary>
public static class SpendingCandidateGenerator
{
    public static IReadOnlyList<string> StrategyFamilies { get; } =
    ["unrestricted", "concentrated", "coverage", "range", "area", "control", "counter", "economy", "deployable"];

    public static IReadOnlyDictionary<string, string> StrategyDescriptions { get; } = new Dictionary<string, string>
    {
        ["unrestricted"] = "All available tower families and authored branches; geometry and marginal utility rank investments.",
        ["concentrated"] = "All available actions; new-tower scores are multiplied by 0.55 after four towers to prefer investment in the existing footprint.",
        ["coverage"] = "All available actions; upgrade scores are multiplied by 0.6 to prefer broader coverage.",
        ["range"] = "Purchases require an authored tower level with range at least 200; support remains available.",
        ["area"] = "Purchases require splash, chain, multiple pellets, or burning at base level; support remains available.",
        ["control"] = "Purchases require slow, stun, expose, armor reduction, or burning at base level; support remains available.",
        ["counter"] = "Purchases require armor piercing, shield bypass, armor reduction, or burning at base level; support remains available.",
        ["economy"] = "All available actions; deferred investments score 1.25 times higher and existing-tower upgrades 1.1 times higher. After three attackers, Forge investments receive at least 1.2 times the best combat-investment score.",
        ["deployable"] = "All available actions; live Protocol and Plate proposals score three times higher. After an attacker exists and free Plate inventory is exhausted, purchases retain the actual current direct Plate price when saving is enabled."
    };

    public static IReadOnlyList<SpendingCandidate> Generate(GameSession session, CandidateGenerationOptions? options = null)
    {
        options ??= new();
        if (options.MaximumCandidates < 1 || options.PositionsPerTower < 1 ||
            !float.IsFinite(options.SpatialStep) || options.SpatialStep < 4 ||
            !float.IsFinite(options.RefinementStep) || options.RefinementStep < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (!StrategyFamilies.Contains(options.Family, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown strategy family '{options.Family}'.", nameof(options));
        if (options.InformationProfile is not ("oracle" or "informed"))
            throw new ArgumentException("Information profile must be oracle or informed.", nameof(options));
        if (options.ExcludedTowerId is not null && !session.Content.Towers.ContainsKey(options.ExcludedTowerId))
            throw new ArgumentException($"Unknown excluded tower '{options.ExcludedTowerId}'.", nameof(options));
        if (session.IsVictory || session.IsDefeat) return Array.Empty<SpendingCandidate>();
        var candidates = new List<SpendingCandidate>();
        if (options.AllowSaving)
            candidates.Add(new() { Id = "wait", Family = "saving", Reason = "Retain all credits and let the current board fight." });
        var threat = ReadThreat(session, options.InformationProfile);
        var allowPurchase = !session.Waves.IsActive || options.AllowCombatPurchases;
        var positions = BuildAnchors(session, options);
        var placements = new List<(TowerDefinition Definition, Vector2 Position, float Score)>();
        if (allowPurchase)
        {
            foreach (var tower in session.Towers.Where(tower => !IsExcluded(tower.Definition, options)).OrderBy(tower => tower.Id))
                AddUpgradeCandidates(session, tower, options, threat, candidates);
            foreach (var definition in session.Content.Towers.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (!session.IsTowerAvailable(definition.Id) || !Allowed(definition, options)) continue;
                var ranked = positions.Where(position => PlacementLegal(session, definition.Id, position))
                    .Select(position => (Position: position, Score: PlacementValue(session, definition, definition.Levels[0], position, threat, options)))
                    .OrderByDescending(value => value.Score).ThenBy(value => value.Position.Y).ThenBy(value => value.Position.X).ToList();
                var refined = new List<(Vector2 Position, float Score)>();
                foreach (var anchor in ranked.Take(3))
                foreach (var dx in new[] { -options.RefinementStep, 0, options.RefinementStep })
                foreach (var dy in new[] { -options.RefinementStep, 0, options.RefinementStep })
                {
                    var position = anchor.Position + new Vector2(dx, dy);
                    if (PlacementLegal(session, definition.Id, position))
                        refined.Add((position, PlacementValue(session, definition, definition.Levels[0], position, threat, options)));
                }
                ranked.AddRange(refined);
                var chosen = new List<(Vector2 Position, float Score)>();
                foreach (var value in ranked.OrderByDescending(value => value.Score))
                {
                    if (chosen.Any(existing => Vector2.DistanceSquared(existing.Position, value.Position) < 32 * 32)) continue;
                    chosen.Add(value);
                    if (chosen.Count >= options.PositionsPerTower) break;
                }
                // One rotating anchor keeps score preferences from permanently excluding a legal neighborhood.
                if (ranked.Count > 0)
                    chosen.Add(ranked[(int)((uint)options.ExplorationOffset % (uint)ranked.Count)]);
                foreach (var value in chosen.DistinctBy(value => value.Position))
                {
                    var command = Place(definition.Id, value.Position);
                    var score = value.Score / Math.Max(1, definition.PurchaseCost);
                    Add(candidates, session, options, "purchase:" + definition.Id, [command], definition.PurchaseCost,
                        score, "Measured route exposure, current support geometry, and authored attack properties rank this placement.");
                    placements.Add((definition, value.Position, value.Score));
                    AddNewTowerBundles(session, definition, value.Position, value.Score, options, candidates);
                }
            }
            AddPurchasePairs(session, options, placements, candidates);
            AddInvestmentPairs(session, options, candidates);
            AddForge(session, options, positions, candidates);
            if (options.IncludeSales && session.SellingEnabled)
                foreach (var tower in session.Towers.OrderBy(tower => tower.LifetimeDamage).ThenBy(tower => tower.Id).Take(3))
                    candidates.Add(new() { Id = $"sell:{tower.Id}", Family = "sale", Cost = -tower.SellValue,
                        Commands = [Command(GameCommandType.SellTower, tower.Id)], Score = 0.001f,
                        Reason = "Recover the actual sell refund; subsequent branch decisions can relocate or concentrate investment." });
        }
        AddTargeting(session, candidates);
        if (options.IncludeTactical) AddTactical(session, options, candidates);
        return Diverse(ApplyFamilyPreferences(session, options, candidates), options.MaximumCandidates);
    }

    public static float ExposureLength(GameSession session, Vector2 center, float radius)
    {
        if (radius <= 0) return 0;
        var total = 0f;
        var path = session.Map.Definition.Path;
        for (var i = 1; i < path.Count; i++)
        {
            var start = path[i - 1].ToVector2();
            var delta = path[i].ToVector2() - start;
            var lengthSquared = delta.LengthSquared();
            if (lengthSquared <= 0) continue;
            var offset = start - center;
            var b = 2 * Vector2.Dot(offset, delta);
            var c = offset.LengthSquared() - radius * radius;
            var discriminant = b * b - 4 * lengthSquared * c;
            if (discriminant <= 0) continue;
            var root = MathF.Sqrt(discriminant);
            var from = MathF.Max(0, (-b - root) / (2 * lengthSquared));
            var to = MathF.Min(1, (-b + root) / (2 * lengthSquared));
            total += MathF.Max(0, to - from) * MathF.Sqrt(lengthSquared);
        }
        return total;
    }

    public static float SupportValue(GameSession session, Vector2 position, TowerLevelDefinition level)
    {
        var value = 0f;
        foreach (var recipient in session.Towers.Where(tower => !tower.IsSupport && !tower.IsSandboxDisabled))
        {
            if (Vector2.DistanceSquared(position, recipient.Position) > level.AuraRange * level.AuraRange) continue;
            var existingRate = 0f;
            var existingRange = 0f;
            foreach (var support in session.Towers.Where(tower => tower.IsSupport && !tower.IsDisrupted && !tower.IsSandboxDisabled))
            {
                if (Vector2.DistanceSquared(support.Position, recipient.Position) > support.EffectiveAuraRange * support.EffectiveAuraRange) continue;
                existingRate = MathF.Max(existingRate, support.EffectiveAuraAttackSpeedBonus);
                existingRange = MathF.Max(existingRange, support.EffectiveAuraTowerRangeBonus);
            }
            var rateGain = MathF.Max(0, level.AuraAttackSpeedBonus - existingRate);
            var rangeGain = MathF.Max(0, level.AuraRangeBonus - existingRange);
            var baseExposure = ExposureLength(session, recipient.Position, recipient.Level.Range);
            var extraExposure = ExposureLength(session, recipient.Position, recipient.Level.Range * (1 + rangeGain)) - baseExposure;
            value += (baseExposure * rateGain + extraExposure) * Throughput(recipient.Level, new());
        }
        return value;
    }

    private static void AddUpgradeCandidates(GameSession session, TowerInstance tower, CandidateGenerationOptions options,
        Threat threat, List<SpendingCandidate> candidates)
    {
        if (tower.IsSupport && !options.AllowSupport) return;
        var before = PlacementValue(session, tower.Definition, tower.Level, tower.Position, threat, options);
        if (session.CanApexUpgrade(tower))
            Add(candidates, session, options, "apex:" + tower.Definition.Id, [Command(GameCommandType.UpgradeTower, tower.Id)],
                tower.ApexUpgradeCost, MathF.Max(1, PlacementValue(session, tower.Definition, tower.ApexPreviewLevel, tower.Position, threat, options) - before) /
                Math.Max(1, tower.ApexUpgradeCost), "Apex conversion competes directly with expansion from identical credits.");
        if (tower.RequiresDoctrine)
        {
            foreach (var doctrine in tower.Definition.Tier2Doctrines)
            {
                var command = Command(GameCommandType.ChooseDoctrine, tower.Id) with { DoctrineId = doctrine.Id };
                var level = tower.Definition.Levels[1].WithDoctrine(doctrine);
                Add(candidates, session, options, "upgrade:" + tower.Definition.Id, [command], doctrine.UpgradeCost,
                    MathF.Max(1, PlacementValue(session, tower.Definition, level, tower.Position, threat, options) - before) / Math.Max(1, doctrine.UpgradeCost),
                    "Authored doctrine changes damage, range, rate, and utility without adding footprint.");
                foreach (var specialization in tower.Definition.Specializations)
                {
                    var cost = doctrine.UpgradeCost + specialization.UpgradeCost;
                    var specialized = specialization.Level.WithDoctrine(doctrine);
                    var commands = new[] { command, Command(GameCommandType.SpecializeTower, tower.Id) with { SpecializationId = specialization.Id } };
                    Add(candidates, session, options, "upgrade-bundle:" + tower.Definition.Id, commands, cost,
                        MathF.Max(1, PlacementValue(session, tower.Definition, specialized, tower.Position, threat, options) - before) / Math.Max(1, cost),
                        "Complete doctrine and specialization purchase sequence.");
                }
            }
        }
        if (tower.RequiresSpecialization)
            foreach (var specialization in tower.Definition.Specializations)
            {
                var commands = new[] { Command(GameCommandType.SpecializeTower, tower.Id) with { SpecializationId = specialization.Id } };
                var level = specialization.Level.WithDoctrine(tower.Doctrine);
                Add(candidates, session, options, "upgrade:" + tower.Definition.Id, commands, specialization.UpgradeCost,
                    MathF.Max(1, PlacementValue(session, tower.Definition, level, tower.Position, threat, options) - before) / Math.Max(1, specialization.UpgradeCost),
                    "Compare every authored specialization on the existing position.");
                if (session.ApexUpgradesUnlocked && tower.Definition.Apex is { } apex)
                    Add(candidates, session, options, "apex-bundle:" + tower.Definition.Id,
                        commands.Append(Command(GameCommandType.UpgradeTower, tower.Id)).ToArray(), specialization.UpgradeCost + apex.UpgradeCost,
                        MathF.Max(1, PlacementValue(session, tower.Definition, level.WithApex(apex), tower.Position, threat, options) - before) /
                        Math.Max(1, specialization.UpgradeCost + apex.UpgradeCost), "Specialization followed by the unlocked Apex conversion.");
            }
        if (tower.CanUpgrade)
            Add(candidates, session, options, "upgrade:" + tower.Definition.Id, [Command(GameCommandType.UpgradeTower, tower.Id)],
                tower.UpgradeCost, MathF.Max(1, PlacementValue(session, tower.Definition,
                    tower.Definition.Levels[tower.LevelIndex + 1].WithDoctrine(tower.Doctrine), tower.Position, threat, options) - before) /
                Math.Max(1, tower.UpgradeCost), "Legal next-level upgrade.");
    }

    private static void AddNewTowerBundles(GameSession session, TowerDefinition definition, Vector2 position,
        float baseValue, CandidateGenerationOptions options, List<SpendingCandidate> candidates)
    {
        foreach (var doctrine in definition.Tier2Doctrines)
        {
            var commands = new[] { Place(definition.Id, position), Command(GameCommandType.ChooseDoctrine, session.NextTowerId) with { DoctrineId = doctrine.Id } };
            var cost = definition.PurchaseCost + doctrine.UpgradeCost;
            Add(candidates, session, options, "mature:" + definition.Id, commands, cost,
                baseValue * doctrine.AttackSpeedMultiplier * doctrine.DamageMultiplier * doctrine.RangeMultiplier / Math.Max(1, cost),
                "Purchase then immediately choose a doctrine; the new identity is resolved from this board.");
            foreach (var specialization in definition.Specializations)
            {
                var fullCost = cost + specialization.UpgradeCost;
                var full = commands.Append(Command(GameCommandType.SpecializeTower, session.NextTowerId) with { SpecializationId = specialization.Id }).ToArray();
                Add(candidates, session, options, "mature:" + definition.Id, full, fullCost,
                    baseValue * MathF.Max(1, Throughput(specialization.Level.WithDoctrine(doctrine), new()) /
                        MathF.Max(1, Throughput(definition.Levels[0], new()))) / Math.Max(1, fullCost),
                    "Compare a fully specialized new tower with upgrades to the existing board.");
            }
        }
    }

    private static void AddPurchasePairs(GameSession session, CandidateGenerationOptions options,
        List<(TowerDefinition Definition, Vector2 Position, float Score)> placements, List<SpendingCandidate> candidates)
    {
        var attackers = placements.Where(value => !IsSupport(value.Definition)).OrderByDescending(value => value.Score /
            Math.Max(1, value.Definition.PurchaseCost)).Take(10).ToArray();
        foreach (var first in placements.Where(value => IsSupport(value.Definition)).Concat(attackers.Take(3)))
        foreach (var second in attackers)
        {
            var gap = Vector2.Distance(first.Position, second.Position);
            if (gap < GameConstants.TowerMinimumGap) continue;
            var support = IsSupport(first.Definition);
            if (support && gap > first.Definition.Levels[0].AuraRange) continue;
            var cost = first.Definition.PurchaseCost + second.Definition.PurchaseCost;
            var score = (first.Score + second.Score * (support ? 1 + first.Definition.Levels[0].AuraAttackSpeedBonus + first.Definition.Levels[0].AuraRangeBonus : 1)) / Math.Max(1, cost);
            var commands = new[] { Place(first.Definition.Id, first.Position), Place(second.Definition.Id, second.Position) };
            Add(candidates, session, options, support ? "support-pair" : "coverage-pair", commands, cost, score,
                support ? "Beacon and recipient are purchased together inside the actual aura radius; support bonuses use strongest-source stacking."
                    : "Two disjoint legal positions compare broader coverage with concentrated upgrades.");
            Add(candidates, session, options, support ? "support-pair" : "coverage-pair", commands.Reverse().ToArray(), cost, score * 0.999f,
                "Reverse purchase order changes identities and can change deterministic targeting ties.");
        }
    }

    private static void AddInvestmentPairs(GameSession session, CandidateGenerationOptions options, List<SpendingCandidate> candidates)
    {
        var investments = candidates.Where(candidate => candidate.WaitForCredits is null && candidate.Commands.Count == 1 &&
            candidate.Family.StartsWith("upgrade:", StringComparison.Ordinal)).OrderByDescending(candidate => candidate.Score).Take(12).ToArray();
        for (var i = 0; i < investments.Length; i++)
        for (var j = i + 1; j < investments.Length; j++)
        {
            if (investments[i].Commands[0].EntityId == investments[j].Commands[0].EntityId) continue;
            var cost = investments[i].Cost + investments[j].Cost;
            Add(candidates, session, options, "distributed-upgrades", investments[i].Commands.Concat(investments[j].Commands).ToArray(),
                cost, (investments[i].Score * investments[i].Cost + investments[j].Score * investments[j].Cost) / Math.Max(1, cost),
                "Spend the same budget upgrading two independent towers.");
        }
    }

    private static void AddTargeting(GameSession session, List<SpendingCandidate> candidates)
    {
        foreach (var tower in session.Towers.Where(tower => !tower.IsSupport).OrderByDescending(tower => tower.InvestedCredits).Take(8))
        foreach (var target in session.AvailableTargetModes.Where(mode => mode != tower.TargetMode))
            candidates.Add(new() { Id = $"target:{tower.Id}:{target}", Family = "targeting", Commands =
                [Command(GameCommandType.SetTargetMode, tower.Id) with { TargetMode = target }], Score = 0.001f,
                Reason = "Reallocate existing damage through a legal targeting mode; combat rollout measures the effect." });
    }

    private static void AddTactical(GameSession session, CandidateGenerationOptions options, List<SpendingCandidate> candidates)
    {
        if (session.ProtocolsEnabled)
        {
            foreach (var tower in session.Towers.OrderByDescending(tower => tower.InvestedCredits).Take(8))
            {
                if (!tower.IsApex && session.AutoOverdriveTowerId != tower.Id)
                    candidates.Add(new() { Id = $"auto:{tower.Id}", Family = "automatic-protocol", Score = 0.001f,
                        Commands = [Command(GameCommandType.ToggleAutoProtocol, tower.Id)], Reason = "Delegate one tower's eligible Protocol timing to the game's automatic trigger." });
                if (session.Waves.IsActive && session.OverdriveCooldownRemaining <= 0 && !tower.IsOverdriven &&
                    tower.ApexProtocolCooldownRemaining <= 0 && session.GetProtocolTargets(tower).Count > 0)
                    candidates.Add(new() { Id = $"protocol:{tower.Id}", Family = "protocol", Score = session.GetProtocolTargets(tower).Count,
                        Commands = [Command(GameCommandType.OverdriveTower, tower.Id)], Reason = "Ready Protocol with live targets; shared cooldown remains authoritative." });
            }
        }
        if (!session.Waves.IsActive || !session.PulsePlatesEnabled) return;
        var definition = session.Content.Tactics.EmergencyDefense;
        var enemies = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
        var distances = enemies.OrderByDescending(enemy => enemy.PathProgress).Take(6)
            .SelectMany(enemy => new[] { enemy.DistanceAlongPath + enemy.CurrentSpeed * (definition.ArmTime + 0.2f),
                enemy.DistanceAlongPath + 80, enemy.DistanceAlongPath + 160 }).ToArray();
        foreach (var distance in distances.Distinct())
        {
            var position = session.Map.Path.GetPosition(distance);
            if (session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position) != PlacementFailure.None) continue;
            var nearby = enemies.Count(enemy => Vector2.Distance(enemy.Position, position) < definition.BlastRadius + 80);
            var cost = session.EmergencyInventory > 0 ? 0 : session.CurrentEmergencyDirectPurchaseCost;
            Add(candidates, session, options, "deployable", [Command(GameCommandType.DeployEmergencyDefense) with { X = position.X, Y = position.Y }],
                cost, nearby * 4, "Place ahead of observed enemies, accounting for arming time, remaining inventory, and escalating direct-purchase price.");
        }
    }

    private static void AddForge(GameSession session, CandidateGenerationOptions options, IReadOnlyList<Vector2> positions, List<SpendingCandidate> candidates)
    {
        if (!options.IncludeTactical || !session.TacticalSystemsEnabled) return;
        if (session.Generator is { } forge)
        {
            if (forge.CanUpgrade) Add(candidates, session, options, "economy", [Command(GameCommandType.UpgradeGenerator)], forge.UpgradeCost, 0.5f,
                "Pay the authored Forge upgrade cost for charge production and plate damage.");
            if (options.IncludeSales && session.SellingEnabled)
                candidates.Add(new() { Id = "sell-forge", Family = "sale", Commands = [Command(GameCommandType.SellGenerator)], Cost = -forge.SellValue,
                    Score = 0.001f, Reason = "Recover the actual Forge sell refund." });
            return;
        }
        var position = positions.Where(position => session.ValidateTacticalPlacement(TacticalPlacementKind.ChargeForge, position) == PlacementFailure.None)
            .OrderByDescending(position => session.Map.Path.DistanceToPath(position)).Cast<Vector2?>().FirstOrDefault();
        if (position is { } spot)
            Add(candidates, session, options, "economy", [Command(GameCommandType.PlaceGenerator) with { X = spot.X, Y = spot.Y }],
                session.Content.Tactics.Generator.PurchaseCost, 0.4f, "Place a Forge away from valuable firing coverage; production occurs only under the real tactical rules.");
    }

    private static void Add(List<SpendingCandidate> candidates, GameSession session, CandidateGenerationOptions options,
        string family, IReadOnlyList<GameCommand> commands, int cost, float score, string reason)
    {
        var waiting = cost > session.Economy.Credits;
        if (waiting && (!options.AllowSaving || cost > Math.Max(600, session.Economy.Credits * 3))) return;
        if (options.Family == "concentrated" && family.StartsWith("purchase:", StringComparison.Ordinal) && session.Towers.Count > 4) score *= 0.55f;
        if (options.Family == "coverage" && family.StartsWith("upgrade", StringComparison.Ordinal)) score *= 0.6f;
        candidates.Add(new() { Id = string.Join(";", commands.Select(Key)), Family = waiting ? "saving:" + family : family,
            Commands = commands, Cost = cost, Score = float.IsFinite(score) ? score : 0, WaitForCredits = waiting ? cost : null, Reason = reason });
    }

    private static IReadOnlyList<SpendingCandidate> Diverse(IEnumerable<SpendingCandidate> source, int maximum)
    {
        var groups = source.DistinctBy(candidate => candidate.Id).GroupBy(candidate => candidate.Family)
            .Select(group => new Queue<SpendingCandidate>(group.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Id, StringComparer.Ordinal)))
            .OrderByDescending(queue => queue.Peek().Family == "saving")
            .ThenByDescending(queue => queue.Peek().Family.StartsWith("apex", StringComparison.Ordinal))
            .ThenByDescending(queue => queue.Peek().Score).ToList();
        var result = new List<SpendingCandidate>();
        while (result.Count < maximum && groups.Any(group => group.Count > 0))
            foreach (var group in groups)
                if (group.Count > 0 && result.Count < maximum) result.Add(group.Dequeue());
        return result;
    }

    private static IEnumerable<SpendingCandidate> ApplyFamilyPreferences(GameSession session,
        CandidateGenerationOptions options, IReadOnlyList<SpendingCandidate> candidates)
    {
        var strongestInvestment = candidates.Where(candidate => candidate.Cost > 0 &&
            candidate.Commands.Any(command => command.Type is GameCommandType.PlaceTower or GameCommandType.UpgradeTower or
                GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower)).Select(candidate => candidate.Score).DefaultIfEmpty(1).Max();
        var attackers = session.Towers.Count(tower => !tower.IsSupport);
        foreach (var original in candidates)
        {
            var candidate = original;
            if (options.Family == "economy")
            {
                var score = candidate.Score;
                if (candidate.WaitForCredits is not null) score *= 1.25f;
                if (candidate.Commands.Any(command => command.Type is GameCommandType.UpgradeTower or
                    GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower)) score *= 1.1f;
                if (attackers >= 3 && candidate.Commands.Any(command => command.Type is GameCommandType.PlaceGenerator or GameCommandType.UpgradeGenerator))
                    score = MathF.Max(score, strongestInvestment * 1.2f);
                candidate = candidate with { Score = score };
            }
            else if (options.Family == "deployable")
            {
                if (candidate.Commands.Any(command => command.Type is GameCommandType.DeployEmergencyDefense or GameCommandType.OverdriveTower))
                    candidate = candidate with { Score = candidate.Score * 3 };
                if (options.AllowSaving && attackers > 0 && session.TacticalSystemsEnabled && session.EmergencyInventory == 0 &&
                    candidate.Cost > 0 && candidate.Commands.Any(command => command.Type is GameCommandType.PlaceTower or
                        GameCommandType.UpgradeTower or GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower or
                        GameCommandType.PlaceGenerator or GameCommandType.UpgradeGenerator))
                {
                    var reserve = session.CurrentEmergencyDirectPurchaseCost;
                    var threshold = (int)Math.Min(int.MaxValue, (long)candidate.Cost + reserve);
                    candidate = candidate with
                    {
                        WaitForCredits = session.Economy.Credits < threshold ? threshold : null,
                        ReserveCredits = reserve,
                        Family = session.Economy.Credits < threshold && !candidate.Family.StartsWith("saving:", StringComparison.Ordinal)
                            ? "saving:" + candidate.Family : candidate.Family,
                        Reason = candidate.Reason + $" Retain {reserve} credits for one Plate at its current direct-purchase price."
                    };
                }
            }
            yield return candidate;
        }
    }

    private static List<Vector2> BuildAnchors(GameSession session, CandidateGenerationOptions options)
    {
        var points = new HashSet<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        {
            points.Add(region.Center.ToVector2());
            // Build regions constrain tower centers; narrow decks can use both legal edges.
            var right = region.Right - 0.01f;
            var bottom = region.Bottom - 0.01f;
            foreach (var x in new[] { (float)region.Left, right })
            {
                points.Add(new(x, bottom));
                for (var y = (float)region.Top; y < region.Bottom; y += options.SpatialStep)
                    points.Add(new(x, y));
            }
            foreach (var y in new[] { (float)region.Top, bottom })
            {
                points.Add(new(right, y));
                for (var x = (float)region.Left; x < region.Right; x += options.SpatialStep)
                    points.Add(new(x, y));
            }
            for (var y = region.Top + GameConstants.TowerRadius; y < region.Bottom; y += options.SpatialStep)
            for (var x = region.Left + GameConstants.TowerRadius; x < region.Right; x += options.SpatialStep)
                points.Add(new(x, y));
        }
        foreach (var node in session.Map.Definition.PowerNodes)
        {
            points.Add(node.Position.ToVector2());
            for (var i = 0; i < 8; i++)
                points.Add(node.Position.ToVector2() + new Vector2(MathF.Cos(i * MathF.PI / 4), MathF.Sin(i * MathF.PI / 4)) * MathF.Max(0, node.Radius - 2));
        }
        foreach (var tower in session.Towers)
        foreach (var radius in new[] { GameConstants.TowerMinimumGap + 2, 80f, 120f })
        for (var i = 0; i < 8; i++)
            points.Add(tower.Position + new Vector2(MathF.Cos(i * MathF.PI / 4), MathF.Sin(i * MathF.PI / 4)) * radius);
        return points.OrderBy(position => position.Y).ThenBy(position => position.X).ToList();
    }

    private static bool PlacementLegal(GameSession session, string towerId, Vector2 position) =>
        GeometryLegal(session, position) &&
        session.ValidatePlacement(towerId, position) is PlacementFailure.None or PlacementFailure.InsufficientCredits;

    private static bool GeometryLegal(GameSession session, Vector2 position) =>
        position.X >= GameConstants.TowerRadius && position.X <= GameConstants.MapWidth - GameConstants.TowerRadius &&
        position.Y >= GameConstants.TopBarHeight + GameConstants.TowerRadius && position.Y <= GameConstants.LogicalHeight - GameConstants.TowerRadius &&
        session.Map.IsBuildable(position) && session.Map.Path.DistanceToPath(position) >= GameConstants.PlacementPathClearance &&
        session.Towers.All(tower => Vector2.DistanceSquared(tower.Position, position) >= GameConstants.TowerMinimumGap * GameConstants.TowerMinimumGap) &&
        (session.Generator is null || Vector2.DistanceSquared(session.Generator.Position, position) >= 48 * 48);

    private static float PlacementValue(GameSession session, TowerDefinition definition, TowerLevelDefinition level,
        Vector2 position, Threat threat, CandidateGenerationOptions options)
    {
        if (!options.UsePlacementHeuristics)
            return 1 + ((int)(position.X * 17 + position.Y * 31) + options.ExplorationOffset & 1023) / 1024f;
        if (IsSupport(definition)) return SupportValue(session, position, level);
        var power = session.Map.GetPowerBuff(position);
        var supportRate = 0f;
        var supportRange = 0f;
        foreach (var support in session.Towers.Where(tower => tower.IsSupport && !tower.IsDisrupted && !tower.IsSandboxDisabled))
        {
            if (Vector2.DistanceSquared(support.Position, position) > support.EffectiveAuraRange * support.EffectiveAuraRange) continue;
            supportRate = MathF.Max(supportRate, support.EffectiveAuraAttackSpeedBonus);
            supportRange = MathF.Max(supportRange, support.EffectiveAuraTowerRangeBonus);
        }
        var exposure = ExposureLength(session, position, level.Range * (1 + power.RangeBonus + supportRange));
        var value = exposure / MathF.Max(1, threat.Speed) * Throughput(level, threat, power.ArmorPierceBonus) *
            (1 + power.AttackSpeedBonus + supportRate) * (1 + power.DamageBonus);
        // Utility is a proposal preference, not a replacement for a combat rollout.
        value *= 1 + level.SlowPercent * 1.5f + level.ExposePercent + level.ArmorReduction * threat.Armored * 0.08f;
        if (threat.Regeneration > 0 && (level.BurnDamagePerSecond > 0 || level.AttacksPerSecond >= 2)) value *= 1.2f;
        return value;
    }

    private static float Throughput(TowerLevelDefinition level, Threat threat, float nodePierce = 0)
    {
        var hit = MathF.Max(level.Damage * 0.1f, level.Damage - MathF.Max(0, threat.Armor - level.ArmorPierce - nodePierce));
        var pelletFactor = MathF.Min(Math.Max(1, level.PelletCount), 1 + threat.Crowd * 2);
        var areaFactor = level.SplashRadius > 0 ? 1 + threat.Crowd * MathF.Min(4, Math.Max(1, level.SplashTargetLimit - 1)) : 1;
        var result = (hit * pelletFactor * areaFactor + level.ChainDamage * level.ChainCount * threat.Crowd) * level.AttacksPerSecond + level.BurnDamagePerSecond;
        if (level.IgnoreShield && threat.Shielded > 0) result *= 1 + threat.Shielded * 0.5f;
        return MathF.Max(1, result);
    }

    private static Threat ReadThreat(GameSession session, string informationProfile)
    {
        var samples = new List<(EnemyDefinition Enemy, int Count)>();
        if (informationProfile == "oracle")
        {
            var wave = session.Waves.IsActive ? session.Waves.ActiveWave : session.Waves.NextWave;
            if (wave is not null)
                foreach (var group in wave.Groups)
                    if (session.Content.Enemies.TryGetValue(group.EnemyId, out var enemy)) samples.Add((enemy, group.Count));
        }
        else
            samples.AddRange(session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).Select(enemy => (enemy.Definition, 1)));
        var total = samples.Sum(sample => sample.Count);
        if (total == 0) return new();
        return new(samples.Sum(sample => sample.Enemy.Armor * sample.Count) / total,
            samples.Sum(sample => sample.Enemy.Speed * sample.Count) / total,
            samples.Sum(sample => sample.Enemy.Armor > 0 ? sample.Count : 0) / (float)total,
            samples.Sum(sample => sample.Enemy.Shield > 0 ? sample.Count : 0) / (float)total,
            samples.Sum(sample => sample.Enemy.RegenerationPerSecond * sample.Count) / total,
            MathF.Min(1, total / 20f));
    }

    private static bool Allowed(TowerDefinition definition, CandidateGenerationOptions options)
    {
        if (IsExcluded(definition, options)) return false;
        if (IsSupport(definition)) return options.AllowSupport;
        var level = definition.Levels[0];
        return options.Family switch
        {
            "range" => definition.Levels.Max(value => value.Range) >= 200,
            "area" => level.SplashRadius > 0 || level.ChainCount > 0 || level.PelletCount > 1 || level.BurnDamagePerSecond > 0,
            "control" => level.SlowPercent > 0 || level.StunDuration > 0 || level.ExposePercent > 0 || level.ArmorReduction > 0 || level.BurnDamagePerSecond > 0,
            "counter" => level.ArmorPierce > 0 || level.IgnoreShield || level.ArmorReduction > 0 || level.BurnDamagePerSecond > 0,
            _ => true
        };
    }

    private static bool IsSupport(TowerDefinition definition) => definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase);
    private static bool IsExcluded(TowerDefinition definition, CandidateGenerationOptions options) =>
        definition.Id.Equals(options.ExcludedTowerId, StringComparison.OrdinalIgnoreCase);
    private static GameCommand Command(GameCommandType type, int entityId = 0) => new() { PlayerId = 1, Type = type, EntityId = entityId };
    private static GameCommand Place(string id, Vector2 position) => Command(GameCommandType.PlaceTower) with { TowerDefinitionId = id, X = position.X, Y = position.Y };
    private static string Key(GameCommand command) => string.Create(CultureInfo.InvariantCulture,
        $"{command.Type}:{command.EntityId}:{command.TowerDefinitionId}:{command.DoctrineId}:{command.SpecializationId}:{command.X:R},{command.Y:R}:{command.TargetMode}");
    private readonly record struct Threat(float Armor = 0, float Speed = 80, float Armored = 0, float Shielded = 0, float Regeneration = 0, float Crowd = 0.5f);
}
