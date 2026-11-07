using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;

namespace MaximalBastion.UI;

public readonly record struct EnemyBreachDisplay(int LivesLost, string Summary);

/// <summary>Read-only enemy details shared by live inspection and wave previews.</summary>
public static class EnemyInfo
{
    public static EnemyBreachDisplay Breach(EnemyInstance enemy) => Breach(enemy.LivesLost);

    public static EnemyBreachDisplay Breach(EnemyDefinition definition, string rank)
    {
        var parsed = Enum.TryParse<EnemyRank>(rank, true, out var value) ? value : EnemyRank.Standard;
        return Breach(EnemyInstance.GetBreachLives(definition, parsed));
    }

    private static EnemyBreachDisplay Breach(int livesLost) =>
        new(livesLost, $"BREACH: LOSE {livesLost} {(livesLost == 1 ? "LIFE" : "LIVES")}");
}
