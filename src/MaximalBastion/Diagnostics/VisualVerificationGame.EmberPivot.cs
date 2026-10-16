using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureEmberPivotReview(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var tower = content.Towers["ember_coil"];
        AssertEmberPivot(tower, assertions);
        scenes.Add(CaptureEmberAimGallery(tower, font));
        scenes.Add(CaptureEmberMotion(tower, font, .75f, "34d-ember-statuses.png"));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_EMBER_ANIMATION") != "1") return;

        Directory.CreateDirectory(Path.Combine(_outputDirectory, "ember-animation"));
        for (var frame = 0; frame < 72; frame++)
            scenes.Add(CaptureEmberMotion(tower, font, frame / 12f,
                $"ember-animation/frame-{frame:000}.png"));
    }

    private void AssertEmberPivot(TowerDefinition tower, ICollection<string> assertions)
    {
        const int scale = 3;
        const int size = 112 * scale;
        var accent = NightGridArt.TowerAccent(tower.Id);
        var sampleRadiusSquared = MathF.Pow(tower.Visual.Radius * .31f * scale, 2);
        for (var stage = 1; stage <= 4; stage++)
        {
            for (var direction = 0; direction < 8; direction++)
            {
                var aim = -MathHelper.PiOver2 + direction * MathHelper.PiOver4;
                var pixels = TierStructurePixels(tower, stage, aim, scale);
                var sum = Vector2.Zero;
                var count = 0;
                // The inner crop excludes the nozzle, rotating coil segments and radiator banks.
                for (var index = 0; index < pixels.Length; index++)
                {
                    var offset = new Vector2(index % size + .5f, index / size + .5f) - new Vector2(size * .5f);
                    if (offset.LengthSquared() > sampleRadiusSquared ||
                        (pixels[index] != accent && pixels[index] != ColorPalette.Paper)) continue;
                    sum += offset;
                    count++;
                }
                var error = count > 0 ? (sum / count).Length() / scale : float.PositiveInfinity;
                Require(count >= 12 && error < .5f,
                    $"Ember Coil stage {stage}: chamber center remains within half a logical pixel of the swivel at {-90 + direction * 45} degrees ({error:0.000}px).",
                    assertions);
            }

            var states = new[] { "idle", "Protocol", "support", "Protocol and support" };
            for (var state = 0; state < states.Length; state++)
                Require(MechanicalReviewPixels(tower, stage, .1f, state, true)
                        .SequenceEqual(MechanicalReviewPixels(tower, stage, 3.57f, state, true)),
                    $"Ember Coil stage {stage}: {states[state]} hardware remains stable in Reduced Effects.",
                    assertions);
        }
    }

    private VisualVerificationScene CaptureEmberAimGallery(TowerDefinition tower, SpriteFont font)
    {
        const int width = 1600;
        const int height = 1026;
        const int header = 130;
        const int rowHeight = 220;
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "EMBER COIL / CENTERED SWIVEL", new Vector2(26, 18), 1);
        MechanicalReviewLabel(font, "2.25x DETAIL ABOVE / FIELD SIZE BELOW: NORMAL, PROTOCOL, SUPPORT",
            new Vector2(26, 65), .61f);
        for (var direction = 0; direction < 8; direction++)
            MechanicalReviewLabel(font, $"{-90 + direction * 45} DEG", new Vector2(Column(direction) - 31, 105), .56f);
        for (var stage = 1; stage <= 4; stage++)
        {
            var y = header + (stage - 1) * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8),
                stage % 2 == 0 ? ColorPalette.Cinderworks.Deck : ColorPalette.Rainline.Deck);
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}", new Vector2(30, y + 83), .72f);
        }
        _batch.End();
        for (var stage = 1; stage <= 4; stage++)
        for (var direction = 0; direction < 8; direction++)
        {
            var x = Column(direction);
            var y = header + (stage - 1) * rowHeight;
            var aim = -MathHelper.PiOver2 + direction * MathHelper.PiOver4;
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, y + 76), 2.25f, aim, .75f, 0);
            for (var state = 0; state < 3; state++)
                DrawMechanicalReviewTower(tower, stage, new Vector2(x + (state - 1) * 53, y + 170),
                    1, aim, .75f, state);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, "34c-ember-swivel-angles.png");

        static int Column(int direction) => 200 + direction * 188;
    }

    private VisualVerificationScene CaptureEmberMotion(TowerDefinition tower, SpriteFont font,
        float time, string fileName)
    {
        const int width = 1600;
        const int height = 410;
        const int header = 118;
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "EMBER COIL / CENTERED SWIVEL", new Vector2(26, 16), 1);
        MechanicalReviewLabel(font, $"REAL-TIME MOTION / {time:0.00}s", new Vector2(1250, 24), .66f);
        MechanicalReviewLabel(font, "2x DETAIL ABOVE / NATIVE FIELD SIZE BELOW", new Vector2(26, 57), .60f);
        MechanicalReviewLabel(font, "NORMAL / PROTOCOL / SUPPORT", new Vector2(1045, 57), .60f);
        for (var stage = 1; stage <= 4; stage++)
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}",
                new Vector2(Column(stage) - 17, 87), .66f);
        _primitives.FillRect(_batch, new Rectangle(16, header, width - 32, height - header - 16),
            ColorPalette.Cinderworks.Deck);
        _batch.End();
        var aim = -MathHelper.PiOver2 + time * MathHelper.TwoPi / 6;
        for (var stage = 1; stage <= 4; stage++)
        for (var state = 0; state < 3; state++)
        {
            var x = Column(stage) + (state - 1) * 117;
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, header + 97), 2,
                aim, time, state);
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, header + 217), 1,
                aim, time, state);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        static int Column(int stage) => 200 + (stage - 1) * 400;
    }
}
