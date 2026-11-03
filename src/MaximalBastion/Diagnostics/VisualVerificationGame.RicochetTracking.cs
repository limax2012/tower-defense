using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertRicochetTracking(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var reduced in new[] { false, true })
            foreach (var corner in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var session = RicochetReviewSession(content, assertions);
                session.Enemies.Clear();
                var start = session.Map.Path.Project(new Vector2(corner ? 382 : 250, 110)).DistanceAlongPath;
                for (var index = 0; index < 2; index++)
                {
                    var enemy = new EnemyInstance(9100 + index, content.Enemies["t2_runner"], session.Map.Path,
                        10, 2, sandboxImmortal: true);
                    enemy.SetSandboxPathDistance(start + index * 65, session.Map.Path);
                    session.Enemies.Add(enemy);
                }
                Require(new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session),
                    "Moving ricochet review fires the authored Needle behavior at real mobile enemies.", assertions);
                session.Projectiles.Update(1, session);
                var bounce = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Ricochet);
                Require(ReferenceEquals(bounce.TargetEnemy, session.Enemies[0]),
                    "Ricochet tracking retains the actual secondary enemy selected by combat.", assertions);
                var age = bounce.TravelDuration + .008f;
                foreach (var enemy in session.Enemies) enemy.UpdateMovement(age, session.Map.Path);
                session.Effects.Update(age);
                const float lead = 1f / 120;
                var presentation = PresentationFrame.Create(session, lead);
                var center = presentation.EnemyPosition(bounce.TargetEnemy!);
                var destination = presentation.RicochetDestination(bounce);
                var label = $"{(corner ? "CORNER" : "STRAIGHT")} / {(reduced ? "REDUCED" : "FULL")}";
                Require(Vector2.Distance(center, bounce.End) > 18 && destination == center,
                    $"{label}: the moving target outruns its original endpoint, while the ricochet follows its interpolated center.", assertions);
                if (corner)
                    Require(Vector2.Distance(center, session.Map.Path.GetPosition(bounce.TargetEnemy!.DistanceAlongPath +
                            bounce.TargetEnemy.CurrentSpeed * lead)) > .1f,
                        $"{label}: the target is within the rounded visual corner, distinct from its raw path center.", assertions);
                var checksum = SessionChecksum.Compute(session, 0);
                var centroid = RicochetImpactCentroid(bounce, presentation, reduced);
                Require(Vector2.Distance(centroid, center) < 5 && Vector2.Distance(centroid, bounce.End) > 14,
                    $"{label}: rendered impact light is centered on the moving body rather than the stale endpoint.", assertions);
                scenes.Add(CaptureRicochetTracking(session, center, lead, font, label,
                    $"needle-ricochet-tracking-{(corner ? "corner" : "straight")}-{(reduced ? "reduced" : "full")}.png"));
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{label}: moving-target rendering and impact measurement leave synchronized state unchanged.", assertions);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private Vector2 RicochetImpactCentroid(EffectInstance effect, PresentationFrame presentation, bool reduced)
    {
        var destination = presentation.RicochetDestination(effect);
        using var target = new RenderTarget2D(GraphicsDevice, 128, 128, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateTranslation(64 - destination.X, 64 - destination.Y, 0));
        RicochetArt.Draw(_batch, _primitives, effect, presentation.EffectRemaining(effect),
            presentation.TimeSeconds, reduced, destination);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        var weighted = Vector2.Zero;
        var total = 0f;
        for (var index = 0; index < pixels.Length; index++)
        {
            var weight = Math.Max(0, pixels[index].G - 100);
            weighted += new Vector2(index % target.Width + .5f, index / target.Width + .5f) * weight;
            total += weight;
        }
        return total > 0 ? weighted / total + destination - new Vector2(64) : new Vector2(float.NaN);
    }

    private VisualVerificationScene CaptureRicochetTracking(GameSession session, Vector2 center, float lead,
        SpriteFont font, string label, string fileName)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "RICOCHET / MOVING TARGET CONTACT", new Vector2(24, 20), .85f);
        MechanicalReviewLabel(font, label + " / FAST RUNNER / FRACTIONAL-FRAME INTERPOLATION", new Vector2(24, 59), .53f);
        for (var column = 0; column < 2; column++)
        {
            _primitives.FillRect(_batch, new Rectangle(16 + column * 632, 112, 616, 588), ColorPalette.Panel);
            MechanicalReviewLabel(font, column == 0 ? "NATIVE GAMEPLAY SIZE" : "3x DETAIL",
                new Vector2(32 + column * 632, 128), .58f);
        }
        _batch.End();
        for (var column = 0; column < 2; column++)
        {
            var scale = column == 0 ? 1 : 3;
            GraphicsDevice.ScissorRectangle = new Rectangle(18 + column * 632, 170, 612, 526);
            var at = new Vector2(324 + column * 632, 400);
            var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X - center.X * scale, at.Y - center.Y * scale, 0);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, clipping, null, transform);
            _renderer.DrawCombatShowcase(_batch, _primitives, session, lead);
            _batch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }
}
