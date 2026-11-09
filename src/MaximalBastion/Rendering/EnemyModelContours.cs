using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Clockwise material boundaries in chassis space: +X is forward, Y is lateral.</summary>
public static class EnemyModelContours
{
    private static readonly string[][] Keys =
    [
        ["crawler", "crawler_elite"],
        ["runner", "runner_elite"],
        ["brute", "brute_elite"],
        ["aegis", "aegis_elite"],
        ["regenerator", "regenerator_elite", "regenerator_boss"]
    ];

    public static IReadOnlyDictionary<string, Vector2[]> Shapes { get; } = new Dictionary<string, Vector2[]>
    {
        ["crawler"] = Mirrored(.70f, 0, .70f, -.40f, .47f, -.53f,
            -.12f, -.48f, -.54f, -.31f, -.64f, -.19f, -.64f, 0),
        ["crawler_elite"] = Mirrored(.78f, 0, .78f, -.38f, .49f, -.68f,
            .18f, -.66f, -.20f, -.49f, -.64f, -.30f, -.75f, -.15f, -.75f, 0),
        ["runner"] = Mirrored(.96f, 0, .12f, -.25f, -.48f, -.70f, -.32f, -.16f, -.65f, 0),
        ["runner_elite"] = Mirrored(.96f, 0, .14f, -.40f, -.28f, -.76f, -.58f, -.76f,
            -.52f, -.32f, -.30f, 0),
        ["brute"] = Mirrored(.74f, 0, .37f, -.68f, -.37f, -.68f, -.74f, 0),
        ["brute_elite"] = Mirrored(.84f, 0, .42f, -.75f, -.42f, -.75f, -.84f, 0),
        ["aegis"] = Mirrored(.75f, 0, .75f, -.28f, .34f, -.62f, -.34f, -.62f, -.75f, -.28f, -.75f, 0),
        ["aegis_elite"] = Mirrored(.82f, 0, .82f, -.32f, .38f, -.74f, -.38f, -.74f, -.82f, -.32f, -.82f, 0)
            .Select(vertex => vertex * EnemyModelArt.EliteAegisBodyScale).ToArray(),
        ["regenerator"] = Mirrored(.85f, 0, .85f, -.28f, .64f, -.45f,
            .48f, -.61f, .32f, -.70f, -.32f, -.70f, -.48f, -.61f,
            -.64f, -.45f, -.85f, -.28f, -.85f, 0),
        ["regenerator_elite"] = Mirrored(.94f, 0, .94f, -.28f, .53f, -.70f,
            -.53f, -.70f, -.94f, -.28f, -.94f, 0),
        ["regenerator_boss"] = Mirrored(1.03f, 0, 1.03f, -.28f, .62f, -.70f,
            -.62f, -.70f, -1.03f, -.28f, -1.03f, 0),
        ["regenerator_boss_armor"] = Mirrored(1.10f, 0, 1.10f, -.285f, .99f, -.40f,
            .64f, -.80f, .565f, -.8375f, -.565f, -.8375f, -.64f, -.80f,
            -.99f, -.40f, -1.10f, -.285f, -1.10f, 0)
    };

    public static string ArmorOutlineId(EnemyInstance enemy) =>
        enemy.IsBoss && enemy.Definition.Id == "t5_regenerator" ? "regenerator_boss_armor" : ShapeId(enemy);

    public static string ShapeId(EnemyInstance enemy)
    {
        var family = enemy.Definition.Id switch
        {
            "t2_runner" => 1,
            "t3_brute" => 2,
            "t4_aegis" => 3,
            "t5_regenerator" => 4,
            _ => 0
        };
        return Keys[family][EnemyModelArt.VisualRank(enemy)];
    }

    private static Vector2[] Mirrored(params float[] upper)
    {
        var count = upper.Length / 2;
        var vertices = new Vector2[count * 2 - 2];
        for (var index = 0; index < count; index++) vertices[index] = new(upper[index * 2], upper[index * 2 + 1]);
        for (var index = 1; index < count - 1; index++)
            vertices[count - 1 + index] = new(upper[(count - 1 - index) * 2], -upper[(count - 1 - index) * 2 + 1]);
        Array.Reverse(vertices);
        return vertices;
    }
}
