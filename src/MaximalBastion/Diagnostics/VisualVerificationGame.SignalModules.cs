using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertSignalModules(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var roles = Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None).ToArray();
        var conditions = new[] { "CLEAR", "WEAKEN", "FROST", "ALL CONDITIONS" };
        var animationSamples = new List<StatusReviewEnemy>();
        foreach (var definition in content.Enemies.Values)
        {
            var samples = new List<StatusReviewEnemy>();
            foreach (var role in roles)
            {
                var baseline = SignalModuleSample(content, definition.Id, role, "CLEAR");
                foreach (var condition in conditions)
                    samples.Add(SignalModuleSample(content, definition.Id, role, condition));
                foreach (var reduced in new[] { false, true })
                foreach (var scale in new[] { 1, 3 })
                {
                    var clear = SignalModulePixels(baseline, .43f, reduced, scale);
                    var mask = SignalHardwareMask(baseline, new Vector2(64), 128, 128, scale);
                    Require(mask.Count(opaque => opaque) >= 12 * scale * scale,
                        $"{definition.Id} {role}: the inset signal reactor has substantial solid coverage at field size.", assertions);
                    foreach (var condition in conditions.Skip(1))
                    {
                        var sample = samples.Last(candidate => candidate.Label == condition);
                        var pixels = SignalModulePixels(sample, .43f, reduced, scale);
                        Require(pixels.Zip(clear, (left, right) => left != right).Count(changed => changed) >= 8,
                            $"{definition.Id} {role} {condition}: temporary conditions remain visible across the complete enemy body at {scale}x scale.", assertions);
                    }
                    var identity = SignalModulePixels(baseline, .43f, reduced, scale, identityOnly: true);
                    var visiblePixels = identity.Count(pixel => pixel != ColorPalette.Canvas);
                    Require(visiblePixels >= 18 * scale * scale,
                        $"{definition.Id} {role}: the powered reactor covers {visiblePixels} visible pixels at {scale}x scale (minimum {18 * scale * scale}).", assertions);
                    Require(SignalModulePixels(baseline, .43f, true, scale, identityOnly: true)
                            .SequenceEqual(SignalModulePixels(baseline, 1.71f, true, scale, identityOnly: true)),
                        $"{definition.Id} {role}: Reduced Effects keeps a steady signal identity.", assertions);
                }
                var expired = SignalModuleSample(content, definition.Id, role, "ALL CONDITIONS");
                expired.Enemy.StatusEffects.Update(7);
                expired.Enemy.AbsorbShield(expired.Enemy.Shield);
                expired.Enemy.RestoreHealth(expired.Enemy.MaxHealth);
                expired = expired with { Repair = false, ShieldPulse = false };
                Require(SignalModulePixels(baseline, .43f, true, 1)
                        .SequenceEqual(SignalModulePixels(expired, .43f, true, 1)),
                    $"{definition.Id} {role}: conditions expire cleanly without removing signal identity.", assertions);
                if (definition.Id == "t1_crawler")
                    animationSamples.Add(samples[^1]);
            }
            scenes.Add(CaptureSignalModuleAtlas(samples, font, definition.DisplayName,
                $"58-signal-modules-{definition.Id}.png"));
        }

        foreach (var role in roles)
        {
            var sample = SignalModuleSample(content, "t2_runner", role, "CLEAR");
            var reference = SignalModulePixels(sample, .43f, true, 1, identityOnly: true);
            foreach (var heading in new[] { -.9f, .9f, 2.5f, -2.5f })
                Require(reference.Zip(SignalModulePixels(sample with { Heading = heading }, .43f, true, 1,
                            identityOnly: true), (a, b) => a != b).Count(changed => changed) >= 8,
                    $"{role}: the recessed reactor follows the Runner's heading instead of remaining a screen-facing label.", assertions);
            sample.Enemy.ApplyHealthDamage(sample.Enemy.MaxHealth);
            Require(SignalModulePixels(sample, .43f, false, 1, identityOnly: true).All(pixel => pixel == ColorPalette.Canvas),
                $"{role}: a destroyed enemy leaves no signal reactor.", assertions);
            sample = SignalModuleSample(content, "t2_runner", role, "CLEAR");
            sample.Enemy.UpdateMovement(10000, sample.Session.Map.Path);
            Require(sample.Enemy.HasEscaped && SignalModulePixels(sample, .43f, false, 1, identityOnly: true)
                    .All(pixel => pixel == ColorPalette.Canvas),
                $"{role}: an escaped enemy leaves no signal reactor.", assertions);
        }
        foreach (var definition in content.Enemies.Values)
        {
            var ordinary = SignalModuleSample(content, definition.Id, EnemySignalRole.None, "ALL CONDITIONS");
            Require(SignalModulePixels(ordinary, .43f, false, 1, identityOnly: true)
                    .All(pixel => pixel == ColorPalette.Canvas),
                $"{definition.Id}: an ordinary enemy has no additional signal overlay.", assertions);
        }
        AssertSignalGemFaces(content, font, assertions, scenes);
        AssertSignalHardwareClearance(content, assertions);
        AssertSignalModuleFieldProtection(content, font, display, assertions, scenes);
        CaptureSignalModuleCombat(content, font, display, assertions, scenes);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_SIGNAL_MODULE_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "signal-modules-animation"));
        for (var frame = 0; frame < 72; frame++)
            scenes.Add(CaptureSignalModuleAtlas(animationSamples, font, "SIGNALS WITH STACKED CONDITIONS",
                $"signal-modules-animation/detail-{frame:000}.png", frame / 24f, singleRow: true));
    }

    private static StatusReviewEnemy SignalModuleSample(GameContent content, string id,
        EnemySignalRole role, string condition)
    {
        var sample = StatusSample(content, id, condition, role) with { Heading = -.55f };
        var enemy = sample.Enemy;
        if (condition is "WEAKEN" or "ALL CONDITIONS")
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Duration = 6, Magnitude = .3f, SourceId = 12 });
        if (condition is "FROST" or "ALL CONDITIONS")
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 6, Magnitude = .5f, SourceId = 12 });
        if (condition == "ALL CONDITIONS")
        {
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Duration = 6, Magnitude = 8, SourceId = 12 });
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.ArmorBreak, Duration = 6, Magnitude = 4, SourceId = 12 });
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 6, Magnitude = 1, SourceId = 12 });
            enemy.GrantShield(30, 30);
            enemy.ApplyHealthDamage(enemy.MaxHealth * .15f);
            enemy.TickRuntimeTimers(1.1f);
            return sample with { Repair = true, ShieldPulse = true };
        }
        return sample;
    }

    private Color[] SignalModulePixels(StatusReviewEnemy sample, float time, bool reduced,
        int scale, bool identityOnly = false)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 128 * scale, 128 * scale,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
        DrawSignalModuleSample(sample, new Vector2(64), time, reduced, identityOnly);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private void DrawSignalModuleSample(StatusReviewEnemy sample, Vector2 center, float time,
        bool reduced, bool identityOnly = false)
    {
        var enemy = sample.Enemy;
        var radius = EnemyModelArt.BodyRadius(enemy);
        if (!identityOnly)
        {
            EnemyModelArt.Draw(_batch, _primitives, enemy, center, sample.Heading, time, reduced);

            EnemyDebuffAuraArt.Draw(_batch, _primitives, sample.Session, enemy, center, radius, time, reduced);
            EnemyStatusArt.DrawDurability(_batch, _primitives, enemy, center, radius, sample.Heading, time, reduced);
            EnemyStunArt.Draw(_batch, _primitives, enemy, center, radius, sample.Heading, time, reduced);
        }
        else EnemySignalArt.DrawIdentity(_batch, _primitives, enemy, center, radius, time, reduced, sample.Heading);
    }

    private bool[] SignalHardwareMask(StatusReviewEnemy sample, Vector2 position, int width, int height, int scale)
    {
        using var target = new RenderTarget2D(GraphicsDevice, width * scale, height * scale, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Transparent);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
        EnemySignalArt.DrawIdentity(_batch, _primitives, sample.Enemy, position,
            EnemyModelArt.BodyRadius(sample.Enemy), 0, true, sample.Heading);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels.Select(pixel => pixel.A == 255).ToArray();
    }

    private void AssertSignalHardwareClearance(GameContent content, ICollection<string> assertions)
    {
        var center = new Vector2(64);
        foreach (var definition in content.Enemies.Values)
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
        foreach (var heading in new[] { 0f, .8f, 1.7f, 3.1f })
        {
            var sample = EnemyRankSample(content, definition.Id, rank, role) with { Heading = heading };
            var radius = EnemyModelArt.BodyRadius(sample.Enemy);
            var pixels = SignalModulePixels(sample, 0, true, 1, identityOnly: true);
            var bounds = EnemySignalArt.IdentityBounds(sample.Enemy, center, radius, heading);
            var contained = true;
            var coreEnvelope = EnemyModelArt.CoreRadius(sample.Enemy) * 1.12f + 2;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (pixels[index] == ColorPalette.Canvas) continue;
                var at = new Vector2(index % 128 + .5f, index / 128 + .5f);
                if (Vector2.DistanceSquared(at, center) > coreEnvelope * coreEnvelope) contained = false;
                if (!bounds.Contains(index % 128, index / 128)) contained = false;
            }
            Require(contained,
                $"{definition.Id} {rank} {role} at {heading:0.0}rad: signal identity remains inside the central reactor socket.", assertions);
        }
    }

    private VisualVerificationScene CaptureSignalModuleAtlas(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, string title, string fileName, float time = .43f, bool singleRow = false)
    {
        var columns = singleRow ? samples.Count : 4;
        var rows = (samples.Count + columns - 1) / columns;
        const int cellWidth = 340;
        const int cellHeight = 228;
        using var target = new RenderTarget2D(GraphicsDevice, columns * cellWidth, 108 + rows * cellHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, title.ToUpperInvariant(), new Vector2(24, 16), .95f);
        StatusLabel(font, "2x DETAIL ABOVE / FIELD SIZE: FULL LEFT, REDUCED RIGHT", new Vector2(24, 56), .62f);
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 108 + index / columns * cellHeight;
            _primitives.FillRect(_batch, new Rectangle(x + 8, y, cellWidth - 16, cellHeight - 8),
                ColorPalette.Panel);
            StatusLabel(font, samples[index].Enemy.SignalRole.ToString().ToUpperInvariant(), new Vector2(x + 18, y + 10), .57f);
            StatusLabel(font, samples[index].Label, new Vector2(x + 18, y + 36), .47f);
        }
        _batch.End();
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 108 + index / columns * cellHeight;
            DrawAt(new Vector2(x + cellWidth * .5f, y + 110), 2, false);
            DrawAt(new Vector2(x + cellWidth * .32f, y + 187), 1, false);
            DrawAt(new Vector2(x + cellWidth * .68f, y + 187), 1, true);
            void DrawAt(Vector2 center, float scale, bool reduced)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X, center.Y, 0));
                DrawSignalModuleSample(samples[index], Vector2.Zero, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private void AssertSignalModuleFieldProtection(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
            foreach (var reduced in new[] { false, true })
            {
                var sample = SignalModuleSample(content, "t5_regenerator", role, "CLEAR");
                var session = sample.Session;
                _renderer.ReducedEffects = reduced;
                _ = RenderPixels(ui, GameState.Playing, session);
                foreach (var status in new[] { StatusType.Slow, StatusType.Burn, StatusType.ArmorBreak, StatusType.Exposed })
                    sample.Enemy.ApplyStatus(new StatusApplication { Type = status, Duration = 6,
                        Magnitude = status is StatusType.Burn or StatusType.ArmorBreak ? 4 : .4f });
                sample.Enemy.GrantShield(30, 30);
                sample.Enemy.SetFormationSpeedMultiplier(1.18f);
                sample.Enemy.ApplyHealthDamage(100);
                sample.Enemy.TickRuntimeTimers(1.1f);
                session.Effects.AddFlash(sample.Enemy.Position, ColorPalette.Paper, .3f, 50);
                session.Effects.AddBeam(sample.Enemy.Position - new Vector2(90, 0),
                    sample.Enemy.Position + new Vector2(90, 0), ColorPalette.Violet, .3f);
                session.Effects.AddImpact(sample.Enemy.Position, ColorPalette.Burn, 40);
                var checksum = SessionChecksum.Compute(session, 0);
                _ = RenderPixels(ui, GameState.Playing, session);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{role}: the complete signal body with stacked conditions and crossing effects does not mutate synchronized gameplay.", assertions);
                scenes.Add(Capture($"58a-signal-protection-{role.ToString().ToLowerInvariant()}-{(reduced ? "reduced" : "full")}.png",
                    ui, GameState.Playing, session));
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private void CaptureSignalModuleCombat(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
        for (var x = region.Left + 24; x < region.Right - 16; x += 24) candidates.Add(new Vector2(x, y));
        var definitions = content.Towers.Values.ToArray();
        for (var index = 0; index < 15; index++)
        {
            var definition = definitions[index % definitions.Length];
            Require(candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 360)))
                    .Any(point => session.TryPlaceTower(definition.Id, point)),
                $"Signal combat places {definition.DisplayName} on a legal foundation.", assertions);
            var tower = session.SelectedTower!;
            if (index >= 5)
            {
                session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id);
                session.TrySpecializeTower(tower.Id, definition.Specializations[0].Id);
            }
            if (index % 5 == 0) tower.ActivateOverdrive();
        }
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var bodies = content.Enemies.Values.ToArray();
        for (var index = 0; index < 36; index++)
        {
            session.SpawnEnemy(bodies[index % bodies.Length].Id, 100, .35f,
                signalRole: (EnemySignalRole)(index % 6));
            var enemy = session.Enemies[^1];
            enemy.SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / 36, session.Map.Path);
            enemy.ApplyHealthDamage(enemy.MaxHealth * .12f);
            enemy.TickRuntimeTimers(1.1f);
            foreach (var status in new[] { StatusType.Slow, StatusType.Burn, StatusType.ArmorBreak, StatusType.Exposed })
                enemy.ApplyStatus(new StatusApplication { Type = status, Duration = 8,
                    Magnitude = status == StatusType.ArmorBreak ? 4 : status == StatusType.Burn ? 4 : .35f,
                    SourceId = session.Towers[0].Id });
            enemy.ArmSignalAbility(0);
        }
        for (var step = 0; step < 12; step++) session.Update(1f / 60);
        Require(Enum.GetValues<EnemySignalRole>().All(role => session.Enemies.Any(enemy => enemy.SignalRole == role)),
            "Dense signal combat includes all five signals and ordinary enemies under overlapping conditions.", assertions);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var checksum = SessionChecksum.Compute(session, 12);
                scenes.Add(Capture($"58b-signal-combat-{(reduced ? "reduced" : "full")}.png", ui, GameState.Playing, session));
                Require(checksum == SessionChecksum.Compute(session, 12),
                    $"Dense {(reduced ? "Reduced" : "Full")} signal rendering preserves synchronized gameplay.", assertions);
            }
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_SIGNAL_MODULE_ANIMATION") != "1") return;
            _renderer.ReducedEffects = false;
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "signal-modules-animation"));
            for (var frame = 0; frame < 72; frame++)
            {
                for (var step = 0; step < 5; step++) session.Update(1f / 120);
                ui.AdvanceVisualTime(1f / 24);
                scenes.Add(Capture($"signal-modules-animation/combat-{frame:000}.png", ui, GameState.Playing, session));
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }
}
