using System.Text.Json;
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
    private static readonly EnemySignalRole[] SupportReviewRoles =
        [EnemySignalRole.Restorer, EnemySignalRole.Bulwark, EnemySignalRole.Accelerator];

    private void AssertSupportPulsePresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var originalRenderer = _renderer;
        try
        {
            foreach (var role in SupportReviewRoles)
            {
                var fixture = BuildSupportPulseFixture(content, role);
                var session = fixture.Session;
                var before = session.Enemies.ToDictionary(enemy => enemy.Id, enemy => (enemy.Health, enemy.Shield));
                ActivateSupportFixture(fixture);
                var gained = session.Enemies.Where(enemy => role == EnemySignalRole.Restorer
                    ? enemy.Health > before[enemy.Id].Health : enemy.Shield > before[enemy.Id].Shield)
                    .Select(enemy => enemy.Id).ToHashSet();
                var received = session.Effects.Effects.Where(effect => effect.Kind == EffectKind.Flash &&
                    effect.Signal.Role == role && effect.Signal.SourceEnemyId == fixture.Source.Id)
                    .Select(effect => effect.Signal.TargetEnemyId).ToHashSet();
                Require(received.SetEquals(gained), $"{role}: recipient flashes exactly match actual health or shield grants.", assertions);
                Require(!session.Effects.Effects.Any(effect => effect.Kind == EffectKind.Beam && effect.Signal.Role == role),
                    $"{role}: support feedback uses no directed transfer beams.", assertions);
                Require(!received.Contains(fixture.Full.Id) && fixture.Far.All(enemy => !received.Contains(enemy.Id)),
                    $"{role}: full and out-of-range enemies receive no grant flash.", assertions);
                if (role == EnemySignalRole.Accelerator)
                {
                    Require(session.Effects.Effects.Count == 0 && fixture.Source.FormationSpeedMultiplier == 1 &&
                        session.Enemies.Any(enemy => enemy.FormationSpeedMultiplier > 1),
                        "Acceleration is a continuous nearby speed field without pulse events or self-acceleration.", assertions);
                }
                else
                {
                    var pulse = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Splash && effect.Signal.Role == role);
                    Require(gained.Count > 0 && gained.Count <= 7 && pulse.Signal.SourceEnemyId == fixture.Source.Id &&
                        pulse.Signal.TargetEnemyId == 0 && pulse.Radius == session.Challenge.CounterSupportRadius,
                        $"{role}: one source wave carries the actual range for a bounded set of successful grants.", assertions);
                    Require(MathF.Abs(fixture.Source.SignalAbilityCooldownRemaining - session.Challenge.CounterPressureInterval) < .001f,
                        $"{role}: presentation preserves the authored support cooldown.", assertions);
                    var full = BuildSupportPulseFixture(content, role, allFull: true);
                    ActivateSupportFixture(full);
                    Require(full.Session.Effects.Effects.Count == 0,
                        $"{role}: a pulse with no possible grant creates no success feedback.", assertions);
                }
            }

            foreach (var reduced in new[] { false, true })
            foreach (var role in SupportReviewRoles)
            {
                var fixture = BuildSupportPulseFixture(content, role);
                ActivateSupportFixture(fixture);
                _renderer = new GameRenderer { ReducedEffects = reduced };
                var ui = SupportReviewUi(content, font, display, fixture.Session);
                var elapsed = 0f;
                foreach (var age in reduced ? new[] { .15f } : role == EnemySignalRole.Accelerator
                             ? new[] { .15f, .75f, 1.5f } : new[] { .15f, .35f })
                {
                    while (elapsed + .001f < age)
                    {
                        fixture.Session.Update(1f / 60);
                        ui.AdvanceVisualTime(1f / 60);
                        elapsed += 1f / 60;
                    }
                    var checksum = SessionChecksum.Compute(fixture.Session, 561);
                    using var scene = RenderScene(ui, GameState.Playing, fixture.Session);
                    using var native = DownsampleEnemyLaneScene(scene);
                    scenes.Add(SaveTierScene(native, $"support-pulse-{role.ToString().ToLowerInvariant()}-{(reduced ? "reduced" : "full")}-{age:0.00}.png"));
                    Require(checksum == SessionChecksum.Compute(fixture.Session, 561),
                        $"{role} at {age:0.00}s ({(reduced ? "reduced" : "full")}): support rendering preserves synchronized state.", assertions);
                }
            }
            AssertAcceleratorRippleMotion(content, assertions);
        }
        finally { _renderer = originalRenderer; }
    }

    private void AssertAcceleratorRippleMotion(GameContent content, ICollection<string> assertions)
    {
        var fixture = BuildSupportPulseFixture(content, EnemySignalRole.Accelerator);
        var checksum = SessionChecksum.Compute(fixture.Session, 695);
        const float period = 1f / .46f;
        const int samples = 18;
        using var target = new RenderTarget2D(GraphicsDevice, 256, 256, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var baseline = CaptureRange(0, true);
        var reducedLater = CaptureRange(period * .5f, true);
        Require(baseline.SequenceEqual(reducedLater),
            "Reduced Effects holds the accelerator boundary steady without animated interior ripples.", assertions);
        var radii = new List<float>();
        var maximumChanged = 0;
        Color[]? initial = null;
        Color[]? final = null;
        for (var sample = 0; sample <= samples; sample++)
        {
            var pixels = CaptureRange(period * sample / samples, false);
            if (sample == 0) initial = pixels;
            if (sample == samples) final = pixels;
            var minimum = float.PositiveInfinity;
            var maximum = 0f;
            var radiusSum = 0f;
            var changed = 0;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (pixels[index] == baseline[index]) continue;
                var distance = Vector2.Distance(new Vector2(index % 256 + .5f, index / 256 + .5f), new Vector2(128));
                minimum = MathF.Min(minimum, distance);
                maximum = MathF.Max(maximum, distance);
                radiusSum += distance;
                changed++;
            }
            maximumChanged = Math.Max(maximumChanged, changed);
            Require(changed == 0 || minimum > 10 && maximum <= fixture.Session.Challenge.CounterSupportRadius + 1 &&
                    maximum - minimum < fixture.Session.Challenge.CounterSupportRadius * .18f,
                $"Accelerator at {sample}/{samples} of its cycle draws one narrow hollow ripple wholly inside the fixed support radius.", assertions);
            radii.Add(changed > 0 ? radiusSum / changed : float.NaN);
        }
        var outwardSteps = 0;
        var cycleRestarts = 0;
        for (var index = 1; index < radii.Count; index++)
        {
            if (!float.IsFinite(radii[index]) || !float.IsFinite(radii[index - 1])) continue;
            var change = radii[index] - radii[index - 1];
            if (change > 1) outwardSteps++;
            else if (change < -20) cycleRestarts++;
        }
        Require(maximumChanged > 150 && outwardSteps >= 12 && cycleRestarts <= 1,
            "The visible accelerator ripple expands continuously, with at most one restart over its 2.17-second cycle.", assertions);
        Require(initial is not null && final is not null &&
                initial.Zip(final, (first, last) => first != last).Count(changed => changed) < 64,
            "The accelerator repeats its visual state every 2.17 seconds without accumulating phase or shape discontinuities.", assertions);
        Require(checksum == SessionChecksum.Compute(fixture.Session, 695),
            "Sampling continuous accelerator ripples leaves synchronized state unchanged.", assertions);

        Color[] CaptureRange(float time, bool reduced)
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Transparent);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            EnemySignalArt.DrawSupportRange(_batch, _primitives, fixture.Session, fixture.Source,
                new Vector2(128), reduced, time);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[256 * 256];
            target.GetData(pixels);
            return pixels;
        }
    }

    private static SupportPulseFixture BuildSupportPulseFixture(GameContent content, EnemySignalRole role, bool allFull = false)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.ConfigureCounterPressureSimulation(supportEnabled: true, attackersEnabled: false);
        const float distance = 1093;
        session.SpawnEnemy("t1_crawler", 1, 1, signalRole: role);
        var source = session.Enemies[^1];
        source.SetSandboxPathDistance(distance, session.Map.Path);
        source.ArmSignalAbility(100);
        var offsets = new[] { -128f, -80, -56, -32, 32, 56, 80, 128 };
        var families = new[] { "t2_runner", "t1_crawler", "t3_brute", "t1_crawler", "t4_aegis", "t1_crawler", "t3_brute", "t2_runner" };
        for (var index = 0; index < offsets.Length; index++)
        {
            session.SpawnEnemy(families[index], 1, 1);
            session.Enemies[^1].SetSandboxPathDistance(distance + offsets[index], session.Map.Path);
        }
        var full = session.Enemies[2];
        foreach (var enemy in session.Enemies)
        {
            enemy.AbsorbShield(enemy.Shield);
            if (!allFull && enemy != full) enemy.ApplyHealthDamage(enemy.MaxHealth * .35f);
            if (role == EnemySignalRole.Bulwark && (allFull || enemy == full))
            {
                var capacity = enemy.Definition.Shield + enemy.MaxHealth * session.Challenge.CounterShieldCapacityFraction;
                enemy.GrantShield(capacity, capacity);
            }
        }
        session.RefreshEnemySignalFormation();
        session.Effects.Clear();
        return new(session, source, full, [session.Enemies[1], session.Enemies[^1]]);
    }

    private static void ActivateSupportFixture(SupportPulseFixture fixture)
    {
        if (fixture.Source.SignalRole == EnemySignalRole.Accelerator)
            fixture.Session.RefreshEnemySignalFormation();
        else
        {
            fixture.Source.ArmSignalAbility(0);
            fixture.Session.TryActivateEnemySignal(fixture.Source);
        }
    }

    private static UIManager SupportReviewUi(GameContent content, SpriteFont font, SpriteFont display, GameSession session)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        ui.HandleGameplayInput(Pointer(0, 0), session);
        return ui;
    }

    private void CaptureSupportPulseMotion(GameContent content, SpriteFont font, SpriteFont display)
    {
        const int fps = 60;
        const int frameCount = 360;
        var fixtures = SupportReviewRoles.Select(role => BuildSupportPulseFixture(content, role)).ToArray();
        var interfaces = fixtures.Select(fixture => SupportReviewUi(content, font, display, fixture.Session)).ToArray();
        var renderers = fixtures.Select(_ => new GameRenderer()).ToArray();
        var originalRenderer = _renderer;
        var frames = new List<object>();
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "support-motion"));
        try
        {
            for (var frame = 0; frame < frameCount; frame++)
            {
                var nativeFrames = new List<RenderTarget2D>();
                try
                {
                    var checksums = new List<string>();
                    for (var index = 0; index < fixtures.Length; index++)
                    {
                        var fixture = fixtures[index];
                        if (frame == 30) ActivateSupportFixture(fixture);
                        var checksum = SessionChecksum.Compute(fixture.Session, frame);
                        _renderer = renderers[index];
                        using var scene = RenderScene(interfaces[index], GameState.Playing, fixture.Session);
                        nativeFrames.Add(DownsampleEnemyLaneScene(scene));
                        if (checksum != SessionChecksum.Compute(fixture.Session, frame))
                            throw new InvalidOperationException("Support motion rendering changed synchronized state.");
                        checksums.Add(checksum.ToString());
                    }
                    using var board = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
                        SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                    GraphicsDevice.SetRenderTarget(board);
                    GraphicsDevice.Clear(ColorPalette.Canvas);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                    StatusLabel(font, "GAUNTLET SUPPORT / NATIVE GAMEPLAY SCALE", new Vector2(20, 20), .85f);
                    StatusLabel(font, $"ACTUAL MOVING ENEMIES / HEAL AND SHIELD: 5s CADENCE / {frame / (float)fps:0.00}s", new Vector2(20, 56), .48f);
                    var labels = new[] { "RESTORER / GREEN WAVE", "BULWARK / BLUE WAVE", "ACCELERATOR / WHITE RIPPLES" };
                    for (var index = 0; index < fixtures.Length; index++)
                    {
                        var x = 20 + index * 420;
                        StatusLabel(font, labels[index], new Vector2(x, 87), .58f);
                        _batch.Draw(nativeFrames[index], new Rectangle(x, 116, 400, 550),
                            new Rectangle(440, 64, 400, 550), Color.White);
                    }
                    StatusLabel(font, "HEALTH AND SHIELD RESPOND TO ACTUAL GRANTS / ACCELERATION REMAINS CONTINUOUS",
                        new Vector2(20, 690), .49f);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    var saved = SaveTierScene(board, $"support-motion/frame-{frame:000}.png");
                    frames.Add(new { frame, seconds = frame / (float)fps, saved.File, saved.Sha256, checksums });
                }
                finally { foreach (var native in nativeFrames) native.Dispose(); }
                for (var index = 0; index < fixtures.Length; index++)
                {
                    fixtures[index].Session.Update(1f / fps);
                    interfaces[index].AdvanceVisualTime(1f / fps);
                }
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "support-motion.json"), JsonSerializer.Serialize(new
        {
            width = 1280, height = 720, fps, frameCount, pulseSeconds = .5f,
            description = "Three independent live support fixtures with fixed native-size gameplay crops. Rendering is checksum-neutral on every frame.",
            frames
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Support motion: {frameCount} native frames captured.");
    }

    private sealed record SupportPulseFixture(GameSession Session, EnemyInstance Source, EnemyInstance Full, EnemyInstance[] Far);
}
