using System.Globalization;
using MaximalBastion.Data;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class EndlessLayoutCli
{
    public static int Run(GameContent content, string[] args, string output)
    {
        var checkpointPath = PlannerCli.Value(args, "--checkpoint") ?? throw new ArgumentException("--checkpoint is required.");
        var planPath = PlannerCli.Value(args, "--plan") ?? throw new ArgumentException("--plan is required.");
        var plan = PlannerStore.Read<EndlessLayoutPlan>(planPath);
        var checkpoint = PlannerStore.Read<PlanningCheckpoint>(checkpointPath);
        var options = new EndlessLayoutOptions
        {
            MaximumTrials = int.Parse(PlannerCli.Value(args, "--simulations") ?? "32", CultureInfo.InvariantCulture),
            MaximumWaveTicks = int.Parse(PlannerCli.Value(args, "--max-wave-ticks") ?? "18000", CultureInfo.InvariantCulture),
            MaximumWallSeconds = double.Parse(PlannerCli.Value(args, "--seconds") ?? "120", CultureInfo.InvariantCulture),
            ForbidCombatSales = args.Contains("--no-combat-sales", StringComparer.OrdinalIgnoreCase)
        };
        PlannerStore.Write(Path.Combine(output, "plan.json"), plan);
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += cancel;
        EndlessLayoutResult result;
        try
        {
            result = EndlessLayoutContinuation.Run(content, checkpoint, plan, options, (row, _) =>
                Console.WriteLine($"{row.PolicyId}: {row.StopReason}; {row.Kills} kills, {row.Lives} lives, {row.Credits} credits."), stop.Token);
        }
        finally { Console.CancelKeyPress -= cancel; }
        PlannerStore.Write(Path.Combine(output, "endless-ready.json"), result.Ready);
        if (result.SelectedCheckpoint is not null) PlannerStore.Write(Path.Combine(output, "checkpoint.json"), result.SelectedCheckpoint);
        if (result.SelectedTrace is not null)
        {
            PlannerStore.Write(Path.Combine(output, "trace.json"), result.SelectedTrace);
            PlannerStore.Write(Path.Combine(output, "verification.json"), result.Verification);
            PlannerReport.WriteBuildOrder(Path.Combine(output, "build-order.csv"), result.SelectedTrace);
        }
        PlannerStore.Write(Path.Combine(output, "results.json"), new
        {
            result.Outcome, result.StopReason, result.PlanId, plan.PreparationTicks, result.WallSeconds, result.Options, result.Trials,
            result.Ready.Scenario, result.Ready.BuildHash, result.Ready.ContentHash,
            CampaignSignature = checkpoint.StateSignature, TransitionSignature = result.Ready.StateSignature,
            result.Verification
        });
        Console.WriteLine($"{result.Outcome}; {result.Trials.Count} wave-31 evaluations\n{Path.Combine(output, "results.json")}");
        return 0;
    }
}
