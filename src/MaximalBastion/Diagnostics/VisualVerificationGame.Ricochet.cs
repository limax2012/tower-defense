using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertRicochetPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var session = RicochetReviewSession(content, assertions);
                var tower = session.Towers[0];
                Require(new TowerSystem(session.TargetSelector).TryFireImmediate(tower, session),
                    "Ricochet review launches a real Ricochet Array projectile.", assertions);
                var shot = session.Projectiles.Projectiles.Single();
                session.Projectiles.Update(1, session);
                var bounce = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Ricochet);
                Require(session.Effects.Effects.All(effect => effect.Kind != EffectKind.Beam) &&
                        bounce.Appearance == shot.Appearance && bounce.Radius == shot.Radius &&
                        MathF.Abs(bounce.TravelDuration * shot.Speed - Vector2.Distance(bounce.Start, bounce.End)) < .01f,
                    "The bounce retains the fired shot's appearance, size and travel speed without a connecting beam.", assertions);
                var mode = reduced ? "reduced" : "full";
                var checksum = SessionChecksum.Compute(session, 0);
                var phases = new[] { bounce.TravelDuration * .2f, bounce.TravelDuration * .5f,
                    bounce.TravelDuration * .8f, bounce.TravelDuration + .025f };
                scenes.Add(CaptureRicochetPhases(session, phases, font, $"needle-ricochet-phases-{mode}.png"));
                SetAge(phases[1]);
                using (var rendered = RenderScene(ui, GameState.Playing, session))
                using (var native = DownsampleEnemyLaneScene(rendered))
                    scenes.Add(SaveTierScene(native, $"needle-ricochet-native-{mode}.png"));
                var centers = new List<float>();
                foreach (var fraction in new[] { .2f, .5f, .8f })
                {
                    var bounds = RicochetBrightBounds(bounce, fraction, reduced);
                    Require(bounds.Width is > 3 and < 40,
                        $"{mode}: ricochet phase {fraction:0.0} occupies a short projectile footprint, not the whole gap.", assertions);
                    centers.Add(bounds.Center.X);
                }
                Require(centers[1] - centers[0] > 15 && centers[2] - centers[1] > 15,
                    $"{mode}: the luminous projectile visibly advances toward the secondary target.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{mode}: ricochet phase sampling and rendering preserve the combat checksum.", assertions);

                void SetAge(float age)
                {
                    foreach (var effect in session.Effects.Effects)
                        effect.Remaining = MathF.Max(0, effect.Duration - age);
                }
            }
            AssertRicochetTracking(content, font, assertions, scenes);
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICOCHET_ANIMATION") != "1") return;
            _renderer.ReducedEffects = false;
            var motion = RicochetReviewSession(content, assertions);
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "ricochet-animation"));
            for (var frame = 0; frame < 90; frame++)
            {
                motion.Update(1f / 60);
                using var rendered = RenderScene(ui, GameState.Playing, motion);
                using var native = DownsampleEnemyLaneScene(rendered);
                var capture = SaveTierScene(native, $"ricochet-animation/frame-{frame:000}.png");
                if (frame == 30) scenes.Add(capture);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private GameSession RicochetReviewSession(GameContent content, ICollection<string> assertions)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("needle_turret", new Vector2(282, 205)) &&
                session.TryChooseTowerDoctrine(session.SelectedTower!.Id, "needle_cycler") &&
                session.TrySpecializeTower(session.SelectedTower!.Id, "rapid_array") &&
                session.SpawnSandboxTargets("t1_crawler", 2, 20, "Standard", true),
            "Ricochet review uses an authored tower and two immortal Sandbox targets.", assertions);
        for (var index = 0; index < session.Enemies.Count; index++)
            session.Enemies[index].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250 + index * 65, 110)).DistanceAlongPath,
                session.Map.Path);
        session.Towers[0].TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        return session;
    }

    private Rectangle RicochetBrightBounds(EffectInstance effect, float phase, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 192, 96, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        var direction = effect.End - effect.Start;
        var transform = Matrix.CreateTranslation(-effect.Start.X, -effect.Start.Y, 0) *
            Matrix.CreateRotationZ(-MathF.Atan2(direction.Y, direction.X)) * Matrix.CreateTranslation(48, 48, 0);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null, transform);
        RicochetArt.Draw(_batch, _primitives, effect, effect.Duration - effect.TravelDuration * phase, 0, reduced);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        var xs = Enumerable.Range(0, pixels.Length).Where(index =>
            Math.Max(pixels[index].R, Math.Max(pixels[index].G, pixels[index].B)) >= 150).Select(index => index % target.Width).ToArray();
        return xs.Length == 0 ? Rectangle.Empty : new Rectangle(xs.Min(), 0, xs.Max() - xs.Min() + 1, 1);
    }

    private VisualVerificationScene CaptureRicochetPhases(GameSession session, float[] phases, SpriteFont font, string fileName)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "NEEDLE TURRET / RICOCHET ARRAY", new Vector2(24, 18), .9f);
        MechanicalReviewLabel(font, "ACTUAL SHOT / 2.5x DETAIL ABOVE / NATIVE GAMEPLAY SIZE BELOW", new Vector2(24, 57), .56f);
        for (var column = 0; column < phases.Length; column++)
        {
            _primitives.FillRect(_batch, new Rectangle(column * 320 + 8, 104, 304, 592), ColorPalette.Panel);
            MechanicalReviewLabel(font, $"+{phases[column]:0.000}s / {(column == 3 ? "IMPACT" : "IN FLIGHT")}",
                new Vector2(column * 320 + 22, 114), .56f);
        }
        _batch.End();
        for (var column = 0; column < phases.Length; column++)
        {
            foreach (var effect in session.Effects.Effects) effect.Remaining = MathF.Max(0, effect.Duration - phases[column]);
            Draw(new Rectangle(column * 320 + 10, 146, 300, 330), 2.5f, new Vector2(column * 320 + 160, 309));
            Draw(new Rectangle(column * 320 + 10, 490, 300, 198), 1, new Vector2(column * 320 + 160, 583));
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        void Draw(Rectangle clip, float scale, Vector2 center)
        {
            GraphicsDevice.ScissorRectangle = clip;
            var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X - 282.5f * scale, center.Y - 153 * scale, 0);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, clipping, null, transform);
            _renderer.DrawCombatShowcase(_batch, _primitives, session);
            _batch.End();
        }
    }
}
