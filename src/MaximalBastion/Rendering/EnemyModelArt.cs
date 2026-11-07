using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public readonly record struct EnemyLegPose(Vector2 Root, Vector2 Knee, Vector2 Foot, float ContactRadius);

/// <summary>Family-specific armor and distance-driven articulation within the road's supporting deck.</summary>
public static partial class EnemyModelArt
{
    public static float BodyRadius(EnemyInstance enemy)
    {
        var rank = enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0;
        return enemy.Definition.Id switch
        {
            "t1_crawler" => rank == 2 ? 22 : rank == 1 ? 18.5f : 16,
            "t2_runner" => rank == 2 ? 20 : rank == 1 ? 15 : 12,
            "t3_brute" => rank == 2 ? 23.5f : rank == 1 ? 22 : 20.5f,
            "t4_aegis" => rank == 2 ? 32 : rank == 1 ? 31 : 30,
            "t5_regenerator" => rank == 2 ? 36 : rank == 1 ? 35 : 34,
            _ => MathF.Min(26, enemy.Radius)
        };
    }

    public static int HullVertexCount(EnemyInstance enemy) => Contour(enemy).Length;
    public static Vector2 HullVertex(EnemyInstance enemy, int index) => Contour(enemy)[index];
    private static Vector2[] Contour(EnemyInstance enemy) => EnemyModelContours.Shapes[EnemyModelContours.ShapeId(enemy)];

    public static void DrawHull(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 center,
        float radius, float heading, Color color) =>
        p.DrawAuthoredShape(batch, center, radius, EnemyModelContours.ShapeId(enemy), color, heading);

    public static EnemyLegPose LegPose(EnemyInstance enemy, Vector2 center, float heading,
        float travelDistance, int side, int legIndex, EnemyLocomotionPath? movementPath = null) =>
        EnemyLocomotion.Sample(enemy, center, heading, travelDistance, side, legIndex, movementPath).Pose;

