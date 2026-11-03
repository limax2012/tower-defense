using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertShardEmitterPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var variants = new[] { (1, 0), (2, 0), (3, 0), (3, 1), (4, 0), (4, 1) };
        var labels = new[] { "T1", "T2 / WIDE", "T3 / BARRAGE", "T3 / PIERCE", "APEX / BARRAGE", "APEX / PIERCE" };
        var fixtures = variants.Select(variant => ShardReviewSession(content, variant.Item1, variant.Item2, assertions)).ToArray();
        scenes.Add(CaptureShardReviewAtlas(fixtures, labels, font, "51-shard-emitter-idle.png"));
        for (var index = 0; index < fixtures.Length; index++)
        {
            var session = fixtures[index];
            var tower = session.Towers[0];
            var initialChecksum = SessionChecksum.Compute(session, 0);
            var initialMode = tower.TargetMode;
            var initialAim = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            tower.TargetMode = TargetMode.Last;
            var changedAim = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            var enemies = session.Enemies.ToArray();
            session.Enemies.Clear();
            var idleAim = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            var laterAim = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .015f));
            session.Enemies.AddRange(enemies);
            tower.TargetMode = initialMode;
            Require(initialAim == changedAim && initialAim == idleAim &&
                    MathF.Abs(laterAim - idleAim - .24f * .005f) < .000001f,
                $"{labels[index]}: the rotor advances steadily at .24 radians per second regardless of targets or targeting mode.", assertions);
            Require(initialChecksum == SessionChecksum.Compute(session, 0),
                $"{labels[index]}: rotor presentation and interpolation preserve the combat checksum.", assertions);
            var system = new TowerSystem(session.TargetSelector);
            var primary = session.TargetSelector.Select(tower.Position, session.GetEffectiveRange(tower), tower.TargetMode, session.Enemies)!;
            var targets = session.TargetSelector.SelectSpreadTargets(tower.Position, session.GetEffectiveRange(tower),
                primary, tower.Level.PelletCount, session.Enemies);
            Require(system.TryFireImmediate(tower, session), $"{labels[index]}: the actual Shard behavior finds its surrounding Sandbox targets.", assertions);
            var shots = session.Projectiles.Projectiles;
            Require(shots.Count == tower.Level.PelletCount && shots.Select((shot, pellet) =>
                    shot.Target == targets[pellet % targets.Count] && shot.Position == tower.Position &&
                    shot.Speed == tower.Level.ProjectileSpeed && shot.Kind == ProjectileKind.Homing).All(valid => valid),
                $"{labels[index]}: pellet count, full-radius target distribution, centered spawn, speed and homing remain authored.", assertions);
            Require(shots.Any(shot => Vector2.Dot(shot.AimPoint - tower.Position, primary.Position - tower.Position) < 0),
                $"{labels[index]}: the emitter legitimately addresses a target behind the primary direction.", assertions);
            if (tower.IsApex)
                Require(shots.Select(shot => shot.Target!.Id).Distinct().Count() == 4,
                    $"{labels[index]}: the actual node-supported range reaches all four sides of the island.", assertions);
            for (var direction = 0; direction < 4; direction++)
                Require(NightGridArt.TowerMuzzlePosition(tower.Position, tower.Definition.Visual.Radius,
                        tower.Definition.Id, tower.LevelIndex + 1, direction * MathHelper.PiOver2, apex: tower.IsApex) == tower.Position,
                    $"{labels[index]}: aim {direction * 90} degrees keeps the muzzle at the exposed central emitter.", assertions);
            session.Projectiles.Update(.04f, session);
            tower.TickVisual(.04f);
        }
        scenes.Add(CaptureShardReviewAtlas(fixtures, labels, font, "51a-shard-emitter-volley.png"));
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var surround = fixtures[4];
        var checksum = SessionChecksum.Compute(surround, 0);
        scenes.Add(Capture("52-shard-sandbox-volley.png", ui, GameState.Playing, surround));
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("52a-shard-sandbox-reduced.png", ui, GameState.Playing, surround));
        _renderer.ReducedEffects = false;
        Require(SessionChecksum.Compute(surround, 0) == checksum,
            "Shard firing presentation in Full and Reduced Effects leaves the synchronized combat checksum unchanged.", assertions);
        AssertShardVolleyLifecycle(content, ui, assertions, scenes);
        AssertShardLaunchPixels(content.Towers["shard_fan"], assertions);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_SHARD_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "shard-animation"));
        var motion = variants.Select(variant => ShardReviewSession(content, variant.Item1, variant.Item2, assertions)).ToArray();
        var steps = 0;
        for (var frame = 0; frame < 72; frame++)
        {
            var targetStep = (frame + 1) * 60 / 24;
            while (steps < targetStep)
            {
                foreach (var session in motion) session.Update(1f / 60);
                steps++;
            }
            scenes.Add(CaptureShardReviewAtlas(motion, labels, font, $"shard-animation/frame-{frame:000}.png"));
        }
    }

    private GameSession ShardReviewSession(GameContent content, int stage, int branch,
        ICollection<string> assertions, bool immortal = true)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("shard_fan", new Vector2(250, 205)),
            $"Shard stage {stage}: a legal center foundation sits between the island's four lanes.", assertions);
        var tower = session.SelectedTower!;
        if (stage >= 2) Require(session.TryChooseTowerDoctrine(tower.Id, "shard_scatter"), "Shard review advances its authored doctrine.", assertions);
        if (stage >= 3) Require(session.TrySpecializeTower(tower.Id, tower.Definition.Specializations[branch].Id), "Shard review advances its authored specialization.", assertions);
        if (stage == 4) Require(session.TryUpgradeTower(tower.Id) && tower.IsApex, "Shard review advances to its authored Apex.", assertions);
        tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        Require(session.SpawnSandboxTargets("t3_brute", 4, 20, "Standard", immortal), "Shard review uses actual Sandbox targets.", assertions);
        var points = new[] { new Vector2(250, 110), new Vector2(390, 205), new Vector2(250, 300), new Vector2(110, 205) };
        for (var index = 0; index < points.Length; index++)
            session.Enemies[index].SetSandboxPathDistance(session.Map.Path.Project(points[index]).DistanceAlongPath, session.Map.Path);
        return session;
    }

    private void AssertShardVolleyLifecycle(GameContent content, UIManager ui, ICollection<string> assertions,
        ICollection<VisualVerificationScene> scenes)
    {
        var single = ShardReviewSession(content, 3, 0, assertions);
        var tower = single.Towers[0];
        var target = single.Enemies[0];
        single.Enemies.RemoveAll(enemy => enemy != target);
        tower.CooldownRemaining = .37f;
        Require(single.TryOverdriveTower(tower.Id) && single.Projectiles.Projectiles.Count == tower.Level.PelletCount &&
                single.Projectiles.Projectiles.All(shot => shot.Target == target && shot.Position == tower.Position) &&
                MathF.Abs(tower.CooldownRemaining - .37f) < .00001f,
            "Razorstorm launches every authored pellet at the lone target without changing the scheduled attack cooldown.", assertions);
        single.Projectiles.Update(.04f, single);
        tower.TickVisual(.04f);
        scenes.Add(Capture("52b-shard-repeated-target.png", ui, GameState.Playing, single));
        single.Projectiles.Update(1, single);
        Require(single.Projectiles.Projectiles.Count == 0, "Shard pellets cleanly expire after reaching an immortal Sandbox target.", assertions);

        var changed = ShardReviewSession(content, 4, 1, assertions, immortal: false);
        tower = changed.Towers[0];
        var primary = changed.TargetSelector.Select(tower.Position, changed.GetEffectiveRange(tower), tower.TargetMode, changed.Enemies)!;
        Require(new TowerSystem(changed.TargetSelector).TryFireImmediate(tower, changed), "Shard lifecycle begins a real surrounding-target volley.", assertions);
        changed.Projectiles.Update(.035f, changed);
        tower.TickVisual(.035f);
        primary.ApplyHealthDamage(primary.MaxHealth * 2);
        tower.TargetMode = TargetMode.Last;
        changed.Projectiles.Update(0, changed);
        Require(changed.Projectiles.Projectiles.Count > 0 && changed.Projectiles.Projectiles.All(shot => shot.Target != primary && !shot.IsExpired),
            "Dead-primary pellets expire while the surviving volley retains its original targets despite a target-mode change.", assertions);
        scenes.Add(Capture("52c-shard-dead-primary.png", ui, GameState.Playing, changed));
        checksumSafe();
        changed.ClearSandboxTargets();
        Require(changed.Projectiles.Projectiles.Count == 0 && changed.Enemies.Count == 0,
            "Clearing the Sandbox removes all Shard targets and in-flight discharge sources.", assertions);

        void checksumSafe()
        {
            var before = SessionChecksum.Compute(changed, 2);
            RenderPixels(ui, GameState.Playing, changed);
            Require(before == SessionChecksum.Compute(changed, 2),
                "A changed primary target cannot make Shard rendering mutate surviving projectiles.", assertions);
        }
    }

    private void AssertShardLaunchPixels(TowerDefinition definition, ICollection<string> assertions)
    {
        const int size = 112;
        var center = new Vector2(size * .5f);
        for (var stage = 1; stage <= 4; stage++)
        for (var direction = 0; direction < 4; direction++)
        {
            var ray = new Vector2(MathF.Cos(direction * MathHelper.PiOver2), MathF.Sin(direction * MathHelper.PiOver2));
            var active = Pixels(.15f, 18);
            var dark = Pixels(0, 18);
            var coreChanged = 0;
            var oppositeChanged = 0;
            var forwardChanged = 0;
            for (var pixel = 0; pixel < active.Length; pixel++)
            {
                if (active[pixel] == dark[pixel]) continue;
                var delta = new Vector2(pixel % size + .5f, pixel / size + .5f) - center;
                if (delta.LengthSquared() < 9) coreChanged++;
                var along = Vector2.Dot(delta, ray);
                if (along > 6 && along < 18) forwardChanged++;
                if (along < -6) oppositeChanged++;
            }
            Require(coreChanged > 0 && forwardChanged > 0 && oppositeChanged == 0,
                $"Shard stage {stage}: launch filament joins the core to the real shot direction {direction * 90}, with no false rear muzzle.", assertions);
            Require(Pixels(.15f, 60).SequenceEqual(dark),
                $"Shard stage {stage}: departing shots leave no launch filament beyond the near-emitter region.", assertions);

            Color[] Pixels(float flash, float distance)
            {
                using var target = new RenderTarget2D(GraphicsDevice, size, size, false, SurfaceFormat.Color,
                    DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                TowerAttackArt.DrawShardLaunch(_batch, _primitives, center, center + ray * distance,
                    definition.Visual.Radius, NightGridArt.TowerAccent(definition.Id), flash, true,
                    Math.Min(stage, 3), stage == 4);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[size * size];
                target.GetData(pixels);
                return pixels;
            }
        }
    }

    private VisualVerificationScene CaptureShardReviewAtlas(IReadOnlyList<GameSession> fixtures,
        IReadOnlyList<string> labels, SpriteFont font, string fileName)
    {
        const int width = 1920;
        const int height = 650;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "SHARD FAN / CENTRAL DISCHARGE", new Vector2(24, 14), .95f);
        MechanicalReviewLabel(font, "2.5x DETAIL ABOVE / NATIVE GAMEPLAY SIZE BELOW / AUTHORED TARGETS AND PELLET COUNTS",
            new Vector2(24, 52), .57f);
        for (var index = 0; index < fixtures.Count; index++)
        {
            var x = index * 320;
            _primitives.FillRect(_batch, new Rectangle(x + 8, 104, 304, height - 118), ColorPalette.Nullspace.Deck);
            MechanicalReviewLabel(font, labels[index], new Vector2(x + 24, 84), .62f);
            MechanicalReviewLabel(font, $"{fixtures[index].Towers[0].Level.PelletCount} PELLETS", new Vector2(x + 24, 354), .50f);
        }
        _batch.End();
        for (var index = 0; index < fixtures.Count; index++)
        {
            Draw(index, new Rectangle(index * 320 + 10, 106, 300, 232), new Vector2(index * 320 + 160, 222), 2.5f);
            Draw(index, new Rectangle(index * 320 + 10, 382, 300, 248), new Vector2(index * 320 + 160, 506), 1);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        void Draw(int index, Rectangle clip, Vector2 center, float scale)
        {
            GraphicsDevice.ScissorRectangle = clip;
            var tower = fixtures[index].Towers[0];
            var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X - tower.Position.X * scale,
                center.Y - tower.Position.Y * scale, 0);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, clipping, null, transform);
            _renderer.DrawCombatShowcase(_batch, _primitives, fixtures[index]);
            _batch.End();
        }
    }
}
