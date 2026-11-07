using MaximalBastion.Data;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class HelixCalibrationTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var trace = PlannerStore.Read<PlanningTrace>(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "helix-bastion-signal-witness.json"));
        Check.True(trace.OriginSaveJson is null && trace.Scenario.StartingProfile == "fresh" &&
            trace.Scenario.MapId == "relay_divide" && trace.Scenario.DifficultyId == "bastion" &&
            trace.Scenario.ChallengeId == "close_quarters" && trace.FinalWave == 30,
            "the calibration certificate covers the full normal-start Helix/Bastion/Signal campaign");
        // A compiler or planner change may alter the assembly identity. Every gameplay
        // state, action, cost and content identity must still match the archived certificate.
        var result = PlanningReplay.Verify(content, trace, requireCurrentBuild: false);
        Check.True(result.VerifiedWin && result.FromNormalStart,
            "the calibration strategy independently clears the unchanged production campaign: " + result.Reason);
        Check.Equal(759, result.ActionsChecked, "the complete calibration command sequence is verified");
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "helix-bastion-signal-plan.json"));
        var rebuilt = LayoutCampaignRunner.Run(content, plan, new LayoutCampaignOptions
            { MaximumTrials = 32, MaximumWallSeconds = 60, BeamWidth = 1 });
        Check.True(rebuilt.Verification is { VerifiedWin: true, FromNormalStart: true },
            "the reusable allocation and policy runner rebuilds the winning campaign from starting resources");
        Check.Equal(trace.FinalSignature, rebuilt.WinningTrace!.FinalSignature,
            "the calibrated policy reproduces the archived campaign outcome without reading its action sequence");
        VerifyStandingBoard(content);
    }

    private static void VerifyStandingBoard(GameContent content)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(fixtures, "helix-bastion-signal-standing-plan.json"));
        var campaign = LayoutCampaignRunner.Run(content, plan, new() { MaximumTrials = 30, MaximumWallSeconds = 120, BeamWidth = 1 });
        Check.True(plan.Scenario.ForbidCombatSales && campaign.Verification is
            { FromNormalStart: true, VerifiedWin: true, CombatSales: 0, IntermissionSales: 0 },
            "the standing-board plan wins from fresh resources under intermission-only selling");
        var board = PlannerSession.Restore(content, campaign.Checkpoint!).Session;
        var towerIds = board.Towers.Select(tower => tower.Id).Order().ToArray();
        Check.Equal(38, towerIds.Length, "the full built defense remains at campaign victory");

        var continuation = PlannerStore.Read<EndlessLayoutPlan>(Path.Combine(fixtures, "helix-bastion-signal-endless31.json"));
        var endless = EndlessLayoutContinuation.Run(content, campaign.Checkpoint!, continuation);
        Check.True(endless.Verification is { FromNormalStart: true, HorizonSurvived: true, CombatSales: 0, IntermissionSales: 0 },
            "the standing defense survives generated Endless wave 31 under the same restriction");
        var survivor = PlannerSession.Restore(content, endless.SelectedCheckpoint!).Session;
        Check.True(survivor.Towers.Select(tower => tower.Id).Order().SequenceEqual(towerIds),
            "all original towers survive the transition and the generated wave");
    }
}
