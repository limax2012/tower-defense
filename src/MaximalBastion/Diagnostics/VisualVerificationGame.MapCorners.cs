using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertMapCornerDecoration(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var maps = content.Maps.Values.ToArray();
        var samples = new List<RenderTarget2D>();
        try
        {
            foreach (var map in maps)
            {
                using var decorated = CornerPath(map, true);
                using var plain = CornerPath(map, false);
                var decorationPixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
                var plainPixels = new Color[decorationPixels.Length];
                decorated.GetData(decorationPixels);
                plain.GetData(plainPixels);
                Require(CountChangedPixels(plainPixels, decorationPixels,
                        new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight)) >= 200,
                    $"{map.Id}: straight route sections retain their district edge markings.", assertions);
                for (var index = 1; index < map.Path.Count - 1; index++)
                {
                    var corner = map.Path[index].ToVector2();
                    var half = map.PathWidth / 2 + 4;
                    var region = new Rectangle((int)corner.X - half, (int)corner.Y - half, half * 2, half * 2);
                    Require(CountChangedPixels(plainPixels, decorationPixels, region) == 0,
                        $"{map.Id} corner {index}: edge motifs remain outside the joined lane footprint.", assertions);
                }
                if (map.PathVisual.Style is "prism" or "surge")
                {
                    var distance = 0f;
                    for (var index = 1; index < map.Path.Count - 1; index++)
                    {
                        var corner = map.Path[index].ToVector2();
                        var incoming = Vector2.Normalize(corner - map.Path[index - 1].ToVector2());
                        var outgoing = Vector2.Normalize(map.Path[index + 1].ToVector2() - corner);
                        distance += Vector2.Distance(corner, map.Path[index - 1].ToVector2());
                        // Keep the moving packet away from these stationary rail samples.
                        using var rail = CornerPath(map, true, ((distance + 48) % 112) / 28);
                        var pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
                        rail.GetData(pixels);
                        var core = Average(pixels, corner);
                        var bed = Average(pixels, corner + incoming * 3 - outgoing * 3);
                        Require(Vector3.DistanceSquared(core, Average(pixels, corner - incoming * 10)) <= 3 &&
                                Vector3.DistanceSquared(core, Average(pixels, corner + outgoing * 10)) <= 3,
                            $"{map.Id} corner {index}: the center rail keeps its straight-section color at the join.", assertions);
                        Require(Vector3.DistanceSquared(bed, Average(pixels, corner - incoming * 10 - outgoing * 3)) <= 3 &&
                                Vector3.DistanceSquared(bed, Average(pixels, corner + outgoing * 10 + incoming * 3)) <= 3,
                            $"{map.Id} corner {index}: the rail bed does not brighten where segment fills overlap.", assertions);
                    }
                }
                samples.Add(DownsampleEnemyLaneScene(decorated));
            }

            using var board = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(board);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            StatusLabel(font, "ROUTE CORNERS / WHOLE EDGE MOTIFS / NATIVE SCALE", new Vector2(20, 14), .72f);
            StatusLabel(font, "Actual district paths / unchanged flowing center packets / each crop copied 1:1", new Vector2(20, 43), .48f);
            for (var row = 0; row < maps.Length; row++)
            {
                var map = maps[row];
                var y = 80 + row * 153;
                StatusLabel(font, map.DisplayName.ToUpperInvariant(), new Vector2(20, y + 8), .56f);
                StatusLabel(font, $"{map.PathWidth} px track", new Vector2(20, y + 37), .48f);
                StatusLabel(font, "Clear turn interior", new Vector2(20, y + 62), .44f);
                var indices = new[] { 1, 2, map.Path.Count - 3, map.Path.Count - 2 };
                for (var column = 0; column < indices.Length; column++)
                {
                    var corner = map.Path[indices[column]].ToVector2();
                    var crop = new Rectangle((int)corner.X - 75, (int)corner.Y - 60, 150, 120);
                    var destination = new Rectangle(270 + column * 245, y, 150, 120);
                    _batch.Draw(samples[row], destination, crop, Color.White);
                    StatusLabel(font, $"TURN {indices[column]}", new Vector2(destination.X, y + 126), .42f);
                }
            }
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            scenes.Add(SaveTierScene(board, "65-map-corner-cleanup.png"));
        }
        finally
        {
            foreach (var sample in samples) sample.Dispose();
        }

        RenderTarget2D CornerPath(MapDefinition map, bool edgeMarkings, float time = 0)
        {
            var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
            MapEnvironmentRenderer.Path(_batch, _primitives, map, time, edgeMarkings);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            return target;
        }

        static Vector3 Average(IReadOnlyList<Color> pixels, Vector2 at)
        {
            var left = (int)(at.X * GameConstants.RenderScale) - 1;
            var top = (int)(at.Y * GameConstants.RenderScale) - 1;
            var sum = Vector3.Zero;
            for (var y = top; y < top + 2; y++)
            for (var x = left; x < left + 2; x++)
            {
                var color = pixels[y * GameConstants.RenderWidth + x];
                sum += new Vector3(color.R, color.G, color.B);
            }
            return sum / 4;
        }
    }
}
