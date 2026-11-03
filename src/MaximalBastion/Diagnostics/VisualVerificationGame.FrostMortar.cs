using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureFrostMortarReview(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        AssertFrostMortarMechanics(content, assertions);
        var phases = new[] { 0f, 1.785f, 3.57f, 5.355f, 7.14f };
        scenes.Add(CaptureMechanicalPhases(content.Towers["frost_spire"], font, phases,
            "34-frost-rotor-clearance.png"));
        scenes.Add(CaptureMechanicalPhases(content.Towers["siege_mortar"], font, phases,
            "34a-mortar-rotation.png"));
        scenes.Add(CaptureFrostMortarStates(content, font, .75f, "34b-frost-mortar-statuses.png"));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_FROST_MORTAR_ANIMATION") != "1") return;

        Directory.CreateDirectory(Path.Combine(_outputDirectory, "frost-mortar-animation"));
        for (var frame = 0; frame < 72; frame++)
            scenes.Add(CaptureFrostMortarStates(content, font, frame / 9f,
                $"frost-mortar-animation/frame-{frame:000}.png"));
    }

    private void AssertFrostMortarMechanics(GameContent content, ICollection<string> assertions)
    {
        const int scale = 3;
        const int size = 112 * scale;
        var frost = content.Towers["frost_spire"];
        for (var stage = 1; stage <= 4; stage++)
        {
            var first = MechanicalReviewPixels(frost, stage, 0, 0, false, RadialReviewAim(frost.Id, 0));
            var second = MechanicalReviewPixels(frost, stage, 3.57f, 0, false, RadialReviewAim(frost.Id, 3.57f));
            var held = MechanicalReviewPixels(frost, stage, 3.57f, 0, false, RadialReviewAim(frost.Id, 0));
            Require(first.SequenceEqual(held),
                $"Frost Spire stage {stage}: crystal and coolant banks preserve one rigid assembly when its phase is held fixed.", assertions);
            var rotorRadius = frost.Visual.Radius * TowerTierVisuals.FrostRotorRadius(stage) * scale;
            // One logical pixel covers the rotated primitive's filtered raster edge.
            var sweptRadiusSquared = MathF.Pow(rotorRadius + scale, 2);
            var offset = TowerTierVisuals.FrostCoolantOffset(stage);
            var length = TowerTierVisuals.FrostCoolantLength(stage);
            var bankHalfWidth = (.30f + stage * .02f) * .5f;
            Require(offset - bankHalfWidth - TowerTierVisuals.FrostRotorRadius(stage) >= .075f,
                $"Frost Spire stage {stage}: the crystal and coolant banks retain physical clearance throughout their shared rotation.", assertions);
            var assemblyRadius = MathF.Max(MathF.Sqrt(offset * offset + length * length) + bankHalfWidth + .10f,
                stage == 4 ? 1.24f : 0) * frost.Visual.Radius;
            var assemblyRadiusSquared = MathF.Pow((assemblyRadius + 2) * scale, 2);
            var rotorChanges = 0;
            var bankChanges = 0;
            var foundationChanges = 0;
            for (var index = 0; index < first.Length; index++)
            {
                if (first[index] == second[index]) continue;
                var x = index % size + .5f - size * .5f;
                var y = index / size + .5f - size * .5f;
                if (x * x + y * y <= sweptRadiusSquared) rotorChanges++;
                else if (x * x + y * y <= assemblyRadiusSquared) bankChanges++;
                else foundationChanges++;
            }
            Require(rotorChanges > 0 && bankChanges > 0,
                $"Frost Spire stage {stage}: crystal and surrounding coolant machinery both visibly rotate ({rotorChanges} core pixels, {bankChanges} outer pixels).",
                assertions);
            Require(foundationChanges == 0,
                $"Frost Spire stage {stage}: the outer foundation remains fixed beyond the rotating assembly's swept radius.",
                assertions);
        }

        var states = new[] { "idle", "Protocol", "support", "Protocol and support" };
        foreach (var id in new[] { "frost_spire", "siege_mortar" })
        for (var stage = 1; stage <= 4; stage++)
        for (var state = 0; state < states.Length; state++)
            Require(MechanicalReviewPixels(content.Towers[id], stage, 0, state, true)
                    .SequenceEqual(MechanicalReviewPixels(content.Towers[id], stage, 3.57f, state, true)),
                $"{content.Towers[id].DisplayName} stage {stage}: {states[state]} hardware remains stable at a fixed assembly phase in Reduced Effects.",
                assertions);
    }

    private Color[] MechanicalReviewPixels(TowerDefinition tower, int stage, float time, int state, bool reduced,
        float aim = -.48f)
    {
        const int scale = 3;
        const int size = 112 * scale;
        using var target = new RenderTarget2D(GraphicsDevice, size, size,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        DrawMechanicalReviewTower(tower, stage, new Vector2(size * .5f), scale, aim, time, state,
            reduced, groundEffects: false, lights: false);
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[size * size];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureMechanicalPhases(TowerDefinition tower, SpriteFont font,
        float[] phases, string fileName)
    {
        const int width = 1240;
        const int height = 990;
        const int header = 130;
        const int rowHeight = 210;
        var frost = tower.Id == "frost_spire";
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, tower.DisplayName.ToUpperInvariant(), new Vector2(26, 18), 1);
        MechanicalReviewLabel(font, frost ? "RIGID ASSEMBLY / CONSTANT 0.22 RAD/S" : "CONCENTRIC LAUNCH WELL / CONSTANT 0.14 RAD/S",
            new Vector2(26, 60), .64f);
        MechanicalReviewLabel(font, "2.2x DETAIL / FIELD SIZE: NORMAL, PROTOCOL, SUPPORT",
            new Vector2(26, 89), .57f);
        for (var column = 0; column < phases.Length; column++)
        {
            var label = $"{phases[column]:0.000}s";
            MechanicalReviewLabel(font, label, new Vector2(Column(column) - 39, 110), .56f);
        }
        for (var stage = 1; stage <= 4; stage++)
        {
            var y = header + (stage - 1) * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8),
                frost ? ColorPalette.Rainline.Deck : ColorPalette.Cinderworks.Deck);
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}", new Vector2(30, y + 78), .72f);
        }
        _batch.End();
        for (var stage = 1; stage <= 4; stage++)
        for (var column = 0; column < phases.Length; column++)
        {
            var x = Column(column);
            var y = header + (stage - 1) * rowHeight;
            var aim = RadialReviewAim(tower.Id, phases[column]);
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, y + 72), 2.2f, aim, phases[column], 0);
            for (var state = 0; state < 3; state++)
                DrawMechanicalReviewTower(tower, stage, new Vector2(x + (state - 1) * 59, y + 166),
                    1, aim, phases[column], state);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        static int Column(int column) => 230 + column * 218;
    }

    private VisualVerificationScene CaptureFrostMortarStates(GameContent content, SpriteFont font,
        float time, string fileName)
    {
        const int width = 1600;
        const int height = 690;
        const int header = 118;
        const int rowHeight = 278;
        var ids = new[] { "frost_spire", "siege_mortar" };
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "FROST / MORTAR", new Vector2(26, 16), 1);
        MechanicalReviewLabel(font, $"REAL-TIME MOTION / {time:0.00}s", new Vector2(1250, 24), .66f);
        MechanicalReviewLabel(font, "1.85x DETAIL ABOVE / NATIVE FIELD SIZE BELOW", new Vector2(26, 57), .60f);
        MechanicalReviewLabel(font, "NORMAL / PROTOCOL / SUPPORT", new Vector2(1045, 57), .60f);
        for (var stage = 1; stage <= 4; stage++)
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}",
                new Vector2(Column(stage) - 17, 87), .66f);
        for (var row = 0; row < ids.Length; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 10),
                row == 0 ? ColorPalette.Rainline.Deck : ColorPalette.Cinderworks.Deck);
            MechanicalReviewLabel(font, content.Towers[ids[row]].DisplayName.ToUpperInvariant(),
                new Vector2(30, y + 10), .66f);
        }
        _batch.End();
        for (var row = 0; row < ids.Length; row++)
        for (var stage = 1; stage <= 4; stage++)
        for (var state = 0; state < 3; state++)
        {
            var tower = content.Towers[ids[row]];
            var x = Column(stage) + (state - 1) * 117;
            var y = header + row * rowHeight;
            var aim = RadialReviewAim(tower.Id, time);
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, y + 114), 1.85f,
                aim, time, state);
            DrawMechanicalReviewTower(tower, stage, new Vector2(x, y + 217), 1,
                aim, time, state);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        static int Column(int stage) => 200 + (stage - 1) * 400;
    }

    private static float RadialReviewAim(string towerId, float time) =>
        -MathHelper.PiOver2 + time * (towerId == "frost_spire" ? .22f : .14f);

    private void DrawMechanicalReviewTower(TowerDefinition tower, int stage, Vector2 at, float scale,
        float aim, float time, int state, bool reduced = false, bool groundEffects = true, bool lights = true)
    {
        var level = Math.Min(stage, 3);
        var apex = stage == 4;
        var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, transform);
        if (state is 1 or 3)
            TowerProtocolArt.DrawEnvelope(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                tower.Id, level, apex, time, reduced);
        NightGridArt.Tower(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
            tower.Id, level, aim, reduced ? 0 : time, lights: lights && !reduced,
            groundEffects: groundEffects, apex: apex);
        if (state is 1 or 3)
            TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                tower.Id, level, apex, aim, time, reduced, glow: groundEffects, envelope: false);
        if (state is 2 or 3)
            TowerStatusArt.DrawSupport(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                tower.Id, level, apex, aim, time, reduced);
        _batch.End();
    }

    private void MechanicalReviewLabel(SpriteFont font, string text, Vector2 at, float scale) =>
        _batch.DrawString(font, text, at, ColorPalette.Paper, 0, Vector2.Zero,
            scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
}
