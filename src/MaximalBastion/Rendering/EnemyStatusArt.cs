using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Localized conditions on authored chassis surfaces, with a separate shield stand-off.</summary>
public static class EnemyStatusArt
{
    private static readonly IReadOnlyDictionary<string, Vector2[]> ShieldEnvelopes =
        EnemyModelContours.Shapes.ToDictionary(pair => pair.Key, pair => ConvexEnvelope(pair.Value));

    internal static IReadOnlyList<Vector2> ShieldEnvelope(EnemyInstance enemy) =>
        ShieldEnvelopes[EnemyModelContours.ArmorOutlineId(enemy)];

    private static Vector2[] ConvexEnvelope(Vector2[] contour)
    {
        var points = contour.Distinct().OrderBy(point => point.X).ThenBy(point => point.Y).ToArray();
        var hull = new Vector2[points.Length * 2];
        var count = 0;
        foreach (var point in points)
        {
            while (count >= 2 && Cross(hull[count - 2], hull[count - 1], point) <= 0) count--;
            hull[count++] = point;
        }
        var lowerCount = count;
        for (var index = points.Length - 2; index >= 0; index--)
        {
            var point = points[index];
            while (count > lowerCount && Cross(hull[count - 2], hull[count - 1], point) <= 0) count--;
            hull[count++] = point;
        }
        count--;
        Array.Resize(ref hull, count);
        if (MathF.Sign(Area(hull)) != MathF.Sign(Area(contour))) Array.Reverse(hull);
        var start = 0;
        for (var index = 1; index < count; index++)
            if (Vector2.DistanceSquared(hull[index], contour[0]) < Vector2.DistanceSquared(hull[start], contour[0])) start = index;
        var ordered = new Vector2[count];
        for (var index = 0; index < count; index++) ordered[index] = hull[(index + start) % count];
        return ordered;

        static float Cross(Vector2 origin, Vector2 first, Vector2 second) =>
            (first.X - origin.X) * (second.Y - origin.Y) - (first.Y - origin.Y) * (second.X - origin.X);

        static float Area(Vector2[] points)
        {
            var area = 0f;
            for (var index = 0; index < points.Length; index++)
            {
                var next = points[(index + 1) % points.Length];
                area += points[index].X * next.Y - next.X * points[index].Y;
            }
            return area;
        }
    }

    public static bool IsRegenerating(EnemyInstance enemy) =>
        !enemy.IsDead && !enemy.HasEscaped && enemy.Definition.RegenerationPerSecond > 0 &&
        enemy.Health < enemy.MaxHealth && enemy.DamagePauseTimer <= 0;

    public static bool IsAccelerated(EnemyInstance enemy) =>
        !enemy.IsDead && !enemy.HasEscaped && enemy.FormationSpeedMultiplier > 1.001f && enemy.CurrentSpeed > 0;

    public static float ShieldFraction(EnemyInstance enemy, float shieldCapacity = 0)
    {
        if (enemy.Shield <= 0) return 0;
        var capacity = shieldCapacity > 0 ? shieldCapacity :
            enemy.Definition.Shield + (enemy.IsBoss ? enemy.MaxHealth * .12f : 0);
        if (capacity <= 0) capacity = enemy.MaxHealth * .20f;
        return Math.Clamp(enemy.Shield / MathF.Max(capacity, enemy.Shield), 0, 1);
    }

