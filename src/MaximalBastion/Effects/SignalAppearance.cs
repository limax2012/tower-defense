using MaximalBastion.Enemies;

namespace MaximalBastion.Effects;

/// <summary>Presentation ownership for a successful signal transfer or emitter activation.</summary>
public readonly record struct SignalAppearance(EnemySignalRole Role, int SourceEnemyId,
    int TargetEnemyId = 0, int TargetTowerId = 0)
{
    public bool IsKnown => Role != EnemySignalRole.None;
}
