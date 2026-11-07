using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaximalBastion.Simulation.Planning;

public sealed record PlannerBudget
{
    public int Simulations { get; init; } = 120;
    public double WallSeconds { get; init; } = 120;
    public int Workers { get; init; } = 1;
    public int MemoryMegabytes { get; init; } = 512;
    public int BeamWidth { get; init; } = 3;
    public int InitialBreadth { get; init; } = 10;
    public int MaximumBreadth { get; init; } = 50;
    public int DecisionTicks { get; init; } = 60;
    public int MaximumWaveTicks { get; init; } = 60 * 240;
    public int BacktrackDepth { get; init; } = 30;
    public int WithinWaveBranches { get; init; } = 2;
    public static PlannerBudget Preset(string id) => id.ToLowerInvariant() switch
    {
        "smoke" => new(),
        "standard" => new() { Simulations = 2000, WallSeconds = 1200, BeamWidth = 4, Workers = 2 },
        "deep" => new() { Simulations = 25000, WallSeconds = 14400, BeamWidth = 8, MaximumBreadth = 256,
            DecisionTicks = 30, MemoryMegabytes = 2048, WithinWaveBranches = 4, Workers = 4 },
        _ => throw new ArgumentException("Budget must be smoke, standard, or deep.")
    };
    public void Validate()
    {
        if (Simulations < 1 || !double.IsFinite(WallSeconds) || WallSeconds <= 0 || Workers is < 1 or > 32 ||
            MemoryMegabytes < 32 || BeamWidth is < 1 or > 64 || InitialBreadth < 1 ||
            MaximumBreadth < InitialBreadth || MaximumBreadth > 4096 || DecisionTicks is < 1 or > 3600 ||
            MaximumWaveTicks is < 60 or > 216000 || BacktrackDepth is < 0 or > 10000 || WithinWaveBranches is < 0 or > 32)
            throw new ArgumentException("Invalid planner resource budget.");
    }
}

public sealed record PlannerRestrictions
{
    public bool PlacementHeuristics { get; init; } = true;
    public bool CombatPurchasing { get; init; } = true;
    public bool SupportReasoning { get; init; } = true;
    public bool Saving { get; init; } = true;
    public bool Backtracking { get; init; } = true;
    public string Family { get; init; } = "unrestricted";
    public string Baseline { get; init; } = "planner";
    public string? ExcludedTowerId { get; init; }
    public int ReactionTicks { get; init; }
    public int MinimumActionTicks { get; init; }
}

public sealed class PlannerNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int Wave { get; set; }
    public required PlanningCheckpoint Checkpoint { get; set; }
    public string Family { get; set; } = "initial";
    public string Composition { get; set; } = "";
    public int Lives { get; set; }
    public int Credits { get; set; }
    public int Investment { get; set; }
    public int NextCandidate { get; set; }
    public bool Preferred { get; set; } = true;
}

public sealed record PlannerRepairTask
{
    public int ParentId { get; init; }
    public int Candidate { get; init; }
    public int ProposalOrdinal { get; init; }
    public int BacktrackDepth { get; init; }
    public required PlanningCheckpoint Checkpoint { get; init; }
}

public sealed class PlannerInvocationBudget
{
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required PlannerBudget Budget { get; init; }
    public double WallSeconds { get; set; }
    public int CompletedTrials { get; set; }
    public long SimulatedTicks { get; set; }
    public int InterruptedTrials { get; set; }
    public long InterruptedSimulatedTicks { get; set; }
    public string StopReason { get; set; } = "running";
}

public sealed record PlannerLeak(long Tick, string EnemyId, string Rank, int LifeCost, float Health, float Shield);
public sealed record PlannerEnemyRemainder(string EnemyId, int Count, float Health, float Shield, float FurthestProgress);
public sealed class PlannerTrial
{
    public int Index { get; set; }
    public int ParentId { get; set; }
    public int Candidate { get; set; }
    public bool CombatRepair { get; set; }
    public string Family { get; set; } = "";
    public int Wave { get; set; }
    public bool Cleared { get; set; }
    public bool Victory { get; set; }
    public int Lives { get; set; }
    public int Credits { get; set; }
    public long Ticks { get; set; }
    public double WallSeconds { get; set; }
    public int AcceptedActions { get; set; }
    public int InvalidActions { get; set; }
    public int CombatActions { get; set; }
    public int PlatePurchases { get; set; }
    public float Damage { get; set; }
    public float Overkill { get; set; }
    public float ArmorAbsorbed { get; set; }
    public float EstimatedRegeneration { get; set; }
    public float SuppressedTowerSeconds { get; set; }
    public int IdleSupports { get; set; }
    public int MissedPurchaseThresholds { get; set; }
    public int BacktrackDepth { get; set; }
    public List<PlannerLeak> Leaks { get; set; } = [];
    public List<PlannerEnemyRemainder> RemainingEnemies { get; set; } = [];
    public string Signature { get; set; } = "";
    public string RestorationMethod { get; set; } = "";
}

public sealed class PlannerSearchState
{
    public const string Version = "legal-spending-planner-v1";
    public string SolverVersion { get; set; } = Version;
    public string BuildHash { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public required PlanningScenario Scenario { get; set; }
    public required PlannerRestrictions Restrictions { get; set; }
    public required PlannerBudget Budget { get; set; }
    public List<PlannerNode> Nodes { get; set; } = [];
    public List<PlannerTrial> Trials { get; set; } = [];
    public List<PlannerRepairTask> PendingRepairs { get; set; } = [];
    public List<PlannerInvocationBudget> BudgetHistory { get; set; } = [];
    public List<string> WitnessFiles { get; set; } = [];
    public Dictionary<string, int> StrategyClusters { get; set; } = new(StringComparer.Ordinal);
    public int Breadth { get; set; }
    public int HighestCompletedWave { get; set; }
    public int Backtracks { get; set; }
    public double WallSeconds { get; set; }
    public double CheckpointMilliseconds { get; set; }
    public long SimulatedTicks { get; set; }
    public int InterruptedTrials { get; set; }
    public long InterruptedSimulatedTicks { get; set; }
    public string Outcome { get; set; } = "NO SOLUTION FOUND WITHIN BUDGET";
    public string StopReason { get; set; } = "not started";
    public string? VerificationFailure { get; set; }
    public bool ImportedStart { get; set; }
    public bool HasVerifiedWin => WitnessFiles.Count > 0 && Outcome == "VERIFIED WIN";
}

public static class PlannerStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    public static T Read<T>(string path) where T : class
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 512L * 1024 * 1024) throw new InvalidDataException("Planner artifact exceeds 512 MiB.");
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidDataException("Empty planner artifact.");
    }
    public static void Write<T>(string path, T value)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, value, Json);
            // Windows readers without delete sharing can briefly block atomic replacement.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    break;
                }
                catch (IOException) when (attempt < 8) { Thread.Sleep(25 * (attempt + 1)); }
                catch (UnauthorizedAccessException) when (attempt < 8) { Thread.Sleep(25 * (attempt + 1)); }
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
