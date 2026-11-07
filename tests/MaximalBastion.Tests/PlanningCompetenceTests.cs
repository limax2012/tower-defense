using System.Diagnostics;
using System.Text;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class PlanningCompetenceTests
{
    private static readonly Vector2 Primary = new(250, 230);
    private static readonly Vector2 Secondary = new(330, 230);

    public sealed record CaseResult(string Case, string Alternative, bool Cleared, int Wave, int Lives, int Credits,
        long Ticks, int Actions, int CombatActions, bool ReplayVerified, bool NormalStart, string Scope);
    public sealed record DiagnosticReport(string Model, string BuildHash, IReadOnlyList<CaseResult> Cases,
        int ExhaustiveSequences, int LegalSequences, int WinningSequences, int EarlierWaveRevisions,
        bool PlannerAgreesWithFiniteOracle, double WallSeconds);

    public static void Run() => Evaluate(null, runPlanner: false);

    public static DiagnosticReport WriteEvidence(string directory) => Evaluate(directory, runPlanner: true);

    private static DiagnosticReport Evaluate(string? directory, bool runPlanner)
    {
        var clock = Stopwatch.StartNew();
        if (directory is not null) Directory.CreateDirectory(directory);
        var rows = new List<CaseResult>();
        UpgradeVersusExpansion(rows, directory);
        SavingForApex(rows, directory);
        BeaconPlacement(rows, directory);
        SustainedRecovery(rows, directory);
        TimelyPlate(rows, directory);
        var finite = EarlierInvestment(rows, directory);
        var agrees = !runPlanner || ComparePlannerToOracle(directory!, finite.WinningSequences > 0);
        var report = new DiagnosticReport(
            "Synthetic competence fixtures: production stepping and legal commands; custom tower/enemy/map data; no production balance claim.",
            PlanningFingerprint.Build(), rows, finite.Sequences, finite.LegalSequences, finite.WinningSequences,
            finite.Revisions, agrees, clock.Elapsed.TotalSeconds);
        if (directory is not null)
        {
            PlannerStore.Write(Path.Combine(directory, "diagnostics.json"), report);
            var text = new StringBuilder("# Synthetic planner competence evidence\n\n")
                .AppendLine(report.Model)
                .AppendLine("\nEvery archived witness is replayed independently with the fixture content. These cases do not count as authored campaign wins.")
                .AppendLine("\n| Case | Alternative | Cleared | Wave | Lives | Credits | Ticks | Actions | Combat actions | Replay |")
                .AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (var row in rows)
                text.AppendLine($"| {row.Case} | {row.Alternative} | {row.Cleared} | {row.Wave} | {row.Lives} | {row.Credits} | {row.Ticks} | {row.Actions} | {row.CombatActions} | {row.ReplayVerified} |");
            text.AppendLine($"\nFinite two-wave oracle: {finite.Sequences} enumerated sequences, {finite.LegalSequences} legal sequences, {finite.WinningSequences} wins. " +
                $"The depth-first search revisited the earlier purchase {finite.Revisions} time(s). General planner agrees on solvability: {agrees}.")
                .AppendLine("\nFinite oracle scope: one fixed initial rifle; before wave 1, buy one rifle at the second anchor or wait; before wave 2, wait, buy at that anchor, or upgrade the original rifle. No combat actions, sale, Protocol, alternate coordinates or early-call timing search. Enumeration is exhaustive only within those restrictions.")
                .AppendLine("\nThe Apex cases begin at a synthetic imported pre-wave-21 state. They are analogous allocation checks, not reproduction of the historical human checkpoint.")
                .AppendLine($"\nWall time: {report.WallSeconds:F3} seconds; build SHA-256: `{report.BuildHash}`.");
            File.WriteAllText(Path.Combine(directory, "report.md"), text.ToString());
        }
        return report;
    }

    private static void UpgradeVersusExpansion(List<CaseResult> rows, string? directory)
    {
        var rifle = Rifle(100, 10, 4);
        rifle.Apex = new TowerApexDefinition { UpgradeCost = 150, DamageMultiplier = 6 };
        var content = Fixture("apex-allocation", 250, [rifle], [Enemy("armored", 300, 80, armor: 20)],
            Enumerable.Range(1, 21).Select(number => Wave(number, "armored")).ToList());
        var initial = ImportedApex(content);
        var candidates = SpendingCandidateGenerator.Generate(initial.Session);
        Check.True(candidates.Any(candidate => candidate.Commands.Any(command => command.Type == GameCommandType.UpgradeTower &&
            command.EntityId == 1) && candidate.Cost == 150), "candidate generation includes the significant legal Apex investment");
        var upgraded = Branch(content, initial, [Command(GameCommandType.UpgradeTower, 1)]);
        var expanded = Branch(content, initial, [Place("rifle", Secondary)]);
        Check.True(upgraded.CampaignVictory && expanded.Session.IsDefeat,
            "same-state Apex beats expansion in the finite armored fixture");
        Add(rows, "Apex allocation", "promote existing rifle", content, upgraded, directory);
        Add(rows, "Apex allocation", "buy another rifle", content, expanded, directory);
    }

    private static void SavingForApex(List<CaseResult> rows, string? directory)
    {
        var rifle = Rifle(100, 10, 4);
        rifle.Apex = new TowerApexDefinition { UpgradeCost = 150, DamageMultiplier = 6 };
        var groups = new List<WaveGroupDefinition>
        {
            new() { EnemyId = "income", Count = 1 },
            new() { EnemyId = "armored", Count = 1, DelayBefore = 1 }
        };
        var waves = Enumerable.Range(1, 21).Select(number => Wave(number, "income")).ToList();
        waves[^1].Groups = groups;
        var content = Fixture("save-for-apex", 200, [rifle],
            [Enemy("income", 20, 40, reward: 140), Enemy("armored", 500, 80, armor: 20)], waves);
        var initial = ImportedApex(content);
        Check.True(SpendingCandidateGenerator.Generate(initial.Session).Any(candidate => candidate.WaitForCredits == 150 &&
                candidate.Commands.Any(command => command.Type == GameCommandType.UpgradeTower)),
            "saving is a conditional Apex candidate before affordability");
        var saved = Branch(content, initial, [], adapter =>
        {
            if (!adapter.Session.Towers[0].IsApex && adapter.Session.Economy.Credits >= 150)
                Accepted(adapter, Command(GameCommandType.UpgradeTower, 1), "credits reach actual Apex price");
        });
        var spent = Branch(content, initial, [Place("rifle", Secondary)]);
        var noCombat = Branch(content, initial, []);
        Check.True(saved.CampaignVictory && spent.Session.IsDefeat && noCombat.Session.IsDefeat,
            "reserving credits and purchasing after income are both necessary in the Apex threshold fixture");
        Add(rows, "Saving and combat income", "wait then Apex at price", content, saved, directory);
        Add(rows, "Saving and combat income", "spend reserve on expansion", content, spent, directory);
        Add(rows, "Saving and combat income", "combat purchasing disabled", content, noCombat, directory);
    }

    private static void BeaconPlacement(List<CaseResult> rows, string? directory)
    {
        var rifle = Rifle(150, 10, 2);
        var beacon = new TowerDefinition
        {
            Id = "beacon", DisplayName = "Fixture Beacon", Behavior = "aura", PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { AuraRange = 110, AuraAttackSpeedBonus = 2 }]
        };
        var content = Fixture("support-pair", 250, [rifle, beacon], [Enemy("durable", 180, 80)], [Wave(1, "durable")]);
        var initial = Fresh(content);
        Accepted(initial, Place("rifle", Primary));
        var poor = new Vector2(750, 400);
        Check.True(SpendingCandidateGenerator.SupportValue(initial.Session, Secondary, beacon.Levels[0]) >
            SpendingCandidateGenerator.SupportValue(initial.Session, poor, beacon.Levels[0]),
            "support proposals distinguish actual recipients from an empty aura");
        var useful = Branch(content, initial, [Place("beacon", Secondary)]);
        var idle = Branch(content, initial, [Place("beacon", poor)]);
        var absent = Branch(content, initial, []);
        Check.True(useful.CampaignVictory && idle.Session.IsDefeat && absent.Session.IsDefeat,
            "a useful Beacon pairing wins while equal-cost isolated support loses");
        Add(rows, "Support geometry", "Beacon reaches rifle", content, useful, directory);
        Add(rows, "Support geometry", "Beacon misses rifle", content, idle, directory);
        Add(rows, "Support geometry", "support disabled", content, absent, directory);
    }

    private static void SustainedRecovery(List<CaseResult> rows, string? directory)
    {
        var sustained = Rifle(100, 10, 2);
        var burst = Rifle(100, 40, .5f);
        burst.Id = "burst";
        var content = Fixture("repair-suppression", 100, [sustained, burst],
            [Enemy("repair", 150, 25, regeneration: 100)], [Wave(1, "repair")]);
        var initial = Fresh(content);
        var frequent = Branch(content, initial, [Place("rifle", Primary)]);
        var infrequent = Branch(content, initial, [Place("burst", Primary)]);
        Check.True(frequent.CampaignVictory && infrequent.Session.IsDefeat,
            "equal nominal DPS differs when frequent health damage suppresses repair");
        Add(rows, "Native recovery", "sustained hits", content, frequent, directory);
        Add(rows, "Native recovery", "equal-DPS slow burst", content, infrequent, directory);
    }

    private static void TimelyPlate(List<CaseResult> rows, string? directory)
    {
        var content = Fixture("timely-plate", 100, [Rifle(200, 10, 2)],
            [Enemy("runner", 38, 100)], [Wave(1, "runner")], tactics: true);
        var initial = Fresh(content);
        PlannerSession Timed(long tick) => Branch(content, initial, [], adapter =>
        {
            if (adapter.Tick == tick)
                Accepted(adapter, Command(GameCommandType.DeployEmergencyDefense) with { X = 400, Y = 120 },
                    "fixed tick in synthetic timing experiment");
        });
        var timely = Timed(180);
        var late = Timed(270);
        var omitted = Branch(content, initial, []);
        Check.True(timely.CampaignVictory && late.Session.IsDefeat && omitted.Session.IsDefeat,
            "the same legal Plate placement can save or miss the runner depending on timing");
        Add(rows, "Deployable timing", "deploy before runner", content, timely, directory);
        Add(rows, "Deployable timing", "90-tick action delay", content, late, directory);
        Add(rows, "Deployable timing", "deployables disabled", content, omitted, directory);
    }

    private static (int Sequences, int LegalSequences, int WinningSequences, int Revisions) EarlierInvestment(
        List<CaseResult> rows, string? directory)
    {
        var content = RevisionContent();
        var initial = Fresh(content);
        Accepted(initial, Place("rifle", Primary));
        var firstChoices = new (string Name, GameCommand[] Commands)[]
        {
            ("buy immediate damage", [Place("rifle", Secondary)]), ("retain upgrade reserve", [])
        };
        var secondChoices = new (string Name, GameCommand[] Commands)[]
        {
            ("wait", []), ("buy rifle", [Place("rifle", Secondary)]), ("upgrade original", [Command(GameCommandType.UpgradeTower, 1)])
        };
        var sequences = 0;
        var legal = 0;
        var wins = 0;
        var revisions = 0;
        long strongerClear = 0;
        long savedClear = 0;
        var firstIndex = 0;
        foreach (var first in firstChoices)
        {
            var waveOne = Branch(content, initial, first.Commands);
            Check.True(!waveOne.Session.IsDefeat && waveOne.Session.CurrentWave == 1, "both earlier boards clear wave one");
            if (firstIndex++ == 0) strongerClear = waveOne.Tick;
            else { savedClear = waveOne.Tick; revisions++; }
            foreach (var second in secondChoices)
            {
                sequences++;
                var branch = PlannerSession.Restore(content, waveOne.Capture());
                if (second.Commands.Any(command => !branch.Apply(command, "finite wave-two alternative").Accepted)) continue;
                legal++;
                RunWave(branch);
                if (branch.CampaignVictory) wins++;
                Add(rows, "Earlier spending revision", first.Name + " / " + second.Name, content, branch, directory);
                if (first.Name == "buy immediate damage")
                    Check.True(!branch.CampaignVictory, "stronger earlier spending cannot solve the later wave in the finite action model");
            }
        }
        Check.True(strongerClear < savedClear && wins == 1,
            "the immediately faster clearing board loses later; exactly one restricted sequence wins after revising earlier spending");
        return (sequences, legal, wins, revisions);
    }

    private static bool ComparePlannerToOracle(string directory, bool oracleHasWin)
    {
        var content = RevisionContent();
        var initial = Fresh(content);
        Accepted(initial, Place("rifle", Primary));
        var state = SpendingPlanner.Run(content, initial.Scenario,
            new PlannerBudget { Simulations = 64, WallSeconds = 30, InitialBreadth = 4, MaximumBreadth = 24,
                BeamWidth = 2, MaximumWaveTicks = 1500, WithinWaveBranches = 0, BacktrackDepth = 2 },
            new PlannerRestrictions { CombatPurchasing = false, Family = "concentrated" },
            Path.Combine(directory, "finite-planner"), initial.Capture());
        Check.Equal(oracleHasWin, state.HasVerifiedWin, "general planner and exhaustive finite oracle agree on a reachable win");
        return state.HasVerifiedWin == oracleHasWin;
    }

    internal static PlannerSession CreateRevisionStart(GameContent content)
    {
        var initial = Fresh(content);
        Accepted(initial, Place("rifle", Primary));
        return initial;
    }

    internal static GameContent RevisionContent()
    {
        var rifle = Rifle(100, 10, 4);
        rifle.Levels[0].UpgradeCost = 150;
        rifle.Levels.Add(new TowerLevelDefinition { Damage = 60, AttacksPerSecond = 4, Range = 600, ArmorPierce = 20 });
        return Fixture("earlier-investment", 200, [rifle],
            [Enemy("opening", 60, 50), Enemy("armored", 300, 80, armor: 20)],
            [Wave(1, "opening"), Wave(2, "armored")]);
    }

    private static PlannerSession ImportedApex(GameContent content)
    {
        var setup = new GameSession(content, content.Map.Id, "fixture", "fixture");
        Check.True(GameCommandProcessor.Apply(setup, Place("rifle", Primary)).Accepted, "prepare imported fixture tower legally");
        var save = setup.CaptureSaveGame();
        save.Waves.CurrentWaveNumber = 20;
        return PlannerSession.FromSave(content, save, Scenario(content) with { StartingProfile = "synthetic-imported" });
    }

    private static PlannerSession Fresh(GameContent content) => PlannerSession.Create(content, Scenario(content));
    private static PlanningScenario Scenario(GameContent content) => new()
    {
        MapId = content.Map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = content.Waves.Waves.Count,
        InformationProfile = "oracle", ExecutionProfile = "expert", Seed = 1337
    };

    private static PlannerSession Branch(GameContent content, PlannerSession initial, IReadOnlyList<GameCommand> commands,
        Action<PlannerSession>? react = null)
    {
        var branch = PlannerSession.Restore(content, initial.Capture());
        foreach (var command in commands) Accepted(branch, command, "finite pre-wave alternative");
        RunWave(branch, react);
        return branch;
    }

    private static void RunWave(PlannerSession adapter, Action<PlannerSession>? react = null)
    {
        Accepted(adapter, Command(GameCommandType.StartWave), "finite fixture wave start");
        var start = adapter.Tick;
        while (adapter.Session.Waves.IsActive && !adapter.Session.IsDefeat && adapter.Tick - start < 3_600)
        {
            react?.Invoke(adapter);
            adapter.Advance(1);
        }
        Check.True(adapter.Tick - start < 3_600, "fixture must reach a real cleared or defeat state within its declared horizon");
    }

    private static void Accepted(PlannerSession adapter, GameCommand command, string trigger = "fixture setup") =>
        Check.True(adapter.Apply(command, trigger).Accepted, "fixture action is accepted by production rules: " + command.Type);

    private static void Add(List<CaseResult> rows, string name, string alternative, GameContent content,
        PlannerSession adapter, string? directory)
    {
        var trace = adapter.ExportTrace();
        var verification = PlanningReplay.Verify(content, trace);
        Check.True(verification.Verified, "independent diagnostic replay: " + verification.Reason);
        rows.Add(new(name, alternative, adapter.CampaignVictory, adapter.Session.CurrentWave, adapter.Session.Economy.Lives,
            adapter.Session.Economy.Credits, adapter.Tick, trace.Actions.Count,
            trace.Actions.Count(action => action.Trigger is "credits reach actual Apex price" or "fixed tick in synthetic timing experiment"),
            verification.Verified, verification.FromNormalStart, "synthetic content only"));
        if (directory is not null && adapter.CampaignVictory)
        {
            var file = $"fixture-witness-{rows.Count:D2}";
            PlannerStore.Write(Path.Combine(directory, file + ".json"), trace);
            PlannerStore.Write(Path.Combine(directory, file + "-content.json"), content);
            PlannerStore.Write(Path.Combine(directory, file + "-verification.json"), verification);
        }
    }

    private static GameCommand Command(GameCommandType type, int id = 0) => new() { PlayerId = 1, Type = type, EntityId = id };
    private static GameCommand Place(string id, Vector2 position) => Command(GameCommandType.PlaceTower) with
        { TowerDefinitionId = id, X = position.X, Y = position.Y };

    private static TowerDefinition Rifle(int price, float damage, float rate) => new()
    {
        Id = "rifle", DisplayName = "Fixture Rifle", Behavior = "beam", PurchaseCost = price,
        Levels = [new TowerLevelDefinition { Range = 600, Damage = damage, AttacksPerSecond = rate }],
        Protocol = new TowerProtocolDefinition { AttackSpeedBonus = 0, DamageBonus = 0, AutoTriggerCount = 100 }
    };

    private static EnemyDefinition Enemy(string id, float health, float speed, float armor = 0, int reward = 0,
        float regeneration = 0) => new()
    {
        Id = id, DisplayName = id, MaxHealth = health, Speed = speed, Armor = armor, Reward = reward,
        RegenerationPerSecond = regeneration, LivesLost = 1, Visual = new EnemyVisualData { Radius = 10 }
    };

    private static WaveDefinition Wave(int number, string enemy) => new()
        { Number = number, Groups = [new WaveGroupDefinition { EnemyId = enemy, Count = 1 }] };

    private static GameContent Fixture(string id, int credits, TowerDefinition[] towers, EnemyDefinition[] enemies,
        List<WaveDefinition> waves, bool tactics = false)
    {
        var map = new MapDefinition
        {
            Id = "fixture-" + id, DisplayName = "Synthetic " + id, StartingCredits = credits,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }],
            PathWidth = 30, BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        return new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Waves = new WaveSetDefinition { Waves = waves },
            Towers = towers.ToDictionary(tower => tower.Id), Enemies = enemies.ToDictionary(enemy => enemy.Id),
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition
                { Id = "fixture", TacticalSystemsEnabled = tactics, ProtocolsEnabled = false, SellingEnabled = false } }
        };
    }
}
