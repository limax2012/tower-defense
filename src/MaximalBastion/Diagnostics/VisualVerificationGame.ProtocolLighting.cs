using System.Diagnostics;
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
    private void AssertProtocolLighting(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var definition in content.Towers.Values)
        for (var stage = 1; stage <= 4; stage++)
        foreach (var reduced in new[] { false, true })
        {
            var minimumExterior = int.MaxValue;
            var minimumSectors = 8;
            Color[]? first = null;
            var stable = true;
            var outsideRadius = definition.Visual.Radius * 1.2f;
            for (var phase = 0; phase < 12; phase++)
            {
                var time = .13f + phase * .37f;
                var normal = ProtocolLightingPixels(definition, stage, time, reduced, 0);
                var active = ProtocolLightingPixels(definition, stage, time, reduced, 1);
                var sectors = new int[8];
                var exterior = 0;
                for (var index = 0; index < normal.Length; index++)
                {
                    var delta = Math.Abs(normal[index].R - active[index].R) +
                        Math.Abs(normal[index].G - active[index].G) + Math.Abs(normal[index].B - active[index].B);
                    if (delta < 8) continue;
                    var offset = new Vector2(index % 112 + .5f - 56, index / 112 + .5f - 56);
                    if (offset.LengthSquared() <= outsideRadius * outsideRadius) continue;
                    exterior++;
                    var angle = MathF.Atan2(offset.Y, offset.X) + MathHelper.Pi;
                    sectors[Math.Min(7, (int)(angle / MathHelper.PiOver4))]++;
                }
                minimumExterior = Math.Min(minimumExterior, exterior);
                minimumSectors = Math.Min(minimumSectors, sectors.Count(count => count >= 2));
                if (first is not null) stable &= first.SequenceEqual(active);
                first ??= active;
            }
            Require(minimumExterior >= 48 && minimumSectors == 8,
                $"{definition.DisplayName} stage {stage}: {(reduced ? "Reduced" : "Full")} Protocol surrounds the assembly throughout twelve native-scale phases (minimum {minimumExterior} perceptible exterior pixels across {minimumSectors}/8 sectors).",
                assertions);
            Require(stable == reduced,
                $"{definition.DisplayName} stage {stage}: Protocol motion follows the effects setting without changing its identity.", assertions);
        }
        foreach (var reduced in new[] { false, true })
        for (var page = 0; page < 2; page++)
            scenes.Add(CaptureProtocolLightingAtlas(content, font, reduced, page));
        AssertProtocolRuntimeLifecycle(content, font, display, assertions, scenes);
        CapturePoweredBattlefields(content, font, display, assertions, scenes);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROTOCOL_ANIMATION") != "1") return;
        CaptureProtocolLightingMotion(content, font, assertions, scenes);
    }

    private Color[] ProtocolLightingPixels(TowerDefinition definition, int stage, float time,
        bool reduced, float strength)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 112, 112, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Rainline.Deck);
        DrawProtocolLightingSample(definition, stage, new Vector2(56), 1, -.55f, time, reduced, strength);
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[112 * 112];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureProtocolLightingAtlas(GameContent content, SpriteFont font,
        bool reduced, int page)
    {
        const int width = 1920;
        const int header = 112;
        const int rowHeight = 320;
        var definitions = content.Towers.Values.Skip(page * 5).Take(5).ToArray();
        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        using var target = new RenderTarget2D(GraphicsDevice, width, header + rowHeight * definitions.Length + 12,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, $"POWERED / PROTOCOL  |  {(reduced ? "REDUCED" : "FULL")} EFFECTS", new Vector2(24, 16), .95f);
        MechanicalReviewLabel(font, "NORMAL LEFT / ACTIVE RIGHT     1.8x DETAIL ABOVE / FIELD SIZE BELOW", new Vector2(24, 56), .60f);
        for (var stage = 1; stage <= 4; stage++)
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}", new Vector2(Column(stage) - 18, 86), .65f);
        for (var row = 0; row < definitions.Length; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8), decks[(page * 5 + row) % decks.Length]);
            MechanicalReviewLabel(font, definitions[row].DisplayName, new Vector2(28, y + 142), .59f);
        }
        _batch.End();
        for (var row = 0; row < definitions.Length; row++)
        for (var stage = 1; stage <= 4; stage++)
        for (var active = 0; active < 2; active++)
        {
            var x = Column(stage) + (active == 0 ? -74 : 74);
            var y = header + row * rowHeight;
            DrawProtocolLightingSample(definitions[row], stage, new Vector2(x, y + 100), 1.8f,
                -.55f, .61f, reduced, active);
            DrawProtocolLightingSample(definitions[row], stage, new Vector2(x, y + 250), 1,
                -.55f, .61f, reduced, active);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, $"54-protocol-lighting-{(reduced ? "reduced" : "full")}-{page + 1}.png");

        static int Column(int stage) => 430 + (stage - 1) * 420;
    }

    private void DrawProtocolLightingSample(TowerDefinition definition, int stage, Vector2 at,
        float scale, float aim, float time, bool reduced, float strength)
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
        if (strength > 0)
            TowerProtocolArt.DrawEnvelope(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, Math.Min(stage, 3), stage == 4, time, reduced, strength);
        NightGridArt.Tower(_batch, _primitives, Vector2.Zero, definition.Visual.Radius, definition.Id,
            Math.Min(stage, 3), aim, reduced ? 0 : time, lights: !reduced, apex: stage == 4);
        if (strength > 0)
            TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, Math.Min(stage, 3), stage == 4, aim, time, reduced, strength: strength, envelope: false);
        _batch.End();
    }

    private void AssertProtocolRuntimeLifecycle(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var definition in content.Towers.Values)
            foreach (var reduced in new[] { false, true })
            {
                var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
                Require(session.TryPlaceTower(definition.Id, new Vector2(270, 350)),
                    $"{definition.Id}: Protocol lifecycle has a legal operating foundation.", assertions);
                var tower = session.SelectedTower!;
                AdvanceProtocolReviewTier(session, tower, 3, assertions);
                tower.TickVisual(1);
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                _renderer.ReducedEffects = reduced;
                var bounds = new Rectangle(214, 294, 112, 112);
                var normal = Read();
                tower.ActivateOverdrive();
                Require(CountChangedPixels(normal, Read(), bounds) >= 96,
                    $"{definition.Id}: Protocol ignition is immediately visible before its attack animation advances.", assertions);
                tower.TickVisual(.25f);
                var active = Read();
                Require(tower.IsOverdriven && CountChangedPixels(normal, active, bounds) >= 96,
                    $"{definition.Id}: actual Protocol duration drives the {(reduced ? "Reduced" : "Full")} field presentation.", assertions);
                tower.TickVisual(tower.OverdriveRemaining - .04f);
                var closing = Read();
                Require(tower.IsOverdriven && CountChangedPixels(normal, closing, bounds) >= 96,
                    $"{definition.Id}: Protocol remains visibly active in the final portion of its real duration.", assertions);
                tower.TickVisual(.05f);
                Require(!tower.IsOverdriven && CountChangedPixels(normal, Read(), bounds) == 0,
                    $"{definition.Id}: Protocol expiry exactly restores the normal powered appearance without residual particles.", assertions);

                Color[] Read()
                {
                    var checksum = SessionChecksum.Compute(session, 0);
                    var pixels = RenderPixels(ui, GameState.Playing, session);
                    Require(checksum == SessionChecksum.Compute(session, 0),
                        $"{definition.Id}: Protocol presentation does not mutate combat state.", assertions);
                    return pixels;
                }
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private void CapturePoweredBattlefields(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var definitions = content.Towers.Values.ToArray();
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var mapId in new[] { "foundry_loop", "crosswind_basin", "prism_circuit", "relay_divide" })
            {
                var session = new GameSession(content, mapId, "normal", "sandbox_lab");
                if (mapId == "relay_divide")
                    Require(session.TryPlaceTower("frost_spire", new Vector2(285, 330)) &&
                            session.TryPlaceTower("signal_beacon", new Vector2(285, 390)),
                        "Powered Helix review includes a Frost receiver on the Amplifier Node and its nearby Beacon.", assertions);
                var candidates = new List<Vector2>();
                foreach (var region in session.Map.BuildableRegions)
                for (var y = region.Top + 24; y < region.Bottom - 16; y += 26)
                for (var x = region.Left + 24; x < region.Right - 16; x += 26)
                    candidates.Add(new Vector2(x, y));
                candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 350))).ToList();
                for (var index = session.Towers.Count; index < 20; index++)
                {
                    var definition = definitions[index % definitions.Length];
                    Require(candidates.Any(point => session.TryPlaceTower(definition.Id, point)),
                        $"{mapId}: powered battlefield places legal {definition.DisplayName} machinery.", assertions);
                    AdvanceProtocolReviewTier(session, session.SelectedTower!, index % 3 + 1, assertions);
                }
                foreach (var tower in session.Towers) tower.TickVisual(1);
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                session.Update(0);
                while (ui.SelectedSandboxWave < 20)
                    ui.HandleGameplayInput(Pointer(0, 0) with { SandboxWaveNextPressed = true }, session);
                Require(session.Towers.All(tower => !tower.IsOverdriven),
                    $"{mapId}: normal lighting review contains twenty operational defenses without a single active Protocol.", assertions);
                CaptureStates("normal");
                var receiver = session.Towers.First(tower => !tower.IsSupport && session.GetSupportBuff(tower).IsActive);
                session.TrySelectTower(receiver.Id);
                session.HandleInspectionInput(Pointer(receiver.Position.X, receiver.Position.Y));
                Require(session.TryToggleAutoProtocol(receiver.Id) && session.TryOverdriveTower(receiver.Id),
                    $"{mapId}: one supported defense enters its real Protocol with selection and Auto enabled.", assertions);
                if (mapId == "relay_divide" && Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROTOCOL_ANIMATION") == "1")
                    CaptureProtocolBattlefieldMotion(session, ui, "protocol-quiet-animation", 48, assertions, scenes);
                Require(session.SpawnSandboxTargets("t3_brute", 24, 20, "Elite", true) && session.StartSandboxWave(20),
                    $"{mapId}: active lighting review runs a real Sandbox wave with durable targets.", assertions);
                for (var index = 0; index < session.Enemies.Count; index++)
                    session.Enemies[index].SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / session.Enemies.Count, session.Map.Path);
                for (var step = 0; step < 30; step++) session.Update(1f / 60);
                Require(receiver.IsOverdriven && session.Towers.Count(tower => tower.IsOverdriven) == 1,
                    $"{mapId}: the crowded combat scene keeps nineteen towers operational without Protocol and one visibly active.", assertions);
                CaptureStates("combat");
                if (mapId == "relay_divide")
                {
                    if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROTOCOL_ANIMATION") == "1")
                        CaptureProtocolBattlefieldMotion(session, ui, "protocol-crowded-animation", 72, assertions, scenes);
                    MeasureProtocolFieldRendering(content, session, ui, assertions, scenes);
                }

                void CaptureStates(string state)
                {
                    var checksum = SessionChecksum.Compute(session, 30);
                    foreach (var reduced in new[] { false, true })
                    {
                        _renderer.ReducedEffects = reduced;
                        scenes.Add(Capture($"55-powered-{mapId}-{state}-{(reduced ? "reduced" : "full")}.png", ui, GameState.Playing, session));
                    }
                    Require(checksum == SessionChecksum.Compute(session, 30),
                        $"{mapId}: {state} lighting and interaction rendering preserve synchronized state.", assertions);
                }
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private void MeasureProtocolFieldRendering(GameContent content, GameSession layout, UIManager ui,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, layout.Map.Definition.Id, "normal", "sandbox_lab");
        foreach (var source in layout.Towers)
        {
            Require(session.TryPlaceTower(source.Definition.Id, source.Position),
                "Protocol render benchmark reproduces the legal twenty-tower Helix layout.", assertions);
            AdvanceProtocolReviewTier(session, session.SelectedTower!, 4, assertions);
        }
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        Require(session.SpawnSandboxTargets("t3_brute", session.Towers.Count, 20, "Elite", true),
            "Concurrent Apex review creates legal priority targets for automatic activation.", assertions);
        for (var index = 0; index < session.Towers.Count; index++)
            session.Enemies[index].SetSandboxPathDistance(session.Map.Path.Project(session.Towers[index].Position).DistanceAlongPath, session.Map.Path);
        for (var step = 0; step < 20; step++) session.Update(1f / 60);
        var activeCount = session.Towers.Count(tower => tower.IsApex && tower.IsOverdriven);
        Require(activeCount >= 12 && session.AutoOverdriveTowerId == 0 && session.OverdriveCooldownRemaining == 0,
            $"Concurrent Apex review automatically activates {activeCount}/20 advanced defenses through real combat eligibility without occupying the manual Protocol slot.", assertions);
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var pixel = new Color[1];
        var priorReduced = _renderer.ReducedEffects;
        _renderer.ReducedEffects = false;
        try
        {
            scenes.Add(Capture("56-concurrent-apex-fields-full.png", ui, GameState.Playing, session));
            _renderer.ReducedEffects = true;
            scenes.Add(Capture("56-concurrent-apex-fields-reduced.png", ui, GameState.Playing, session));
            _renderer.ReducedEffects = false;
            var checksum = SessionChecksum.Compute(session, 0);
            var (normal, active) = Measure();
            Require(checksum == SessionChecksum.Compute(session, 0),
                "Paired field-hidden and active-field draws preserve the same concurrent Apex combat simulation.", assertions);
            Require(double.IsFinite(normal) && double.IsFinite(active) && normal > 0 && active > 0,
                $"Twenty-tower Helix with {activeCount} legitimate concurrent Apex Protocols at {GameConstants.RenderWidth}x{GameConstants.RenderHeight}: GPU-synchronized mean of thirty alternating-order pairs after ten paired warmups is {normal:0.00}ms fields hidden / {active:0.00}ms fields present on the same combat frame ({active - normal:+0.00;-0.00;0.00}ms difference).",
                assertions);
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROTOCOL_ANIMATION") == "1")
                CaptureProtocolBattlefieldMotion(session, ui, "protocol-apex-animation", 72, assertions, scenes);
        }
        finally { _renderer.ReducedEffects = priorReduced; }

        (double Normal, double Active) Measure()
        {
            for (var frame = 0; frame < 10; frame++)
            {
                Draw(false);
                Draw(null);
            }
            var normalTicks = 0L;
            var activeTicks = 0L;
            for (var pair = 0; pair < 30; pair++)
            {
                if (pair % 2 == 0)
                {
                    normalTicks += TimedDraw(false);
                    activeTicks += TimedDraw(null);
                }
                else
                {
                    activeTicks += TimedDraw(null);
                    normalTicks += TimedDraw(false);
                }
            }
            var milliseconds = 1000d / Stopwatch.Frequency / 30;
            return (normalTicks * milliseconds, activeTicks * milliseconds);
        }

        long TimedDraw(bool? protocolOverride)
        {
            var start = Stopwatch.GetTimestamp();
            Draw(protocolOverride);
            return Stopwatch.GetTimestamp() - start;
        }

        void Draw(bool? protocolOverride)
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
            _renderer.Draw(_batch, _primitives, session, protocolVisualOverride: protocolOverride);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            target.GetData(0, new Rectangle(0, 0, 1, 1), pixel, 0, 1);
        }
    }

    private void CaptureProtocolBattlefieldMotion(GameSession session, UIManager ui, string directory, int frames,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        Directory.CreateDirectory(Path.Combine(_outputDirectory, directory));
        var priorReduced = _renderer.ReducedEffects;
        _renderer.ReducedEffects = false;
        try
        {
            var steps = 0;
            for (var frame = 0; frame < frames; frame++)
            {
                var targetStep = (frame + 1) * 60 / 24;
                while (steps < targetStep)
                {
                    session.Update(1f / 60);
                    steps++;
                }
                ui.AdvanceVisualTime(1f / 24);
                var checksum = SessionChecksum.Compute(session, steps);
                scenes.Add(Capture($"{directory}/frame-{frame:000}.png", ui, GameState.Playing, session));
                Require(checksum == SessionChecksum.Compute(session, steps),
                    $"{directory} frame {frame}: native gameplay rendering preserves its actual combat state.", assertions);
            }
        }
        finally { _renderer.ReducedEffects = priorReduced; }
    }

    private void CaptureProtocolLightingMotion(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var fixtures = new List<GameSession>();
        foreach (var definition in content.Towers.Values)
        {
            var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
            Require(session.TryPlaceTower(definition.Id, new Vector2(250, 205)),
                $"{definition.Id}: motion review uses a legal radial combat foundation.", assertions);
            var tower = session.SelectedTower!;
            AdvanceProtocolReviewTier(session, tower, 3, assertions);
            tower.TickVisual(1);
            session.CancelPlacement();
            session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            Require(session.SpawnSandboxTargets("t3_brute", 4, 20, "Standard", true),
                $"{definition.Id}: motion review uses actual immortal Sandbox targets.", assertions);
            var points = new[] { new Vector2(250, 110), new Vector2(390, 205), new Vector2(250, 300), new Vector2(110, 205) };
            for (var target = 0; target < points.Length; target++)
                session.Enemies[target].SetSandboxPathDistance(session.Map.Path.Project(points[target]).DistanceAlongPath, session.Map.Path);
            fixtures.Add(session);
        }
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "protocol-animation"));
        var steps = 0;
        for (var frame = 0; frame < 96; frame++)
        {
            var targetStep = (frame + 1) * 60 / 24;
            while (steps < targetStep)
            {
                foreach (var session in fixtures) session.Update(1f / 60);
                steps++;
            }
            var checksums = fixtures.Select(session => SessionChecksum.Compute(session, steps)).ToArray();
            scenes.Add(CaptureProtocolMotionFrame(fixtures, font, $"protocol-animation/frame-{frame:000}.png"));
            Require(fixtures.Select((session, index) => SessionChecksum.Compute(session, steps) == checksums[index]).All(same => same),
                $"Protocol motion frame {frame}: paired appearance overrides preserve all ten identical combat fixture checksums.", assertions);
        }
    }

    private VisualVerificationScene CaptureProtocolMotionFrame(IReadOnlyList<GameSession> fixtures,
        SpriteFont font, string fileName)
    {
        const int width = 1920;
        const int header = 104;
        const int rowHeight = 130;
        var rows = fixtures.Count;
        using var target = new RenderTarget2D(GraphicsDevice, width, header + rows * rowHeight + 12,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "OPERATION / PROTOCOL", new Vector2(24, 14), .95f);
        MechanicalReviewLabel(font, "T3 / SAME FIRING FRAME ON BOTH SIDES / NATIVE FIELD SIZE / 24 FPS", new Vector2(24, 52), .57f);
        MechanicalReviewLabel(font, "NORMAL", new Vector2(580, 78), .60f);
        MechanicalReviewLabel(font, "ACTIVE", new Vector2(1405, 78), .60f);
        for (var row = 0; row < rows; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 6), ColorPalette.Panel);
            MechanicalReviewLabel(font, fixtures[row].Towers[0].Definition.DisplayName, new Vector2(26, y + 48), .60f);
        }
        _batch.End();
        for (var row = 0; row < rows; row++)
        for (var active = 0; active < 2; active++)
        {
            var session = fixtures[row];
            var tower = session.Towers[0];
            var center = new Vector2(620 + active * 825, header + row * rowHeight + 63);
            GraphicsDevice.ScissorRectangle = new Rectangle(220 + active * 825, header + row * rowHeight + 2, 810, rowHeight - 10);
            const float scale = 1;
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, clipping, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X - tower.Position.X * scale,
                    center.Y - tower.Position.Y * scale, 0));
            _renderer.DrawCombatShowcase(_batch, _primitives, session, protocolVisualOverride: active == 1);
            _batch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private static void AdvanceProtocolReviewTier(GameSession session, TowerInstance tower, int stage,
        ICollection<string> assertions)
    {
        if (stage >= 2)
            Require(session.TryChooseTowerDoctrine(tower.Id, tower.Definition.Tier2Doctrines[0].Id),
                $"{tower.Definition.Id}: lighting review applies its authored doctrine.", assertions);
        if (stage >= 3)
            Require(session.TrySpecializeTower(tower.Id, tower.Definition.Specializations[0].Id),
                $"{tower.Definition.Id}: lighting review applies its authored specialization.", assertions);
        if (stage >= 4)
            Require(session.TryUpgradeTower(tower.Id) && tower.IsApex,
                $"{tower.Definition.Id}: lighting review applies its authored Apex upgrade.", assertions);
    }
}
