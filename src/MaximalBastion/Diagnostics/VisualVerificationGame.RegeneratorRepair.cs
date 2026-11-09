using System.Text.Json;
using MaximalBastion.Combat;
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
    private void AssertRegeneratorRepair(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var rows = new List<Texture2D[]>();
        var ranks = new[] { "Standard", "Elite", "Boss" };
        try
        {
            foreach (var rank in ranks)
            {
                var sample = EnemyRankSample(content, "t5_regenerator", rank) with { Heading = 0 };
                var enemy = sample.Enemy;
                const float time = 1.66f;
                var full = Pixels(time);
                Hit(sample.Session, enemy);
                var damagedHealth = enemy.Health;
                var paused = Pixels(time);
                Require(paused.SequenceEqual(full) && !EnemyStatusArt.IsRegenerating(enemy),
                    $"{rank} Regenerator: health damage pauses repair without adding a body cue.", assertions);
                var system = new EnemySystem();
                system.Update(.9f, sample.Session);
                Require(enemy.Health == damagedHealth && !EnemyStatusArt.IsRegenerating(enemy),
                    $"{rank} Regenerator: repair remains inactive during the first 900ms after health damage.", assertions);
                system.Update(.11f, sample.Session);
                Require(enemy.Health > damagedHealth && EnemyStatusArt.IsRegenerating(enemy),
                    $"{rank} Regenerator: natural healing resumes after the one-second damage pause.", assertions);
                var active = Pixels(time);
                var changed = Enumerable.Range(0, active.Length).Where(i => active[i] != full[i]).ToArray();
                var bezelLimit = RepairBezelLimit(enemy);
                Require(changed.Length > 45 && changed.All(i =>
                        Vector2.Distance(new Vector2(i % 128 + .5f, i / 128 + .5f), new Vector2(64)) < bezelLimit),
                    $"{rank} Regenerator: active repair is visible at native size and confined to the jewel's octagonal bezel, preserving the outer body bevels.", assertions);
                var changedMask = changed.ToHashSet();
                var mirrored = changed.Count(i => changedMask.Contains(i / 128 * 128 + 127 - i % 128) &&
                                                 changedMask.Contains((127 - i / 128) * 128 + i % 128));
                Require(mirrored >= changed.Length * .88f,
                    $"{rank} Regenerator: repair lights have matching opposing sides and ends.", assertions);
                var coveredChanges = Enumerable.Range(0, active.Length).Where(i => active[i] != full[i] &&
                    RepairCoveredSurface(new Vector2(i % 128 + .5f, i / 128 + .5f) - new Vector2(64), enemy)).ToArray();
                if (coveredChanges.Length > 0)
                    File.WriteAllText(Path.Combine(_outputDirectory, $"regenerator-repair-protected-{rank.ToLowerInvariant()}.json"),
                        JsonSerializer.Serialize(new
                        {
                            rank, bodyRadius = EnemyModelArt.BodyRadius(enemy), coreRadius = EnemyModelArt.CoreRadius(enemy),
                            pixels = coveredChanges.Select(i =>
                            {
                                var local = new Vector2(i % 128 + .5f, i / 128 + .5f) - new Vector2(64);
                                return new
                                {
                                    x = local.X, y = local.Y, distance = local.Length(),
                                    coreFraction = local.Length() / EnemyModelArt.CoreRadius(enemy),
                                    before = new { full[i].R, full[i].G, full[i].B, full[i].A },
                                    after = new { active[i].R, active[i].G, active[i].B, active[i].A }
                                };
                            }).ToArray()
                        }, new JsonSerializerOptions { WriteIndented = true }));
                Require(coveredChanges.Length == 0,
                    $"{rank} Regenerator: repair lighting preserves the reactor gem and cannot shine through its side armor.", assertions);
                var checksum = SessionChecksum.Compute(sample.Session, 917);
                var pulseSamples = Enumerable.Range(0, 7).Select(i => Pixels(time + i * .2f)).ToArray();
                Require(pulseSamples.Skip(1).Any(pixels => changed.Count(i => pixels[i] != active[i]) > 25),
                    $"{rank} Regenerator: body-mounted repair lighting visibly breathes through its cycle.", assertions);
                var reduced = Pixels(time, reduced: true);
                Require(!reduced.SequenceEqual(paused) && reduced.SequenceEqual(Pixels(time + .47f, reduced: true)),
                    $"{rank} Regenerator: Reduced Effects keeps a steady repair cue.", assertions);
                var resting = Pixels(time, atRest: true);
                Require(checksum == SessionChecksum.Compute(sample.Session, 917),
                    $"{rank} Regenerator: repair rendering preserves synchronized gameplay state.", assertions);

                enemy.GrantShield(100, enemy.Shield + 100);
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 5 });
                Require(EnemyStatusArt.IsRegenerating(enemy) && active.SequenceEqual(Pixels(time)),
                    $"{rank} Regenerator: shield-only hits leave the active repair cue intact.", assertions);
                Hit(sample.Session, enemy);
                var hitAgain = Pixels(time);
                Require(hitAgain.SequenceEqual(paused) && !EnemyStatusArt.IsRegenerating(enemy),
                    $"{rank} Regenerator: the first new health hit immediately removes the repair cue.", assertions);
                Require(resting.SequenceEqual(Pixels(time, atRest: true)),
                    $"{rank} Regenerator: resting Intel art is identical during active and paused repair.", assertions);
                system.Update(1.01f, sample.Session);
                enemy.RestoreHealth(enemy.MaxHealth);
                Require(Pixels(time).SequenceEqual(full) && !EnemyStatusArt.IsRegenerating(enemy),
                    $"{rank} Regenerator: reaching full health removes the repair cue.", assertions);
                rows.Add(new[] { full, paused, active, hitAgain, Pixels(time) }.Select(ReadabilityTexture).ToArray());

                Color[] Pixels(float sampleTime, bool reduced = false, bool atRest = false) =>
                    BossExhaustPixels(sample, sampleTime, reduced, new(1, 0, 0, 0), atRest);
            }
            scenes.Add(CaptureReadabilityTransitionAtlas(rows, ranks, font, "REGENERATOR / SELF-REPAIR BEZEL",
                ["FULL HEALTH", "DAMAGE / PAUSED", "REPAIRING", "HIT / PAUSED", "FULL / INACTIVE"],
                "regenerator-repair-states.png"));
        }
        finally { foreach (var row in rows) foreach (var texture in row) texture.Dispose(); }

        AssertRegeneratorRepairFog(content, font, display, assertions, scenes);

        var fixture = BuildRegeneratorRepairLane(content);
        var ui = BossExhaustUi(content, font, display, fixture.Session);
        var originalRenderer = _renderer;
        try
        {
            _renderer = new GameRenderer();
            Capture("regenerator-repair-lane-paused.png");
            for (var tick = 0; tick < 84; tick++) fixture.Session.Update(1f / 60);
            Capture("regenerator-repair-lane-active.png");
            Require(EnemyStatusArt.IsRegenerating(fixture.Subject) && _renderer.LastEnemyGlyphCount == 0 &&
                    _renderer.LastEnemyHealthBarCount == fixture.Session.Enemies.Count,
                "Regenerator repair in a mixed lane retains the existing health bars and has no status icons.", assertions);
            Hit(fixture.Session, fixture.Subject);
            Capture("regenerator-repair-lane-hit.png");

            void Capture(string filename)
            {
                var checksum = SessionChecksum.Compute(fixture.Session, 918);
                using var scene = RenderScene(ui, GameState.Playing, fixture.Session);
                using var native = DownsampleEnemyLaneScene(scene);
                scenes.Add(SaveTierScene(native, filename));
                Require(checksum == SessionChecksum.Compute(fixture.Session, 918),
                    $"{filename}: native gameplay rendering does not change synchronized state.", assertions);
            }
        }
        finally { _renderer = originalRenderer; }

        static void Hit(GameSession session, EnemyInstance enemy) => session.DamageResolver.Apply(enemy,
            new DamagePayload { Damage = 200, IgnoreShield = true, ArmorPierce = 1000 });
    }

    private static float RepairBezelLimit(EnemyInstance enemy) =>
        EnemyModelArt.CoreRadius(enemy) + EnemyModelArt.BodyRadius(enemy) * .03f;

    private static bool RepairCoveredSurface(Vector2 local, EnemyInstance enemy)
    {
        var radius = EnemyModelArt.BodyRadius(enemy);
        var across = MathF.Max(MathF.Max(MathF.Abs(local.X), MathF.Abs(local.Y)),
            (MathF.Abs(local.X) + MathF.Abs(local.Y)) * MathF.Sqrt(2) * .5f);
        // Bilinear edge coverage is outside the exact-color interior comparison.
        var protectedCore = enemy.Rank == EnemyRank.Standard
            ? local.LengthSquared() < radius * radius * .30f * .30f
            : across < EnemyModelArt.CoreRadius(enemy) * .89f * MathF.Cos(MathHelper.Pi / 8) - 1f;
        var sideCenter = enemy.Rank == EnemyRank.Standard ? .515f : .585f;
        var sideLength = enemy.Rank switch
        {
            EnemyRank.Elite => .49f,
            EnemyRank.Boss => .575f,
            _ => .40f
        };
        return protectedCore ||
            MathF.Abs(local.X) < radius * sideLength && MathF.Abs(local.Y) > radius * (sideCenter - .08f) &&
            MathF.Abs(local.Y) < radius * (sideCenter + .08f);
    }

    private void AssertRegeneratorRepairFog(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var conditions = new StatusType[][] { [], [StatusType.Slow], [StatusType.Exposed],
            [StatusType.Slow, StatusType.Exposed, StatusType.ArmorBreak],
            [StatusType.Burn, StatusType.Slow, StatusType.Exposed, StatusType.ArmorBreak] };
        var labels = new[] { "CLEAR", "SLOW", "EXPOSE", "THREE DEBUFFS", "FOUR + SHIELD" };
        var pausedRow = new List<Texture2D>();
        var activeRow = new List<Texture2D>();
        var measurements = new List<object>();
        var appearance = new List<(bool Passed, string Description)>();
        var originalRenderer = _renderer;
        try
        {
            for (var index = 0; index < conditions.Length; index++)
            {
                var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
                for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
                session.SpawnEnemy("t5_regenerator", 5, 1);
                var enemy = session.Enemies.Single();
                enemy.SetSandboxPathDistance(875, session.Map.Path);
                enemy.ApplyHealthDamage(600);
                for (var tick = 0; tick < 61; tick++) session.Update(1f / 60);
                foreach (var type in conditions[index]) ApplyRepairFog(enemy, type);
                if (conditions[index].Contains(StatusType.Burn))
                {
                    enemy.GrantShield(600, 600);
                    var health = enemy.Health;
                    var shield = enemy.Shield;
                    new EnemySystem().Update(.26f, session);
                    Require(enemy.Health > health && enemy.Shield < shield && EnemyStatusArt.IsRegenerating(enemy),
                        "Shield-absorbed burn ticks permit genuine regeneration beneath four active debuff colors.", assertions);
                }
                var ui = BossExhaustUi(content, font, display, session);
                var center = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
                var crop = new Rectangle((int)center.X - 64, (int)center.Y - 64, 128, 128);
                var forward = EnemyLocomotionPath.For(session.Map).Forward(enemy.DistanceAlongPath,
                    EnemyLocomotion.MotionRadius(enemy), EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                var side = new Vector2(-forward.Y, forward.X);
                var bezelLimit = RepairBezelLimit(enemy);
                var active = Pixels();
                enemy.ApplyHealthDamage(200);
                enemy.TickRuntimeTimers(.2f);
                var paused = Pixels();
                var greenFront = 0;
                var greenRear = 0;
                var coveredChanged = 0;
                for (var pixel = 0; pixel < active.Length; pixel++)
                {
                    var offset = new Vector2(crop.X + pixel % 128 + .5f, crop.Y + pixel / 128 + .5f) - center;
                    var local = new Vector2(Vector2.Dot(offset, forward), Vector2.Dot(offset, side));
                    if (RepairCoveredSurface(local, enemy) && active[pixel] != paused[pixel]) coveredChanged++;
                    var color = active[pixel];
                    if (offset.LengthSquared() > bezelLimit * bezelLimit ||
                        color.G < color.R + 8 || color.G < color.B + 3 || color.G < paused[pixel].G + 4) continue;
                    if (local.X > 0) greenFront++; else greenRear++;
                }
                measurements.Add(new { condition = labels[index], greenFront, greenRear, coveredChanged });
                appearance.Add((greenFront >= 3 && greenRear >= 3,
                    $"{labels[index]}: active healing remains visibly green on both halves of the jewel bezel at native gameplay size ({greenFront}/{greenRear} pixels)."));
                appearance.Add((coveredChanged == 0,
                    $"{labels[index]}: healing preserves the gem and covering side armor beneath the unchanged fog."));
                pausedRow.Add(ReadabilityTexture(paused));
                activeRow.Add(ReadabilityTexture(active));

                Color[] Pixels()
                {
                    var checksum = SessionChecksum.Compute(session, 919);
                    _renderer = new GameRenderer();
                    using var scene = RenderScene(ui, GameState.Playing, session);
                    using var native = DownsampleEnemyLaneScene(scene);
                    var pixels = new Color[128 * 128];
                    native.GetData(0, crop, pixels, 0, pixels.Length);
                    Require(checksum == SessionChecksum.Compute(session, 919),
                        $"{labels[index]}: repair and fog rendering preserves synchronized state.", assertions);
                    return pixels;
                }
            }
            scenes.Add(CaptureRegeneratorFogAtlas(pausedRow, activeRow, labels, font));
            foreach (var check in appearance) Require(check.Passed, check.Description, assertions);
        }
        finally
        {
            _renderer = originalRenderer;
            foreach (var texture in pausedRow.Concat(activeRow)) texture.Dispose();
            File.WriteAllText(Path.Combine(_outputDirectory, "regenerator-repair-fog-measurements.json"),
                JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private VisualVerificationScene CaptureRegeneratorFogAtlas(IReadOnlyList<Texture2D> paused,
        IReadOnlyList<Texture2D> active, IReadOnlyList<string> labels, SpriteFont font)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 1620, 524, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "REGENERATOR / REPAIR BEZEL BENEATH DEBUFF FOG", new Vector2(24, 16), .78f);
        StatusLabel(font, "EACH CELL: 2x DETAIL LEFT / GAMEPLAY SIZE RIGHT / MATCHED SIMULATION TIME",
            new Vector2(24, 50), .57f);
        for (var column = 0; column < labels.Count; column++)
            StatusLabel(font, labels[column], new Vector2(182 + column * 288, 84), .52f);
        var source = new Rectangle(24, 24, 80, 80);
        for (var row = 0; row < 2; row++)
        {
            var y = 112 + row * 200;
            _primitives.FillRect(_batch, new Rectangle(12, y, 1596, 194), ColorPalette.Panel);
            StatusLabel(font, row == 0 ? "PAUSED" : "REPAIRING", new Vector2(24, y + 90), .63f);
            var textures = row == 0 ? paused : active;
            for (var column = 0; column < textures.Count; column++)
            {
                var x = 170 + column * 288;
                _batch.Draw(textures[column], new Rectangle(x + 12, y + 20, 160, 160), source, Color.White);
                _batch.Draw(textures[column], new Rectangle(x + 190, y + 60, 80, 80), source, Color.White);
            }
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, "regenerator-repair-under-fog.png");
    }

    private static void ApplyRepairFog(EnemyInstance enemy, StatusType type) => enemy.ApplyStatus(new StatusApplication
    {
        Type = type, Duration = 30, TickInterval = .25f,
        Magnitude = type == StatusType.Burn ? 4 : type == StatusType.ArmorBreak ? 4 : .35f
    });

    private static (GameSession Session, EnemyInstance Subject) BuildRegeneratorRepairLane(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
        foreach (var (id, distance) in new[] { ("t3_brute", 690f), ("t1_crawler", 740f),
                     ("t4_aegis", 800f), ("t5_regenerator", 875f) })
        {
            session.SpawnEnemy(id, 5, 1);
            session.Enemies[^1].SetSandboxPathDistance(distance, session.Map.Path);
        }
        var subject = session.Enemies[^1];
        session.DamageResolver.Apply(subject, new DamagePayload
            { Damage = 600, IgnoreShield = true, ArmorPierce = 1000 });
        return (session, subject);
    }

    private void CaptureRegeneratorRepairMotion(GameContent content, SpriteFont font, SpriteFont display)
    {
        var fixture = BuildRegeneratorRepairLane(content);
        foreach (var type in FogReviewDebuffs) ApplyRepairFog(fixture.Subject, type);
        fixture.Subject.GrantShield(600, 600);
        var ui = BossExhaustUi(content, font, display, fixture.Session);
        var originalRenderer = _renderer;
        var frames = new List<object>();
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "regenerator-repair-motion"));
        try
        {
            _renderer = new GameRenderer();
            for (var frame = 0; frame < 180; frame++)
            {
                if (frame == 90) fixture.Session.DamageResolver.Apply(fixture.Subject, new DamagePayload
                    { Damage = 200, IgnoreShield = true, ArmorPierce = 1000 });
                if (frame == 150) fixture.Subject.RestoreHealth(fixture.Subject.MaxHealth);
                var checksum = SessionChecksum.Compute(fixture.Session, frame);
                using var scene = RenderScene(ui, GameState.Playing, fixture.Session);
                using var native = DownsampleEnemyLaneScene(scene);
                var center = PresentationFrame.Create(fixture.Session, 0).EnemyPosition(fixture.Subject);
                var crop = new Rectangle(Math.Clamp((int)center.X - 160, 0, 640),
                    Math.Clamp((int)center.Y - 80, 64, 560), 320, 160);
                using var board = new RenderTarget2D(GraphicsDevice, 1040, 424, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(board);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                var state = fixture.Subject.Health >= fixture.Subject.MaxHealth ? "FULL HEALTH" :
                    EnemyStatusArt.IsRegenerating(fixture.Subject) ? "REPAIRING / +18 HEALTH/S" : "REPAIR PAUSED";
                StatusLabel(font, $"REGENERATOR / {state}", new Vector2(20, 12), .72f);
                StatusLabel(font, "GAMEPLAY SIZE", new Vector2(20, 52), .5f);
                StatusLabel(font, "2x DETAIL", new Vector2(380, 52), .5f);
                _batch.Draw(native, new Rectangle(20, 162, 320, 160), crop, Color.White);
                _batch.Draw(scene, new Rectangle(380, 82, 640, 320),
                    new Rectangle(crop.X * 2, crop.Y * 2, crop.Width * 2, crop.Height * 2), Color.White);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
                SaveTierScene(board, $"regenerator-repair-motion/frame-{frame:000}.png");
                if (checksum != SessionChecksum.Compute(fixture.Session, frame))
                    throw new InvalidOperationException("Regenerator repair motion capture changed synchronized state.");
                frames.Add(new { frame, seconds = frame / 30f, checksum, fixture.Subject.Health,
                    fixture.Subject.DamagePauseTimer, repairing = EnemyStatusArt.IsRegenerating(fixture.Subject), crop });
                for (var tick = 0; tick < 2; tick++) fixture.Session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "regenerator-repair-motion.json"),
            JsonSerializer.Serialize(new { fps = 30, healthHitFrame = 90, fullHealthFrame = 150, frames },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
