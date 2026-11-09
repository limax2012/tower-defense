using MaximalBastion.Core;

namespace MaximalBastion.Enemies;

public static class EnemyRoster
{
    private static readonly IReadOnlyList<EnemyRank> OrdinaryRanks =
        Array.AsReadOnly(new[] { EnemyRank.Standard, EnemyRank.Elite });
    private static readonly IReadOnlyList<EnemyRank> RegeneratorRanks =
        Array.AsReadOnly(new[] { EnemyRank.Standard, EnemyRank.Elite, EnemyRank.Boss });

    public static bool SupportsRank(string enemyId, EnemyRank rank) =>
        rank is EnemyRank.Standard or EnemyRank.Elite ||
        rank == EnemyRank.Boss && enemyId.Equals("t5_regenerator", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<EnemyRank> RanksFor(string enemyId) =>
        SupportsRank(enemyId, EnemyRank.Boss) ? RegeneratorRanks : OrdinaryRanks;
}
