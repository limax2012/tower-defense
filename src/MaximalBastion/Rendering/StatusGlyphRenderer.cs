using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public static class StatusGlyphRenderer
{
    public static void DrawArmorBreak(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius)
    {
        var offset = radius + 7;
        const float halfHeight = 5;
        const float tooth = 4;
        var left = center - new Vector2(offset, 0);
        var right = center + new Vector2(offset, 0);
        primitives.Line(batch, left + new Vector2(tooth, -halfHeight), left, ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, left, left + new Vector2(tooth, halfHeight), ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, right - new Vector2(tooth, halfHeight), right, ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, right, right - new Vector2(tooth, -halfHeight), ColorPalette.ArmorBreak, 2.5f);
    }

    public static void DrawStun(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius,
        float time, bool reducedEffects = false, float heading = 0)
    {
        var phase = reducedEffects ? 0 : time * 12;
        var frame = (int)MathF.Floor(phase);
        var discharge = 1 - (phase - frame);
        var direction = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var normal = new Vector2(-direction.Y, direction.X);
        var strength = reducedEffects ? .8f : .64f + discharge * (.2f + Noise(frame, 2) * .16f);
        var width = Math.Clamp(radius * .075f, .9f, 1.4f);
        var angle = Noise(frame, 1) * MathHelper.TwoPi;
        var axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var cross = new Vector2(-axis.Y, axis.X);
        var origin = new Vector2((Noise(frame, 9) - .5f) * .4f, (Noise(frame, 10) - .5f) * .44f);
        var halfLength = .16f + Noise(frame, 11) * .44f;
        var count = 3 + Math.Min(2, (int)(Noise(frame, 12) * 3));

        // Each discharge changes its span and direction inside a shared inset hull envelope.
        Span<Vector2> points = stackalloc Vector2[5];
        for (var i = 0; i < count; i++)
        {
            var along = (i / (float)(count - 1) * 2 - 1) * halfLength;
            var across = (Noise(frame, i + 3) - .5f) * halfLength * .65f;
            points[i] = Inset(origin + axis * along + cross * across);
        }
        if (!reducedEffects)
            primitives.Glow(batch, World(Inset(origin)), radius * (.24f + halfLength * .35f), ColorPalette.Stun, .28f * strength);
        for (var i = 1; i < count; i++)
            Filament(World(points[i - 1]), World(points[i]), 1);

        var junction = points[1 + Math.Min(count - 3, (int)(Noise(frame, 13) * (count - 2)))];
        var branchAngle = angle + (.55f + Noise(frame, 14) * 1.7f) * (Noise(frame, 8) < .5f ? -1 : 1);
        var branch = new Vector2(MathF.Cos(branchAngle), MathF.Sin(branchAngle));
        var branchLength = .09f + Noise(frame, 15) * .29f;
        var fork = Inset(junction + branch * branchLength * .5f + axis * .06f);
        var contact = Inset(junction + branch * branchLength);
        Filament(World(junction), World(fork), .65f);
        Filament(World(fork), World(contact), .5f);

        Vector2 World(Vector2 point) => center + (direction * point.X + normal * point.Y) * radius;

        static Vector2 Inset(Vector2 point)
        {
            // The envelope fits runner triangles as well as crawler and heavy armor.
            var limit = MathF.Max(1, MathF.Max(-point.X / .4f,
                MathF.Max((MathF.Abs(point.X) + MathF.Abs(point.Y)) / .68f,
                    (point.X + 1.732051f * MathF.Abs(point.Y)) / .82f)));
            return point / limit;
        }

        void Filament(Vector2 from, Vector2 to, float intensity)
        {
            var alpha = strength * intensity;
            if (!reducedEffects)
                primitives.Line(batch, from, to, ColorPalette.Stun * (.18f * alpha), width * 2.6f);
            primitives.Line(batch, from, to, ColorPalette.Stun * alpha, width);
            if (!reducedEffects)
                primitives.Line(batch, from, to, ColorPalette.Paper * (.78f * alpha), width * .4f);
        }
    }

    private static float Noise(int frame, int salt)
    {
        var hash = unchecked((uint)frame * 747796405u + (uint)salt * 2891336453u);
        hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
        return ((hash >> 22) ^ hash) / (float)uint.MaxValue;
    }
}
