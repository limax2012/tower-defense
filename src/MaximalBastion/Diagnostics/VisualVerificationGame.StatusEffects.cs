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
        foreach (var id in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
        {
            var baseline = StatusSample(content, id, "PLAIN");
            var plain = StatusEnemyPixels(baseline, .23f, true);
            var nativePlain = StatusEnemyPixels(baseline, .23f, true, scale: 1);
            foreach (var label in labels)
            {
                var sample = StatusSample(content, id, label);
                ApplyReviewStatus(sample, label);
                samples.Add(sample);
                var quiet = StatusEnemyPixels(sample, .23f, true);
                Require(quiet.SequenceEqual(StatusEnemyPixels(sample, .87f, true)),
                    $"{id} {label}: Reduced Effects retains stable hull-attached status geometry.", assertions);
                if (label != "PLAIN")
                {
                    Require(quiet.Zip(plain, (a, b) => a != b).Count(changed => changed) >= 8,
                        $"{id} {label}: the active condition remains visible at the hull without relying on bloom.", assertions);
                    var native = StatusEnemyPixels(sample, .23f, true, scale: 1);
                    var changedBodyPixels = native.Where((pixel, index) => pixel != nativePlain[index]).Count();
                    var minimumBodyPixels = label == "EXPOSED" ? 4 : 10;
                    Require(changedBodyPixels >= minimumBodyPixels,
                        $"{id} {label}: secondary hull reinforcement remains visible at native scale ({changedBodyPixels} changed pixels).", assertions);
                    if (label == "EXPOSED")
                        Require(native.Where((pixel, index) => pixel != nativePlain[index] &&
                                pixel.B > pixel.G * 1.08f && pixel.R > pixel.G * 1.02f).Count() >= 2,
                            $"{id}: the small Expose incision retains its Prism source color at native scale.", assertions);
                }
                if (label == "SLOW")
                {
                    AssertFrostMaterialCoverage(content, sample, nativePlain, assertions);
                    Require(StatusEnemyPixels(sample, .23f, false, materialOnly: true)
                            .SequenceEqual(StatusEnemyPixels(sample, .87f, false, materialOnly: true)),
                        $"{id}: settled matte rime stays attached without a continuous surface wobble.", assertions);
                    AssertMaterialApplication(content, id, StatusType.Slow, assertions);
                }
                if (label == "BREAK")
                    Require(StatusEnemyPixels(sample, .23f, false, materialOnly: true)
                            .SequenceEqual(StatusEnemyPixels(sample, .87f, false, materialOnly: true)),
                        $"{id} {label}: attached solid material remains steady between attack events.", assertions);
                if (label is "BURN" or "EXPOSED" or "SHIELD" or "HASTE" or "STACKED")
                    Require(!StatusEnemyPixels(sample, .23f, false, materialOnly: true)
                            .SequenceEqual(StatusEnemyPixels(sample, .87f, false, materialOnly: true)),
                        $"{id} {label}: sustained activity animates on the body without a HUD glyph.", assertions);
            }
            var stunned = StatusSample(content, id, "STUN");
            stunned.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 1, Magnitude = 1 });
            var stunQuiet = StatusEnemyPixels(stunned, .23f, true);
            Require(stunQuiet.Zip(plain, (a, b) => a != b).Count(changed => changed) >= 8 &&
                    stunQuiet.SequenceEqual(StatusEnemyPixels(stunned, .87f, true)),
                $"{id}: Stun remains visible as stable internal discharges in Reduced Effects.", assertions);
            Require(!StatusEnemyPixels(stunned, .23f, false, materialOnly: true)
                    .SequenceEqual(StatusEnemyPixels(stunned, .87f, false, materialOnly: true)),
                $"{id}: Stun sustains changing fine discharge paths for its actual active duration.", assertions);
            AssertMaterialApplication(content, id, StatusType.Stun, assertions);
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
        AssertConditionComposition(content, assertions, scenes, font);
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
            "Regeneration state distinguishes full health, the damage pause and actual health recovery.", assertions);
        foreach (var reduced in new[] { false, true })
        {
            var activePixels = StatusEnemyPixels(healing, .23f, reduced, scale: 1, materialOnly: true);
            var pausedPixels = StatusEnemyPixels(paused, .23f, reduced, scale: 1, materialOnly: true);
            var fullPixels = StatusEnemyPixels(full, .23f, reduced, scale: 1, materialOnly: true);
            Require(activePixels.SequenceEqual(fullPixels) && pausedPixels.SequenceEqual(fullPixels) &&
                    fullPixels.All(pixel => pixel == ColorPalette.Canvas),
                $"Regenerator {(reduced ? "Reduced" : "Full")}: native repair leaves the separate status-overlay layer empty.", assertions);
            var fullBody = StatusEnemyPixels(full, .23f, reduced, scale: 1, runtimePresentation: true);
            Require(StatusEnemyPixels(paused, .23f, reduced, scale: 1, runtimePresentation: true).SequenceEqual(fullBody) &&
                    !StatusEnemyPixels(healing, .23f, reduced, scale: 1, runtimePresentation: true).SequenceEqual(fullBody),
                $"Regenerator {(reduced ? "Reduced" : "Full")}: the chassis shows a repair cue only while health recovery is active.", assertions);
        }
        Require(StatusEnemyPixels(healing, .23f, true, runtimePresentation: true)
                .SequenceEqual(StatusEnemyPixels(healing, .87f, true, runtimePresentation: true)),
            "Reduced Effects holds the active repair cue steady.", assertions);
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
            "Native regeneration stops when health reaches its maximum.", assertions);
        scenes.Add(CaptureSignalHardwareGallery(content, font));
        scenes.Add(CaptureTowerStatusGallery(content, font));
        AssertSupportLifecycle(content, font, display, assertions, scenes);
        AssertSignalLifecycle(content, assertions);
        CaptureDenseStatusCombat(content, font, display, assertions, scenes);
        AssertAegisPresentation(content, font, display, assertions, scenes);
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

    private void AssertFrostMaterialCoverage(GameContent content, StatusReviewEnemy sample,
        Color[] plain, ICollection<string> assertions)
    {
        var enemy = sample.Enemy;
        var radius = EnemyModelArt.BodyRadius(enemy);
        var forward = new Vector2(MathF.Cos(sample.Heading), MathF.Sin(sample.Heading));
        var side = new Vector2(-forward.Y, forward.X);
        var frost = StatusEnemyPixels(sample, .23f, true, scale: 1);
        var shield = StatusSample(content, enemy.Definition.Id, "SHIELD");
        ApplyReviewStatus(shield, "SHIELD");
        var shieldOnly = StatusEnemyPixels(shield, .23f, true, scale: 1, materialOnly: true);
        var shieldBody = StatusEnemyPixels(shield, .23f, true, scale: 1);
        ApplyReviewStatus(shield, "SLOW");
        var frostShield = StatusEnemyPixels(shield, .23f, true, scale: 1);
        var banks = new int[2];
        var corePixels = 0;
        var changedCore = 0;
        var centerlinePixels = 0;
        var changedCenterline = 0;
        var shellPixels = 0;
        var changedShell = 0;
        var frostOnShell = 0;
        var mask = Enumerable.Repeat(ColorPalette.Canvas, plain.Length).ToArray();
        for (var index = 0; index < frost.Length; index++)
        {
            var at = new Vector2(index % 320 + .5f, index / 320 + .5f) - new Vector2(53);
            var local = new Vector2(Vector2.Dot(at, forward), Vector2.Dot(at, side));
            var changed = frost[index] != plain[index];
            if (changed && MathF.Abs(local.Y) > radius * .12f)
            {
                banks[local.Y < 0 ? 0 : 1]++;
                mask[index] = local.Y < 0 ? ColorPalette.FrostShot : ColorPalette.FrostHighlight;
            }
            if (at.LengthSquared() < radius * radius * .5f * .5f &&
                plain[index].R > plain[index].G * 1.6f && plain[index].B > plain[index].G * 1.3f)
            {
                corePixels++;
                if (changed) changedCore++;
                mask[index] = changed ? ColorPalette.Burn : ColorPalette.Hostile;
            }
            if (MathF.Abs(local.Y) < radius * .08f && MathF.Abs(local.X) > radius * .28f &&
                MathF.Abs(local.X) < radius * .58f && plain[index] != ColorPalette.Canvas)
            {
                centerlinePixels++;
                if (changed) changedCenterline++;
                mask[index] = changed ? ColorPalette.Burn : ColorPalette.Green;
            }
            var shell = shieldOnly[index];
            if (shell.B > 185 && shell.G > 140 && shell.B > shell.R * 1.7f)
            {
                shellPixels++;
                if (frostShield[index] != shieldBody[index]) changedShell++;
                if (changed) frostOnShell++;
                mask[index] = frostShield[index] != shieldBody[index] ? ColorPalette.Burn : ColorPalette.Shield;
            }
        }
        var directory = Path.Combine(_outputDirectory, "material-masks");
        Directory.CreateDirectory(directory);
        var report = new
        {
            family = enemy.Definition.Id, resolution = "native logical pixels", radius,
            bankPixels = banks, corePixels, changedCore, centerlinePixels, changedCenterline,
            shellPixels, changedShell, frostOnShell,
            maskLegend = "blue/pale: changed bank material; magenta: preserved reactor; green: preserved centerline material; cyan: preserved shield core; red: constraint violation"
        };
        File.WriteAllText(Path.Combine(directory, enemy.Definition.Id + ".json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        using (var texture = new Texture2D(GraphicsDevice, 320, 320))
        {
            texture.SetData(mask);
            using var stream = File.Create(Path.Combine(directory, enemy.Definition.Id + ".png"));
            texture.SaveAsPng(stream, 320, 320);
        }
        Require(banks[0] >= 8 && banks[1] >= 8,
            $"{enemy.Definition.Id}: frost visibly coats both authored plate banks ({banks[0]}/{banks[1]} native pixels).", assertions);
        Require(corePixels > 0 && changedCore == 0 && centerlinePixels > 0 && changedCenterline == 0,
            $"{enemy.Definition.Id}: the frosted banks retain the reactor and centerline material ({corePixels}/{centerlinePixels} protected native pixels).", assertions);
        Require(shellPixels >= 8 && changedShell == 0 && frostOnShell == 0,
            $"{enemy.Definition.Id}: frost stays separate from all {shellPixels} sampled luminous shell pixels and cannot form a second shield outline.", assertions);
    }

    private void AssertMaterialApplication(GameContent content, string id, StatusType type,
        ICollection<string> assertions)
    {
        var sample = StatusSample(content, id, type.ToString());
        var timeline = new EnemyVisualTimeline();
        timeline.Observe(sample.Session, 0);
        sample.Enemy.ApplyStatus(new StatusApplication { Type = type, Magnitude = .4f, Duration = 1 });
        var checksum = SessionChecksum.Compute(sample.Session, 0);
        timeline.Observe(sample.Session, .1f);
        var onset = StatusEnemyPixels(sample, .1f, false, scale: 1, materialOnly: true,
            feedback: timeline.Feedback(sample.Enemy, .1f));
        timeline.Observe(sample.Session, .7f);
        var settled = StatusEnemyPixels(sample, .7f, false, scale: 1, materialOnly: true,
            feedback: timeline.Feedback(sample.Enemy, .7f));
        Require(onset.Count(pixel => pixel != ColorPalette.Canvas) >= 8 && !onset.SequenceEqual(settled),
            $"{id} {type}: an actual observed application starts visible material that settles into its ordinary state.", assertions);
        Require(SessionChecksum.Compute(sample.Session, 0) == checksum,
            $"{id} {type}: material onset and settling leave authoritative timing unchanged.", assertions);
        sample.Enemy.StatusEffects.Update(2);
        timeline.Observe(sample.Session, .8f);
        var expired = StatusEnemyPixels(sample, .8f, false, scale: 1, materialOnly: true,
            feedback: timeline.Feedback(sample.Enemy, .8f));
        Require(expired.All(pixel => pixel == ColorPalette.Canvas),
            $"{id} {type}: expiry immediately removes hull-attached material; detached remnants are separate.", assertions);
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

    private Color[] StatusEnemyPixels(StatusReviewEnemy sample, float time, bool reduced,
        float scale = 3, bool materialOnly = false, EnemyVisualFeedback? feedback = null, bool runtimePresentation = false)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 320, 320,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
        if (materialOnly)
            EnemyStatusArt.DrawAnimated(_batch, _primitives, sample.Enemy, new Vector2(53),
                EnemyModelArt.BodyRadius(sample.Enemy), sample.Heading, time,
                feedback ?? new EnemyVisualFeedback(1, 0, 0, 0), reduced,
                exposureAccent: EnemyConditionPresentation.ExposeAccent(sample.Session, sample.Enemy));
        else
            DrawStatusEnemy(sample, new Vector2(53), sample.Heading, time, reduced, indicators: runtimePresentation);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private void DrawStatusEnemy(StatusReviewEnemy sample, Vector2 center, float heading, float time, bool reduced,
        bool indicators = true)
    {
        var enemy = sample.Enemy;
        var radius = EnemyModelArt.BodyRadius(enemy);
        EnemyModelArt.Draw(_batch, _primitives, enemy, center, heading, time, reduced);

        var exposureAccent = EnemyConditionPresentation.ExposeAccent(sample.Session, enemy);
        if (indicators)
        {
            EnemyDebuffAuraArt.Draw(_batch, _primitives, sample.Session, enemy, center, radius, time, reduced);
            EnemyStatusArt.DrawDurability(_batch, _primitives, enemy, center, radius, heading, time, reduced);
            EnemyStunArt.Draw(_batch, _primitives, enemy, center, radius, heading, time, reduced);
        }
        else
            EnemyStatusArt.Draw(_batch, _primitives, enemy, center, radius, heading, time, reduced,
                exposureAccent: exposureAccent);
        if (sample.Repair)
            EnemyStatusArt.DrawRepairPulse(_batch, _primitives, enemy, center, radius, heading, .78f, time, reduced);
        if (sample.ShieldPulse)
            EnemyStatusArt.DrawShieldPulse(_batch, _primitives, enemy, center, radius, heading, .78f, time, reduced);
    }

    private void AssertConditionComposition(GameContent content, ICollection<string> assertions,
        ICollection<VisualVerificationScene> scenes, SpriteFont font)
    {
        var combined = new List<StatusReviewEnemy>();
        Span<EnemyCondition> zones = stackalloc EnemyCondition[4];
        foreach (var id in content.Enemies.Keys)
        foreach (var pair in new[] { "BURN + FROST", "BURN + BREAK", "FROST + EXPOSE", "SHIELD + BURN", "STUN + BREAK", "ALL CONDITIONS" })
        {
            var sample = StatusSample(content, id, pair);
            if (pair.Contains("BURN", StringComparison.Ordinal) || pair == "ALL CONDITIONS") ApplyReviewStatus(sample, "BURN");
            if (pair.Contains("FROST", StringComparison.Ordinal) || pair == "ALL CONDITIONS") ApplyReviewStatus(sample, "SLOW");
            if (pair.Contains("BREAK", StringComparison.Ordinal) || pair == "ALL CONDITIONS") ApplyReviewStatus(sample, "BREAK");
            if (pair.Contains("EXPOSE", StringComparison.Ordinal) || pair == "ALL CONDITIONS") ApplyReviewStatus(sample, "EXPOSED");
            if (pair.Contains("STUN", StringComparison.Ordinal) || pair == "ALL CONDITIONS")
                sample.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 5, Magnitude = 1 });
            if (pair.Contains("SHIELD", StringComparison.Ordinal) || pair == "ALL CONDITIONS")
                sample.Enemy.GrantShield(sample.Enemy.MaxHealth * .10f, sample.Enemy.MaxHealth * .20f);
            var before = SessionChecksum.Compute(sample.Session, 0);
            var composition = EnemyConditionPresentation.Compose(sample.Enemy);
            var regionCount = EnemyDebuffAuraArt.CopyDebuffs(sample.Enemy, zones);
            Require(regionCount <= 4 && regionCount <= composition.ActiveCount,
                $"{id} {pair}: at most four translucent debuff zones summarize the inspectable condition set.", assertions);
            if (pair == "ALL CONDITIONS")
                Require(regionCount == 4 && composition.Has(EnemyCondition.Stun),
                    $"{id}: four sustained debuffs use fog while Stun remains in the inspectable condition set.", assertions);
            _ = StatusEnemyPixels(sample, .23f, false);
            _ = StatusEnemyPixels(sample, .87f, true);
            Require(before == SessionChecksum.Compute(sample.Session, 0),
                $"{id} {pair}: condition composition and rendering leave synchronized state unchanged.", assertions);
            combined.Add(sample);
        }
        scenes.Add(CaptureStatusEnemyGallery(combined, font, 6, "64-condition-combinations.png"));

        var healingBurn = StatusSample(content, "t5_regenerator", "REPAIR + BURN");
        healingBurn.Enemy.ApplyHealthDamage(150);
        healingBurn.Enemy.TickRuntimeTimers(1.1f);
        healingBurn.Enemy.GrantShield(healingBurn.Enemy.MaxHealth * .10f, healingBurn.Enemy.MaxHealth * .20f);
        ApplyReviewStatus(healingBurn, "BURN");
        var pausedBurn = StatusSample(content, "t5_regenerator", "REPAIR PAUSED + BURN");
        pausedBurn.Enemy.ApplyHealthDamage(150);
        ApplyReviewStatus(pausedBurn, "BURN");
        Require(EnemyConditionPresentation.IsPresent(healingBurn.Enemy, EnemyCondition.Regeneration) &&
                !EnemyConditionPresentation.IsPresent(pausedBurn.Enemy, EnemyCondition.Regeneration),
            "Repair activity reflects the health-damage pause independently of the burn's material condition.", assertions);
        var shieldSamples = new List<StatusReviewEnemy>();
        foreach (var fraction in new[] { 1f, .5f, .125f, 0f })
        {
            var sample = StatusSample(content, "t4_aegis", $"SHIELD {fraction:P0}");
            sample.Enemy.GrantShield(sample.Enemy.Definition.Shield * fraction, sample.Enemy.Definition.Shield);
            Require(MathF.Abs(EnemyStatusArt.ShieldFraction(sample.Enemy) - fraction) < .001f,
                $"Aegis: the quantitative reserve remains {fraction:P0} of authored shield capacity.", assertions);
            shieldSamples.Add(sample);
        }
        foreach (var reduced in new[] { false, true })
        {
            var intact = StatusEnemyPixels(shieldSamples[0], .23f, reduced, scale: 1, materialOnly: true);
            foreach (var sample in shieldSamples.Skip(1).Take(2))
                Require(intact.SequenceEqual(StatusEnemyPixels(sample, .23f, reduced, scale: 1, materialOnly: true)),
                    $"Aegis {(reduced ? "Reduced" : "Full")}: every positive reserve retains the same whole shield shell.", assertions);
            var broken = StatusEnemyPixels(shieldSamples[3], .23f, reduced, scale: 1, materialOnly: true);
            Require(broken.All(pixel => pixel == ColorPalette.Canvas) && intact.Count(pixel => pixel != ColorPalette.Canvas) >= 30,
                $"Aegis {(reduced ? "Reduced" : "Full")}: the shield shell disappears only when its actual reserve reaches zero.", assertions);
        }
        scenes.Add(CaptureStatusEnemyGallery([healingBurn, pausedBurn, .. shieldSamples], font, 3,
            "64a-repair-shield-transitions.png"));

        var all = StatusSample(content, "t5_regenerator", "ALL ACTIVE STATES");
        all.Enemy.ApplyHealthDamage(150);
        all.Enemy.TickRuntimeTimers(1.1f);
        ApplyReviewStatus(all, "STACKED");
        all.Enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 5, Magnitude = 1 });
        Span<EnemyCondition> active = stackalloc EnemyCondition[8];
        var written = EnemyConditionPresentation.CopyActive(all.Enemy, active);
        var every = EnemyConditionPresentation.Compose(all.Enemy);
        Span<EnemyCondition> allZones = stackalloc EnemyCondition[4];
        Require(written == 8 && every.ActiveCount == 8 && EnemyDebuffAuraArt.CopyDebuffs(all.Enemy, allZones) == 4,
            "Four sustained debuffs form fog regions while all eight states remain available as inspector text.", assertions);
        Require(EnemyConditionPresentation.Accent(EnemyCondition.Exposed) == ColorPalette.Violet &&
                EnemyConditionPresentation.Accent(EnemyCondition.Burn) == ColorPalette.EmberShot &&
                EnemyConditionPresentation.Accent(EnemyCondition.ArmorBreak) == ColorPalette.BreakerShot &&
                EnemyConditionPresentation.Accent(EnemyCondition.Stun) == ColorPalette.ArcShot &&
                EnemyConditionPresentation.Accent(EnemyCondition.Slow) == ColorPalette.FrostAura,
            "Continuing conditions use canonical prismatic, thermal, fracture, electrical, and frost colors.", assertions);
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
        StatusLabel(font, "ENEMY STATES / PHYSICAL CONDITIONS", new Vector2(24, 15), .95f);
        StatusLabel(font, "SYNTHETIC STATE FIXTURES / 3x DETAIL / FIELD SIZE AND REDUCED BELOW", new Vector2(24, 53), .68f);
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
                if (column is 2 or 5)
                    TowerProtocolArt.DrawEnvelope(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, .23f, reduced);
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, tower.Visual.Radius, tower.Id,
                    3, aim, lights: false, groundEffects: false, power: column == 4 ? .18f : 1);
                if (tower.Id != "signal_beacon" && column is 1 or 5)
                    TowerStatusArt.DrawSupport(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced);
                if (column is 2 or 5)
                    TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, 3, false, aim, .23f, reduced, envelope: false);
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
