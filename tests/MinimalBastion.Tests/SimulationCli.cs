using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalBastion.Data;
using MinimalBastion.Simulation;

namespace MinimalBastion.Tests;

internal static class SimulationCli
{
    public static int Run(GameContent content, string[] args, bool deep)
    {
        var selectedStrategy = ReadValue(args, "--strategy");
        var strategies = selectedStrategy is not null && Enum.TryParse<AutoPlayerStrategy>(selectedStrategy, true, out var parsed)
            ? new[] { parsed }
            : Enum.GetValues<AutoPlayerStrategy>();
        var baseSeed = int.TryParse(ReadValue(args, "--seed"), out var parsedSeed) ? parsedSeed : 1337;
        var runsPerStrategy = int.TryParse(ReadValue(args, "--runs"), out var parsedRuns)
            ? Math.Clamp(parsedRuns, 1, 100)
            : deep ? 5 : 1;
        var selectedMap = ReadValue(args, "--map");
        var maps = selectedMap is not null && content.Maps.ContainsKey(selectedMap)
            ? new[] { selectedMap }
            : deep ? content.Maps.Keys.OrderBy(x => x).ToArray() : new[] { content.Map.Id };
        var maximumWave = int.TryParse(ReadValue(args, "--max-wave"), out var parsedMaximumWave)
            ? Math.Max(1, parsedMaximumWave)
            : int.MaxValue;

        var runs = new List<SimulationRunResult>();
        foreach (var mapId in maps)
            foreach (var strategy in strategies)
            {
                for (var index = 0; index < runsPerStrategy; index++)
                {
                    var seed = baseSeed + index * 7919;
                    var result = HeadlessSimulation.Run(content, new SimulationOptions
                    {
                        Strategy = strategy,
                        Seed = seed,
                        MapId = mapId,
                        MaximumWave = maximumWave,
                        ContinueEndless = maximumWave > content.Waves.Waves.Count
                    });
                    runs.Add(result);
                    Console.WriteLine($"{mapId,-15} {strategy,-16} seed {seed,7}  {result.Result,-7}  wave {result.WaveReached,2}  lives {result.LivesRemaining,2}  spent {result.CreditsSpent,5}  towers {result.Towers.Values.Sum(x => x.Purchases),2}  plates {result.EmergencyDeployments,2}");
                }
            }

        var batch = new SimulationBatchResult { Runs = runs };
        Console.WriteLine();
        Console.WriteLine($"Runs {runs.Count}, wins {batch.Wins}, win rate {batch.WinRate:P1}, average wave {batch.AverageWaveReached:0.0}, average lives {batch.AverageLivesRemaining:0.0}.");
        PrintStrategySummary(runs);
        PrintMapSummary(runs);
        PrintTowerSummary(runs);
        Console.WriteLine($"Early calls earned {runs.Sum(x => x.EarlyStartCreditsEarned)} credits; overdrives {runs.Sum(x => x.Overdrives)}. Emergency defenses: {runs.Sum(x => x.EmergencyDeployments)} deployed, {runs.Sum(x => x.EmergencyTriggers)} triggers, {runs.Sum(x => x.EmergencyKills)} kills, {runs.Sum(x => x.EmergencyDamage):0} damage; generators {runs.Sum(x => x.GeneratorPurchases)}.");

        var root = FindProjectRoot();
        var output = ReadValue(args, "--output") ?? Path.Combine(root, ".build", "balance", deep ? "full-latest.json" : "latest.json");
        if (!Path.IsPathRooted(output)) output = Path.Combine(root, output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(output, JsonSerializer.Serialize(batch, jsonOptions));
        Console.WriteLine($"Machine-readable report: {output}");
        return 0;
    }

    private static void PrintMapSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("MAP SUMMARY");
        foreach (var group in runs.GroupBy(x => x.MapId).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-18} {group.Count(x => x.Won),2}/{group.Count(),-2} wins  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintStrategySummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("STRATEGY SUMMARY");
        foreach (var group in runs.GroupBy(x => x.Strategy).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-16} {group.Count(x => x.Won),2}/{group.Count(),-2} wins  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintTowerSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("TOWER USAGE");
        var towerRows = runs
            .SelectMany(run => run.Towers.Values)
            .GroupBy(x => x.TowerId)
            .Select(group => new
            {
                Id = group.Key,
                Picks = group.Sum(x => x.Purchases),
                Upgrades = group.Sum(x => x.Upgrades),
                Damage = group.Sum(x => x.Damage),
                Spent = group.Sum(x => x.CreditsSpent)
            })
            .OrderByDescending(x => x.Damage);
        foreach (var row in towerRows)
            Console.WriteLine($"{row.Id,-20} picks {row.Picks,3}  upgrades {row.Upgrades,3}  damage {row.Damage,10:0}  damage/credit {(row.Spent == 0 ? 0 : row.Damage / row.Spent),6:0.0}");
    }

    private static string? ReadValue(string[] args, string name)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return args[index][(name.Length + 1)..];
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length) return args[index + 1];
        }
        return null;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MinimalBastion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
