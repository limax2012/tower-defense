using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Short, branching surface discharges driven only by active Stun and presentation time.</summary>
internal static class EnemyStunArt
{
    internal const int MaximumSprites = 192;
    private readonly record struct Filament(Vector2 Start, Vector2 End, float Energy, bool Trunk, float Taper);
    private readonly record struct Contact(Vector2 Position, float Energy);

    internal static void Draw(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float time, bool reducedEffects)
    {
        var remaining = EnemyStunMotion.RemainingSeconds(enemy);
        if (remaining <= 0) return;
        var release = MathHelper.SmoothStep(0, 1, Math.Clamp(remaining / .045f, 0, 1));
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var center = position + EnemyStunMotion.WorldOffset(enemy, heading, time, reducedEffects);
        var contour = EnemyModelContours.Shapes[EnemyModelContours.ShapeId(enemy)];
        Span<Filament> filaments = stackalloc Filament[72];
        Span<Contact> contacts = stackalloc Contact[8];
        Span<Vector2> path = stackalloc Vector2[9];
        var filamentCount = 0;
        var contactCount = 0;
        var routes = reducedEffects ? 2 : enemy.Definition.Id is "t2_runner" or "t1_crawler" ? 3 : 4;
        for (var route = 0; route < routes; route++)
        {
            var clock = reducedEffects ? .34f : time * (12.4f + route * .83f) + route * .271f + enemy.Id * .618034f;
            var serial = (int)MathF.Floor(clock);
            var age = clock - serial;
            var seed = unchecked((uint)(enemy.Id * 92821 + route * 68917 + serial * 19349663));
            var energy = release * (reducedEffects ? .62f :
                MathHelper.SmoothStep(0, 1, Math.Clamp(age / .075f, 0, 1)) * MathF.Pow(1 - age, .62f));
            if (energy < .015f) continue;
            var ends = Endpoints(enemy, route, seed);
            var startAnchor = Vector2.Lerp(ends.Start, ends.End, Unit(seed + 43) * .24f);
            var endAnchor = Vector2.Lerp(ends.Start, ends.End, .78f + Unit(seed + 59) * .22f);
            if ((Hash(seed + 67) & 1) == 0) (startAnchor, endAnchor) = (endAnchor, startAnchor);
            path[0] = Constrain(startAnchor + Jitter(seed + 17) * .065f, startAnchor, contour, enemy);
            path[8] = Constrain(endAnchor + Jitter(seed + 31) * .065f, endAnchor, contour, enemy);
            var axis = path[8] - path[0];
            var length = axis.Length();
            var tangent = axis / MathF.Max(.001f, length);
            var normal = new Vector2(-tangent.Y, tangent.X);
            // Successive subdivisions create coarse bends, fine kinks, and unequal segment lengths.
            for (var stride = 8; stride >= 2; stride /= 2)
            for (var start = 0; start < 8; start += stride)
            {
                var middle = start + stride / 2;
                var reference = Vector2.Lerp(path[start], path[start + stride], .5f);
                if (reference.LengthSquared() < .08f)
                    reference += normal * (.30f * ((seed & 1) == 0 ? -1 : 1));
                var amplitude = MathF.Min(.14f, length * .15f) * MathF.Pow(stride / 8f, .74f);
                var kink = normal * Signed(seed + (uint)(middle * 371 + stride * 91)) * amplitude +
                    tangent * Signed(seed + (uint)(middle * 83 + stride * 457)) * amplitude * .35f;
                path[middle] = Constrain(reference + kink, reference, contour, enemy);
            }
            var head = Math.Clamp(age * 4.8f, 0, 1);
            for (var index = 0; index < 8; index++)
            {
                var along = (index + .5f) / 8;
                var reached = reducedEffects ? 1 : Math.Clamp((head - along) * 12 + 1, 0, 1);
                var hotFront = reducedEffects ? 1 : .70f + .30f * MathF.Exp(-MathF.Abs(along - head) * 12);
                filaments[filamentCount++] = new(path[index], path[index + 1], energy * reached * hotFront, true, 1);
            }
            contacts[contactCount++] = new(path[0], energy * .7f);
            contacts[contactCount++] = new(path[Math.Clamp((int)(head * 8), 0, 8)], energy);
            var forks = reducedEffects ? 1 : 2;
            for (var fork = 0; fork < forks; fork++)
            {
                var joint = 2 + (int)(Hash(seed + (uint)(fork * 883 + 19)) % 5);
                var start = path[joint];
                var sign = fork == 0 ? -1 : 1;
                var reach = (.08f + Unit(seed + (uint)(fork * 61 + 73)) * .15f);
                var aim = normal * sign + tangent * Signed(seed + (uint)(fork * 29 + 127)) * .65f;
                var previous = start;
                for (var twig = 1; twig <= 3; twig++)
                {
                    var fraction = twig / 3f;
                    var next = Constrain(start + aim * (reach * fraction) +
                        Jitter(seed + (uint)(fork * 1103 + twig * 83)) * (.035f * MathF.Sin(fraction * MathHelper.Pi)),
                        start, contour, enemy);
                    filaments[filamentCount++] = new(previous, next, energy * (.83f - fraction * .35f), false, 1 - fraction * .4f);
                    if (!reducedEffects && fork == 0 && twig == 2)
                    {
                        var tip = Constrain(next + tangent * .07f - normal * sign * .04f, next, contour, enemy);
                        var bend = Constrain(Vector2.Lerp(next, tip, .4f) + normal * .025f, next, contour, enemy);
                        filaments[filamentCount++] = new(next, bend, energy * .48f, false, .55f);
                        filaments[filamentCount++] = new(bend, tip, energy * .27f, false, .35f);
                    }
                    previous = next;
                }
            }
            if (!reducedEffects)
            for (var spark = 0; spark < 2; spark++)
            {
                var source = path[2 + (int)(Hash(seed + (uint)(spark * 37 + 239)) % 5)];
                var direction = Jitter(seed + (uint)(spark * 919 + 701));
                var travel = (1.2f + age * 3) / radius;
                var start = Constrain(source + direction * travel, source, contour, enemy);
                var end = Constrain(start + direction * (1.3f / radius), source, contour, enemy);
                filaments[filamentCount++] = new(start, end, energy * .85f, false, .65f);
            }
        }

        Vector2 World(Vector2 local) => center + (forward * local.X + side * local.Y) * radius;
        // Keep the soft contact lights together, then submit all fine lines from the shared pixel texture.
        if (!reducedEffects)
        for (var index = 0; index < contactCount; index++)
            p.Glow(batch, World(contacts[index].Position), 4.5f + radius * .09f,
                ColorPalette.ArcShot, contacts[index].Energy * .48f);
        for (var index = 0; index < filamentCount; index++)
        {
            var filament = filaments[index];
            if (filament.Energy < .015f) continue;
            var first = World(filament.Start);
            var last = World(filament.End);
            var radiance = reducedEffects ? filament.Energy : MathF.Sqrt(filament.Energy) * release;
            if (filament.Trunk && !reducedEffects)
                p.Line(batch, first, last, ColorPalette.ArcShot * (radiance * .18f), 2.25f);
            p.Line(batch, first, last, ColorPalette.ArcShot * radiance,
                (filament.Trunk ? 1.10f : .76f) * filament.Taper);
            p.Line(batch, first, last, ColorPalette.ArcHot * (radiance * .98f),
                (filament.Trunk ? .52f : .36f) * filament.Taper);
        }
    }

