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
