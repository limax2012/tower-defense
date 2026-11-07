using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class PlanningCandidateTests
{
    public static void Run()
    {
        RegistryDiscoversContentAndScopes();
        RouteExposureCountsRepeatedPasses();
        FreshBundlesPassAuthoritativeCommands();
        NarrowDeckEdgesSupportLegalPacking();
        SupportNeedsARecipientAndDoesNotStack();
        PlayerObservationsExcludeFutureWaveComposition();
        SavingAndUpgradeAlternativesAreExplicit();
        PortfolioPreferencesAndFamilyExclusion();
    }

    private static GameContent Content() => new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();

    private static void RegistryDiscoversContentAndScopes()
    {
        var content = Content();
        var competitive = content.Challenges.Values.Count(challenge => !challenge.IsSandbox);
        var registry = PlanningScenarioRegistry.Discover(content, [1337, 7331], includeCoOp: true, endlessHorizon: 40);
        Check.Equal(content.Maps.Count * content.Difficulties.Count * competitive * 2 * 2 * 2, registry.Scenarios.Count,
            "registry includes every competitive map/difficulty/directive, seed, participation and horizon");
        Check.True(registry.Exclusions.Any(exclusion => exclusion.ChallengeId == "sandbox_lab" && exclusion.Scope == "coop"),
            "invalid sandbox co-op is explained");
        Check.True(registry.Scenarios.All(scenario => scenario.StartingProfile == "fresh"), "registry does not assume purchased unlocks");
        content.Maps.Add("future-fixture", new MapDefinition { Id = "future-fixture", Path = content.Map.Path,
            BuildableRegions = content.Map.BuildableRegions, WaveSet = content.Map.WaveSet });
        Check.True(PlanningScenarioRegistry.Discover(content).Scenarios.Any(scenario => scenario.MapId == "future-fixture"),
            "future maps register through data");
    }

    private static void RouteExposureCountsRepeatedPasses()
    {
        var content = Content();
        content.Maps.Add("exposure-fixture", new MapDefinition { Id = "exposure-fixture", WaveSet = content.Map.WaveSet,
            Path = [new() { X = 0, Y = 100 }, new() { X = 100, Y = 100 }, new() { X = 0, Y = 100 }] });
        var session = new GameSession(content, "exposure-fixture", "normal", "standard");
        Check.Nearly(80, SpendingCandidateGenerator.ExposureLength(session, new Vector2(50, 100), 20),
            "circle intersections count two passes through the same physical lane");
        Check.Nearly(0, SpendingCandidateGenerator.ExposureLength(session, new Vector2(50, 121), 20),
            "nearby geometry without range intersection has no exposure");
    }

    private static void FreshBundlesPassAuthoritativeCommands()
    {
        var content = Content();
        foreach (var map in content.Maps.Keys)
        {
            var session = new GameSession(content, map, "easy", "standard");
            var candidates = SpendingCandidateGenerator.Generate(session, new() { MaximumCandidates = 150 });
            Check.True(candidates.Any(candidate => candidate.Commands.Count > 1 && candidate.WaitForCredits is null),
                "fresh search includes complete affordable spending bundles");
            foreach (var candidate in candidates.Where(candidate => candidate.WaitForCredits is null))
            {
                var branch = new GameSession(content, map, "easy", "standard");
                var credits = branch.Economy.Credits;
                foreach (var command in candidate.Commands)
                    Check.True(GameCommandProcessor.Apply(branch, command).Accepted, "proposed affordable bundle passes player rules: " + candidate.Id);
                Check.Equal(candidate.Cost, credits - branch.Economy.Credits, "proposal cost equals authoritative credit delta");
            }
            Check.Equal(0, session.Towers.Count, "candidate generation cannot mutate the source board");
        }
    }

    private static void SupportNeedsARecipientAndDoesNotStack()
    {
        var content = Content();
        var session = new GameSession(content);
        var beacon = content.Towers["signal_beacon"];
        var recipient = new TowerInstance(1, content.Towers["needle_turret"], session.Map.Path.GetPosition(session.Map.Path.TotalLength * .5f));
        var spot = recipient.Position + new Vector2(50, 0);
        Check.Nearly(0, SpendingCandidateGenerator.SupportValue(session, spot, beacon.Levels[0]), "idle Beacon receives no support value");
        session.Towers.Add(recipient);
        Check.True(SpendingCandidateGenerator.SupportValue(session, spot, beacon.Levels[0]) > 0, "Beacon values a reachable recipient");
        session.Towers.Add(new TowerInstance(2, beacon, spot));
        Check.Nearly(0, SpendingCandidateGenerator.SupportValue(session, spot + new Vector2(1, 0), beacon.Levels[0]),
            "equal overlapping Beacons do not add attack or range bonuses");
        Check.Nearly(0, SpendingCandidateGenerator.SupportValue(session, recipient.Position + new Vector2(1000, 0), beacon.Levels[0]),
            "poor Beacon position is rejected by measured recipient geometry");
    }

    private static void NarrowDeckEdgesSupportLegalPacking()
    {
        var tower = new TowerDefinition { Id = "narrow_deck_tower", Behavior = "beam", PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { Range = 200, Damage = 10, AttacksPerSecond = 1 }] };
        var map = new MapDefinition { Id = "narrow_deck", StartingCredits = 400, PathWidth = 30,
            Path = [new() { X = 0, Y = 100 }, new() { X = 800, Y = 100 }],
            BuildableRegions = [new() { X = 200, Y = 250, Width = 20, Height = 50 }] };
        var content = new GameContent { Map = map, Maps = new() { [map.Id] = map },
            Towers = new() { [tower.Id] = tower }, Enemies = new(), Waves = new(),
            Difficulties = new() { ["fixture"] = new() { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new() { Id = "fixture" } } };
        var session = new GameSession(content, map.Id, "fixture", "fixture");
        var options = new CandidateGenerationOptions { MaximumCandidates = 100, PositionsPerTower = 100,
            AllowSaving = false, IncludeTactical = false, IncludeSales = false };
        var candidates = SpendingCandidateGenerator.Generate(session, options);
        var first = candidates.FirstOrDefault(candidate => candidate.Commands.Count == 1 &&
            candidate.Commands[0].Type == GameCommandType.PlaceTower && candidate.Commands[0].Y == 250);
        Check.True(first is not null, "narrow build decks retain the legal boundary closest to the route");
        foreach (var candidate in candidates)
        {
            var branch = new GameSession(content, map.Id, "fixture", "fixture");
            foreach (var command in candidate.Commands)
                Check.True(GameCommandProcessor.Apply(branch, command).Accepted,
                    "boundary and corner proposals pass authoritative placement and spacing rules");
        }
        Check.Equal(0, session.Towers.Count, "boundary candidate generation leaves the source board unchanged");
        Check.True(GameCommandProcessor.Apply(session, first!.Commands[0]).Accepted,
            "place the first narrow-deck tower at its legal boundary");
        var second = SpendingCandidateGenerator.Generate(session, options).FirstOrDefault(candidate =>
            candidate.Commands.Count == 1 && candidate.Commands[0].Type == GameCommandType.PlaceTower);
        Check.True(second is not null && GameCommandProcessor.Apply(session, second.Commands[0]).Accepted &&
            session.Towers.Count == 2, "narrow-deck boundary anchors permit a second legally spaced row");
    }

    private static void PlayerObservationsExcludeFutureWaveComposition()
    {
        var content = Content();
        var session = new GameSession(content);
        var player = PlanningObservation.Read(session, "informed");
        var oracle = PlanningObservation.Read(session, "oracle");
        Check.True(player.OracleWaveComposition is null && oracle.OracleWaveComposition is { Count: > 0 },
            "informed interface omits future waves while oracle planning labels foreknowledge");
        var options = new CandidateGenerationOptions { InformationProfile = "informed", MaximumCandidates = 20 };
        var before = SpendingCandidateGenerator.Generate(session, options).Select(candidate => candidate.Id).ToArray();
        var original = session.Waves.NextWave!.Groups[0].Count;
        try
        {
            session.Waves.NextWave.Groups[0].Count += 100;
            var after = SpendingCandidateGenerator.Generate(session, options).Select(candidate => candidate.Id).ToArray();
            Check.True(before.SequenceEqual(after), "informed proposals cannot depend on a hidden future count");
        }
        finally { session.Waves.NextWave!.Groups[0].Count = original; }
    }

    private static void SavingAndUpgradeAlternativesAreExplicit()
    {
        var content = Content();
        var session = new GameSession(content, difficultyId: "easy");
        var initial = SpendingCandidateGenerator.Generate(session, new() { MaximumCandidates = 300 });
        var purchase = initial.First(candidate => candidate.Commands.Count == 1 && candidate.WaitForCredits is null &&
            candidate.Commands[0].Type == GameCommandType.PlaceTower && candidate.Commands[0].TowerDefinitionId == "needle_turret");
        Check.True(GameCommandProcessor.Apply(session, purchase.Commands[0]).Accepted, "place fixture tower legally");
        var choices = SpendingCandidateGenerator.Generate(session, new() { MaximumCandidates = 400 });
        Check.True(choices.Any(candidate => candidate.Id == "wait"), "waiting is an explicit alternative");
        Check.True(choices.Any(candidate => candidate.Commands.Any(command => command.Type == GameCommandType.ChooseDoctrine)),
            "existing tower doctrine competes with expansion");
        Check.True(choices.Any(candidate => candidate.WaitForCredits > session.Economy.Credits), "saving records an actual affordability threshold");
        var noSaving = SpendingCandidateGenerator.Generate(session, new() { AllowSaving = false, MaximumCandidates = 100 });
        Check.True(noSaving.All(candidate => candidate.Id != "wait" && candidate.WaitForCredits is null), "saving ablation removes deferred purchases");
    }

    private static void PortfolioPreferencesAndFamilyExclusion()
    {
        var content = Content();
        // This fixture isolates policy ranking; its resource values are not used for campaign evidence.
        content.Map.StartingCredits = 1200;
        content.Tactics.EmergencyDefense.StartingInventory = 0;
        var session = new GameSession(content, content.Map.Id, "easy", "standard");
        var options = new CandidateGenerationOptions { MaximumCandidates = 500, PositionsPerTower = 2 };
        for (var i = 0; i < 3; i++)
        {
            var purchase = SpendingCandidateGenerator.Generate(session, options).First(candidate => candidate.Commands.Count == 1 &&
                candidate.Commands[0].Type == GameCommandType.PlaceTower && candidate.Commands[0].TowerDefinitionId == "needle_turret");
            Check.True(GameCommandProcessor.Apply(session, purchase.Commands[0]).Accepted, "portfolio fixture builds a legal foundation");
        }
        var ordinary = SpendingCandidateGenerator.Generate(session, options).ToDictionary(candidate => candidate.Id);
        var economy = SpendingCandidateGenerator.Generate(session, options with { Family = "economy" });
        var forge = economy.First(candidate => candidate.Commands.Any(command => command.Type == GameCommandType.PlaceGenerator));
        Check.True(forge.Score > ordinary[forge.Id].Score, "economy family has an explicit Forge investment preference");
        var deployable = SpendingCandidateGenerator.Generate(session, options with { Family = "deployable" });
        Check.True(deployable.Any(candidate => candidate.ReserveCredits == session.CurrentEmergencyDirectPurchaseCost && candidate.Cost > 0),
            "deployable family retains the actual current Plate price after its free inventory is exhausted");
        Check.True(deployable.Where(candidate => candidate.ReserveCredits > 0 && candidate.WaitForCredits is not null)
            .All(candidate => candidate.WaitForCredits == candidate.Cost + candidate.ReserveCredits), "Plate reserve threshold is separate from actual purchase cost");
        var noSaving = SpendingCandidateGenerator.Generate(session, options with { Family = "deployable", AllowSaving = false });
        Check.True(noSaving.All(candidate => candidate.ReserveCredits == 0 && candidate.WaitForCredits is null),
            "saving ablation disables retained Plate reserve thresholds");
        var excluded = SpendingCandidateGenerator.Generate(session, options with { ExcludedTowerId = "needle_turret" });
        Check.True(excluded.SelectMany(candidate => candidate.Commands).All(command =>
            command.TowerDefinitionId != "needle_turret" &&
            !(command.Type is GameCommandType.UpgradeTower or GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower &&
                session.Towers.Any(tower => tower.Id == command.EntityId && tower.Definition.Id == "needle_turret"))),
            "tower-family exclusion removes both new purchases and upgrades to existing members");
        Check.True(excluded.Any(candidate => candidate.Commands.Any(command => command.Type == GameCommandType.PlaceTower)),
            "excluding one family retains other legal investments");
    }
}
