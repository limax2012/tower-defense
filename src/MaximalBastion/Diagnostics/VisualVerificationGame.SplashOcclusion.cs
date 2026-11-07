using MaximalBastion.Combat;
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
    private void AssertSplashBodyOcclusion(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var originalRenderer = _renderer;
        var weapons = new[] { "frost_spire", "ember_coil", "breaker_cannon", "siege_mortar" };
        const int width = 270;
        const int height = 116;
        var body = new Rectangle(width / 2 - 10, height / 2 - 10, 20, 20);
        var core = new Rectangle(width / 2 - 5, height / 2 - 5, 10, 10);
        using var cell = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                using var board = new RenderTarget2D(GraphicsDevice, 1280, 1130, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(board);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                MechanicalReviewLabel(font, "SPLASH / RECIPIENT DEPTH", new Vector2(20, 16), .86f);
                MechanicalReviewLabel(font, $"ACTUAL SHOTS / NATIVE GAMEPLAY SIZE / LATE SPLASH / {(reduced ? "REDUCED" : "FULL")} EFFECTS",
                    new Vector2(20, 54), .52f);
                var columns = new[] { "BASELINE", "OWN SPLASH", "FRONT BODY COVERS", "STRUCK BODY IN FRONT" };
                for (var column = 0; column < columns.Length; column++)
                    MechanicalReviewLabel(font, columns[column], new Vector2(160 + column * 280, 86), .50f);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);

                for (var weaponIndex = 0; weaponIndex < weapons.Length; weaponIndex++)
                for (var carrier = 0; carrier < 2; carrier++)
                {
                    var weapon = weapons[weaponIndex];
                    var role = carrier == 0 ? EnemySignalRole.None : EnemySignalRole.Disruptor;
                    var label = $"{weapon} / {role} / {(reduced ? "Reduced" : "Full")}";
                    var row = weaponIndex * 2 + carrier;
                    _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
                    var session = CreateBurstSession(content, weapon, weapon == "breaker_cannon" ? 3 : 2, assertions);
                    Require(session.SpawnSandboxTargets("t5_regenerator", 1, 20, "Standard", false, role),
                        $"{label}: splash depth uses a durable live recipient.", assertions);
                    var struck = session.Enemies[0];
                    struck.SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath,
                        session.Map.Path);
                    TowerBehaviorRegistry.Create(session.Towers[0].Definition.Behavior).Attack(new TowerInstanceContext
                        { Session = session, Tower = session.Towers[0], Target = struck });
                    var projectile = session.Projectiles.Projectiles.Single();
                    struck.SetSandboxPathDistance(session.Map.Path.Project(projectile.AimPoint).DistanceAlongPath,
                        session.Map.Path);
                    session.Projectiles.Update(10, session);
                    var splash = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Splash);
                    Require(splash.TargetEnemy == struck && !struck.IsDead,
                        $"{label}: the actual projectile splash retains its surviving recipient.", assertions);
                    var impact = splash.Start;
                    var radius = splash.Radius;
                    var duration = splash.Duration;
                    var focus = PresentationFrame.Create(session, 0).EnemyPosition(struck);

                    foreach (var remaining in new[] { .72f, .32f })
                    {
                        splash.Remaining = duration * remaining;
                        var baseline = RenderAttackOcclusionCell(cell, session, focus, false);
                        if (remaining < .5f) Place(0);
                        var checksum = SessionChecksum.Compute(session, 921);
                        var own = RenderAttackOcclusionCell(cell, session, focus, true);
                        if (remaining < .5f) Place(1);
                        Require(NativeAttackChanges(baseline, own, width, body) > 3 &&
                                _renderer.LastEnemyAttackCount == 1 &&
                                _renderer.LastEnemyContactCount == (remaining < .5f ? 0 : 1),
                            $"{label}: the full splash draws above its recipient {(remaining < .5f ? "after the compact hit has ended" : "during the initial hit")}.", assertions);
                        Require(checksum == SessionChecksum.Compute(session, 921) && splash.Start == impact &&
                                splash.Radius == radius && splash.Duration == duration,
                            $"{label}: splash depth preserves the fixed blast center, radius, lifetime, and simulation.", assertions);
                    }

                    var front = Occluder(role == EnemySignalRole.None ? EnemySignalRole.None : EnemySignalRole.Jammer);
                    var coveredBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    var covered = RenderAttackOcclusionCell(cell, session, focus, true);
                    Place(2);
                    Require(NativeAttackChanges(coveredBaseline, covered, width, core) == 0,
                        $"{label}: the later body covers the rear recipient's complete splash, including its late plume.", assertions);
                    session.Enemies.Remove(front);
                    session.Enemies.Insert(0, front);
                    var reversedBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    var reversed = RenderAttackOcclusionCell(cell, session, focus, true);
                    Place(3);
                    Require(NativeAttackChanges(reversedBaseline, reversed, width, body) > 3,
                        $"{label}: moving the struck body in front restores its splash above the rear body.", assertions);

                    if (weapon == "siege_mortar")
                    {
                        session.Enemies.Remove(front);
                        front = Occluder(role == EnemySignalRole.None ? EnemySignalRole.Jammer : EnemySignalRole.None);
                        session.Enemies.Remove(front);
                        session.Enemies.Insert(0, front);
                        var priorityBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                        var priority = RenderAttackOcclusionCell(cell, session, focus, true);
                        var changes = NativeAttackChanges(priorityBaseline, priority, width, core);
                        Require(role == EnemySignalRole.None ? changes == 0 : changes > 3,
                            $"{label}: full splash depth follows signal-carrier body priority independently of list order.", assertions);
                    }

                    struck.ApplyHealthDamage(struck.Health + 1);
                    var deadChecksum = SessionChecksum.Compute(session, 922);
                    var fallbackBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    var fallback = RenderAttackOcclusionCell(cell, session, focus, true);
                    Require(_renderer.LastEnemyAttackCount == 0 &&
                            NativeAttackChanges(fallbackBaseline, fallback, width, core) == 0,
                        $"{label}: an expired recipient leaves the remaining splash beneath surviving enemies.", assertions);
                    Require(deadChecksum == SessionChecksum.Compute(session, 922) && splash.Start == impact &&
                            splash.Radius == radius && splash.Remaining == duration * .32f,
                        $"{label}: fallback rendering preserves effect geometry, elapsed lifetime, and synchronized state.", assertions);
                    front.SetSandboxPathDistance(front.DistanceAlongPath + radius + 60, session.Map.Path);
                    var uncoveredBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    var uncovered = RenderAttackOcclusionCell(cell, session, focus, true);
                    Require(NativeAttackChanges(uncoveredBaseline, uncovered, width, body) > 3,
                        $"{label}: removing the occluding body reveals the surviving ground splash at its original impact point.", assertions);

                    GraphicsDevice.SetRenderTarget(board);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                    MechanicalReviewLabel(font, content.Towers[weapon].DisplayName.ToUpperInvariant() +
                        (carrier == 0 ? "\nORDINARY" : "\nSIGNAL"), new Vector2(18, 143 + row * 126), .45f);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);

                    EnemyInstance Occluder(EnemySignalRole occluderRole)
                    {
                        Require(session.SpawnSandboxTargets("t5_regenerator", 1, 20, "Standard", true, occluderRole),
                            $"{label}: overlapping depth review creates a second complete body.", assertions);
                        var enemy = session.Enemies[^1];
                        enemy.SetSandboxPathDistance(struck.DistanceAlongPath + 6, session.Map.Path);
                        return enemy;
                    }

                    void Place(int column)
                    {
                        GraphicsDevice.SetRenderTarget(board);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                        _batch.Draw(cell, new Vector2(144 + column * 280, 110 + row * 126), Color.White);
                        _batch.End();
                        GraphicsDevice.SetRenderTarget(null);
                    }
                }
                scenes.Add(SaveTierScene(board, $"splash-body-depth-{(reduced ? "reduced" : "full")}.png"));
            }
        }
        finally { _renderer = originalRenderer; }
    }
}
