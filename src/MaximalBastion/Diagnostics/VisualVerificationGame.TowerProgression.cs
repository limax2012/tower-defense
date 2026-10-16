using System.Security.Cryptography;
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
    private void AssertTowerProgression(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var tower in content.Towers.Values)
        {
            var previous = TierStructurePixels(tower, 1, 0, 3);
            for (var stage = 2; stage <= 4; stage++)
            {
                var current = TierStructurePixels(tower, stage, 0, 3);
                var changed = StructuralDifferences(previous, current, tower.Visual.Radius, tower.Id, 3);
                Require(changed >= 80,
                    $"{tower.Id}: stage {stage - 1} to {stage} changes mechanical geometry without bloom or tier lamps ({changed} detail pixels).",
                    assertions);
                previous = current;
            }

            var first = TierStructurePixels(tower, 1, -.62f, 1);
            var last = TierStructurePixels(tower, 4, -.62f, 1);
            var fieldChanges = StructuralDifferences(first, last, tower.Visual.Radius, tower.Id, 1);
            Require(fieldChanges >= 16,
                $"{tower.Id}: T1 and Apex remain structurally distinct at native field size without glow ({fieldChanges} pixels).",
                assertions);
            foreach (var reduced in new[] { false, true })
            {
                var firstAttack = TierAttackPixels(tower, 1, .18f, reduced);
                var finalAttack = TierAttackPixels(tower, 4, .18f, reduced);
                var changed = firstAttack.Zip(finalAttack, (a, b) => a != b).Count(value => value);
                Require(changed >= 48,
                    $"{tower.Id}: T1 and Apex attacks have visibly different energy signatures with {(reduced ? "Reduced" : "Full")} Effects ({changed} pixels).",
                    assertions);
            }
            for (var stage = 1; stage <= 4; stage++)
                Require(TierAttackPixels(tower, stage, .18f, true).SequenceEqual(TierAttackPixels(tower, stage, .77f, true)),
                    $"{tower.Id}: stage {stage} keeps a stable attack silhouette in Reduced Effects.", assertions);
        }
        scenes.Add(CaptureTowerUpgradeGallery(content, font, -MathHelper.PiOver2,
            "33-tower-upgrades.png", "UP / SAME AIM AT EVERY TIER"));
        scenes.Add(CaptureTowerUpgradeGallery(content, font, -.48f,
            "33b-tower-upgrades-diagonal.png", "DIAGONAL / SAME AIM AT EVERY TIER"));
        scenes.Add(CaptureTierAttackGallery(content, font, .18f, false,
            "35-tier-attack-signatures.png"));
        scenes.Add(CaptureTierAttackGallery(content, font, .31f, true,
            "35a-tier-attack-signatures-reduced.png"));
        CaptureMixedTierCombat(content, font, display, assertions, scenes);
    }

    private Color[] TierStructurePixels(TowerDefinition tower, int stage, float angle, int scale)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 112 * scale, 112 * scale,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
        NightGridArt.Tower(_batch, _primitives, new Vector2(56), tower.Visual.Radius, tower.Id,
            level: Math.Min(stage, 3), angle: angle, lights: false, groundEffects: false, apex: stage == 4);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private Color[] TierAttackPixels(TowerDefinition tower, int stage, float time, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 480, 220,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateTranslation(0, 55, 0) * Matrix.CreateScale(2));
        DrawTierAttackSample(tower, stage, time, reduced, includeTower: false);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private static int StructuralDifferences(Color[] first, Color[] second, float radius, string towerId, int scale)
    {
        var width = 112 * scale;
        var changed = 0;
        for (var index = 0; index < first.Length; index++)
        {
            var at = new Vector2(index % width / (float)scale, index / width / (float)scale) - new Vector2(56);
            if (TierLamp(at, radius, towerId)) continue;
            if (Armor(first[index]) != Armor(second[index])) changed++;
        }
        return changed;

        static bool Armor(Color color) => color.R + color.G + color.B > 150 &&
            Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) < 56;

        static bool TierLamp(Vector2 at, float radius, string towerId)
        {
            var triangular = towerId is "arc_relay" or "signal_beacon" or "shard_fan";
            for (var side = -1; side <= 1; side += 2)
            {
                var upper = triangular ? new Vector2(side * .62f, .23f) : new Vector2(side * .55f, -.36f);
                var lower = triangular ? new Vector2(side * .34f, .43f) : new Vector2(side * .455f, .54f);
                if (Vector2.DistanceSquared(at, upper * radius) < radius * radius * .055f)
                    return true;
                if (Vector2.DistanceSquared(at, lower * radius) < radius * radius * .065f)
                    return true;
            }
            return false;
        }
    }

    private VisualVerificationScene CaptureTowerUpgradeGallery(GameContent content, SpriteFont font,
        float angle, string fileName, string heading)
    {
        const int width = 2200;
        const int header = 112;
        const int rowHeight = 208;
        var towers = content.Towers.Values.ToArray();
        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        var height = header + towers.Length * rowHeight + 16;
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label("TOWER PROGRESSION", new Vector2(24, 16), 1.05f);
        Label(heading, new Vector2(590, 21), .68f);
        Label("2.2x DETAIL ABOVE / FIELD SIZE BELOW / REDUCED TO ITS RIGHT", new Vector2(24, 65), .58f);
        for (var stage = 1; stage <= 4; stage++)
            CenterLabel(stage == 4 ? "APEX" : $"T{stage}", new Vector2(Column(stage), 73), .74f);
        for (var index = 0; index < towers.Length; index++)
        {
            var y = header + index * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8), decks[index % decks.Length]);
            Label(towers[index].DisplayName, new Vector2(30, y + 78), .70f);
            for (var stage = 1; stage <= 4; stage++)
            {
                CenterLabel("FIELD", new Vector2(Column(stage) - 58, y + 181), .58f);
                CenterLabel("REDUCED", new Vector2(Column(stage) + 58, y + 181), .58f);
            }
        }
        _batch.End();
        for (var index = 0; index < towers.Length; index++)
        {
            var y = header + index * rowHeight;
            for (var stage = 1; stage <= 4; stage++)
            {
                Sample(new Vector2(Column(stage), y + 76), 2.2f, stage, false);
                Sample(new Vector2(Column(stage) - 58, y + 154), 1, stage, false);
                Sample(new Vector2(Column(stage) + 58, y + 154), 1, stage, true);
            }

            void Sample(Vector2 at, float scale, int stage, bool reduced)
            {
                var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, transform);
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, towers[index].Visual.Radius,
                    towers[index].Id, level: Math.Min(stage, 3), angle: angle,
                    time: reduced ? 0 : .23f, lights: !reduced, apex: stage == 4);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        static int Column(int stage) => 470 + (stage - 1) * 470;
        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
        void CenterLabel(string text, Vector2 at, float scale) => Label(text,
            at - new Vector2(font.MeasureString(text).X * scale * GameConstants.FontDrawScale * .5f, 0), scale);
    }

    private VisualVerificationScene CaptureTierAttackGallery(GameContent content, SpriteFont font,
        float time, bool reduced, string fileName)
    {
        const int width = 2200;
        const int header = 105;
        const int rowHeight = 230;
        var towers = content.Towers.Values.ToArray();
        using var target = new RenderTarget2D(GraphicsDevice, width, header + towers.Length * rowHeight + 12,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label(reduced ? "TIER ATTACKS / REDUCED EFFECTS" : "TIER ATTACKS / FULL EFFECTS", new Vector2(24, 16), 1);
        Label("1.7x DETAIL ABOVE / NATIVE FIELD SIZE BELOW", new Vector2(24, 62), .57f);
        for (var stage = 1; stage <= 4; stage++)
        {
            var text = stage == 4 ? "APEX" : $"T{stage}";
            var x = 465 + (stage - 1) * 470 - font.MeasureString(text).X * .68f * GameConstants.FontDrawScale * .5f;
            Label(text, new Vector2(x, 64), .68f);
        }
        for (var index = 0; index < towers.Length; index++)
        {
            var y = header + index * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8),
                index % 2 == 0 ? ColorPalette.Panel : ColorPalette.PanelAlt);
            Label(towers[index].DisplayName, new Vector2(30, y + 88), .69f);
        }
        _batch.End();
        for (var index = 0; index < towers.Length; index++)
        for (var stage = 1; stage <= 4; stage++)
        {
            var x = 270 + (stage - 1) * 470;
            var y = header + index * rowHeight;
            DrawAt(new Vector2(x, y + 90), 1.7f);
            DrawAt(new Vector2(x + 72, y + 183), 1);

            void DrawAt(Vector2 offset, float scale)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(offset.X, offset.Y, 0));
                DrawTierAttackSample(towers[index], stage, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private void DrawTierAttackSample(TowerDefinition tower, int stage, float time, bool reduced, bool includeTower = true)
    {
        var level = Math.Min(stage, 3);
        var apex = stage == 4;
        var center = new Vector2(tower.Id == "signal_beacon" ? 110 : 30, 0);
        var accent = NightGridArt.TowerAccent(tower.Id);
        if (includeTower)
            NightGridArt.Tower(_batch, _primitives, center, tower.Visual.Radius, tower.Id,
                level: level, angle: 0, time: reduced ? 0 : time, recoil: .10f,
                lights: !reduced, apex: apex);
        if (tower.Id == "signal_beacon")
        {
            TowerAttackArt.DrawBeaconBroadcast(_batch, _primitives, center,
                accent, tower.Visual.Radius, time, reduced, level, apex);
            return;
        }
        var end = new Vector2(193, 0);
        if (tower.Id == "prism_beam")
        {
            var start = center + Vector2.UnitX * tower.Visual.Radius * TowerTierVisuals.PrismBarrelLength(level, apex);
            PrismBeamArt.Draw(_batch, _primitives, start, end, accent, 2, .82f, reduced, true, level, apex);
            if (!reduced)
                NightGridArt.DrawMuzzleFlash(_batch, _primitives, start, tower.Visual.Radius, accent, .10f);
        }
        else if (tower.Id == "arc_relay")
        {
            TowerAttackArt.DrawArc(_batch, _primitives, NightGridArt.TowerMuzzlePosition(center,
                    tower.Visual.Radius, tower.Id, level, 0, apex: apex),
                new Vector2(138, -10), accent, .82f, time, reduced, level, apex);
            TowerAttackArt.DrawArc(_batch, _primitives, new Vector2(138, -10),
                end + new Vector2(0, 18), accent, .70f, time, reduced, level, apex);
        }
        else
        {
            var radius = tower.Id == "siege_mortar" ? 7 : tower.Id == "shard_fan" ? 3 : 5;
            ProjectileArt.Draw(_batch, _primitives, tower.Id, new Vector2(118, 0), Vector2.UnitX,
                radius, accent, time, reduced, level, apex);
            if (tower.Id == "shard_fan")
            {
                ProjectileArt.Draw(_batch, _primitives, tower.Id, new Vector2(115, -14), Vector2.UnitX,
                    radius, accent, time + .03f, reduced, level, apex);
                ProjectileArt.Draw(_batch, _primitives, tower.Id, new Vector2(115, 14), Vector2.UnitX,
                    radius, accent, time + .06f, reduced, level, apex);
            }
        }
        TowerAttackArt.DrawImpact(_batch, _primitives, tower.Id, end, Vector2.UnitX,
            accent, 8, .78f, time, reduced, level, apex);
    }

    private void CaptureMixedTierCombat(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var towers = content.Towers.Values.ToArray();
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
        for (var x = region.Left + 24; x < region.Right - 16; x += 24)
            candidates.Add(new Vector2(x, y));
        candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(490, 360))).ToList();
        for (var index = 0; index < 30; index++)
        {
            var definition = towers[index % towers.Length];
            Require(candidates.Any(point => session.TryPlaceTower(definition.Id, point)),
                $"The mixed-tier battlefield legally places {definition.DisplayName} #{index / towers.Length + 1}.", assertions);
            var tower = session.SelectedTower!;
            var stage = (index + index / towers.Length) % 4 + 1;
            if (stage >= 2)
                Require(session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id),
                    $"The mixed-tier battlefield advances {tower.Id} to T2.", assertions);
            if (stage >= 3)
                Require(session.TrySpecializeTower(tower.Id, definition.Specializations[index % definition.Specializations.Count].Id),
                    $"The mixed-tier battlefield advances {tower.Id} to T3.", assertions);
            if (stage == 4)
                Require(session.TryUpgradeTower(tower.Id) && tower.IsApex,
                    $"The mixed-tier battlefield advances {tower.Id} to Apex.", assertions);
        }
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        Require(session.SpawnSandboxTargets("t3_brute", 24, 20, "Elite", true),
            "The mixed-tier battlefield contains durable targets for every attack family.", assertions);
        for (var index = 0; index < session.Enemies.Count; index++)
            session.Enemies[index].SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / session.Enemies.Count,
                session.Map.Path);
        for (var step = 0; step < 42; step++) session.Update(1f / 60);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var checksum = SessionChecksum.Compute(session, 42);
        scenes.Add(Capture("36-mixed-tier-combat.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("36a-mixed-tier-combat-reduced.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = false;
        Require(SessionChecksum.Compute(session, 42) == checksum,
            "Mixed-tier combat and Reduced Effects rendering preserve the deterministic session checksum.", assertions);
        var animation = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_TIER_ANIMATION") == "1";
        var frames = animation ? 120 : 3;
        if (animation) Directory.CreateDirectory(Path.Combine(_outputDirectory, "tier-animation"));
        for (var frame = 0; frame < frames; frame++)
        {
            var steps = animation ? 1 : 9;
            for (var step = 0; step < steps; step++) session.Update(1f / 60);
            ui.AdvanceVisualTime(steps / 60f);
            var fileName = animation ? $"tier-animation/frame-{frame:000}.png" : $"36b-mixed-tier-phase-{frame + 1}.png";
            scenes.Add(Capture(fileName, ui, GameState.Playing, session));
        }
    }

    private VisualVerificationScene SaveTierScene(RenderTarget2D target, string fileName)
    {
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return new VisualVerificationScene(fileName, target.Width, target.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            pixels.Count(pixel => pixel != ColorPalette.Canvas));
    }
}
