using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class CinderworksCalibrationTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(fixtures, "cinderworks-bastion-signal-plan.json"));
        var campaign = LayoutCampaignRunner.Run(content, plan,
            new() { MaximumTrials = 30, MaximumWallSeconds = 120, BeamWidth = 1 });
        Check.True(plan.Scenario is
            { MapId: "foundry_loop", DifficultyId: "bastion", ChallengeId: "close_quarters", Horizon: 30, ForbidCombatSales: true } &&
            campaign.Verification is { VerifiedWin: true, FromNormalStart: true, CombatSales: 0, IntermissionSales: 0 },
            "the Cinderworks plan clears the fresh Bastion / Signal campaign without sales");
        Check.True(campaign.Trials.Count == 30 && campaign.SelectedWaveCheckpoints.Count == 30,
            "the Cinderworks fixture reproduces its selected policy in one evaluation per wave");
        var board = PlannerSession.Restore(content, campaign.Checkpoint!).Session;
        Check.True(board.CurrentWave == 30 && board.Towers.Count == 38 && board.Towers.Count(tower => tower.IsApex) == 3 &&
            board.Generator is { LevelIndex: 2 } && board.Economy is
                { Lives: 1, EscapedEnemies: 0, Credits: 321, TotalKills: 2158 },
            "the campaign retains all 38 towers, three Apex upgrades and the mature Forge without leaks");

        var continuation = PlannerStore.Read<EndlessLayoutPlan>(Path.Combine(fixtures, "cinderworks-bastion-signal-endless31.json"));
        var endless = EndlessLayoutContinuation.Run(content, campaign.Checkpoint!, continuation,
            new() { MaximumTrials = 1, MaximumWallSeconds = 120 });
        Check.True(endless.Verification is
            { Verified: true, FromNormalStart: true, HorizonSurvived: true, VerifiedWin: false, CombatSales: 0, IntermissionSales: 0 },
            "the same Cinderworks defense survives generated Endless wave 31");
        var survivor = PlannerSession.Restore(content, endless.SelectedCheckpoint!).Session;
        Check.True(survivor.CurrentWave == 31 && survivor.Economy is
                { Lives: 1, EscapedEnemies: 0, Credits: 298, TotalKills: 2294 } &&
            survivor.Towers.OrderBy(tower => tower.Id).Select(tower => (tower.Id, tower.Position, tower.LevelIndex,
                tower.DoctrineId, tower.SpecializationId, tower.IsApex, tower.InvestedCredits))
            .SequenceEqual(board.Towers.OrderBy(tower => tower.Id).Select(tower => (tower.Id, tower.Position, tower.LevelIndex,
                tower.DoctrineId, tower.SpecializationId, tower.IsApex, tower.InvestedCredits))),
            "wave 31 preserves every tower and upgrade without leaks");
        Check.True(survivor.Generator is { } forge && board.Generator is { } originalForge &&
            forge.Position == originalForge.Position && forge.LevelIndex == originalForge.LevelIndex &&
            forge.InvestedCredits == originalForge.InvestedCredits,
            "the Endless continuation retains the existing Forge without further investment");
        var continuationActions = endless.SelectedTrace!.Actions.Skip(campaign.WinningTrace!.Actions.Count).ToArray();
        Check.True(continuationActions.All(action => action.Command.Type is
                GameCommandType.ContinueEndless or GameCommandType.StartWave or GameCommandType.SetTargetMode or
                GameCommandType.OverdriveTower or GameCommandType.DeployEmergencyDefense) &&
            continuationActions.Count(action => action.Accepted && action.ActualCost > 0 &&
                action.Command.Type == GameCommandType.DeployEmergencyDefense) == 7,
            "the standing-board continuation uses targeting, protocols and seven paid Plates without construction or sales");
    }
}
