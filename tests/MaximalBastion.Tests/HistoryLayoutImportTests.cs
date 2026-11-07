using System.Security.Cryptography;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class HistoryLayoutImportTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var frost = new TowerSaveData { Id = 5, DefinitionId = "frost_spire", X = 499.76382f, Y = 333.6216f,
            LevelIndex = 2, DoctrineId = "frost_deep_chill", SpecializationId = "permafrost", IsApex = true,
            InvestedCredits = 1015, TargetMode = TargetMode.Fastest };
        var beacon = new TowerSaveData { Id = 12, DefinitionId = "signal_beacon", X = 523.7553f, Y = 439.99f,
            LevelIndex = 2, DoctrineId = "beacon_repeater", SpecializationId = "horizon_beacon" };
        var entry = new RunHistoryEntry
        {
            RunId = "recorded-run", CompletedAtUtc = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Victory = true, CurrentWave = 30, TotalWaves = 30, StartingLives = 1, Lives = 1, CreditsRemaining = 277,
            MapId = "relay_divide", MapName = "test map", DifficultyId = "bastion", DifficultyName = "Bastion",
            ChallengeId = "close_quarters", ChallengeName = "Signal Gauntlet",
            FinalLayout = new() { StoredPlates = 3, AutoProtocolTowerId = 12, Towers = [beacon, frost] }
        };
        var before = JsonSerializer.Serialize(entry, PlannerStore.Json);
        var imported = HistoryLayoutImporter.Convert(content, entry, 28, forbidCombatSales: true);
        Check.Equal(before, JsonSerializer.Serialize(entry, PlannerStore.Json), "conversion leaves the history entry unchanged");
        Check.True(imported.Layout.Towers.Count == 2 && imported.Layout.Towers[0] is
            { Key: "goal-5", TowerId: "frost_spire", DoctrineId: "frost_deep_chill", SpecializationId: "permafrost", ApexFromWave: 28 } &&
            imported.Layout.Towers[0].X == frost.X && imported.Layout.Towers[0].Y == frost.Y &&
            imported.Layout.Towers[1].ApexFromWave == 0 && imported.Layout.AutomaticProtocolPreference.SequenceEqual(["goal-12"]),
            "history preserves each surviving position and branch in ID order and maps the automatic Protocol by goal key");
        var fresh = PlannerSession.Create(content, imported.Plan.Scenario);
        Check.True(imported.Plan.Scenario.ForbidCombatSales && fresh.Session.CurrentWave == 0 && fresh.Session.Towers.Count == 0 &&
            fresh.Session.Economy.Credits != entry.CreditsRemaining && fresh.Session.EmergencyInventory != entry.FinalLayout.StoredPlates,
            "imported plans start with authored normal resources and an empty board");
        var early = imported.Plan.Waves[0];
        var late = imported.Plan.Waves[1];
        Check.True(early.FirstWave == 1 && early.LastWave + 1 == GameConstants.ApexUnlockWave &&
            early.Policy is { DecisionTicks: 60, SpendingDecisionTicks: 60, PrepareBeforeStart: true, ReactAtBoundary: false,
                DirectPlateLimit: 0, TargetingProfileId: "first" } && early.Policy.BehaviorTargetModes.Count == 0 &&
            late.FirstWave == GameConstants.ApexUnlockWave && late.LastWave == GameConstants.CampaignWaveCount &&
            late.Policy is { DecisionTicks: 15, SpendingDecisionTicks: 15, PrepareBeforeStart: true, ReactAtBoundary: false,
                DirectPlateLimit: 0, TargetingProfileId: "split" } && late.Policy.BehaviorTargetModes["slow_projectile"] == TargetMode.Fastest,
            "starter policies switch cadence at Apex unlock and spend on the layout before paid defenses");

        var aggregate = entry with { FinalLayout = null };
        Check.Throws<InvalidDataException>(() => HistoryLayoutImporter.Convert(content, aggregate), "aggregate history cannot supply positions");
        SaveGameData MatchingSave() => new() { RunId = entry.RunId, MapId = entry.MapId, DifficultyId = entry.DifficultyId,
            ChallengeId = entry.ChallengeId, NextTowerId = 13, Towers = [frost], AutoOverdriveTowerId = 5,
            Waves = new() { CurrentWaveNumber = 28 } };
        var alternate = HistoryLayoutImporter.Convert(content, aggregate, 28, fallback: MatchingSave());
        Check.True(alternate.Provenance.LayoutSource == "matching-raw-save" && alternate.Provenance.SourceWave == 28 &&
            alternate.Layout.Towers.Count == 1, "matching save geometry is distinguished from the later history outcome");
        Check.True(HistoryLayoutImporter.Convert(content, entry, 28, fallback: MatchingSave()).Layout.Towers.Count == 2,
            "recorded final geometry takes precedence over a matching earlier snapshot");
        foreach (var change in new Action<SaveGameData>[]
        {
            save => save.RunId = "another", save => save.MapId = "another", save => save.DifficultyId = "another",
            save => save.ChallengeId = "another", save => save.IsCoOp = true, save => save.Waves.EndlessModeEnabled = true
        })
        {
            var save = MatchingSave();
            change(save);
            Check.Throws<InvalidDataException>(() => HistoryLayoutImporter.Convert(content, aggregate, 28, fallback: save),
                "alternate geometry must match all recorded identity fields and modes");
        }
        Check.Throws<InvalidDataException>(() => HistoryLayoutImporter.Convert(content, entry with
            { FinalLayout = new() { Towers = [frost, new() { Id = 7, DefinitionId = "needle_turret", X = frost.X + 1, Y = frost.Y }] } }),
            "a currently illegal position is reported rather than dropped");
        Check.Throws<InvalidDataException>(() => RunHistoryRepository.ValidateEntries([null!]),
            "a null JSON history record is rejected with a validation error");
        Check.Throws<InvalidDataException>(() => HistoryLayoutImporter.Convert(content, entry with
            { FinalLayout = new() { Towers = [null!] } }), "a null JSON tower is rejected with a validation error");
        FileImport(content, entry);
    }

    private static void FileImport(GameContent content, RunHistoryEntry entry)
    {
        var folder = Path.Combine(Path.GetTempPath(), "MaximalBastionHistoryImport-" + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(folder, "layout.json");
        var output = Path.Combine(folder, "output");
        try
        {
            PlannerStore.Write(input, new[] { entry });
            var original = File.ReadAllBytes(input);
            var args = new[] { $"--run-id={entry.RunId}", "--history", input, "--apex-from-wave=28", "--no-combat-sales" };
            Check.Throws<ArgumentException>(() => HistoryLayoutCli.Run(content, args, folder), "output cannot replace input history");
            Check.Equal(0, HistoryLayoutCli.Run(content, args, output), "explicit history imports a reusable layout and plan");
            var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(output, "plan.json"));
            plan.Validate(content);
            using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "provenance.json")));
            Check.Equal(Convert.ToHexString(SHA256.HashData(original)), provenance.RootElement.GetProperty("historySha256").GetString()!,
                "evidence identifies the exact source bytes");
            Check.True(File.ReadAllBytes(input).SequenceEqual(original), "history bytes remain unchanged");
            using (var oversized = new FileStream(input, FileMode.Open, FileAccess.Write))
                oversized.SetLength(RunHistoryRepository.MaximumHistoryFileBytes + 1);
            Check.Throws<InvalidDataException>(() => HistoryLayoutCli.Run(content, args, output), "over-limit history is rejected before parsing");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
