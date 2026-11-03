using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertTowerSignalPolish(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var definition in content.Towers.Values)
        for (var stage = 1; stage <= 4; stage++)
        {
            var normal = TowerSignalPixels(definition, stage, 0, .17f, true);
            foreach (var state in new[] { 1, 2 })
            {
                if (definition.Id == "signal_beacon" && state == 1) continue;
                var active = TowerSignalPixels(definition, stage, state, .17f, true);
                var name = state == 1 ? "Jammer" : "Disrupt";
                Require(active.Zip(normal, (left, right) => left != right).Count(changed => changed) >= 8,
                    $"{definition.DisplayName} stage {stage}: {name} visibly changes physical hardware at field scale.", assertions);
                Require(active.SequenceEqual(TowerSignalPixels(definition, stage, state, .61f, true)),
                    $"{definition.DisplayName} stage {stage}: Reduced Effects holds {name} feedback steady.", assertions);
                Require(!TowerSignalPixels(definition, stage, state, .17f, false)
                        .SequenceEqual(TowerSignalPixels(definition, stage, state, .61f, false)),
                    $"{definition.DisplayName} stage {stage}: {name} hardware feedback animates in Full Effects.", assertions);
            }
        }
        foreach (var id in new[] { "watchtower", "needle_turret" })
        foreach (var reduced in new[] { false, true })
            scenes.Add(CaptureTowerSignalRotations(content.Towers[id], font, reduced));
        foreach (var reduced in new[] { false, true })
            scenes.Add(CaptureTowerSignalColors(content, font, reduced));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_COLOR_ANIMATION") == "1")
            CaptureTowerColorAnimation(content, font, scenes);
        for (var stage = 1; stage <= 4; stage++)
            scenes.Add(CaptureTowerSignalStates(content.Towers.Values.ToArray(), font, stage,
                $"59b-tower-interference-stage-{stage}.png", .43f));

        AssertTowerSignalLifecycle(content, font, display, assertions, scenes);
        AssertSignalTransmissions(content, font, assertions, scenes);
        CapturePrismDisruptionReview(content, font, scenes);
        var combat = TowerSignalCombat(content, assertions);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var checksum = SessionChecksum.Compute(combat, 0);
                scenes.Add(Capture($"59d-signal-links-{(reduced ? "reduced" : "full")}.png", ui, GameState.Playing, combat));
                Require(checksum == SessionChecksum.Compute(combat, 0),
                    $"{(reduced ? "Reduced" : "Full")} signal-link rendering preserves synchronized gameplay.", assertions);
            }
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_TOWER_SIGNAL_ANIMATION") != "1") return;
            _renderer.ReducedEffects = false;
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "tower-signal-animation"));
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "tower-signal-combat-animation"));
            var representatives = new[] { "needle_turret", "watchtower", "arc_relay", "frost_spire", "prism_beam" }
                .Select(id => content.Towers[id]).ToArray();
            for (var frame = 0; frame < 60; frame++)
            {
                scenes.Add(CaptureTowerSignalStates(representatives, font, 3,
                    $"tower-signal-animation/frame-{frame:000}.png", frame / 30f));
                scenes.Add(Capture($"tower-signal-combat-animation/frame-{frame:000}.png", ui, GameState.Playing, combat));
                for (var step = 0; step < 2; step++) combat.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private Color[] TowerSignalPixels(TowerDefinition definition, int stage, int state, float time, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 112, 112, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Rainline.Deck);
        DrawTowerSignalSample(definition, stage, state, new Vector2(56), 1, -.55f, time, reduced);
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[112 * 112];
        target.GetData(pixels);
        return pixels;
    }

    private void DrawTowerSignalSample(TowerDefinition definition, int stage, int state, Vector2 at,
        float scale, float aim, float time, bool reduced, bool protocol = false)
    {
        var level = Math.Min(stage, 3);
        var apex = stage == 4;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
        if (protocol)
            TowerProtocolArt.DrawEnvelope(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, level, apex, time, reduced);
        NightGridArt.Tower(_batch, _primitives, Vector2.Zero, definition.Visual.Radius, definition.Id,
            level, aim, time: 0, lights: !reduced, apex: apex, power: state == 2 ? .18f : 1);
        if (protocol)
            TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, level, apex, aim, time, reduced, envelope: false);
        if (state == 1 && definition.Id != "signal_beacon")
            TowerStatusArt.DrawSuppressed(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, level, apex, aim, time, reduced);
        if (state == 2)
            TowerStatusArt.DrawDisrupted(_batch, _primitives, Vector2.Zero, definition.Visual.Radius,
                definition.Id, level, apex, aim, time, reduced);
        _batch.End();
    }

    private VisualVerificationScene CaptureTowerSignalRotations(TowerDefinition definition, SpriteFont font, bool reduced)
    {
        const int width = 1440;
        const int rowHeight = 220;
        using var target = new RenderTarget2D(GraphicsDevice, width, 100 + rowHeight * 4,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, $"{definition.DisplayName.ToUpperInvariant()} / {(reduced ? "REDUCED" : "FULL")} / ALL TIERS",
            new Vector2(24, 16), .95f);
        StatusLabel(font, "2x DETAIL ABOVE / FIELD SIZE BELOW / SIX AIM DIRECTIONS", new Vector2(24, 55), .65f);
        for (var row = 0; row < 4; row++)
        {
            var y = 100 + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8),
                row % 2 == 0 ? ColorPalette.Rainline.Deck : ColorPalette.Cinderworks.Deck);
            StatusLabel(font, row == 3 ? "APEX" : $"T{row + 1}", new Vector2(28, y + 90), .7f);
        }
        _batch.End();
        for (var stage = 1; stage <= 4; stage++)
        for (var column = 0; column < 6; column++)
        {
            var x = 240 + column * 210;
            var y = 100 + (stage - 1) * rowHeight;
            var aim = -MathHelper.PiOver2 + column * MathHelper.PiOver4;
            DrawTowerSignalSample(definition, stage, 0, new Vector2(x, y + 76), 2, aim, .43f, reduced);
            DrawTowerSignalSample(definition, stage, 0, new Vector2(x, y + 174), 1, aim, .43f, reduced);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, $"59-tower-polish-{definition.Id}-{(reduced ? "reduced" : "full")}.png");
    }

    private VisualVerificationScene CaptureTowerSignalColors(GameContent content, SpriteFont font, bool reduced)
    {
        var definitions = new[] { "needle_turret", "arc_relay", "frost_spire" }.Select(id => content.Towers[id]).ToArray();
        const int width = 1800;
        const int cellWidth = 440;
        const int rowHeight = 350;
        using var target = new RenderTarget2D(GraphicsDevice, width, 100 + rowHeight * 3,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, $"NEEDLE / ARC / FROST / {(reduced ? "REDUCED" : "FULL")} EFFECTS", new Vector2(24, 16), .95f);
        StatusLabel(font, "NORMAL + PROTOCOL ABOVE / 1.5x ATTACKS AT CENTER / FIELD SIZE BELOW", new Vector2(24, 55), .65f);
        for (var row = 0; row < definitions.Length; row++)
        for (var stage = 1; stage <= 4; stage++)
        {
            var x = 20 + (stage - 1) * cellWidth;
            var y = 100 + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(x, y, cellWidth - 10, rowHeight - 8), ColorPalette.Rainline.Deck);
            StatusLabel(font, $"{definitions[row].DisplayName} / {(stage == 4 ? "APEX" : $"T{stage}")}",
                new Vector2(x + 12, y + 10), .57f);
        }
        _batch.End();
        for (var row = 0; row < definitions.Length; row++)
        for (var stage = 1; stage <= 4; stage++)
        {
            var x = 20 + (stage - 1) * cellWidth;
            var y = 100 + row * rowHeight;
            var definition = definitions[row];
            DrawTowerSignalSample(definition, stage, 0, new Vector2(x + 100, y + 100), 1.5f, -.55f, .43f, reduced);
            DrawTowerSignalSample(definition, stage, 0, new Vector2(x + 300, y + 100), 1.5f, -.55f, .43f, reduced, protocol: true);
            foreach (var scale in new[] { 1.5f, 1f })
            {
                var at = new Vector2(x + (scale > 1 ? 40 : 95), y + (scale > 1 ? 222 : 300));
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
                DrawTierAttackSample(definition, stage, .43f, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, $"59a-tower-color-separation-{(reduced ? "reduced" : "full")}.png");
    }

    private VisualVerificationScene CaptureTowerSignalStates(IReadOnlyList<TowerDefinition> definitions,
        SpriteFont font, int stage, string fileName, float time)
    {
        const int width = 1440;
        const int rowHeight = 220;
        using var target = new RenderTarget2D(GraphicsDevice, width, 110 + rowHeight * definitions.Count,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, $"TOWER INTERFERENCE / {(stage == 4 ? "APEX" : $"T{stage}")}", new Vector2(24, 16), .95f);
        StatusLabel(font, "2x DETAIL ABOVE / FIELD SIZE: FULL LEFT, REDUCED RIGHT", new Vector2(24, 55), .65f);
        for (var row = 0; row < definitions.Count; row++)
        {
            var y = 110 + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8),
                row % 2 == 0 ? ColorPalette.Rainline.Deck : ColorPalette.Cinderworks.Deck);
            StatusLabel(font, definitions[row].DisplayName, new Vector2(28, y + 88), .6f);
            for (var state = 0; state < 3; state++)
                StatusLabel(font, state == 0 ? "NORMAL" : state == 2 ? "DISRUPTED" :
                    definitions[row].Id == "signal_beacon" ? "JAMMER: IMMUNE" : "JAMMED",
                    new Vector2(310 + state * 380, y + 10), .55f);
        }
        _batch.End();
        for (var row = 0; row < definitions.Count; row++)
        for (var state = 0; state < 3; state++)
        {
            var x = 400 + state * 380;
            var y = 110 + row * rowHeight;
            DrawTowerSignalSample(definitions[row], stage, state, new Vector2(x, y + 83), 2, -.55f, time, false);
            DrawTowerSignalSample(definitions[row], stage, state, new Vector2(x - 50, y + 176), 1, -.55f, time, false);
            DrawTowerSignalSample(definitions[row], stage, state, new Vector2(x + 50, y + 176), 1, -.55f, time, true);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private void CaptureTowerColorAnimation(GameContent content, SpriteFont font,
        ICollection<VisualVerificationScene> scenes)
    {
        var definitions = new[] { "needle_turret", "frost_spire", "arc_relay" }
            .Select(id => content.Towers[id]).ToArray();
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "colors-animation"));
        using var target = new RenderTarget2D(GraphicsDevice, 1500, 450,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        for (var frame = 0; frame < 60; frame++)
        {
            var time = frame / 30f;
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            StatusLabel(font, "WEAPON COLORS / T3", new Vector2(24, 12), .85f);
            StatusLabel(font, "POWERED / PROTOCOL ABOVE    ATTACKS: DETAIL / FIELD BELOW", new Vector2(24, 47), .55f);
            for (var column = 0; column < definitions.Length; column++)
            {
                var x = column * 500;
                _primitives.FillRect(_batch, new Rectangle(x + 8, 82, 484, 360), ColorPalette.PanelAlt);
                StatusLabel(font, definitions[column].DisplayName.ToUpperInvariant(), new Vector2(x + 22, 95), .62f);
            }
            _batch.End();
            for (var column = 0; column < definitions.Length; column++)
            {
                var x = column * 500;
                var definition = definitions[column];
                DrawTowerSignalSample(definition, 3, 0, new Vector2(x + 130, 190), 1.7f, -.55f, time, false);
                DrawTowerSignalSample(definition, 3, 0, new Vector2(x + 365, 190), 1.7f, -.55f, time, false, protocol: true);
                DrawAttack(new Vector2(x + 52, 315), 1.8f);
                DrawAttack(new Vector2(x + 145, 397), 1);

                void DrawAttack(Vector2 at, float scale)
                {
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
                    DrawTierAttackSample(definition, 3, time, false);
                    _batch.End();
                }
            }
            GraphicsDevice.SetRenderTarget(null);
            scenes.Add(SaveTierScene(target, $"colors-animation/frame-{frame:000}.png"));
        }
    }

    private void AssertTowerSignalLifecycle(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        foreach (var role in new[] { EnemySignalRole.Jammer, EnemySignalRole.Disruptor })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            Require(session.TryPlaceTower("prism_beam", new Vector2(270, 350)),
                $"{role} lifecycle places a legal Prism recipient.", assertions);
            var tower = session.SelectedTower!;
            tower.TickVisual(1);
            session.SpawnEnemy("t4_aegis", 50, .2f, signalRole: role);
            var source = session.Enemies.Single();
            source.SetSandboxPathDistance(session.Map.Path.Project(tower.Position).DistanceAlongPath, session.Map.Path);
            source.ArmSignalAbility(0);
            session.CancelPlacement();
            session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            var bounds = new Rectangle(236, 316, 68, 68);
            var normal = RenderPixels(ui, GameState.Playing, session);
            if (role == EnemySignalRole.Disruptor)
                scenes.Add(CapturePrismDisruptionNativeState(ui, session, "before"));
            session.TryActivateEnemySignal(source);
            Require(role == EnemySignalRole.Jammer ? tower.IsSuppressed : tower.IsDisrupted,
                $"{role}: a real enemy activation applies its authored tower condition.", assertions);
            var links = session.Effects.Effects.Where(effect => effect.Kind == EffectKind.Beam && effect.Signal.Role == role).ToArray();
            Require(links.Length == 1 && links[0].Signal.SourceEnemyId == source.Id &&
                    links[0].Signal.TargetTowerId == tower.Id && links[0].Start == source.Position && links[0].End == tower.Position,
                $"{role}: successful application owns a source-to-recipient link with accurate endpoints.", assertions);
            Require(CountChangedPixels(normal, RenderPixels(ui, GameState.Playing, session), bounds) >= 12,
                $"{role}: actual field rendering visibly changes the recipient hardware.", assertions);
            scenes.Add(Capture($"59c-{role.ToString().ToLowerInvariant()}-active.png", ui, GameState.Playing, session));
            if (role == EnemySignalRole.Disruptor)
                scenes.Add(CapturePrismDisruptionNativeState(ui, session, "active"));
            tower.TickVisual(Math.Max(tower.SuppressionRemaining, tower.DisruptionRemaining) + .01f);
            session.Effects.Update(1);
            Require(!tower.IsSuppressed && !tower.IsDisrupted && session.Effects.Effects.Count == 0 &&
                    CountChangedPixels(normal, RenderPixels(ui, GameState.Playing, session), bounds) == 0,
                $"{role}: expiry restores the exact ordinary recipient appearance and removes transient connections.", assertions);
            scenes.Add(Capture($"59c-{role.ToString().ToLowerInvariant()}-expired.png", ui, GameState.Playing, session));
            if (role == EnemySignalRole.Disruptor)
                scenes.Add(CapturePrismDisruptionNativeState(ui, session, "recovered"));
        }
    }

    private static GameSession TowerSignalCombat(GameContent content, ICollection<string> assertions)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 26)
        for (var x = region.Left + 24; x < region.Right - 16; x += 26) candidates.Add(new Vector2(x, y));
        var ids = new[] { "needle_turret", "watchtower", "arc_relay", "frost_spire", "prism_beam", "breaker_cannon" };
        for (var index = 0; index < ids.Length; index++)
        {
            var preferred = index < 3 ? new Vector2(250, 350) : new Vector2(740, 350);
            Require(candidates.OrderBy(point => Vector2.DistanceSquared(point, preferred))
                    .Any(point => session.TryPlaceTower(ids[index], point)),
                $"Signal-link combat places {ids[index]} on a legal foundation.", assertions);
            AdvanceProtocolReviewTier(session, session.SelectedTower!, index % 3 + 1, assertions);
        }
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        for (var index = 0; index < 18; index++)
        {
            session.SpawnEnemy(index % 3 == 0 ? "t4_aegis" : index % 3 == 1 ? "t1_crawler" : "t3_brute", 50, .3f);
            session.Enemies[^1].SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / 18, session.Map.Path);
        }
        foreach (var (role, recipient) in new[]
                 { (EnemySignalRole.Jammer, session.Towers[0]), (EnemySignalRole.Disruptor, session.Towers[3]) })
        {
            session.SpawnEnemy("t5_regenerator", 100, .3f, signalRole: role);
            var source = session.Enemies[^1];
            source.SetSandboxPathDistance(session.Map.Path.Project(recipient.Position).DistanceAlongPath, session.Map.Path);
            source.ArmSignalAbility(0);
            session.TryActivateEnemySignal(source);
            Require(session.Effects.Effects.Any(effect => effect.Kind == EffectKind.Beam && effect.Signal.Role == role),
                $"Signal-link combat begins with a real {role} connection amid other enemies and defenses.", assertions);
        }
        return session;
    }
}
