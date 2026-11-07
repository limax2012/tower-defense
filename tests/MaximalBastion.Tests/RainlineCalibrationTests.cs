using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class RainlineCalibrationTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(fixtures, "rainline-bastion-signal-plan.json"));
        var campaign = LayoutCampaignRunner.Run(content, plan,
            new() { MaximumTrials = 30, MaximumWallSeconds = 120, BeamWidth = 1 });
        Check.True(plan.Scenario is { MapId: "crosswind_basin", DifficultyId: "bastion", ChallengeId: "close_quarters", ForbidCombatSales: true } &&
            campaign.Verification is { VerifiedWin: true, FromNormalStart: true, CombatSales: 0, IntermissionSales: 0 },
            "the Rainline plan clears the fresh Bastion / Signal campaign without sales");
        var board = PlannerSession.Restore(content, campaign.Checkpoint!).Session;
        Check.True(board.Towers.Count == 35 && board.Towers.All(tower => !tower.IsApex) && board.Economy.EscapedEnemies == 0,
            "the Rainline campaign retains its full 35-tower defense without leaks");
        var continuation = PlannerStore.Read<EndlessLayoutPlan>(Path.Combine(fixtures, "rainline-bastion-signal-endless31.json"));
        var endless = EndlessLayoutContinuation.Run(content, campaign.Checkpoint!, continuation,
            new() { MaximumTrials = 1, MaximumWallSeconds = 120 });
        Check.True(endless.Verification is { FromNormalStart: true, HorizonSurvived: true, VerifiedWin: false, CombatSales: 0, IntermissionSales: 0 },
            "the same Rainline defense survives generated Endless wave 31");
        var survivor = PlannerSession.Restore(content, endless.SelectedCheckpoint!).Session;
        Check.True(survivor.Economy.EscapedEnemies == 0 &&
            survivor.Towers.OrderBy(tower => tower.Id).Select(tower => (tower.Id, tower.Position, tower.LevelIndex,
                tower.DoctrineId, tower.SpecializationId, tower.IsApex, tower.InvestedCredits))
            .SequenceEqual(board.Towers.OrderBy(tower => tower.Id).Select(tower => (tower.Id, tower.Position, tower.LevelIndex,
                tower.DoctrineId, tower.SpecializationId, tower.IsApex, tower.InvestedCredits))),
            "wave 31 preserves every tower and upgrade");
        Check.True(endless.SelectedTrace!.Actions.Skip(campaign.WinningTrace!.Actions.Count)
            .All(action => action.ActualCost <= 0 && action.Command.Type is
                GameCommandType.ContinueEndless or GameCommandType.StartWave or GameCommandType.SetTargetMode),
            "the Rainline continuation needs only wave-start and targeting commands");
    }
}
