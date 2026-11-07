using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed partial class PrimitiveRenderer
{
    private const int ExhaustRasterScale = 4;
    private const int ExhaustCellWidth = 96;
    private const int ExhaustCellHeight = 48;
    private const int ExhaustAtlasColumns = 8;
    private const int ExhaustFlowFrames = 64;
    private const int ExhaustOnsetFrames = 64;
    private const int ExhaustShutterFrames = 32;
    private const float ExhaustOnsetDuration = .65f;
    private const float ExhaustFrequency = 3.8f;
    private readonly Texture2D _bossExhaustAtlas;

    internal void BossExhaust(SpriteBatch batch, Vector2 center, float radius, float heading,
        float time, float elapsed, bool reduced)
    {
        var clock = reduced ? .75f : float.IsFinite(elapsed) ? elapsed : time;
        var onset = !reduced && float.IsFinite(elapsed) && elapsed < ExhaustOnsetDuration;
        var cycle = (clock - .12f) * ExhaustFrequency;
        cycle -= MathF.Floor(cycle);
        var frame = onset
            ? ExhaustFlowFrames + (int)MathF.Round(Math.Clamp(elapsed / ExhaustOnsetDuration, 0, 1) * (ExhaustOnsetFrames - 1))
            : (int)(cycle * ExhaustFlowFrames) % ExhaustFlowFrames;
        var source = ExhaustSource(frame);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var scale = radius / 36;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var port = center + (forward * -28.2f + side * (sign * 12.8f)) * scale;
            batch.Draw(_bossExhaustAtlas, port, source, Color.White * (reduced ? .75f : 1),
                heading + MathHelper.Pi, new Vector2(4, 24), scale / ExhaustRasterScale, SpriteEffects.None, 0);
        }
    }

    internal void BossExhaustShutters(SpriteBatch batch, Vector2 center, float radius, float heading, float elapsed)
    {
        var frame = ExhaustFlowFrames + ExhaustOnsetFrames +
            (int)MathF.Round(Math.Clamp(elapsed / .32f, 0, 1) * (ExhaustShutterFrames - 1));
        var source = ExhaustSource(frame);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var scale = radius / 36;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var origin = center + (forward * -30 + side * (sign * 12)) * scale;
            batch.Draw(_bossExhaustAtlas, origin, source, Color.White, heading,
                new Vector2(0, 24), scale / ExhaustRasterScale,
                sign < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
        }
    }

    private static Rectangle ExhaustSource(int frame) => new(
        frame % ExhaustAtlasColumns * ExhaustCellWidth, frame / ExhaustAtlasColumns * ExhaustCellHeight,
        ExhaustCellWidth, ExhaustCellHeight);

    private static Texture2D CreateBossExhaustAtlas(GraphicsDevice device)
    {
        const int count = ExhaustFlowFrames + ExhaustOnsetFrames + ExhaustShutterFrames;
        const int width = ExhaustAtlasColumns * ExhaustCellWidth;
        const int height = count / ExhaustAtlasColumns * ExhaustCellHeight;
        var pixels = new Color[width * height];
        for (var frame = 0; frame < count; frame++)
        {
            var source = ExhaustSource(frame);
            var hardware = frame >= ExhaustFlowFrames + ExhaustOnsetFrames;
            var onset = frame >= ExhaustFlowFrames;
            var time = hardware ? (frame - ExhaustFlowFrames - ExhaustOnsetFrames) * .32f / (ExhaustShutterFrames - 1)
                : onset ? (frame - ExhaustFlowFrames) * ExhaustOnsetDuration / (ExhaustOnsetFrames - 1)
                : .12f + frame / (ExhaustFrequency * ExhaustFlowFrames);
            for (var y = 0; y < ExhaustCellHeight; y++)
            for (var x = 0; x < ExhaustCellWidth; x++)
            {
                var local = new Vector2(x + .5f, y + .5f) / ExhaustRasterScale;
                pixels[(source.Y + y) * width + source.X + x] = hardware
                    ? SampleExhaustShutter(local + new Vector2(-30, 6), time)
                    : SampleExhaustFlow(local.X - 1, local.Y - 6, time, onset);
            }
        }
        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    private static Color SampleExhaustFlow(float distance, float lateral, float time, bool onset)
    {
        var result = Vector4.Zero;
        if (distance >= 0 && distance <= 20)
        {
            var u = distance / 20;
            var age = time - .12f - distance / 48;
            var start = onset ? ExhaustEase(age / .09f) : 1;
            var wave = .5f + .5f * MathF.Cos(MathHelper.TwoPi * age * ExhaustFrequency);
            var compression = wave * wave;
            var width = 2.1f * (1 - .88f * MathF.Pow(u, 1.3f));
            var fade = 1 - ExhaustEase((u - .56f) / .44f);
            Band(1.35f, ColorPalette.EnemyMagenta, .065f);
            Band(1.12f, ColorPalette.EnemyMagenta, .12f);
            Band(.93f, ColorPalette.EnemyMagenta, .38f + .17f * compression);
            Band(.52f, ColorPalette.EnemyHot, .22f + .5f * compression);
            Band(.18f, ColorPalette.EnemyExhaustHighlight, .09f + .4f * compression);

            void Band(float scale, Color tint, float opacity)
            {
                var coverage = Math.Clamp(.5f + (width * scale - MathF.Abs(lateral)) * ExhaustRasterScale, 0, 1);
                ExhaustComposite(ref result, tint, coverage * opacity * start * fade);
            }
        }
        var charge = onset ? ExhaustEase((time - .12f) / .12f) : 1;
        var glowRadius = new Vector2((distance - .5f) / 3.2f, lateral / 1.9f).Length();
        ExhaustComposite(ref result, ColorPalette.EnemyHot, Math.Clamp(1 - glowRadius, 0, 1) * .23f * charge);
        var lineDistance = ExhaustSegmentDistance(new(distance, lateral), new(-.2f, 0), new(2.7f, 0));
        ExhaustComposite(ref result, ColorPalette.EnemyHot,
            Math.Clamp(.5f + (.475f - lineDistance) * ExhaustRasterScale, 0, 1) * .75f * charge);
        return new Color(result);
    }

    private static Color SampleExhaustShutter(Vector2 at, float time)
    {
        var result = Vector4.Zero;
        var deployed = ExhaustEase((time - .025f) / .28f);
        var heat = ExhaustEase((time - .09f) / .16f);
        Polygon([new(-29,10.4f),new(-26.5f,9.3f),new(-18,10),new(-15.7f,12),
            new(-17.7f,15.8f),new(-26.4f,16),new(-29,14.8f)], ColorPalette.EnemyVentHousing);
        Polygon([new(-28.4f,10.8f),new(-24.8f,10.4f),new(-18.7f,11.3f),new(-18.7f,14.2f),
            new(-27.7f,15)], ColorPalette.EnemyVentRecess);
        Stroke(new(-28.55f,10.8f),new(-28.55f,14.8f),ColorPalette.EnemyVentEdge,.80f,.7f);
        Stroke(new(-28.2f,11.1f),new(-28.2f,14.5f),ColorPalette.EnemyHot,heat,1.15f);
        for (var n = 0; n < 3; n++)
        {
            var x = -25.4f + n * 2.5f;
            Stroke(new(x,11.3f),new(x+.45f,14.3f),ColorPalette.EnemyMagenta,.63f*heat,1);
        }
        var lift = 2.15f * deployed;
        var slide = .65f * deployed;
        var offset = new Vector2(slide, -lift);
        Polygon([new(-24.8f,11.4f),new(-21.3f,9.8f),new(-15.9f,11.3f),new(-17.6f,13.6f),new(-23,13.8f)],
            ColorPalette.EnemyVentHousing,offset);
        Polygon([new(-25,10.9f),new(-21.4f,9.3f),new(-15.9f,10.8f),new(-17.6f,13.1f),new(-23.1f,13.3f)],
            ColorPalette.EnemyVentArmor,offset);
        Stroke(new Vector2(-24.6f,10.9f)+offset,new Vector2(-21.3f,9.6f)+offset,ColorPalette.EnemyVentEdge,.85f,.6f);
        Stroke(new Vector2(-21.3f,9.6f)+offset,new Vector2(-16.6f,10.8f)+offset,ColorPalette.EnemyVentEdge,.85f,.6f);
        Stroke(new(-18,13.3f),new Vector2(-18,12.9f)+offset,ColorPalette.EnemyVentEdge,.85f,.75f);
        return new Color(result);

        void Stroke(Vector2 a, Vector2 b, Color color, float opacity, float width) =>
            ExhaustComposite(ref result, color, opacity * Math.Clamp(
                .5f + (width * .5f - ExhaustSegmentDistance(at,a,b)) * ExhaustRasterScale,0,1));

        void Polygon(ReadOnlySpan<Vector2> vertices, Color color, Vector2 shift = default)
        {
            var point = at - shift;
            var inside = false;
            var distance = float.PositiveInfinity;
            var previous = vertices[^1];
            foreach (var current in vertices)
            {
                if ((current.Y > point.Y) != (previous.Y > point.Y) &&
                    point.X < (previous.X-current.X)*(point.Y-current.Y)/(previous.Y-current.Y)+current.X)
                    inside = !inside;
                distance = MathF.Min(distance,ExhaustSegmentDistance(point,previous,current));
                previous = current;
            }
            ExhaustComposite(ref result,color,Math.Clamp(.5f + (inside ? distance : -distance)*ExhaustRasterScale,0,1));
        }
    }

    private static float ExhaustSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var delta = b - a;
        var t = Math.Clamp(Vector2.Dot(point - a, delta) / MathF.Max(.0001f, delta.LengthSquared()), 0, 1);
        return Vector2.Distance(point, a + delta * t);
    }

    private static float ExhaustEase(float value) => MathHelper.SmoothStep(0, 1, Math.Clamp(value, 0, 1));

    private static void ExhaustComposite(ref Vector4 background, Color foreground, float opacity)
    {
        opacity = Math.Clamp(opacity, 0, 1);
        background = foreground.ToVector4() * opacity + background * (1 - opacity);
    }
}