    private static (Vector2 Start, Vector2 End) Endpoints(EnemyInstance enemy, int route, uint seed)
    {
        var sign = (route & 1) == 0 ? -1 : 1;
        return enemy.Definition.Id switch
        {
            "t2_runner" => route < 2
                ? (new(-.39f, sign * .41f), new(.62f, sign * .10f))
                : (new(.12f, -.20f), new(.56f, .12f)),
            "t1_crawler" => route < 2
                ? (new(-.40f, sign * .23f), new(.48f, sign * .32f))
                : (Hash(seed + 5) % 3 == 0 ? (new(-.30f, -.26f), new(.48f, .28f)) :
                    (new(.48f, -.28f), new(.50f, .31f))),
            "t3_brute" => route < 2
                ? (new(-.42f, sign * .43f), new(.46f, sign * .48f))
                : (Hash(seed + 5) % 3 == 0 ? (new(-.41f, sign * .37f), new(.46f, -sign * .38f)) :
                    (new(sign * .49f, -.39f), new(sign * .49f, .39f))),
            "t4_aegis" => route < 2
                ? (new(-.46f, sign * .44f), new(.47f, sign * .44f))
                : (new(sign * .68f, -.21f), new(sign * .68f, .22f)),
            _ => route < 2
                ? (new(-.62f, sign * .34f), new(.62f, sign * .34f))
                : (Hash(seed + 5) % 3 == 0 ? (new(sign * .30f, -.35f), new(sign * .58f, .34f)) :
                    (new(sign * .75f, -.19f), new(sign * .75f, .19f)))
        };
    }

    private static Vector2 Constrain(Vector2 point, Vector2 anchor, Vector2[] contour, EnemyInstance enemy)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (Inside(point, contour) && (enemy.Definition.Id != "t4_aegis" ||
                    MathF.Abs(point.X) > .56f || MathF.Abs(point.Y) > .30f) &&
                point.LengthSquared() > .052f) return point;
            point = Vector2.Lerp(point, anchor, .5f);
        }
        return anchor;
    }

    private static bool Inside(Vector2 point, Vector2[] contour)
    {
        var inside = false;
        for (var first = 0; first < contour.Length; first++)
        {
            var a = contour[first];
            var b = contour[(first + 1) % contour.Length];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352d;
        value ^= value >> 15;
        value *= 0x846ca68b;
        return value ^ (value >> 16);
    }

    private static float Unit(uint seed) => (Hash(seed) & 0xffff) / 65535f;
    private static float Signed(uint seed) => Unit(seed) * 2 - 1;
    private static Vector2 Jitter(uint seed) => new(Signed(seed), Signed(seed + 7919));
}
