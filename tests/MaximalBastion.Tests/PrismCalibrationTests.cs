using MaximalBastion.Data;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class PrismCalibrationTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "prism-bastion-signal-plan.json"));
        var result = LayoutCampaignRunner.Run(content, plan, new()
        {
            MaximumTrials = 60, MaximumWallSeconds = 120, BeamWidth = 3,
            DirectPlateLimits = [0, 8, 16], PlateLimitsFromWave = 29
        });
        Check.True(plan.Scenario.ForbidCombatSales && result.Verification is
            { VerifiedWin: true, FromNormalStart: true, CombatSales: 0, IntermissionSales: 0 },
            "the Prism history layout can be purchased from a fresh start and clears Bastion / Signal");
        var session = PlannerSession.Restore(content, result.Checkpoint!).Session;
        Check.True(session.Economy.EscapedEnemies == 0 && session.Towers.Count == 30 && session.Towers.Count(tower => tower.IsApex) == 6,
            "the Prism solution retains its mature board");
        Check.True(result.Trials.Where(row => row.Wave == 29).Select(row => row.DirectPlateLimit).Distinct().Order()
            .SequenceEqual(new[] { 0, 8, 16 }), "the wave-29 comparison tests each requested Plate limit");

        var trace = result.WinningTrace!;
        Check.Equal(30, result.SelectedWaveCheckpoints.Count, "all chosen campaign boundaries are available");
        foreach (var boundary in result.SelectedWaveCheckpoints)
        {
            var prefix = boundary.Actions.Where(action => action.Accepted).ToArray();
            Check.True(trace.Actions.Take(prefix.Length).SequenceEqual(prefix),
                "each selected checkpoint belongs to the actual winning branch");
        }
    }
}
