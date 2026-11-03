using MaximalBastion.Core;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertBruteLegAttachments(IReadOnlyList<StatusReviewEnemy> samples, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var brutes = samples.Where(sample => sample.Enemy.Definition.Id == "t3_brute").ToArray();
        scenes.Add(CaptureBruteLegAttachments(brutes, font));
        const int size = 192;
        const int rasterScale = GameConstants.RenderScale;
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var pixels = new Color[size * size];
        var solid = new bool[pixels.Length];
        var visited = new bool[pixels.Length];
        var queue = new int[pixels.Length];
        var measurements = new List<object>();
        foreach (var sample in brutes)
        for (var headingIndex = 0; headingIndex < 4; headingIndex++)
        for (var phaseIndex = 0; phaseIndex < 12; phaseIndex++)
        {
            var heading = headingIndex * MathHelper.PiOver2;
            var distance = EnemyLocomotion.StrideDistance(sample.Enemy) * phaseIndex / 12;
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Transparent);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(rasterScale));
            EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, new Vector2(size / (rasterScale * 2f)),
                heading, .43f, true, travelDistance: distance, castShadows: false);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            target.GetData(pixels);
            for (var pixel = 0; pixel < pixels.Length; pixel++) solid[pixel] = pixels[pixel].A >= 240;
            Array.Clear(visited);
            var center = size / 2 + size / 2 * size;
            var connected = solid[center] ? Flood(center) : 0;
            var largestDetached = 0;
            var detached = 0;
            for (var pixel = 0; pixel < pixels.Length; pixel++)
            {
                if (!solid[pixel] || visited[pixel]) continue;
                var component = Flood(pixel);
                detached += component;
                largestDetached = Math.Max(largestDetached, component);
            }
            measurements.Add(new
            {
                rank = sample.Enemy.Rank.ToString(), headingDegrees = headingIndex * 90,
                phase = phaseIndex / 12f, connectedPixels = connected,
                detachedPixels = detached, largestDetachedComponent = largestDetached
            });
            var valid = connected > 0 && largestDetached <= 2;
            if (!valid)
                scenes.Add(SaveTierScene(target,
                    $"brute-attachment-failure-{sample.Enemy.Rank.ToString().ToLowerInvariant()}-{headingIndex}-{phaseIndex:00}.png"));
            Require(valid,
                $"Brute {sample.Enemy.Rank}, heading {headingIndex * 90}, gait phase {phaseIndex}/12: " +
                $"{connected} solid pixels connect to the chassis; the largest detached component is {largestDetached}px " +
                "(at most two isolated antialias pixels at internal resolution).", assertions);
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "brute-attachment-measurements.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                rasterScale, alphaThreshold = 240, connectivity = 8, measurements
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        int Flood(int start)
        {
            var read = 0;
            var write = 1;
            queue[0] = start;
            visited[start] = true;
            while (read < write)
            {
                var index = queue[read++];
                var x = index % size;
                var y = index / size;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nextX = x + dx;
                    var nextY = y + dy;
                    if (nextX < 0 || nextX >= size || nextY < 0 || nextY >= size) continue;
                    var next = nextX + nextY * size;
                    if (!solid[next] || visited[next]) continue;
                    visited[next] = true;
                    queue[write++] = next;
                }
            }
            return write;
        }
    }

    private VisualVerificationScene CaptureBruteLegAttachments(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font)
    {
        const int width = 1280;
        const int height = 1040;
        var lane = Color.Lerp(ColorPalette.Helix.Lane, ColorPalette.Helix.Ground, .64f);
        using var target = new RenderTarget2D(GraphicsDevice, width * 2, height * 2, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "BRUTE / LEG ATTACHMENT + CONTACT REVIEW", new Vector2(20, 24), .78f);
        StatusLabel(font, "TWO-TIMES DETAIL + NATIVE SIZE / FOUR QUARTERS OF ONE WALK CYCLE", new Vector2(20, 62), .52f);
        for (var phase = 0; phase < 4; phase++)
            StatusLabel(font, $"PHASE {phase * 25}%", new Vector2(154 + phase * 280, 100), .52f);
        for (var rank = 0; rank < samples.Count; rank++)
        for (var direction = 0; direction < 2; direction++)
        {
            var y = 132 + (rank * 2 + direction) * 146;
            StatusLabel(font, samples[rank].Enemy.Rank.ToString().ToUpperInvariant(), new Vector2(20, y + 48), .48f);
            StatusLabel(font, direction == 0 ? "UP" : "RIGHT", new Vector2(20, y + 72), .45f);
            for (var phase = 0; phase < 4; phase++)
            {
                var x = 140 + phase * 280;
                _primitives.FillRect(_batch, new Rectangle(x, y, 272, 136), lane);
                for (var band = 0; band < 4; band++)
                    _primitives.Line(_batch, new Vector2(x, y + 17 + band * 34),
                        new Vector2(x + 272, y + 17 + band * 34), ColorPalette.Helix.Edge * .12f, 1);
            }
        }
        _batch.End();
        for (var rank = 0; rank < samples.Count; rank++)
        for (var direction = 0; direction < 2; direction++)
        for (var phase = 0; phase < 4; phase++)
        {
            var sample = samples[rank];
            var y = 200 + (rank * 2 + direction) * 146;
            var heading = direction == 0 ? -MathHelper.PiOver2 : 0;
            var distance = EnemyLocomotion.StrideDistance(sample.Enemy) * phase / 4;
            DrawEnemyArmorBody(sample, new Vector2(230 + phase * 280, y), 2, heading, .43f, distance);
            DrawEnemyArmorBody(sample, new Vector2(365 + phase * 280, y), 1, heading, .43f, distance);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(native);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(target, new Rectangle(0, 0, width, height), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(native, "brute-leg-attachments.png");
    }
}
