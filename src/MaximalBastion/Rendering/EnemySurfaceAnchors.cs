using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

internal readonly record struct EnemySurfaceAnchorSet(
    Vector2 Vent, Vector2 IceRear, Vector2 IceFront, Vector2 Fracture,
    Vector2 ElectricalContact, Vector2 WeakPoint, Vector2 RepairStart, Vector2 RepairEnd,
    float DriveRear);

/// <summary>Condition attachment points on physical plates; +X follows the nose and Y crosses the hull.</summary>
internal static class EnemySurfaceAnchors
{
    internal static EnemySurfaceAnchorSet For(EnemyInstance enemy) => enemy.Definition.Id switch
    {
        "t2_runner" => new(new(-.24f, .40f), new(-.42f, -.47f), new(-.10f, -.36f), new(-.24f, -.39f),
            new(.30f, .18f), new(.20f, .28f), new(-.18f, .30f), new(.18f, .16f), -.72f),
        "t3_brute" => new(new(-.50f, 0), new(-.29f, -.56f), new(.31f, -.56f), new(.04f, -.40f),
            new(.20f, .28f), new(.28f, .30f), new(.12f, -.38f), new(.42f, -.38f), -.70f),
        "t4_aegis" => new(new(-.65f, 0), new(-.35f, -.48f), new(.38f, -.48f), new(.05f, -.38f),
            new(.20f, .41f), new(.26f, .43f), new(.13f, .43f), new(.54f, .43f), -.88f),
        "t5_regenerator" => new(new(-.30f, -.39f), new(-.52f, -.47f), new(.53f, -.47f), new(.05f, -.37f),
            new(.18f, .39f), new(.42f, .39f), new(.12f, -.36f), new(.64f, -.36f), -.95f),
        _ => new(new(-.50f, 0), new(-.17f, -.46f), new(.35f, -.48f), new(.02f, -.28f),
            new(.22f, .23f), new(.30f, .20f), new(.10f, -.17f), new(.35f, .16f), -.70f)
    };

    internal static (Vector2 First, Vector2 Last) HeatEnds(EnemyInstance enemy) => enemy.Definition.Id switch
    {
        "t2_runner" => (new(-.34f, .46f), new(-.10f, .28f)),
        "t3_brute" => (new(-.43f, -.21f), new(-.43f, .21f)),
        "t4_aegis" => (new(-.59f, -.18f), new(-.59f, .18f)),
        "t5_regenerator" => (new(-.53f, -.40f), new(-.12f, -.30f)),
        _ => (new(-.43f, -.15f), new(-.43f, .15f))
    };
}
