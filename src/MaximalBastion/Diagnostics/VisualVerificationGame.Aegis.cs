using MaximalBastion.Combat;
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
    private void AssertAegisPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var shielded = AegisSample(content, "SHIELDED");
        var weakened = AegisSample(content, "WEAKENED");
        ApplyReviewStatus(weakened, "EXPOSED");
        var depleted = AegisSample(content, "SHIELD DEPLETED");
        depleted.Enemy.AbsorbShield(depleted.Enemy.Shield);
        var stacked = AegisSample(content, "WEAKEN + OTHER DEBUFFS");
        ApplyReviewStatus(stacked, "STACKED");
        var samples = new List<StatusReviewEnemy> { shielded, weakened, depleted, stacked };
        foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
            samples.Add(AegisSample(content, role.ToString().ToUpperInvariant(), role));
        samples.Add(AegisSample(content, "ELITE", rank: "Elite"));
        samples.Add(AegisSample(content, "BOSS", rank: "Boss"));
        var expired = AegisSample(content, "WEAKEN EXPIRED");
        expired.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Magnitude = .25f, Duration = .1f });
        expired.Enemy.StatusEffects.Update(.11f);
        samples.Add(expired);

        Require(shielded.Enemy.Shield == content.Enemies["t4_aegis"].Shield &&
                shielded.Enemy.StatusEffects.DamageMultiplier == 1,
            "The normal Aegis review uses its authored shield without an Exposed status.", assertions);
        var normalPixels = AegisPixels(shielded, .23f, true);
        var weakPixels = AegisPixels(weakened, .23f, true);
        Require(normalPixels.Zip(weakPixels, (a, b) => a != b).Count(changed => changed) >= 12,
            "Aegis Weaken adds visible energy that is absent from its ordinary shielded hull.", assertions);
        Require(normalPixels.SequenceEqual(AegisPixels(expired, .23f, true)),
            "Aegis returns to its ordinary shielded appearance immediately after Weaken expires.", assertions);
        Require(!normalPixels.SequenceEqual(AegisPixels(depleted, .23f, true)),
            "Aegis shield depletion visibly removes its active defensive contour.", assertions);
        foreach (var sample in samples)
            Require(AegisPixels(sample, .23f, true).SequenceEqual(AegisPixels(sample, .91f, true)),
                $"Aegis {sample.Label}: Reduced Effects keeps the hull and status treatment steady.", assertions);
        Require(!AegisPixels(weakened, .23f, false).SequenceEqual(AegisPixels(weakened, .91f, false)),
            "Aegis Weaken energy animates with the real enemy render clock.", assertions);
        var checksums = samples.Select(sample => SessionChecksum.Compute(sample.Session, 0)).ToArray();
        scenes.Add(CaptureAegisGallery(samples, font, "57-aegis-states.png", .23f));
        Require(samples.Select((sample, index) => SessionChecksum.Compute(sample.Session, 0) == checksums[index]).All(same => same),
            "Aegis state and signal-variant presentation preserves the synchronized state.", assertions);

        var session = AegisCombatSession(content, assertions, false);
        var prism = session.Towers[0];
        var target = session.Enemies[0];
        var neighbor = session.Enemies[1];
        Require(new TowerSystem(session.TargetSelector).TryFireImmediate(prism, session) &&
                target.StatusEffects.DamageMultiplier > 1 && neighbor.StatusEffects.DamageMultiplier == 1,
            "An actual Prism shot exposes its Aegis primary while the neighboring Aegis keeps its normal appearance.", assertions);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var checksum = SessionChecksum.Compute(session, 0);
        scenes.Add(Capture("57a-aegis-prism-sandbox.png", ui, GameState.Playing, session));
        var previousReduced = _renderer.ReducedEffects;
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("57b-aegis-prism-reduced.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = previousReduced;
        Require(SessionChecksum.Compute(session, 0) == checksum,
            "Full and Reduced Aegis combat renders leave shield, health, timing and status durations unchanged.", assertions);
        var dense = AegisCombatSession(content, assertions, true);
        for (var step = 0; step < 20; step++) dense.Update(1f / 60);
        scenes.Add(Capture("57c-aegis-crowded-sandbox.png", ui, GameState.Playing, dense));

        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_AEGIS_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "aegis-animation"));
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "aegis-combat-animation"));
        for (var frame = 0; frame < 60; frame++)
        {
            for (var step = 0; step < 2; step++) dense.Update(1f / 60);
            ui.AdvanceVisualTime(1f / 30);
            scenes.Add(CaptureAegisGallery(samples.Take(4).ToArray(), font,
                $"aegis-animation/frame-{frame:000}.png", frame / 30f));
            scenes.Add(Capture($"aegis-combat-animation/frame-{frame:000}.png", ui, GameState.Playing, dense));
        }
    }

    private static StatusReviewEnemy AegisSample(GameContent content, string label,
        EnemySignalRole role = EnemySignalRole.None, string rank = "Standard")
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        session.SpawnEnemy("t4_aegis", 5, 1, rank, role);
        var enemy = session.Enemies.Single();
        enemy.SetSandboxPathDistance(430, session.Map.Path);
        return new StatusReviewEnemy(label, session, enemy, Heading: -.48f);
    }

    private void DrawAegisSample(StatusReviewEnemy sample, Vector2 center, float time, bool reduced)
    {
        var enemy = sample.Enemy;
        NightGridArt.Enemy(_batch, _primitives, center, enemy.Radius, enemy.Definition.Id,
            sample.Heading, reduced ? 0 : time, enemy.IsBoss, lights: !reduced);
        EnemySignalArt.DrawHardware(_batch, _primitives, enemy, center, enemy.Radius, sample.Heading, time, reduced);
        EnemyStatusArt.Draw(_batch, _primitives, enemy, center, enemy.Radius, sample.Heading, time, reduced);
    }

    private Color[] AegisPixels(StatusReviewEnemy sample, float time, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 320, 320, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(3));
        DrawAegisSample(sample, new Vector2(53), time, reduced);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureAegisGallery(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, string fileName, float time)
    {
        const int columns = 4;
        const int cellWidth = 400;
        const int cellHeight = 330;
        var rows = (samples.Count + columns - 1) / columns;
        using var target = new RenderTarget2D(GraphicsDevice, columns * cellWidth, 100 + rows * cellHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "AEGIS / DEFENSE AND WEAKEN", new Vector2(24, 15), .95f);
        StatusLabel(font, "3x DETAIL ABOVE / FIELD SIZE AND REDUCED BELOW", new Vector2(24, 53), .68f);
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 100 + index / columns * cellHeight;
            _primitives.FillRect(_batch, new Rectangle(x + 8, y, cellWidth - 16, cellHeight - 10),
                index / columns % 2 == 0 ? ColorPalette.Panel : ColorPalette.PanelAlt);
            StatusLabel(font, samples[index].Label, new Vector2(x + 18, y + 12), .65f);
        }
        _batch.End();
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 100 + index / columns * cellHeight;
            DrawAt(new Vector2(x + cellWidth * .5f, y + 145), 3, false);
            DrawAt(new Vector2(x + cellWidth * .35f, y + 270), 1, false);
            DrawAt(new Vector2(x + cellWidth * .65f, y + 270), 1, true);
            void DrawAt(Vector2 at, float scale, bool reduced)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
                DrawAegisSample(samples[index], Vector2.Zero, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private GameSession AegisCombatSession(GameContent content, ICollection<string> assertions, bool dense)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        for (var step = 0; step < 270; step++) session.Update(1f / 60);
        Require(session.TryPlaceTower("prism_beam", new Vector2(250, 205)),
            "Aegis combat review uses a legal Prism island foundation.", assertions);
        var prism = session.SelectedTower!;
        Require(session.TryChooseTowerDoctrine(prism.Id, "prism_aperture") &&
                session.TrySpecializeTower(prism.Id, "core_lance"),
            "Aegis combat review advances Prism through its authored Shield Piercer branch.", assertions);
        prism.TargetMode = TargetMode.Strongest;
        prism.TickVisual(1);
        session.SpawnEnemy("t4_aegis", 50, .3f);
        session.SpawnEnemy("t4_aegis", 20, .3f);
        session.Enemies[0].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(350, 110)).DistanceAlongPath, session.Map.Path);
        session.Enemies[1].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 300)).DistanceAlongPath, session.Map.Path);
        if (dense)
        {
            var candidates = session.Map.BuildableRegions.SelectMany(region =>
                Enumerable.Range(0, Math.Max(1, region.Width / 32)).SelectMany(x =>
                    Enumerable.Range(0, Math.Max(1, region.Height / 32)).Select(y =>
                        new Vector2(region.Left + 24 + x * 32, region.Top + 24 + y * 32))))
                .OrderBy(point => Vector2.DistanceSquared(point, new Vector2(270, 230))).ToArray();
            foreach (var id in new[] { "needle_turret", "ember_coil", "breaker_cannon", "frost_spire" })
                Require(candidates.Any(point => session.TryPlaceTower(id, point)),
                    $"The dense Aegis review places {id} on a legal foundation.", assertions);
            for (var index = 0; index < 15; index++)
            {
                var id = (index % 4) switch
                {
                    1 => "t1_crawler",
                    2 => "t2_runner",
                    3 => "t3_brute",
                    _ => "t4_aegis"
                };
                session.SpawnEnemy(id, 20, .25f, signalRole: (EnemySignalRole)(index % 6));
                var enemy = session.Enemies[^1];
                enemy.SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 220) / 15, session.Map.Path);
                if (index % 3 == 0)
                    ApplyReviewStatus(new StatusReviewEnemy("STACKED", session, enemy), "STACKED");
            }
        }
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        return session;
    }
}
