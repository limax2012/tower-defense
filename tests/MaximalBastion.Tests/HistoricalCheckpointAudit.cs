using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class HistoricalCheckpointAudit
{
    public sealed record AlternativeResult(string Id, string Kind, int UpfrontCost, bool Accepted, bool Cleared,
        bool CampaignVictory, int Lives, int Credits, int Kills, int CombatActions, int PlatePurchases,
        long Ticks, double WallSeconds, bool ReplayVerified, string Trace, string? Error);

    public static void Run(GameContent content, string savePath, string directory)
    {
        savePath = Path.GetFullPath(savePath);
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var save = PlannerStore.Read<SaveGameData>(savePath);
        var scenario = new PlanningScenario
        {
            MapId = save.MapId, DifficultyId = save.DifficultyId, ChallengeId = save.ChallengeId,
            StartingProfile = "imported", Seed = 1337, CoOp = save.IsCoOp,
            Horizon = save.Waves.CurrentWaveNumber + 1, InformationProfile = "oracle", ExecutionProfile = "expert"
        };
        var initial = PlannerSession.FromSave(content, save, scenario);
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(savePath)));
        var sourceCopy = Path.Combine(directory, "source-checkpoint.json");
        if (!string.Equals(savePath, sourceCopy, StringComparison.OrdinalIgnoreCase)) File.Copy(savePath, sourceCopy, true);
        var alternatives = new List<SpendingCandidate>
        {
            new() { Id = "retain-board", Family = "baseline", Reason = "Retain the exact existing defense and credits." }
        };
        foreach (var tower in initial.Session.Towers.Where(initial.Session.CanApexUpgrade).OrderBy(tower => tower.Id))
        {
            if (tower.ApexUpgradeCost > initial.Session.Economy.Credits) continue;
            alternatives.Add(new()
            {
                Id = $"apex-{tower.Id}-{tower.Definition.Id}", Family = "Apex", Cost = tower.ApexUpgradeCost,
                Commands = [new GameCommand { PlayerId = 1, Type = GameCommandType.UpgradeTower, EntityId = tower.Id }]
            });
        }
        var proposed = SpendingCandidateGenerator.Generate(initial.Session, new CandidateGenerationOptions
            { MaximumCandidates = 256, PositionsPerTower = 3, IncludeSales = false, IncludeTactical = false });
        alternatives.AddRange(proposed.Where(candidate => candidate.WaitForCredits is null && candidate.Cost > 0 &&
                candidate.Commands.Count > 0 && candidate.Commands[0].Type == GameCommandType.PlaceTower)
            .GroupBy(candidate => candidate.Commands[0].TowerDefinitionId)
            .SelectMany(group => group.OrderByDescending(candidate => candidate.Score).Take(2))
            .Take(20).Select((candidate, index) => candidate with { Id = $"expansion-{index:D2}-{candidate.Commands[0].TowerDefinitionId}", Family = "expansion" }));

        var results = new List<AlternativeResult>();
        foreach (var candidate in alternatives)
        {
            var clock = Stopwatch.StartNew();
            var branch = PlannerSession.Restore(content, initial.Capture());
            var accepted = true;
            foreach (var command in candidate.Commands)
            {
                if (branch.Apply(command, "same-budget initial alternative: " + candidate.Id).Accepted) continue;
                accepted = false;
                break;
            }
            var initialSpent = initial.Session.Economy.Credits - branch.Session.Economy.Credits;
            if (!accepted)
            {
                results.Add(new(candidate.Id, candidate.Family, initialSpent, false, false, false,
                    branch.Session.Economy.Lives, branch.Session.Economy.Credits, 0, 0, 0, 0,
                    clock.Elapsed.TotalSeconds, false, "", "Initial bundle rejected by current game rules."));
                continue;
            }
            var policy = new AutoPlayer(branch.Session, AutoPlayerStrategy.Experienced, scenario.Seed,
                new SimulationOptions { HoldFootprint = true, UseApexUpgrades = false }, command =>
                {
                    if (command.Type is not (GameCommandType.SetTargetMode or GameCommandType.OverdriveTower or GameCommandType.DeployEmergencyDefense))
                        return false;
                    if (command.Type == GameCommandType.SetTargetMode &&
                        branch.Session.Towers.FirstOrDefault(tower => tower.Id == command.EntityId)?.TargetMode == command.TargetMode)
                        return true;
                    return branch.Apply(command, "matched one-second targeting/Protocol/Plate policy").Accepted;
                });
            policy.PrepareForWave(branch.Session);
            var startKills = branch.Session.Economy.TotalKills;
            var combatActions = branch.Actions.Count;
            var start = branch.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave }, "same-state measured next wave");
            if (start.Accepted)
            {
                while (branch.Session.Waves.IsActive && !branch.Session.IsDefeat && branch.Tick < 18_000)
                {
                    branch.Advance(1);
                    if (branch.Tick % 60 == 0 && branch.Session.Waves.IsActive && !branch.Session.IsDefeat)
                        policy.ReactDuringWave(branch.Session);
                }
            }
            var cleared = !branch.Session.IsDefeat && !branch.Session.Waves.IsActive && branch.Session.Enemies.Count == 0 &&
                branch.Session.CurrentWave == scenario.Horizon;
            var trace = branch.ExportTrace();
            var verification = PlanningReplay.Verify(content, trace);
            var traceName = candidate.Id + ".trace.json";
            PlannerStore.Write(Path.Combine(directory, traceName), trace);
            PlannerStore.Write(Path.Combine(directory, candidate.Id + ".verification.json"), verification);
            PlannerReport.WriteBuildOrder(Path.Combine(directory, candidate.Id + ".csv"), trace);
            results.Add(new(candidate.Id, candidate.Family, initialSpent, start.Accepted, cleared,
                branch.CampaignVictory, branch.Session.Economy.Lives, branch.Session.Economy.Credits,
                branch.Session.Economy.TotalKills - startKills,
                branch.Actions.Skip(combatActions).Count(action => action.Accepted && action.Command.Type != GameCommandType.StartWave),
                branch.Session.EmergencyDirectPurchasesThisWave, branch.Tick, clock.Elapsed.TotalSeconds,
                verification.Verified, traceName, verification.Verified ? null : verification.Reason));
        }
        PlannerStore.Write(Path.Combine(directory, "alternatives.json"), new
        {
            Source = savePath, SourceSha256 = sourceHash, BuildHash = PlanningFingerprint.Build(),
            ContentHash = PlanningFingerprint.Content(content), Scenario = scenario,
            StartingCredits = initial.Session.Economy.Credits, StartingLives = initial.Session.Economy.Lives,
            StartingTowers = initial.Session.Towers.Count,
            CommonContinuation = "Experienced targeting/Protocols/Plates every 60 ticks; no additional tower purchases/upgrades/sales/Forge. 18,000-tick wave cutoff.",
            Results = results
        });
        var markdown = new StringBuilder("# Imported checkpoint allocation experiment\n\n")
            .AppendLine($"Scenario: `{scenario.Key}`. Source checkpoint SHA-256 `{sourceHash}`; current content `{PlanningFingerprint.Content(content)}`; build `{PlanningFingerprint.Build()}`.")
            .AppendLine($"\nIdentical initial state: {initial.Session.Economy.Credits} credits, {initial.Session.Economy.Lives} lives, {initial.Session.Towers.Count} towers, next wave {scenario.Horizon}.")
            .AppendLine("\nEvery affordable existing Apex competes with a limited set of geometry-ranked new-tower bundles and retaining the board. After the initial choice, all branches use the same one-second Experienced targeting/Protocol/Plate policy and forbid further tower/Forge purchases, upgrades and sales. Initial purchase order and live tactical income remain authoritative.")
            .AppendLine("\n| Alternative | Initial cost | Wave cleared | Campaign terminal | Lives | Credits | Kills | Combat actions | Plates bought | Replay | Trace |")
            .AppendLine("| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var result in results)
            markdown.AppendLine($"| {result.Id} | {result.UpfrontCost} | {result.Cleared} | {result.CampaignVictory} | {result.Lives} | {result.Credits} | {result.Kills} | {result.CombatActions} | {result.PlatePurchases} | {result.ReplayVerified} | [{result.Trace}]({result.Trace}) |");
        markdown.AppendLine("\nA clear establishes only this imported continuation under current content. Historical timing is not recovered. No claim of normal-start reachability, impossible alternatives, or player win probability follows. Untried sales, placements, mid-wave investments, tactics and earlier spending remain alternatives.")
            .AppendLine("\nThe source save is read-only; its unchanged copy is [source-checkpoint.json](source-checkpoint.json). Full machine-readable results: [alternatives.json](alternatives.json).");
        File.WriteAllText(Path.Combine(directory, "report.md"), markdown.ToString());
        File.WriteAllText(Path.Combine(directory, "alternatives.csv"),
            "id,kind,upfront_cost,accepted,wave_cleared,campaign_terminal,lives,credits,kills,combat_actions,plates,ticks,wall_seconds,replay_verified\n" +
            string.Join("\n", results.Select(result => $"{result.Id},{result.Kind},{result.UpfrontCost},{result.Accepted},{result.Cleared},{result.CampaignVictory},{result.Lives},{result.Credits},{result.Kills},{result.CombatActions},{result.PlatePurchases},{result.Ticks},{result.WallSeconds.ToString("R", CultureInfo.InvariantCulture)},{result.ReplayVerified}")));
    }
}
