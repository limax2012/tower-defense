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
    private sealed record StatusReviewEnemy(string Label, GameSession Session, EnemyInstance Enemy,
        bool Repair = false, bool ShieldPulse = false, float Heading = .38f);

    private void AssertStatusPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var labels = new[] { "PLAIN", "SLOW", "BURN", "BREAK", "EXPOSED", "SHIELD", "HASTE", "STACKED" };
        var samples = new List<StatusReviewEnemy>();
        var stunSamples = new List<StatusReviewEnemy>();
        foreach (var id in new[] { "t2_runner", "t1_crawler", "t3_brute" })
        {
            var baseline = StatusSample(content, id, "PLAIN");
            var plain = StatusEnemyPixels(baseline, .23f, true);
            foreach (var label in labels)
            {
                var sample = StatusSample(content, id, label);
                ApplyReviewStatus(sample, label);
                samples.Add(sample);
                var quiet = StatusEnemyPixels(sample, .23f, true);
                Require(quiet.SequenceEqual(StatusEnemyPixels(sample, .87f, true)),
                    $"{id} {label}: Reduced Effects retains stable hull-attached status geometry.", assertions);
                if (label != "PLAIN")
                    Require(quiet.Zip(plain, (a, b) => a != b).Count(changed => changed) >= 8,
                        $"{id} {label}: the active condition remains visible at the hull without relying on bloom.", assertions);
                if (label == "SLOW")
                {
                    var sides = id == "t2_runner" ? 3 : id == "t1_crawler" ? 4 : 6;
                    var hullRadius = sample.Enemy.Radius * (sides == 3 ? 1 : .85f) * .94f;
                    for (var edge = 0; edge < sides; edge++)
                    {
                        var firstAngle = sample.Heading + edge * MathHelper.TwoPi / sides;
                        var nextAngle = firstAngle + MathHelper.TwoPi / sides;
                        var midpoint = (new Vector2(MathF.Cos(firstAngle), MathF.Sin(firstAngle)) +
                            new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle))) * hullRadius * .5f;
                        var at = (new Vector2(53) + midpoint) * 3;
                        var covered = 0;
                        for (var y = (int)at.Y - 3; y <= (int)at.Y + 3; y++)
                        for (var x = (int)at.X - 3; x <= (int)at.X + 3; x++)
                            if (plain[y * 320 + x] != quiet[y * 320 + x]) covered++;
                        Require(covered >= 2,
                            $"{id}: frost visibly covers hull edge {edge + 1} of {sides}.", assertions);
                    }
                }
                if (label is "SLOW" or "BURN" or "BREAK" or "EXPOSED" or "SHIELD" or "HASTE" or "STACKED")
                    Require(!StatusEnemyPixels(sample, .23f, false).SequenceEqual(StatusEnemyPixels(sample, .87f, false)),
                        $"{id} {label}: full status energy animates while remaining attached to its hull.", assertions);
            }
            var stunned = StatusSample(content, id, "STUN");
            stunned.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 1, Magnitude = 1 });
            var stunQuiet = StatusEnemyPixels(stunned, .23f, true);
            Require(stunQuiet.Zip(plain, (a, b) => a != b).Count(changed => changed) >= 8 &&
                    stunQuiet.SequenceEqual(StatusEnemyPixels(stunned, .87f, true)),
                $"{id}: Stun remains visible as stable internal discharges in Reduced Effects.", assertions);
            Require(!StatusEnemyPixels(stunned, .23f, false).SequenceEqual(StatusEnemyPixels(stunned, .31f, false)),
                $"{id}: Stun electricity flickers within the enemy while active.", assertions);
            var overlap = StatusSample(content, id, "STUN + DEBUFFS");
            ApplyReviewStatus(overlap, "STACKED");
            overlap.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 1, Magnitude = 1 });
            stunSamples.Add(baseline);
            stunSamples.Add(stunned);
            stunSamples.Add(overlap);
            var expiredStun = StatusSample(content, id, "STUN EXPIRED");
            expiredStun.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .1f, Magnitude = 1 });
            expiredStun.Enemy.StatusEffects.Update(.11f);
            Require(StatusEnemyPixels(expiredStun, .23f, true).SequenceEqual(plain),
                $"{id}: Stun discharges disappear immediately when the actual stun expires.", assertions);
            stunSamples.Add(expiredStun);
            var expiring = StatusSample(content, id, "STACKED");
            ApplyReviewStatus(expiring, "STACKED");
            expiring.Enemy.StatusEffects.Update(6);
            expiring.Enemy.AbsorbShield(expiring.Enemy.Shield);
            expiring.Enemy.SetFormationSpeedMultiplier(1);
            Require(StatusEnemyPixels(expiring, .23f, true).SequenceEqual(plain),
                $"{id}: expired debuffs, depleted shield and expired haste leave no rendered residue.", assertions);
        }
        scenes.Add(CaptureStatusEnemyGallery(samples, font, 8, "37-enemy-statuses.png"));
        scenes.Add(CaptureStatusEnemyGallery(stunSamples, font, 4, "37a-arc-stun.png"));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_DEBUFF_ANIMATION") == "1")
        {
            var animated = samples.Where(sample => sample.Label is "SLOW" or "BURN" or "BREAK" or "EXPOSED")
                .Concat(stunSamples.Where(sample => sample.Label == "STUN"))
                .OrderBy(sample => sample.Enemy.Definition.Id).ToArray();
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "debuff-animation"));
            for (var frame = 0; frame < 60; frame++)
                scenes.Add(CaptureStatusEnemyGallery(animated, font, 5,
                    $"debuff-animation/frame-{frame:000}.png", frame / 30f));
        }

        var full = StatusSample(content, "t5_regenerator", "FULL HEALTH");
        var paused = StatusSample(content, "t5_regenerator", "DAMAGE PAUSE");
        paused.Enemy.ApplyHealthDamage(100);
        var healing = StatusSample(content, "t5_regenerator", "REGENERATING");
        healing.Enemy.ApplyHealthDamage(100);
        healing.Enemy.TickRuntimeTimers(1.1f);
        var beforeHeal = healing.Enemy.Health;
        healing.Enemy.Regenerate(.1f);
        Require(!EnemyStatusArt.IsRegenerating(full.Enemy) && !EnemyStatusArt.IsRegenerating(paused.Enemy) &&
                EnemyStatusArt.IsRegenerating(healing.Enemy) && healing.Enemy.Health > beforeHeal,
            "Regeneration art distinguishes full health, the damage pause and actual health recovery.", assertions);
        var burned = StatusSample(content, "t5_regenerator", "BURN / PAUSED");
        burned.Enemy.ApplyHealthDamage(100);
        ApplyReviewStatus(burned, "BURN");
        var shield = StatusSample(content, "t4_aegis", "SHIELDED");
        shield.Enemy.GrantShield(100, 100);
        var depleted = StatusSample(content, "t4_aegis", "SHIELD DEPLETED");
        var repair = StatusSample(content, "t1_crawler", "REPAIR TRANSFER") with { Repair = true };
        repair.Enemy.ApplyHealthDamage(20);
        var grant = StatusSample(content, "t1_crawler", "SHIELD TRANSFER") with { ShieldPulse = true };
        grant.Enemy.GrantShield(30, 30);
        scenes.Add(CaptureStatusEnemyGallery([full, paused, healing, burned, shield, depleted, repair, grant],
            font, 4, "38-status-lifecycles.png"));
        healing.Enemy.Regenerate(1000);
        Require(!EnemyStatusArt.IsRegenerating(healing.Enemy),
            "Regeneration illumination stops when health reaches its maximum.", assertions);
        scenes.Add(CaptureSignalHardwareGallery(content, font));
        scenes.Add(CaptureTowerStatusGallery(content, font));
        AssertSupportLifecycle(content, font, display, assertions, scenes);
        AssertSignalLifecycle(content, assertions);
        CaptureDenseStatusCombat(content, font, display, assertions, scenes);
    }

    private static StatusReviewEnemy StatusSample(GameContent content, string id, string label,
        EnemySignalRole role = EnemySignalRole.None)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy(id, 5, 1, signalRole: role);
        var enemy = session.Enemies.Single();
        enemy.SetSandboxPathDistance(430, session.Map.Path);
        enemy.AbsorbShield(enemy.Shield);
        return new StatusReviewEnemy(label, session, enemy);
    }

    private static void ApplyReviewStatus(StatusReviewEnemy sample, string label)
    {
        var enemy = sample.Enemy;
        foreach (var (name, type, magnitude) in new[]
                 { ("SLOW", StatusType.Slow, .45f), ("BURN", StatusType.Burn, 6f),
                   ("BREAK", StatusType.ArmorBreak, 3f), ("EXPOSED", StatusType.Exposed, .24f) })
            if (label == name || label == "STACKED")
                enemy.ApplyStatus(new StatusApplication { Type = type, Duration = 5, Magnitude = magnitude, SourceId = 10 });
        if (label is "SHIELD" or "STACKED") enemy.GrantShield(30, 30);
        if (label is "HASTE" or "STACKED")
        {
            sample.Session.SpawnEnemy("t1_crawler", 1, 1, signalRole: EnemySignalRole.Accelerator);
            sample.Session.Enemies[^1].SetSandboxPathDistance(400, sample.Session.Map.Path);
            sample.Session.RefreshEnemySignalFormation();
        }
    }

    private Color[] StatusEnemyPixels(StatusReviewEnemy sample, float time, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 320, 320,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(3));
        DrawStatusEnemy(sample, new Vector2(53), sample.Heading, time, reduced);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private void DrawStatusEnemy(StatusReviewEnemy sample, Vector2 center, float heading, float time, bool reduced)
    {
        var enemy = sample.Enemy;
        NightGridArt.Enemy(_batch, _primitives, center, enemy.Radius, enemy.Definition.Id, heading, 0,
            enemy.IsBoss, lights: false);
        EnemySignalArt.DrawHardware(_batch, _primitives, enemy, center, enemy.Radius, heading, time, reduced);
        EnemyStatusArt.Draw(_batch, _primitives, enemy, center, enemy.Radius, heading, time, reduced);
        if (sample.Repair)
            EnemyStatusArt.DrawRepairPulse(_batch, _primitives, enemy, center, enemy.Radius, heading, .78f, time, reduced);
        if (sample.ShieldPulse)
            EnemyStatusArt.DrawShieldPulse(_batch, _primitives, enemy, center, enemy.Radius, heading, .78f, time, reduced);
    }

    private VisualVerificationScene CaptureStatusEnemyGallery(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, int columns, string fileName, float time = .23f)
    {
        var rows = (samples.Count + columns - 1) / columns;
        var cellWidth = columns == 8 ? 260 : 440;
        var width = columns * cellWidth;
        var height = 100 + rows * 300;
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "ENEMY STATES / HULL-ATTACHED ENERGY", new Vector2(24, 15), .95f);
        StatusLabel(font, "3x DETAIL ABOVE / FIELD SIZE AND REDUCED BELOW", new Vector2(24, 53), .68f);
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 100 + index / columns * 300;
            _primitives.FillRect(_batch, new Rectangle(x + 8, y, cellWidth - 16, 290),
                index / columns % 2 == 0 ? ColorPalette.Panel : ColorPalette.PanelAlt);
            StatusLabel(font, samples[index].Label, new Vector2(x + 18, y + 12), .65f);
            StatusLabel(font, samples[index].Enemy.Definition.DisplayName,
                new Vector2(x + 18, y + 44), .54f);
        }
        _batch.End();
        for (var index = 0; index < samples.Count; index++)
        {
            var x = index % columns * cellWidth;
            var y = 100 + index / columns * 300;
            DrawAt(new Vector2(x + cellWidth * .5f, y + 141), 3, false);
            DrawAt(new Vector2(x + cellWidth * .35f, y + 248), 1, false);
            DrawAt(new Vector2(x + cellWidth * .65f, y + 248), 1, true);
            void DrawAt(Vector2 at, float scale, bool reduced)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
                DrawStatusEnemy(samples[index], Vector2.Zero, samples[index].Heading, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private VisualVerificationScene CaptureSignalHardwareGallery(GameContent content, SpriteFont font)
    {
        var samples = new List<StatusReviewEnemy>();
        foreach (var id in new[] { "t2_runner", "t1_crawler", "t3_brute" })
        foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
        {
            var sample = StatusSample(content, id, role.ToString().ToUpperInvariant(), role) with
                { Heading = id == "t2_runner" ? .38f : id == "t1_crawler" ? -1.1f : 2.2f };
            if (id == "t3_brute") ApplyReviewStatus(sample, "STACKED");
            samples.Add(sample);
        }
        return CaptureStatusEnemyGallery(samples, font, 5, "39-signal-hardware.png");
    }

    private VisualVerificationScene CaptureTowerStatusGallery(GameContent content, SpriteFont font)
    {
        var towers = content.Towers.Values.ToArray();
        var labels = new[] { "IDLE", "SUPPORTED", "PROTOCOL", "SUPPRESSED", "DISRUPTED", "SUPPORT + PROTOCOL" };
        const int width = 2240;
        using var target = new RenderTarget2D(GraphicsDevice, width, 110 + towers.Length * 200,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "TOWER STATES / POWER AND OUTPUT HARDWARE", new Vector2(24, 16), .95f);
        StatusLabel(font, "T3 / 2x DETAIL / FIELD SIZE AND REDUCED BELOW", new Vector2(24, 55), .65f);
        for (var row = 0; row < towers.Length; row++)
        {
            var y = 110 + row * 200;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, 192),
                row % 2 == 0 ? ColorPalette.Panel : ColorPalette.PanelAlt);
            StatusLabel(font, towers[row].DisplayName, new Vector2(28, y + 80), .65f);
            for (var column = 0; column < labels.Length; column++)
                StatusLabel(font, towers[row].Id == "signal_beacon" && column is 1 or 3 ? "NOT ELIGIBLE" : labels[column],
                    new Vector2(220 + column * 330, y + 12), .52f);
        }
        _batch.End();
        for (var row = 0; row < towers.Length; row++)
        for (var column = 0; column < labels.Length; column++)
        {
            var at = new Vector2(345 + column * 330, 110 + row * 200);
            Sample(at + new Vector2(0, 81), 2, false);
            Sample(at + new Vector2(-45, 155), 1, false);
            Sample(at + new Vector2(45, 155), 1, true);
            void Sample(Vector2 center, float scale, bool reduced)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X, center.Y, 0));
                var tower = towers[row];
                const float aim = -.55f;
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, tower.Visual.Radius, tower.Id,
                    3, aim, lights: false, groundEffects: false);
                if (tower.Id != "signal_beacon" && column is 1 or 5)
                    TowerStatusArt.DrawSupport(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced);
                if (column is 2 or 5)
                    TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced);
                if (column == 3 && tower.Id != "signal_beacon")
                    TowerStatusArt.DrawSuppressed(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced);
                if (column == 4)
                    TowerStatusArt.DrawDisrupted(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, "40-tower-statuses.png");
    }

    private void AssertSupportLifecycle(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("signal_beacon", new Vector2(205, 350)) &&
                session.TryPlaceTower("prism_beam", new Vector2(270, 350)),
            "The support lifecycle uses legal Beacon and Prism positions.", assertions);
        var source = session.Towers[0];
        var recipient = session.Towers[1];
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        session.Update(0);
        Require(session.GetSupportBuff(recipient).IsActive, "A live in-range Beacon powers its eligible recipient.", assertions);
        scenes.Add(Capture("41-support-active.png", ui, GameState.Playing, session));
        session.ToggleSandboxTower(source.Id);
        session.Update(0);
        Require(!session.GetSupportBuff(recipient).IsActive, "Disabling a Beacon removes its receiving status on the next simulation update.", assertions);
        scenes.Add(Capture("41a-support-source-disabled.png", ui, GameState.Playing, session));
        session.ToggleSandboxTower(source.Id);
        source.ApplyDisruption(.3f, 0);
        session.Update(0);
        Require(!session.GetSupportBuff(recipient).IsActive, "A disrupted Beacon stops supplying support.", assertions);
        scenes.Add(Capture("41b-support-source-disrupted.png", ui, GameState.Playing, session));
        source.TickVisual(.31f);
        session.Update(0);
        Require(session.GetSupportBuff(recipient).IsActive, "Support resumes after source disruption expires.", assertions);
        recipient.ApplySuppression(.3f, 0);
        session.Update(0);
        Require(session.GetSupportBuff(recipient).IsActive && recipient.IsSuppressed,
            "Suppression lowers output while retaining valid Beacon support.", assertions);
        scenes.Add(Capture("41c-support-plus-suppression.png", ui, GameState.Playing, session));
        recipient.TickVisual(.31f);
        recipient.ApplyDisruption(.3f, 0);
        session.Update(0);
        Require(!session.GetSupportBuff(recipient).IsActive, "A disrupted recipient loses its receiving support status.", assertions);
        recipient.TickVisual(.31f);
        recipient.ActivateOverdrive();
        Require(recipient.IsOverdriven, "A Protocol becomes active for its actual authored duration.", assertions);
        recipient.TickVisual(recipient.Protocol.DurationSeconds + .1f);
        Require(!recipient.IsOverdriven && !recipient.IsSuppressed && !recipient.IsDisrupted,
            "Expired Protocol, suppression and disruption timers leave no active temporary tower state.", assertions);
        session.RemoveSandboxTower(source.Id);
        session.Update(0);
        Require(!session.GetSupportBuff(recipient).IsActive, "Removing the Beacon source removes receiving support.", assertions);
        scenes.Add(Capture("41d-support-source-removed.png", ui, GameState.Playing, session));
    }

    private static void AssertSignalLifecycle(GameContent content, ICollection<string> assertions)
    {
        var sample = StatusSample(content, "t1_crawler", "TARGET");
        var session = sample.Session;
        var recipient = sample.Enemy;
        recipient.ApplyHealthDamage(50);
        session.SpawnEnemy("t1_crawler", 5, 1, signalRole: EnemySignalRole.Restorer);
        var restorer = session.Enemies[^1];
        restorer.SetSandboxPathDistance(400, session.Map.Path);
        restorer.ArmSignalAbility(0);
        var before = recipient.Health;
        session.TryActivateEnemySignal(restorer);
        Require(recipient.Health > before && recipient.DamagePauseTimer > 0 && session.Effects.Effects.Count > 0,
            "A Restorer performs a real repair transfer even during the recipient's regeneration damage pause.", assertions);
        session.Effects.Update(1);
        Require(session.Effects.Effects.Count == 0, "Expired signal transfer effects leave no event residue.", assertions);
        session.SpawnEnemy("t1_crawler", 5, 1, signalRole: EnemySignalRole.Bulwark);
        var bulwark = session.Enemies[^1];
        bulwark.SetSandboxPathDistance(400, session.Map.Path);
        bulwark.ArmSignalAbility(0);
        session.TryActivateEnemySignal(bulwark);
        Require(recipient.Shield > 0, "A Bulwark grants actual persistent shielding to a nearby recipient.", assertions);
        session.Enemies.Remove(bulwark);
        Require(recipient.Shield > 0, "Granted shields persist after their Bulwark source is removed.", assertions);
        recipient.AbsorbShield(recipient.Shield);
        Require(recipient.Shield == 0, "Shield depletion removes the condition represented by shield hull art.", assertions);
        session.SpawnEnemy("t1_crawler", 5, 1, signalRole: EnemySignalRole.Accelerator);
        var accelerator = session.Enemies[^1];
        accelerator.SetSandboxPathDistance(400, session.Map.Path);
        session.RefreshEnemySignalFormation();
        Require(EnemyStatusArt.IsAccelerated(recipient), "An in-range Accelerator powers an actual moving recipient.", assertions);
        session.Enemies.Remove(accelerator);
        session.RefreshEnemySignalFormation();
        Require(!EnemyStatusArt.IsAccelerated(recipient), "Accelerator drive illumination stops when its source disappears.", assertions);
    }

    private void CaptureDenseStatusCombat(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
        for (var x = region.Left + 24; x < region.Right - 16; x += 24) candidates.Add(new Vector2(x, y));
        var definitions = content.Towers.Values.ToArray();
        for (var index = 0; index < 20; index++)
        {
            var definition = definitions[index % definitions.Length];
            Require(candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 360)))
                    .Any(point => session.TryPlaceTower(definition.Id, point)),
                $"Status combat places {definition.DisplayName} on a legal deck.", assertions);
            var tower = session.SelectedTower!;
            if (index >= 10)
            {
                session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id);
                session.TrySpecializeTower(tower.Id, definition.Specializations[0].Id);
            }
            if (index % 5 == 0) tower.ActivateOverdrive();
            if (index % 7 == 0) tower.ApplySuppression(4, 0);
            else if (index % 9 == 0) tower.ApplyDisruption(1.25f, 0);
        }
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var enemies = content.Enemies.Values.ToArray();
        for (var index = 0; index < 24; index++)
        {
            session.SpawnEnemy(enemies[index % enemies.Length].Id, 100, .2f,
                signalRole: (EnemySignalRole)(index % 6));
            var enemy = session.Enemies[^1];
            enemy.SetSandboxPathDistance(100 + index * (session.Map.Path.TotalLength - 200) / 24, session.Map.Path);
            enemy.ApplyHealthDamage(enemy.MaxHealth * .12f);
            enemy.TickRuntimeTimers(1.1f);
            foreach (var status in new[] { StatusType.Slow, StatusType.Burn, StatusType.ArmorBreak, StatusType.Exposed })
                if ((index + (int)status) % 3 != 0)
                    enemy.ApplyStatus(new StatusApplication { Type = status, Duration = 4.5f,
                        Magnitude = status == StatusType.ArmorBreak ? 4 : status == StatusType.Burn ? 4 : .35f,
                        SourceId = session.Towers.First(tower => tower.Definition.Id == "ember_coil").Id });
            enemy.ArmSignalAbility(0);
        }
        for (var step = 0; step < 12; step++) session.Update(1f / 60);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var checksum = SessionChecksum.Compute(session, 12);
        scenes.Add(Capture("42-status-combat-full.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("42a-status-combat-reduced.png", ui, GameState.Playing, session));
        _renderer.ReducedEffects = false;
        Require(SessionChecksum.Compute(session, 12) == checksum,
            "Full and Reduced status rendering preserve synchronized combat state.", assertions);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_STATUS_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "status-animation"));
        for (var frame = 0; frame < 120; frame++)
        {
            session.Update(1f / 60);
            ui.AdvanceVisualTime(1f / 60);
            scenes.Add(Capture($"status-animation/frame-{frame:000}.png", ui, GameState.Playing, session));
        }
    }

    private void StatusLabel(SpriteFont font, string text, Vector2 at, float scale) =>
        _batch.DrawString(font, text, at, ColorPalette.Paper, 0, Vector2.Zero,
            scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
}
