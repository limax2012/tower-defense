using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertBattlefieldInteractionPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var tower in content.Towers.Values)
        for (var stage = 1; stage <= 4; stage++)
        {
            var idle = InteractionTowerPixels(tower, stage, 0, .23f, false);
            var protocol = InteractionTowerPixels(tower, stage, 1, .23f, false);
            Require(Changed(idle, protocol) >= 12,
                $"{tower.DisplayName} stage {stage}: Protocol illumination changes at least twelve native-scale pixels.", assertions);
            if (tower.Id != "signal_beacon")
            {
                var supported = InteractionTowerPixels(tower, stage, 2, .23f, false);
                var both = InteractionTowerPixels(tower, stage, 3, .23f, false);
                Require(Changed(idle, supported) >= 6 && Changed(protocol, both) >= 6 && Changed(supported, both) >= 12,
                    $"{tower.DisplayName} stage {stage}: support and Protocol remain independently readable when combined at native scale.", assertions);
            }
            for (var state = 1; state <= 3; state++)
            {
                var first = InteractionTowerPixels(tower, stage, state, .23f, true);
                Require(first.SequenceEqual(InteractionTowerPixels(tower, stage, state, .91f, true)),
                    $"{tower.DisplayName} stage {stage}: interaction state {state} remains still in Reduced Effects.", assertions);
            }
        }
        scenes.Add(CaptureInteractionAtlas(content, font, false));
        scenes.Add(CaptureInteractionAtlas(content, font, true));
        AssertInteractionRanges(content, font, display, assertions, scenes);
        AssertInteractionLifecycle(content, font, display, assertions, scenes);
        CaptureInteractionCombat(content, font, display, assertions, scenes);

        static int Changed(Color[] first, Color[] second) => first.Zip(second, (a, b) => a != b).Count(changed => changed);
    }

    private void AssertInteractionRanges(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var id in new[] { "needle_turret", "signal_beacon" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            Require(session.TryPlaceTower(id, new Vector2(270, 350)),
                $"{id}: tactical range review uses an unobstructed legal foundation.", assertions);
            var tower = session.SelectedTower!;
            tower.TickVisual(1);
            session.CancelPlacement();
            session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            if (tower.IsSupport) session.ToggleSandboxTower(tower.Id);
            var idle = RenderPixels(ui, GameState.Playing, session);
            if (tower.IsSupport) session.ToggleSandboxTower(tower.Id);
            else session.TrySelectTower(tower.Id);
            var active = RenderPixels(ui, GameState.Playing, session);
            var radius = tower.IsSupport ? session.GetEffectiveAuraRange(tower) : session.GetEffectiveRange(tower);
            var samples = 0;
            var exteriorChanges = 0;
            for (var index = 0; index < 64; index++)
            {
                var angle = index * MathHelper.TwoPi / 64;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var boundary = tower.Position + direction * radius;
                var outside = tower.Position + direction * (radius + 5);
                var sample = new Rectangle((int)MathF.Floor(boundary.X) - 2, (int)MathF.Floor(boundary.Y) - 2, 5, 5);
                var exterior = new Rectangle((int)MathF.Floor(outside.X) - 1, (int)MathF.Floor(outside.Y) - 1, 3, 3);
                if (CountChangedPixels(idle, active, sample) > 0) samples++;
                exteriorChanges += CountChangedPixels(idle, active, exterior);
            }
            Require(exteriorChanges == 0,
                $"{id}: all 64 sampled directions keep the tactical boundary thin outside its true radius.", assertions);
            Require(samples == 64,
                $"{id}: all 64 sampled directions display a continuous boundary at the effective gameplay radius.", assertions);
            scenes.Add(Capture($"46-range-{id}.png", ui, GameState.Playing, session));
            if (tower.IsSupport) continue;
            session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            Require(CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session),
                    new Rectangle(130, 210, 280, 280)) == 0,
                "Clearing tower selection restores its entire field footprint and range to the idle state.", assertions);
            session.HandleInspectionInput(Pointer(tower.Position.X, tower.Position.Y));
            Require(session.HoveredTower == tower && session.SelectedTower is null &&
                    CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session),
                        new Rectangle(240, 320, 60, 60)) > 60,
                "Hover alone illuminates the foundation without changing selection.", assertions);
            scenes.Add(Capture("46a-hover-footprint.png", ui, GameState.Playing, session));
        }
    }

    private Color[] InteractionTowerPixels(TowerDefinition tower, int stage, int state, float time, bool reduced)
    {
        const int size = 112;
        using var target = new RenderTarget2D(GraphicsDevice, size, size,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Rainline.Deck);
        if (tower.Id == "signal_beacon") state = state is 1 or 3 ? 1 : 0;
        DrawMechanicalReviewTower(tower, stage, new Vector2(size * .5f), 1, -.55f, time,
            state, reduced, groundEffects: false, lights: false);
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[size * size];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureInteractionAtlas(GameContent content, SpriteFont font, bool reduced)
    {
        var towers = content.Towers.Values.ToArray();
        const int width = 1800;
        const int header = 124;
        const int rowHeight = 105;
        var height = header + towers.Length * rowHeight + 16;
        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        var states = new[] { "IDLE", "PROTOCOL", "SUPPORT", "BOTH", "SELECT / AUTO" };
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, $"TOWER HARDWARE / NATIVE FIELD SIZE / {(reduced ? "REDUCED" : "FULL")} EFFECTS",
            new Vector2(24, 16), .95f);
        MechanicalReviewLabel(font, "Four stages / fixed aim / green support circuitry / family Protocol lighting",
            new Vector2(24, 52), .62f);
        for (var stage = 1; stage <= 4; stage++)
        {
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}", new Vector2(Group(stage), 82), .65f);
            for (var state = 0; state < states.Length; state++)
                MechanicalReviewLabel(font, states[state], new Vector2(Column(stage, state) - 29, 108), .35f);
        }
        for (var row = 0; row < towers.Length; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 5), decks[row % decks.Length]);
            MechanicalReviewLabel(font, towers[row].DisplayName, new Vector2(26, y + 35), .57f);
        }
        _batch.End();
        for (var row = 0; row < towers.Length; row++)
        for (var stage = 1; stage <= 4; stage++)
        for (var state = 0; state < states.Length; state++)
        {
            var tower = towers[row];
            var center = new Vector2(Column(stage, state), header + row * rowHeight + 48);
            var level = Math.Min(stage, 3);
            if (state == 4)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                BattlefieldInteractionArt.DrawFootprint(_batch, _primitives, center, tower.Visual.Radius,
                    tower.Id, level, stage == 4, ColorPalette.Gold, 1, true, !reduced);
                _batch.End();
            }
            var hardwareState = state == 4 ? 0 : state;
            if (tower.Id == "signal_beacon") hardwareState = hardwareState is 1 or 3 ? 1 : 0;
            DrawMechanicalReviewTower(tower, stage, center, 1, -.55f, .23f, hardwareState, reduced);
            if (state != 4) continue;
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            if (stage < 4)
                BattlefieldInteractionArt.DrawAutoController(_batch, _primitives, center, tower.Visual.Radius,
                    tower.Id, .23f, reduced);
            _batch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, reduced ? "43a-interaction-hardware-reduced.png" : "43-interaction-hardware-native.png");

        static int Group(int stage) => 220 + (stage - 1) * 390;
        static int Column(int stage, int state) => Group(stage) + 35 + state * 74;
    }

    private void AssertInteractionLifecycle(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("signal_beacon", new Vector2(205, 350)) &&
                session.TryPlaceTower("prism_beam", new Vector2(270, 350)),
            "Interaction lifecycle uses legal source and receiver positions.", assertions);
        var beacon = session.Towers[0];
        var receiver = session.Towers[1];
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        _renderer.ReducedEffects = true;
        session.ToggleSandboxTower(beacon.Id);
        session.Update(0);
        var idle = RenderPixels(ui, GameState.Playing, session);
        var receiverBounds = new Rectangle(240, 320, 60, 60);
        session.ToggleSandboxTower(beacon.Id);
        session.Update(0);
        var supported = RenderPixels(ui, GameState.Playing, session);
        Require(session.GetSupportBuff(receiver).IsActive && CountChangedPixels(idle, supported, receiverBounds) >= 24,
            "The actual support system activates visible circuitry on an eligible receiver.", assertions);
        session.ToggleSandboxTower(beacon.Id);
        session.Update(0);
        Require(!session.GetSupportBuff(receiver).IsActive &&
                CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
            "Support deactivation restores the receiver's exact idle hardware pixels.", assertions);
        receiver.ActivateOverdrive();
        var protocol = RenderPixels(ui, GameState.Playing, session);
        Require(receiver.IsOverdriven && CountChangedPixels(idle, protocol, receiverBounds) >= 48,
            "A real Protocol timer activates visible receiver hardware.", assertions);
        receiver.TickVisual(receiver.Protocol.DurationSeconds + .1f);
        Require(!receiver.IsOverdriven && CountChangedPixels(idle,
                RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
            "An expired Protocol leaves no residual illumination on its receiver.", assertions);
        session.ToggleSandboxTower(beacon.Id);
        session.Update(0);
        receiver.ActivateOverdrive();
        session.TrySelectTower(receiver.Id);
        Require(session.TryToggleAutoProtocol(receiver.Id), "Combined interaction state has a selected Auto receiver.", assertions);
        scenes.Add(Capture("44-interaction-states-combined.png", ui, GameState.Playing, session));
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        Require(session.SelectedTower is null && session.AutoOverdriveTowerId == receiver.Id,
            "Clearing selection preserves an armed Auto controller and its valid status effects.", assertions);
        scenes.Add(Capture("44a-interaction-selection-cleared.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = false;
    }

    private void CaptureInteractionCombat(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var towers = content.Towers.Values.ToArray();
        foreach (var mapId in new[] { "foundry_loop", "crosswind_basin", "prism_circuit", "relay_divide" })
        {
            var session = new GameSession(content, mapId, "normal", "sandbox_lab");
            var candidates = new List<Vector2>();
            foreach (var region in session.Map.BuildableRegions)
            for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
            for (var x = region.Left + 24; x < region.Right - 16; x += 24)
                candidates.Add(new Vector2(x, y));
            candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 350))).ToList();
            for (var index = 0; index < 20; index++)
            {
                var definition = towers[index % towers.Length];
                Require(candidates.Any(point => session.TryPlaceTower(definition.Id, point)),
                    $"{mapId}: interaction review places legal {definition.DisplayName} hardware.", assertions);
                var tower = session.SelectedTower!;
                var stage = index % 4 + 1;
                if (stage >= 2) session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id);
                if (stage >= 3) session.TrySpecializeTower(tower.Id, definition.Specializations[0].Id);
                if (stage == 4) session.TryUpgradeTower(tower.Id);
                if (index % 3 == 0) tower.ActivateOverdrive();
            }
            session.CancelPlacement();
            Require(session.SpawnSandboxTargets("t3_brute", 24, 20, "Elite", true) && session.StartSandboxWave(20),
                $"{mapId}: interaction review runs an authored Sandbox wave with durable combat targets.", assertions);
            for (var index = 0; index < session.Enemies.Count; index++)
                session.Enemies[index].SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / session.Enemies.Count,
                    session.Map.Path);
            for (var step = 0; step < 30; step++) session.Update(1f / 60);
            var receiver = session.Towers.First(tower => !tower.IsApex && !tower.IsSupport);
            session.TrySelectTower(receiver.Id);
            Require(session.TryToggleAutoProtocol(receiver.Id), $"{mapId}: a selected defense arms its Auto controller.", assertions);
            Require(session.Towers.Any(tower => session.GetSupportBuff(tower).IsActive),
                $"{mapId}: dense combat includes live Beacon recipients.", assertions);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            for (var wave = 1; wave < 20; wave++)
                ui.HandleGameplayInput(Pointer(0, 0) with { SandboxWaveNextPressed = true }, session);
            Require(ui.SelectedSandboxWave == 20, $"{mapId}: Sandbox review controls display the active authored wave.", assertions);
            var checksum = SessionChecksum.Compute(session, 30);
            scenes.Add(Capture($"45-interaction-combat-{mapId}.png", ui, GameState.Playing, session));
            _renderer.ReducedEffects = true;
            scenes.Add(Capture($"45a-interaction-combat-{mapId}-reduced.png", ui, GameState.Playing, session));
            _renderer.ReducedEffects = false;
            Require(SessionChecksum.Compute(session, 30) == checksum,
                $"{mapId}: Full and Reduced interaction rendering preserve synchronized combat state.", assertions);
            if (mapId != "foundry_loop" || Environment.GetEnvironmentVariable("MAXIMAL_BASTION_INTERACTION_ANIMATION") != "1") continue;
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "interaction-animation"));
            for (var frame = 0; frame < 72; frame++)
            {
                for (var step = 0; step < 5; step++) session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 12);
                scenes.Add(Capture($"interaction-animation/frame-{frame:000}.png", ui, GameState.Playing, session));
            }
        }
    }
}