    internal static void DrawDurability(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        if (enemy.Shield > 0)
            DrawShieldContour(batch, p, enemy, position, radius, heading, time, true, quiet: true);
    }

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 position,
        float radius, float headingAngle, float time, bool reducedEffects, Color? exposureAccent = null)
        => DrawAnimated(batch, p, enemy, position, radius, headingAngle, time,
            new(1, 0, 0, 0), reducedEffects, exposureAccent: exposureAccent);

    internal static void DrawAnimated(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 position,
        float radius, float headingAngle, float time, EnemyVisualFeedback feedback, bool reducedEffects,
        bool richHighlights = false, Color? exposureAccent = null)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        var phase = reducedEffects ? 0 : time + enemy.Id * .173f;
        DrawPhysicalConditions(batch, p, enemy, position, radius, headingAngle, phase,
            reducedEffects, EnemyConditionPresentation.Compose(enemy),
            exposureAccent ?? ColorPalette.Expose, feedback, richHighlights);
        EnemyStunArt.Draw(batch, p, enemy, position, radius, headingAngle, time, reducedEffects);
    }

    internal static uint StrongSurfaceMask(EnemyInstance enemy) =>
        StrongSurfaceMask(enemy, EnemyConditionPresentation.Compose(enemy));

    private static uint StrongSurfaceMask(EnemyInstance enemy, EnemyConditionComposition composition)
    {
        var mask = 0u;
        var count = 0;
        Add(EnemyCondition.Stun);
        Add(EnemyCondition.Slow);
        Add(EnemyCondition.Burn);
        Add(EnemyCondition.ArmorBreak);
        Add(EnemyCondition.Exposed);
        if (enemy.CurrentSpeed > 0) Add(EnemyCondition.Acceleration);
        if (composition.Has(EnemyCondition.Shield)) mask |= 1u << (int)EnemyCondition.Shield;
        return mask;

        void Add(EnemyCondition condition)
        {
            if (count >= 2 || !composition.Has(condition)) return;
            mask |= 1u << (int)condition;
            count++;
        }
    }

    private static bool HasSurfaceCue(uint mask, EnemyCondition condition) => (mask & 1u << (int)condition) != 0;

    internal static float HeatPlateWidth(float radius) => Math.Clamp(radius * .14f, 1.8f, 3.6f);

    private static void DrawPhysicalConditions(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float phase, bool reduced,
        EnemyConditionComposition composition, Color exposureAccent, EnemyVisualFeedback feedback, bool richHighlights)
    {
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var cues = composition.ActiveMask;
        var emphasized = StrongSurfaceMask(enemy, composition);
        var anchors = EnemySurfaceAnchors.For(enemy);
        Vector2 At(Vector2 local) => position + (forward * local.X + side * local.Y) * radius;
        Vector2 Local(float x, float y) => At(new Vector2(x, y));

        if (enemy.Shield > 0)
            DrawShieldContour(batch, p, enemy, position, radius, heading, phase, reduced);
        DrawPhysicalSurfaces(batch, p, enemy, position, radius, forward, side, phase, reduced, cues, emphasized,
            feedback, richHighlights);

        if (HasSurfaceCue(cues, EnemyCondition.Exposed))
        {
            var weakPoint = At(anchors.WeakPoint);
            var axis = enemy.Definition.Id == "t2_runner" ? Vector2.Normalize(forward - side * .45f) : forward;
            var normal = new Vector2(-axis.Y, axis.X);
            var strength = HasSurfaceCue(emphasized, EnemyCondition.Exposed) ? .86f : .55f;
            var halfLength = Math.Clamp(radius * .16f, 2.4f, 3.8f);
            var scan = reduced ? .10f : MathF.Sin(phase * 1.8f) * .55f;
            var current = reduced ? .88f : .72f + .16f * MathF.Sin(phase * 3.1f) * MathF.Sin(phase * 1.7f + .6f);
            var first = weakPoint - axis * halfLength;
            var last = weakPoint + axis * halfLength;
            p.Line(batch, first, last, ColorPalette.Ink, 2.8f);
            p.Line(batch, first, last, exposureAccent * (strength * current), 1.6f);
            p.Line(batch, first + normal * .8f, last + normal * .8f,
                Color.Lerp(exposureAccent, ColorPalette.Paper, .20f) * strength, .65f);
            var contact = weakPoint + axis * halfLength * scan;
            p.Circle(batch, contact, .75f, Color.Lerp(exposureAccent, ColorPalette.Paper, .36f) * strength);
        }

        if (HasSurfaceCue(cues, EnemyCondition.Acceleration) && enemy.CurrentSpeed > 0)
        {
            var energy = (reduced ? .85f : .78f + MathF.Sin(phase * 3.2f) * .07f) *
                (HasSurfaceCue(emphasized, EnemyCondition.Acceleration) ? 1 : .62f);
            var travel = reduced ? .35f : (phase * .8f) % 1;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var at = Local(anchors.DriveRear, sign * .18f);
                var tail = at - forward * (3.2f + travel * 1.5f);
                p.Line(batch, at, tail, ColorPalette.Cyan * energy, 1.35f);
                p.Line(batch, tail, tail - forward * 1.5f, ColorPalette.Cyan * (energy * .35f), .85f);
            }
        }

    }

    private static void DrawPhysicalSurfaces(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, Vector2 forward, Vector2 side, float phase, bool reducedEffects,
        uint cues, uint emphasized, EnemyVisualFeedback feedback, bool richHighlights)
    {
        var anchors = EnemySurfaceAnchors.For(enemy);
        Vector2 At(Vector2 local) => position + (forward * local.X + side * local.Y) * radius;
        float Strength(EnemyCondition condition) => HasSurfaceCue(emphasized, condition) ? 1 : .62f;
        if (HasSurfaceCue(cues, EnemyCondition.Slow))
            DrawRime(batch, p, enemy, position, radius, forward, side, phase, reducedEffects,
                Strength(EnemyCondition.Slow), feedback.FrostSpread, richHighlights);

        if (HasSurfaceCue(cues, EnemyCondition.ArmorBreak))
        {
            var strength = Strength(EnemyCondition.ArmorBreak);
            var crack = At(anchors.Fracture);
            var length = Math.Clamp(radius * .38f, 4.5f, 8.5f);
            var first = crack - forward * length * .48f - side * 1.3f;
            var bend = crack - forward * .8f + side * 1.2f;
            var last = crack + forward * length * .48f - side * 1.1f;
            p.Line(batch, first, bend, ColorPalette.Ink, 3.7f);
            p.Line(batch, bend, last, ColorPalette.Ink, 3.7f);
            p.Line(batch, first - side * 1.7f, bend - side * 1.7f,
                ColorPalette.ArmorBreak * strength, 1.1f);
            p.Line(batch, bend - side * 1.7f, last - side * 1.7f,
                ColorPalette.ArmorBreak * strength, 1.1f);
            p.Line(batch, first + side * 1.7f, bend + side * 1.7f,
                Color.Lerp(ColorPalette.Metal, ColorPalette.ArmorBreak, .42f) * strength, .9f);
            p.Line(batch, bend + side * 1.7f, last + side * 1.7f,
                Color.Lerp(ColorPalette.Metal, ColorPalette.ArmorBreak, .42f) * strength, .9f);
            p.Line(batch, first + side * 3.2f, last + side * 3.2f,
                ColorPalette.Metal * (.7f * strength), 1.8f);
            p.Line(batch, first + side * 4, first + side * 2,
                ColorPalette.Muted * strength, .7f);
        }

        if (HasSurfaceCue(cues, EnemyCondition.Burn))
            DrawBurningSurfaces(batch, p, enemy, position, radius, forward, side, phase, reducedEffects,
                Strength(EnemyCondition.Burn), feedback.BurnIgnition, feedback.OrnamentScale, richHighlights);
    }

    private static void DrawRime(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, Vector2 forward, Vector2 side, float phase, bool reduced,
        float strength, float spread, bool richHighlights)
    {
        var anchors = EnemySurfaceAnchors.For(enemy);
        Vector2 At(Vector2 local) => position + (forward * local.X + side * local.Y) * radius;
        var pale = Color.Lerp(ColorPalette.Metal, ColorPalette.FrostHighlight, .60f);
        var bed = Color.Lerp(ColorPalette.Metal, ColorPalette.FrostShot, .32f);
        // Rime follows both exposed plate lips; the inset steel and running lights remain visible.
        for (var bank = 0; bank < 2; bank++)
        {
            var sign = bank == 0 ? 1 : -1;
            var localRear = new Vector2(anchors.IceRear.X, anchors.IceRear.Y * sign);
            var localFront = new Vector2(anchors.IceFront.X, anchors.IceFront.Y * sign);
            var rear = At(localRear);
            var front = At(localFront);
            var tangent = Vector2.Normalize(front - rear);
            var outward = side * -sign;
            var heading = MathF.Atan2(tangent.Y, tangent.X);
            for (var grain = 0; grain < 5; grain++)
            {
                var fraction = .08f + grain * .205f;
                var growth = Math.Clamp(spread * 1.6f - grain * .12f - bank * .10f, .16f, 1);
                var irregularity = MathF.Sin(grain * 2.17f + bank * 1.3f + .4f);
                var root = Vector2.Lerp(rear, front, fraction) + outward * (irregularity * .35f);
                var size = Math.Clamp(radius * .055f, .70f, 1.45f) * (1 + irregularity * .15f) * growth;
                p.Line(batch, root - outward * (1.4f + size), root + outward * .15f,
                    bed * (.70f * strength), size * 2.6f);
                p.DrawPolygon(batch, root, size, 5, false,
                    pale * (.91f * strength), heading + grain * .73f);
                p.Line(batch, root - tangent * size * .6f, root + tangent * size * .5f,
                    ColorPalette.FrostHighlight * (.75f * strength), .6f);
                if (grain % 2 == bank)
                {
                    var bolt = root - outward * (1.2f + size);
                    p.Circle(batch, bolt, size * .58f, pale * (.80f * strength));
                    p.Circle(batch, bolt, .3f, ColorPalette.Metal * strength);
                }
            }
            if (richHighlights && !reduced)
            {
                var patch = Vector2.Lerp(rear, front, .35f) - outward * 1.6f;
                p.Glow(batch, patch, 4.3f, ColorPalette.FrostHighlight, .035f * strength * spread);
            }
        }
    }

    private static void DrawBurningSurfaces(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, Vector2 forward, Vector2 side, float phase, bool reduced,
        float strength, float ignition, float ornamentScale, bool richHighlights)
    {
        var anchors = EnemySurfaceAnchors.For(enemy);
        Vector2 At(Vector2 local) => position + (forward * local.X + side * local.Y) * radius;
        var vent = At(anchors.Vent);
        var heatEnds = EnemySurfaceAnchors.HeatEnds(enemy);
        var plateWidth = HeatPlateWidth(radius);
        // Two connected seams radiate heat from the recessed service bridge onto adjacent plate faces.
        for (var seam = 0; seam < 2; seam++)
        {
            var sign = seam == 0 ? -1 : 1;
            var end = At(seam == 0 ? heatEnds.First : heatEnds.Last);
            var bend = Vector2.Lerp(vent, end, .58f);
            var activity = reduced ? .5f : .5f + .5f * MathF.Sin(phase * (5.3f + seam * 1.8f) + seam * 2.2f);
            var hot = Color.Lerp(ColorPalette.Burn, ColorPalette.EmberFlame, .28f + activity * .24f);
            var plateHeat = Color.Lerp(ColorPalette.Metal, ColorPalette.Burn, .45f);
            p.Line(batch, vent, bend, plateHeat * (.74f * strength), plateWidth);
            p.Line(batch, bend, end, plateHeat * (.74f * strength), plateWidth);
            p.Line(batch, vent, bend, ColorPalette.Ink, plateWidth * .66f);
            p.Line(batch, bend, end, ColorPalette.Ink, plateWidth * .66f);
            p.Line(batch, vent, bend, hot * (.86f * strength), MathF.Min(1.25f, plateWidth * .42f));
            p.Line(batch, bend, end, hot * (.86f * strength), MathF.Min(1.25f, plateWidth * .42f));
            if (!reduced)
            {
                var spill = richHighlights ? .15f : .065f;
                p.Glow(batch, bend, (richHighlights ? 5.4f : 3.5f) + activity,
                    ColorPalette.Burn, (spill + ignition * .055f) * strength * ornamentScale);
            }
            var tongues = reduced ? 1 : 2;
            for (var tongue = 0; tongue < tongues; tongue++)
            {
                var cycle = reduced ? .36f : (phase * (1.45f + seam * .23f) + seam * .37f + tongue * .51f) % 1;
                var fold = MathF.Sin(cycle * MathHelper.Pi);
                var origin = Vector2.Lerp(vent, end, .26f + tongue * .46f);
                var length = .7f + fold * (richHighlights ? 3.6f : 2.6f) * ornamentScale;
                var direction = Vector2.Normalize(-forward * .7f + side * sign * .35f - Vector2.UnitY * .55f);
                var curl = side * sign * MathF.Sin(cycle * MathHelper.TwoPi) * .85f;
                var mid = origin + direction * length * .55f + curl;
                var tip = origin + direction * length - curl * .4f;
                p.Line(batch, origin, mid, ColorPalette.Burn * (.90f * strength), 1.7f);
                p.Line(batch, mid, tip, ColorPalette.EmberFlame * (.90f * strength), .85f);
                p.Line(batch, origin, Vector2.Lerp(origin, mid, .65f), ColorPalette.EmberCore * strength, .65f);
            }
        }
    }

    private static void DrawShieldContour(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float headingAngle, float phase, bool reducedEffects, bool quiet = false)
    {
        var envelope = ShieldEnvelope(enemy);
        var sides = envelope.Count;
        var forward = new Vector2(MathF.Cos(headingAngle), MathF.Sin(headingAngle));
        var side = new Vector2(-forward.Y, forward.X);
        var outlineRadius = radius + 3.5f;
        Vector2 Vertex(int index)
        {
            var point = envelope[index % sides];
            return position + (forward * point.X + side * point.Y) * outlineRadius;
        }
        var shimmer = quiet ? .68f : reducedEffects ? .88f : .84f + MathF.Sin(phase * 2.2f) * .04f;
        for (var edge = 0; edge < sides; edge++)
        {
            var first = Vertex(edge);
            var last = Vertex(edge + 1);
            p.Line(batch, first, last, ColorPalette.Ink * .72f, quiet ? 2.3f : 2.9f);
            p.Line(batch, first, last, ColorPalette.Shield * shimmer, quiet ? 1f : 1.35f);
            if (quiet) continue;
            var scan = reducedEffects ? .32f : .18f + (.5f + .5f * MathF.Sin(phase * 1.1f + edge)) * .50f;
            p.Line(batch, Vector2.Lerp(first, last, scan), Vector2.Lerp(first, last, MathF.Min(.96f, scan + .14f)),
                ColorPalette.Paper * .42f, .6f);
        }
    }

    public static void DrawRepairPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float headingAngle, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || progress <= 0) return;
        var contour = EnemyModelContours.Shapes[EnemyModelContours.ShapeId(enemy)];
        var edgeStart = Vector2.Zero;
        var edgeEnd = Vector2.Zero;
        var bestScore = 0f;
        for (var edge = 0; edge < contour.Length; edge++)
        {
            var first = contour[edge];
            var last = contour[(edge + 1) % contour.Length];
            var lateral = (first.Y + last.Y) * .5f;
            if (lateral <= 0) continue;
            var score = Vector2.DistanceSquared(first, last) * lateral * lateral;
            if (score <= bestScore) continue;
            bestScore = score;
            edgeStart = Vector2.Lerp(first, last, .12f);
            edgeEnd = Vector2.Lerp(first, last, .88f);
        }
        var forward = new Vector2(MathF.Cos(headingAngle), MathF.Sin(headingAngle));
        var side = new Vector2(-forward.Y, forward.X);
        var fade = MathF.Pow(MathHelper.Clamp(progress, 0, 1), 1.35f);
        var hot = Color.Lerp(ColorPalette.Green, ColorPalette.Paper, .35f);
        // Opposing plate edges acknowledge the same instantaneous health grant.
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var first = position + (forward * edgeStart.X + side * (edgeStart.Y * sign)) * radius;
            var last = position + (forward * edgeEnd.X + side * (edgeEnd.Y * sign)) * radius;
            if (!reducedEffects)
                p.Line(batch, first, last, ColorPalette.Green * (fade * .08f), 4);
            p.Line(batch, first, last, ColorPalette.Green * (fade * (reducedEffects ? .62f : .82f)),
                reducedEffects ? 1 : 1.35f);
            if (!reducedEffects)
                p.Line(batch, Vector2.Lerp(first, last, .28f), Vector2.Lerp(first, last, .72f),
                    hot * (fade * .55f), .65f);
        }
    }

    public static void DrawShieldPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float headingAngle, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.Shield <= 0 || progress <= 0) return;
        var fade = MathF.Pow(MathHelper.Clamp(progress, 0, 1), 1.35f);
        var envelope = ShieldEnvelope(enemy);
        var sides = envelope.Count;
        var forward = new Vector2(MathF.Cos(headingAngle), MathF.Sin(headingAngle));
        var side = new Vector2(-forward.Y, forward.X);
        var hot = Color.Lerp(ColorPalette.Shield, ColorPalette.Paper, .5f);
        for (var edge = 0; edge < sides; edge++)
        {
            var firstLocal = envelope[edge];
            var nextLocal = envelope[(edge + 1) % sides];
            var first = position + (forward * firstLocal.X + side * firstLocal.Y) * (radius + 3.5f);
            var next = position + (forward * nextLocal.X + side * nextLocal.Y) * (radius + 3.5f);
            if (!reducedEffects)
                p.Line(batch, first, next, ColorPalette.Shield * (fade * .09f), 4);
            p.Line(batch, first, next, ColorPalette.Shield * (fade * (reducedEffects ? .6f : .82f)),
                reducedEffects ? 1 : 1.5f);
            if (!reducedEffects)
                p.Line(batch, first, next, hot * (fade * .5f), .65f);
        }
    }
}
