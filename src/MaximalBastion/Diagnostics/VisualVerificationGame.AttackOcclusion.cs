using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertAttackBodyOcclusion(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var originalRenderer = _renderer;
        var targets = new[]
        {
            ("t4_aegis", EnemySignalRole.None, "AEGIS / ORDINARY"),
            ("t4_aegis", EnemySignalRole.Disruptor, "AEGIS / SIGNAL"),
            ("t5_regenerator", EnemySignalRole.None, "REGENERATOR / ORDINARY"),
            ("t5_regenerator", EnemySignalRole.Disruptor, "REGENERATOR / SIGNAL")
        };
        const int cellWidth = 270;
        const int cellHeight = 116;
        using var cell = new RenderTarget2D(GraphicsDevice, cellWidth, cellHeight, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        try
        {
            foreach (var reduced in new[] { false, true })
            foreach (var weapon in new[] { "needle_turret", "arc_relay", "prism_beam" })
            {
                if (reduced && weapon != "needle_turret") continue;
                var targetRows = weapon == "needle_turret" ? targets : new[] { targets[weapon == "arc_relay" ? 1 : 3] };
                using var board = new RenderTarget2D(GraphicsDevice, 1280, targetRows.Length == 1 ? 300 : 720, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(board);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                MechanicalReviewLabel(font, $"{content.Towers[weapon].DisplayName.ToUpperInvariant()} / ENEMY CONTACT DEPTH",
                    new Vector2(22, 18), .86f);
                MechanicalReviewLabel(font, $"ACTUAL ATTACK / NATIVE GAMEPLAY SIZE / {(reduced ? "REDUCED" : "FULL")} EFFECTS",
                    new Vector2(22, 56), .52f);
                var headings = new[] { "BASELINE", "OWN HIT", "FRONT / BASELINE", "FRONT / REAR HIT" };
                for (var column = 0; column < 4; column++)
                    MechanicalReviewLabel(font, headings[column], new Vector2(162 + column * 280, 91), .52f);
                for (var row = 0; row < targetRows.Length; row++)
                    MechanicalReviewLabel(font, targetRows[row].Item3.Replace(" / ", "\n"),
                        new Vector2(18, 155 + row * 140), .48f);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);

                for (var row = 0; row < targetRows.Length; row++)
                {
                    var (family, role, label) = targetRows[row];
                    _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
                    var session = CreateAttackOcclusionSession(content, weapon, family, role, assertions);
                    var struck = session.Enemies[0];
                    TowerBehaviorRegistry.Create(session.Towers[0].Definition.Behavior).Attack(new TowerInstanceContext
                        { Session = session, Tower = session.Towers[0], Target = struck });
                    session.Projectiles.Update(10, session);
                    foreach (var effect in session.Effects.Effects) effect.Remaining = effect.Duration * .65f;
                    var focus = PresentationFrame.Create(session, 0).EnemyPosition(struck);
                    var checksum = SessionChecksum.Compute(session, 0);
                    var baseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    Place(0);
                    var contact = RenderAttackOcclusionCell(cell, session, focus, true);
                    Place(1);
                    Require(NativeAttackChanges(baseline, contact, cellWidth,
                                new Rectangle(cellWidth / 2 - 10, cellHeight / 2 - 10, 20, 20)) > 3 &&
                            _renderer.LastEnemyContactCount == 1,
                        $"{weapon} / {label} / {(reduced ? "Reduced" : "Full")}: its own attack contact is visible above the struck hull at native size.", assertions);
                    Require(checksum == SessionChecksum.Compute(session, 0),
                        $"{weapon} / {label}: sampling contact visibility leaves synchronized state unchanged.", assertions);

                    var front = AddAttackOccluder(session, family, role, struck, assertions);
                    checksum = SessionChecksum.Compute(session, 0);
                    var coveredBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    Place(2);
                    var coveredHit = RenderAttackOcclusionCell(cell, session, focus, true);
                    Place(3);
                    var core = new Rectangle(cellWidth / 2 - 5, cellHeight / 2 - 5, 10, 10);
                    Require(NativeAttackChanges(coveredBaseline, coveredHit, cellWidth, core) == 0,
                        $"{weapon} / {label}: a front enemy's complete hull occludes the rear enemy's contact and incoming attack.", assertions);
                    Require(checksum == SessionChecksum.Compute(session, 0),
                        $"{weapon} / {label}: attack depth does not change synchronized state.", assertions);

                    session.Enemies.Remove(front);
                    session.Enemies.Insert(0, front);
                    var reversedBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
                    var reversedHit = RenderAttackOcclusionCell(cell, session, focus, true);
                    Require(NativeAttackChanges(reversedBaseline, reversedHit, cellWidth,
                                new Rectangle(cellWidth / 2 - 10, cellHeight / 2 - 10, 20, 20)) > 3,
                        $"{weapon} / {label}: putting the struck body in front restores its own contact above the rear body.", assertions);

                    void Place(int column)
                    {
                        GraphicsDevice.SetRenderTarget(board);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                        _batch.Draw(cell, new Vector2(144 + column * 280, 120 + row * 140), Color.White);
                        _batch.End();
                        GraphicsDevice.SetRenderTarget(null);
                    }
                }
                scenes.Add(SaveTierScene(board, $"attack-body-depth-{weapon}-{(reduced ? "reduced" : "full")}.png"));
            }
            AssertIncomingAttackDepth(content, assertions, scenes, cell);
            AssertSplashBodyOcclusion(content, font, assertions, scenes);
        }
        finally { _renderer = originalRenderer; }
    }

    private void AssertIncomingAttackDepth(GameContent content, ICollection<string> assertions,
        ICollection<VisualVerificationScene> scenes, RenderTarget2D cell)
    {
        foreach (var reduced in new[] { false, true })
        foreach (var family in new[] { "t4_aegis", "t5_regenerator" })
        foreach (var role in new[] { EnemySignalRole.None, EnemySignalRole.Disruptor })
        foreach (var weapon in new[] { "needle_turret", "siege_mortar" })
        {
            if (weapon == "siege_mortar" && (reduced || family != "t5_regenerator" || role != EnemySignalRole.Disruptor))
                continue;
            _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
            var session = CreateAttackOcclusionSession(content, weapon, family, role, assertions);
            var struck = session.Enemies[0];
            TowerBehaviorRegistry.Create(session.Towers[0].Definition.Behavior).Attack(new TowerInstanceContext
                { Session = session, Tower = session.Towers[0], Target = struck });
            var projectile = session.Projectiles.Projectiles.Single();
            var distance = Vector2.Distance(projectile.Position, projectile.AimPoint);
            Require(!projectile.Update((distance - 10) / projectile.Speed),
                $"{weapon} / {family}: the actual projectile remains in flight as it reaches the target's visible hull.", assertions);
            var presentation = PresentationFrame.Create(session, 0);
            var focus = presentation.EnemyPosition(struck);
            var at = presentation.ProjectilePosition(projectile) - focus + new Vector2(cell.Width / 2f, cell.Height / 2f);
            var bodyPatch = new Rectangle((int)at.X - 3, (int)at.Y - 3, 7, 7);
            var checksum = SessionChecksum.Compute(session, 0);
            var baseline = RenderAttackOcclusionCell(cell, session, focus, false);
            var incoming = RenderAttackOcclusionCell(cell, session, focus, true);
            Require(NativeAttackChanges(baseline, incoming, cell.Width, bodyPatch) > 2 &&
                    _renderer.LastEnemyAttackCount == 1 && _renderer.LastEnemyContactCount == 0,
                $"{weapon} / {family} / {role}: the incoming projectile remains visible above its own target's hull before contact.", assertions);
            if (!reduced)
                scenes.Add(SaveTierScene(cell, $"attack-flight-body-{weapon}-{family}-{role.ToString().ToLowerInvariant()}.png"));
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{weapon} / {family}: presenting the incoming projectile does not advance its simulation position.", assertions);
            AddAttackOccluder(session, family, role, struck, assertions);
            var coveredBaseline = RenderAttackOcclusionCell(cell, session, focus, false);
            var coveredIncoming = RenderAttackOcclusionCell(cell, session, focus, true);
            Require(NativeAttackChanges(coveredBaseline, coveredIncoming, cell.Width, bodyPatch) == 0,
                $"{weapon} / {family} / {role}: a foreground enemy's hull covers the projectile owned by the rear target.", assertions);
        }
    }

    private static GameSession CreateAttackOcclusionSession(GameContent content, string weapon, string family,
        EnemySignalRole role, ICollection<string> assertions)
    {
        var session = CreateBurstSession(content, weapon, 2, assertions);
        Require(session.SpawnSandboxTargets(family, 1, 20, "Standard", true, role),
            $"{weapon}: depth review creates an actual {family} / {role} recipient.", assertions);
        var enemy = session.Enemies[0];
        enemy.AbsorbShield(enemy.Shield);
        enemy.SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath, session.Map.Path);
        return session;
    }

    private static EnemyInstance AddAttackOccluder(GameSession session, string family, EnemySignalRole role,
        EnemyInstance struck, ICollection<string> assertions)
    {
        Require(session.SpawnSandboxTargets(family, 1, 20, "Standard", true,
                role == EnemySignalRole.None ? EnemySignalRole.None : EnemySignalRole.Jammer),
            "Attack depth review creates a foreground body in the same render group.", assertions);
        var front = session.Enemies[^1];
        front.AbsorbShield(front.Shield);
        front.SetSandboxPathDistance(struck.DistanceAlongPath + 6, session.Map.Path);
        return front;
    }

    private Color[] RenderAttackOcclusionCell(RenderTarget2D target, GameSession session, Vector2 focus, bool combat)
    {
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateTranslation(target.Width / 2f - focus.X, target.Height / 2f - focus.Y, 0));
        _renderer.Draw(_batch, _primitives, session, showTransientCombat: combat);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private static int NativeAttackChanges(Color[] before, Color[] after, int width, Rectangle bounds)
    {
        var changed = 0;
        for (var y = Math.Max(0, bounds.Top); y < Math.Min(before.Length / width, bounds.Bottom); y++)
        for (var x = Math.Max(0, bounds.Left); x < Math.Min(width, bounds.Right); x++)
            if (before[y * width + x] != after[y * width + x]) changed++;
        return changed;
    }
}
