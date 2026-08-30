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
        if (wave.Number == 20 && wave.Groups.Any(IsBossGroup))
            return FilterAvailability(ResolveWave20BossFormationRole(wave, groupIndex), supportEnabled, attackersEnabled);
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
        return FilterAvailability(role, supportEnabled, attackersEnabled);
    }

    private static EnemySignalRole ResolveWave20BossFormationRole(WaveDefinition wave, int groupIndex)
    {
        var firstBossIndex = wave.Groups.FindIndex(IsBossGroup);
        if (firstBossIndex <= 0) return EnemySignalRole.None;

        var restorerIndex = -1;
        for (var index = 0; index < firstBossIndex; index++)
        {
            if (!wave.Groups[index].EnemyId.Contains("regenerator", StringComparison.OrdinalIgnoreCase)) continue;
            restorerIndex = index;
            break;
        }

        var jammerIndex = -1;
        var jammerSearchStart = (firstBossIndex * 2 + 2) / 3;
        for (var index = jammerSearchStart; index < firstBossIndex; index++)
        {
            var enemyId = wave.Groups[index].EnemyId;
            if (enemyId.Contains("aegis", StringComparison.OrdinalIgnoreCase) ||
                enemyId.Contains("regenerator", StringComparison.OrdinalIgnoreCase)) continue;
            jammerIndex = index;
            break;
        }

        if (groupIndex == restorerIndex) return EnemySignalRole.Restorer;
        if (groupIndex == jammerIndex) return EnemySignalRole.Jammer;
        return EnemySignalRole.None;
    }

    private static bool IsBossGroup(WaveGroupDefinition group) =>
        Enum.TryParse<EnemyRank>(group.Rank, true, out var rank) && rank == EnemyRank.Boss;

    private static EnemySignalRole FilterAvailability(EnemySignalRole role, bool supportEnabled, bool attackersEnabled) =>
        role switch
        {
            EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark
                when !supportEnabled => EnemySignalRole.None,
            EnemySignalRole.Jammer or EnemySignalRole.Disruptor when !attackersEnabled => EnemySignalRole.None,
            _ => role
        };
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
