using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Clockwise material boundaries in chassis space: +X is forward, Y is lateral.</summary>
public static class EnemyModelContours
{
    private static readonly string[][] Keys =
    [
        ["crawler", "crawler_elite", "crawler_boss"],
        ["runner", "runner_elite", "runner_boss"],
        ["brute", "brute_elite", "brute_boss"],
        ["aegis", "aegis_elite", "aegis_boss"],
        ["regenerator", "regenerator_elite", "regenerator_boss"]
    ];

    public static IReadOnlyDictionary<string, Vector2[]> Shapes { get; } = new Dictionary<string, Vector2[]>
    {
        ["crawler"] = Mirrored(.70f, 0, .70f, -.40f, .47f, -.53f,
            -.12f, -.48f, -.54f, -.31f, -.64f, -.19f, -.64f, 0),
        ["crawler_elite"] = Mirrored(.76f, 0, .76f, -.43f, .48f, -.57f,
            .12f, -.52f, .12f, -.65f, -.25f, -.65f, -.25f, -.46f,
            -.62f, -.32f, -.70f, -.18f, -.70f, 0),
        ["crawler_boss"] = Mirrored(.80f, 0, .80f, -.43f, .51f, -.59f,
            .13f, -.53f, .13f, -.68f, -.39f, -.68f, -.39f, -.48f,
            -.72f, -.32f, -.79f, -.18f, -.79f, 0),
        ["runner"] = Mirrored(.96f, 0, .12f, -.25f, -.48f, -.70f, -.32f, -.16f, -.65f, 0),
        ["runner_elite"] = Mirrored(.96f, 0, .14f, -.40f, -.28f, -.76f, -.58f, -.76f,
            -.52f, -.32f, -.30f, 0),
        ["runner_boss"] = Mirrored(.96f, 0, .35f, -.27f, .35f, -.40f, -.18f, -.58f,
            -.35f, -.80f, -.61f, -.70f, -.55f, -.30f, -.36f, 0),
        ["brute"] = Mirrored(.74f, 0, .37f, -.68f, -.37f, -.68f, -.74f, 0),
        ["brute_elite"] = Mirrored(.79f, 0, .41f, -.71f, .28f, -.71f, .28f, -.64f,
            -.28f, -.64f, -.28f, -.71f, -.41f, -.71f, -.79f, 0),
        ["brute_boss"] = Mirrored(.84f, 0, .44f, -.75f, .29f, -.75f, .29f, -.67f,
            -.29f, -.67f, -.29f, -.75f, -.44f, -.75f, -.84f, 0),
        ["aegis"] = Mirrored(.75f, 0, .75f, -.28f, .34f, -.62f, -.34f, -.62f, -.75f, -.28f, -.75f, 0),
        ["aegis_elite"] = Mirrored(.79f, 0, .79f, -.30f, .36f, -.64f, -.36f, -.64f, -.79f, -.30f, -.79f, 0),
        ["aegis_boss"] = Mirrored(.82f, 0, .82f, -.28f, .38f, -.62f, .20f, -.62f,
            .20f, -.57f, -.20f, -.57f, -.20f, -.62f, -.38f, -.62f, -.82f, -.28f, -.82f, 0),
        ["regenerator"] = Mirrored(.85f, 0, .85f, -.28f, .64f, -.45f,
            .48f, -.61f, .32f, -.70f, -.32f, -.70f, -.48f, -.61f,
            -.64f, -.45f, -.85f, -.28f, -.85f, 0),
        ["regenerator_elite"] = Mirrored(.87f, 0, .87f, -.29f, .64f, -.45f,
            .48f, -.61f, .26f, -.695f, -.26f, -.695f, -.48f, -.61f,
            -.64f, -.45f, -.87f, -.29f, -.87f, 0),
        ["regenerator_boss"] = Mirrored(.89f, 0, .89f, -.30f, .64f, -.45f,
            .48f, -.61f, .28f, -.67f, -.28f, -.67f, -.48f, -.61f,
            -.64f, -.45f, -.89f, -.30f, -.89f, 0)
    };

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
        return Keys[family][enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0];
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