    internal static EnemyLegPose DisplaceLegPose(EnemyLegPose grounded, Vector2 bodyDisplacement) =>
        grounded with { Root = grounded.Root + bodyDisplacement, Knee = grounded.Knee + bodyDisplacement * .35f };

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 center,
        float heading, float time, bool reducedEffects, float travelDistance = 0, float hit = 0,
        float overdriveOpening = 1f, EnemyLocomotionPath? movementPath = null, bool atRest = false,
        bool castShadows = true, scoped ReadOnlySpan<EnemyGroundContact> groundContacts = default,
        float overdriveElapsed = float.PositiveInfinity)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        var f = new Chassis(batch, p, enemy, center, heading, time, reducedEffects, hit,
            atRest ? 0 : travelDistance, overdriveOpening, atRest);
        Span<EnemyGroundContact> sampled = stackalloc EnemyGroundContact[8];
        var contactCount = EnemyLocomotion.ContactSlots(enemy).Length;
        if (groundContacts.Length != contactCount)
        {
            SampleGroundContacts(enemy, center, heading, travelDistance, movementPath, atRest, sampled[..contactCount]);
            groundContacts = sampled[..contactCount];
        }
        if (castShadows) DrawGroundShadow(f, groundContacts);
        var exhaustBoss = enemy.IsBoss && enemy.Definition.Id == "t5_regenerator";
        if (exhaustBoss && enemy.BossPhaseActive && !atRest)
            p.BossExhaust(batch, f.Center, f.Radius, heading, time, overdriveElapsed, reducedEffects);
        if (!reducedEffects) p.Glow(batch, f.Center, f.Radius * .65f, f.Energy, .16f);
        DrawLegs(f, groundContacts);
        DrawHull(batch, p, enemy, f.Center, f.Radius, heading, f.Armor);
        DrawArmoredFamily(f);
        DrawRankArmor(f);
        if (enemy.BossPhaseActive) DrawOverdrivePanels(f);
        if (exhaustBoss)
            p.BossExhaustShutters(batch, f.Center, f.Radius, heading,
                enemy.BossPhaseActive ? reducedEffects || atRest ? 1 : overdriveElapsed : -1);
        DrawFacetedCore(f, time);
    }

    internal static void SampleGroundContacts(EnemyInstance enemy, Vector2 center, float heading,
        float travelDistance, EnemyLocomotionPath? movementPath, bool atRest, Span<EnemyGroundContact> contacts)
    {
        var slots = EnemyLocomotion.ContactSlots(enemy);
        for (var index = 0; index < slots.Length; index++)
        {
            var (side, leg) = slots[index];
            contacts[index] = atRest
                ? EnemyLocomotion.RestPose(enemy, center, heading, side, leg)
                : EnemyLocomotion.Sample(enemy, center, heading,
                    travelDistance, side, leg, movementPath);
        }
    }

    private static void DrawLegs(Chassis f, ReadOnlySpan<EnemyGroundContact> contacts)
    {
        foreach (var contact in contacts)
        {
            var pose = DisplaceLegPose(contact.Pose, f.BodyDisplacement);
            var upper = pose.Knee - pose.Root;
            var upperAngle = MathF.Atan2(upper.Y, upper.X);
            var upperCenter = Vector2.Lerp(pose.Root, pose.Knee, .51f);
            var upperWidth = (f.Enemy.Definition.Id == "t1_crawler" ? 4.6f : 5.2f) + f.Rank * .3f;
            f.P.ArmorLine(f.Batch, pose.Root, pose.Knee, ColorPalette.EnemyRecess, upperWidth + .8f);
            f.P.ArmorPlate(f.Batch, upperCenter, upper.Length() * .92f, upperWidth, upperAngle, f.Shell);
            f.P.ArmorCircle(f.Batch, pose.Knee, 1.5f + f.Rank * .15f, ColorPalette.EnemyRecess);
            f.P.ArmorCircle(f.Batch, pose.Knee, .85f, f.Shell);
            var delta = pose.Foot - pose.Knee;
            var length = delta.Length();
            var direction = delta / MathF.Max(.001f, length);
            var angle = MathF.Atan2(delta.Y, delta.X);
            var center = Vector2.Lerp(pose.Knee, pose.Foot, f.Enemy.Definition.Id == "t4_aegis" ? .24f : .32f);
            var armorLength = MathF.Max(3.6f, length * .85f);
            var width = (f.Enemy.Definition.Id == "t1_crawler" ? 4.8f : 5.4f) + f.Rank * .35f;
            f.P.ArmorLine(f.Batch, pose.Knee, pose.Foot, ColorPalette.EnemyRecess, 2.2f);
            f.P.ArmorPlate(f.Batch, center + new Vector2(.1f, .35f), armorLength + .6f, width + .4f,
                angle, ColorPalette.EnemyRecess);
            f.P.ArmorPlate(f.Batch, center, armorLength, width, angle, f.Edge);
            var lampStart = center - direction * armorLength * .16f;
            var lampEnd = center + direction * armorLength * .22f;
            f.P.ArmorLine(f.Batch, lampStart, lampEnd, ColorPalette.EnemyRecess, 1.6f);
            f.P.ArmorLine(f.Batch, lampStart, lampEnd, f.Energy * f.Reactor, .9f);
            f.P.ArmorLine(f.Batch, lampStart, Vector2.Lerp(lampStart, lampEnd, .6f), ColorPalette.EnemyHot * .8f, .4f);
            f.P.ArmorLine(f.Batch, pose.Foot - direction * .9f, pose.Foot + direction * .45f,
                contact.Planted ? f.Shell : f.Armor, pose.ContactRadius * 1.05f);
        }
    }

    public static (float HalfLength, float HalfWidth, float LateralOffset) TrackGeometry(EnemyInstance enemy) =>
        enemy.IsBoss ? (.28f, .08f, .59f) : enemy.IsElite ? (.26f, .105f, .59f) : (.29f, .11f, .59f);

    private static void DrawTracks(Chassis f)
    {
        var track = TrackGeometry(f.Enemy);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var y = sign * track.LateralOffset;
            Pod(-track.HalfLength, y, track.HalfLength, y, ColorPalette.EnemyRecess, track.HalfWidth * 2);
            Pod(-track.HalfLength, y, track.HalfLength, y, ColorPalette.EnemyChassis, track.HalfWidth * 1.5f);
            Rail(-track.HalfLength, y + sign * (track.HalfWidth - .015f),
                track.HalfLength, y + sign * (track.HalfWidth - .015f), f.Shell, .02f);
            for (var tread = 0; tread < 6; tread++)
            {
                var cleat = EnemyLocomotion.TrackCleat(tread, f.TravelDistance, f.Radius, track.HalfLength);
                var x = cleat.Along;
                var fade = cleat.Opacity;
                Rail(x, y - sign * .06f, x, y + sign * (track.HalfWidth - .02f), f.Edge * fade, .037f);
            }
        }

        Vector2 At(float x, float y) => f.GroundCenter + (f.Forward * x + f.Side * y) * f.Radius;
        void Rail(float x1, float y1, float x2, float y2, Color color, float width) =>
            f.P.ArmorLine(f.Batch, At(x1, y1), At(x2, y2), color, width * f.Radius);
        void Pod(float x1, float y1, float x2, float y2, Color color, float width)
        {
            Rail(x1, y1, x2, y2, color, width);
            f.P.ArmorCircle(f.Batch, At(x1, y1), width * f.Radius * .5f, color);
            f.P.ArmorCircle(f.Batch, At(x2, y2), width * f.Radius * .5f, color);
        }
    }

    private static void DrawOverdrivePanels(Chassis f)
    {
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var spread = .28f + f.Opening * .12f;
            Plate(f, new(-.05f, sign * spread), .46f, .10f, 0, f.Edge);
            Window(f, new(-.23f, sign * (spread + .025f)), new(.14f, sign * (spread + .025f)), .035f);
        }
    }

    private readonly struct Chassis
    {
        public readonly SpriteBatch Batch;
        public readonly PrimitiveRenderer P;
        public readonly EnemyInstance Enemy;
        public readonly Vector2 Center, GroundCenter, BodyDisplacement, Forward, Side;
        public readonly float Heading, Radius, Reactor, TravelDistance, Opening;
        public readonly bool Reduced, AtRest;
        public readonly int Rank;
        public readonly Color Armor, Shell, Edge, Energy;

        public Chassis(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 center,
            float heading, float time, bool reduced, float hit, float travelDistance, float overdriveOpening, bool atRest = false)
        {
            Batch = batch;
            P = p;
            Enemy = enemy;
            GroundCenter = center;
            AtRest = atRest;
            BodyDisplacement = atRest ? Vector2.Zero : EnemyStunMotion.WorldOffset(enemy, heading, time, reduced);
            Center = center + BodyDisplacement;
            Heading = heading;
            Radius = BodyRadius(enemy);
            TravelDistance = travelDistance;
            Reduced = reduced;
            Opening = enemy.BossPhaseActive ? reduced ? 1f : Math.Clamp(overdriveOpening, 0, 1) : 0;
            Rank = enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0;
            Forward = new(MathF.Cos(heading), MathF.Sin(heading));
            Side = new(-Forward.Y, Forward.X);
            Armor = Color.Lerp(ColorPalette.EnemyArmor, ColorPalette.Paper, Math.Clamp(hit, 0, 1));
            Shell = Color.Lerp(ColorPalette.EnemyArmor, ColorPalette.EnemyArmorLight, .28f + Rank * .09f + Math.Clamp(hit, 0, 1) * .30f);
            Edge = ColorPalette.EnemyArmorLight;
            Energy = enemy.BossPhaseActive ? Color.Lerp(ColorPalette.EnemyMagenta, ColorPalette.EnemyHot, .18f) : ColorPalette.EnemyMagenta;
            Reactor = reduced ? .90f : enemy.BossPhaseActive
                ? .93f + MathF.Sin(time * 4) * .05f
                : .84f + MathF.Sin(time * 2.8f) * .06f;
        }

        public Vector2 At(float x, float y) => Center + (Forward * x + Side * y) * Radius;
        public void Rail(float x1, float y1, float x2, float y2, Color color, float width) =>
            P.ArmorLine(Batch, At(x1, y1), At(x2, y2), color, width * Radius);
    }
}
