using MinimalBastion.Core;
using MinimalBastion.Data;

namespace MinimalBastion.Enemies;

public enum EnemySignalRole
{
    None,
    Accelerator,
    Restorer,
    Bulwark,
    Jammer,
    Disruptor
}

public static class EnemySignalSchedule
{
    public static EnemySignalRole Resolve(WaveDefinition wave, int groupIndex, int spawnedInGroup,
        WaveGroupDefinition group, bool supportEnabled = true, bool attackersEnabled = true)
    {
        if (wave.Number < 2 || group.Count <= 0) return EnemySignalRole.None;
        if (Enum.TryParse<EnemyRank>(group.Rank, true, out var rank) && rank is EnemyRank.Elite or EnemyRank.Boss)
            return attackersEnabled ? EnemySignalRole.Disruptor : EnemySignalRole.None;

        var signalIndex = (group.Count - 1) / 2;
        if (spawnedInGroup != signalIndex) return EnemySignalRole.None;
        if (wave.Number >= 6 && (wave.Number + groupIndex) % 2 != 0) return EnemySignalRole.None;

        EnemySignalRole[] roles =
        [
            EnemySignalRole.Accelerator,
            EnemySignalRole.Restorer,
            EnemySignalRole.Bulwark,
            EnemySignalRole.Jammer
        ];
        var role = wave.Number <= 5
            ? groupIndex < wave.Number - 1 ? roles[groupIndex] : EnemySignalRole.None
            : group.EnemyId.Contains("aegis", StringComparison.OrdinalIgnoreCase)
                ? EnemySignalRole.Bulwark
                : group.EnemyId.Contains("regenerator", StringComparison.OrdinalIgnoreCase)
                    ? EnemySignalRole.Restorer
                    : roles[((wave.Number + groupIndex) / 2) % roles.Length];
        return role switch
        {
            EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark
                when !supportEnabled => EnemySignalRole.None,
            EnemySignalRole.Jammer when !attackersEnabled => EnemySignalRole.None,
            _ => role
        };
    }
}

public static class EnemySignalTuning
{
    public static float DisruptorPause(ChallengeDefinition rules, EnemyRank rank) =>
        rules.CounterPressureDuration * (rank switch
        {
            EnemyRank.Elite => 1.08f,
            EnemyRank.Boss => 1.20f,
            _ => 1f
        });

    public static float DisruptorReach(ChallengeDefinition rules, EnemyRank rank) =>
        rules.CounterPressureRadius * (rank switch
        {
            EnemyRank.Elite => 1.08f,
            EnemyRank.Boss => 1.22f,
            _ => 1f
        });
}
