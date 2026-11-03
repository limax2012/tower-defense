using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyReadability(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        AssertEnemySelection(content, font, display, assertions, scenes);
        var ranks = new[] { "Standard", "Elite", "Boss" };
        var families = content.Enemies.Values.ToArray();
        var samples = families.SelectMany(definition => ranks.Select(rank =>
            EnemyRankSample(content, definition.Id, rank) with { Heading = -MathHelper.PiOver2 })).ToArray();
        var monochrome = new List<Texture2D>();
        var color = new List<Texture2D>();
        try
        {
            foreach (var sample in samples)
            {
                sample.Enemy.AbsorbShield(sample.Enemy.Shield);
                var checksum = SessionChecksum.Compute(sample.Session, 0);
                var pixels = EnemyRankPixels(sample, .43f, true, bodyOnly: true);
                var colorPixels = pixels.Select(pixel => pixel == ColorPalette.Canvas ? Color.Transparent : pixel).ToArray();
                var colorTexture = new Texture2D(GraphicsDevice, 128, 128);
                colorTexture.SetData(colorPixels);
                color.Add(colorTexture);
                var grayscale = new Color[pixels.Length];
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    if (pixel == ColorPalette.Canvas) continue;
                    var luminance = (byte)MathF.Round(pixel.R * .2126f + pixel.G * .7152f + pixel.B * .0722f);
                    grayscale[index] = new Color(luminance, luminance, luminance, pixel.A);
                }
                var texture = new Texture2D(GraphicsDevice, 128, 128);
                texture.SetData(grayscale);
                monochrome.Add(texture);
                Require(sample.Enemy.Shield == 0 && sample.Enemy.SignalRole == EnemySignalRole.None &&
                        sample.Enemy.StatusEffects.Active.Count == 0,
                    $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: grayscale construction is isolated from shields, signals, and conditions.", assertions);
                Require(checksum == SessionChecksum.Compute(sample.Session, 0),
                    $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: grayscale construction leaves synchronized state untouched.", assertions);
            }
            for (var family = 0; family < families.Length; family++)
            for (var rank = 1; rank < ranks.Length; rank++)
            {
                var previous = samples[family * 3 + rank - 1];
                var current = samples[family * 3 + rank];
                var first = EnemyFamilyModelSilhouette(previous.Enemy, previous.Heading);
                var second = EnemyFamilyModelSilhouette(current.Enemy, current.Heading);
                var difference = SilhouetteDifference(first, second);
                Require(difference >= .04f,
                    $"{families[family].Id}: unshielded {ranks[rank]} differs across {difference:P1} of the complete solid silhouette from {ranks[rank - 1]}, independently of color.", assertions);
            }
            scenes.Add(CaptureUnshieldedGrayscaleRanks(samples, monochrome, font));
            scenes.Add(CaptureUnshieldedGrayscaleRanks(samples, color, font,
                "UNSHIELDED CONSTRUCTION / COLOR", "62a-enemy-unshielded-color-ranks.png"));
        }
        finally
        {
            foreach (var texture in monochrome) texture.Dispose();
            foreach (var texture in color) texture.Dispose();
        }

        foreach (var reduced in new[] { false, true })
        {
            var mode = reduced ? "reduced" : "full";
            scenes.Add(CaptureBossOverdriveTransitions(content, font, reduced, assertions,
                $"63-boss-overdrive-transitions-{mode}.png"));
            scenes.Add(CaptureShieldDepletionTransitions(content, font, reduced, assertions,
                $"64-shield-depletion-transitions-{mode}.png"));
        }
    }

    private VisualVerificationScene CaptureUnshieldedGrayscaleRanks(IReadOnlyList<StatusReviewEnemy> samples,
        IReadOnlyList<Texture2D> textures, SpriteFont font,
        string title = "UNSHIELDED CONSTRUCTION / GRAYSCALE",
        string fileName = "62-enemy-unshielded-grayscale-ranks.png")
    {
        const int width = 1120;
        const int top = 110;
        const int rowHeight = 126;
        using var target = new RenderTarget2D(GraphicsDevice, width, top + samples.Count / 3 * rowHeight + 16,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, title, new Vector2(24, 16), .78f);
        StatusLabel(font, "NO SIGNALS OR CONDITIONS / EACH CELL: 2x DETAIL, GAMEPLAY SIZE, 65% SIZE",
            new Vector2(24, 52), .55f);
        for (var rank = 0; rank < 3; rank++)
            StatusLabel(font, new[] { "STANDARD", "ELITE", "BOSS" }[rank], new Vector2(210 + rank * 296, 83), .57f);
        for (var family = 0; family < samples.Count / 3; family++)
        {
            var y = top + family * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(12, y, width - 24, rowHeight - 5), ColorPalette.Panel);
            StatusLabel(font, samples[family * 3].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(27, y + 49), .66f);
        }
        for (var index = 0; index < samples.Count; index++)
        {
            var x = 207 + index % 3 * 296;
            var y = top + index / 3 * rowHeight + 61;
            foreach (var (offset, scale) in new[] { (45f, 2f), (160f, 1f), (230f, .65f) })
                _batch.Draw(textures[index], new Vector2(x + offset, y), null, Color.White,
                    0, new Vector2(64), scale, SpriteEffects.None, 0);
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private VisualVerificationScene CaptureBossOverdriveTransitions(GameContent content, SpriteFont font,
        bool reduced, ICollection<string> assertions, string fileName)
    {
        var rows = new List<Texture2D[]>();
        var labels = new List<string>();
        try
        {
            foreach (var definition in content.Enemies.Values)
            {
                var sample = EnemyRankSample(content, definition.Id, "Boss");
                var enemy = sample.Enemy;
                enemy.AbsorbShield(enemy.Shield);
                var timeline = new EnemyVisualTimeline();
                timeline.Observe(sample.Session, 0);
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload
                {
                    Damage = enemy.MaxHealth * .49f, ArmorPierce = 1000, IgnoreShield = true
                });
                Require(!enemy.BossPhaseActive && MathF.Abs(enemy.Health / enemy.MaxHealth - .51f) < .0001f,
                    $"{definition.Id}: the Boss remains closed above the existing half-health threshold.", assertions);
                var row = new List<Texture2D>
                {
                    ReadabilityTexture(ReadabilityModelPixels(sample, 1, reduced, timeline.Feedback(enemy, 1)))
                };
                rows.Add(row.ToArray());
                labels.Add(definition.DisplayName.ToUpperInvariant());
                var priorSpeed = enemy.CurrentSpeed;
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload
                {
                    Damage = enemy.MaxHealth * .02f, ArmorPierce = 1000, IgnoreShield = true
                });
                timeline.Observe(sample.Session, 1);
                Require(enemy.BossPhaseActive && !enemy.IsDead &&
                        MathF.Abs(enemy.Health / enemy.MaxHealth - .49f) < .0001f &&
                        MathF.Abs(enemy.Shield / enemy.MaxHealth - .12f) < .0001f &&
                        MathF.Abs(enemy.CurrentSpeed / priorSpeed - 1.28f) < .0001f,
                    $"{definition.Id}: actual health damage activates the unchanged Boss phase, shield reserve, and speed multiplier.", assertions);
                var checksum = SessionChecksum.Compute(sample.Session, 0);
                foreach (var elapsed in new[] { 0f, .175f, .35f, .65f })
                {
                    var feedback = timeline.Feedback(enemy, 1 + elapsed);
                    Require(feedback.OverdriveOpening >= 0 && feedback.OverdriveOpening <= 1,
                        $"{definition.Id}: opening at {elapsed:0.000}s remains bounded.", assertions);
                    row.Add(ReadabilityTexture(ReadabilityModelPixels(sample, 1 + elapsed, reduced, feedback)));
                }
                rows[^1] = row.ToArray();
                Require(timeline.Feedback(enemy, 1).OverdriveOpening == 0 &&
                        MathF.Abs(timeline.Feedback(enemy, 1.175f).OverdriveOpening - .5f) < .001f &&
                        timeline.Feedback(enemy, 1.35f).OverdriveOpening == 1 &&
                        timeline.Feedback(enemy, 1.65f).OverdriveTransition == 0,
                    $"{definition.Id}: the local opening eases over 350ms and the command release expires after 650ms.", assertions);
                if (reduced)
                    Require(ReadabilityModelPixels(sample, 1, true, timeline.Feedback(enemy, 1), bodyOnly: true)
                            .SequenceEqual(ReadabilityModelPixels(sample, 1.35f, true,
                                timeline.Feedback(enemy, 1.35f), bodyOnly: true)),
                        $"{definition.Id}: Reduced Effects retains an open, steady Boss chassis throughout the phase transition.", assertions);
                Require(checksum == SessionChecksum.Compute(sample.Session, 0),
                    $"{definition.Id}: overdrive frame sampling leaves synchronized state untouched in {(reduced ? "Reduced" : "Full")} effects.", assertions);
            }
            return CaptureReadabilityTransitionAtlas(rows, labels, font,
                $"BOSS OVERDRIVE / {(reduced ? "REDUCED EFFECTS" : "FULL EFFECTS")}",
                ["51% HEALTH", "49% / PHASE ENTRY", "+175ms / OPENING", "+350ms / OPEN", "+650ms / SETTLED"], fileName);
        }
        finally { foreach (var row in rows) foreach (var texture in row) texture.Dispose(); }
    }

    private VisualVerificationScene CaptureShieldDepletionTransitions(GameContent content, SpriteFont font,
        bool reduced, ICollection<string> assertions, string fileName)
    {
        var rows = new List<Texture2D[]>();
        var labels = new List<string>();
        try
        {
            foreach (var definition in content.Enemies.Values)
            {
                var sample = EnemyRankSample(content, definition.Id, "Standard");
                var enemy = sample.Enemy;
                enemy.AbsorbShield(enemy.Shield);
                var capacity = definition.Shield > 0 ? definition.Shield : enemy.MaxHealth * .20f;
                enemy.GrantShield(capacity, capacity);
                var health = enemy.Health;
                var timeline = new EnemyVisualTimeline();
                timeline.Observe(sample.Session, 0);
                var row = new List<Texture2D>
                {
                    ReadabilityTexture(ReadabilityModelPixels(sample, 1, reduced, timeline.Feedback(enemy, 1)))
                };
                rows.Add(row.ToArray());
                labels.Add(definition.DisplayName.ToUpperInvariant());
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload { Damage = capacity * .75f });
                timeline.Observe(sample.Session, 1);
                Require(MathF.Abs(enemy.Shield / capacity - .25f) < .0001f && enemy.Health == health,
                    $"{definition.Id}: an ordinary hit removes raw shield while preserving health and the physical chassis.", assertions);
                row.Add(ReadabilityTexture(ReadabilityModelPixels(sample, 1.1f, reduced, timeline.Feedback(enemy, 1.1f))));
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload { Damage = enemy.Shield });
                timeline.Observe(sample.Session, 1.2f);
                Require(enemy.Shield == 0 && enemy.Health == health &&
                        timeline.Feedback(enemy, 1.2f).ShieldBreak == 1,
                    $"{definition.Id}: exact shield depletion starts a local contour release without changing health.", assertions);
                var checksum = SessionChecksum.Compute(sample.Session, 0);
                foreach (var elapsed in new[] { 0f, .21f, .50f })
                    row.Add(ReadabilityTexture(ReadabilityModelPixels(sample, 1.2f + elapsed,
                        reduced, timeline.Feedback(enemy, 1.2f + elapsed))));
                rows[^1] = row.ToArray();
                Require(timeline.Feedback(enemy, 1.7f).ShieldImpact == 0 &&
                        timeline.Feedback(enemy, 1.7f).ShieldBreak == 0,
                    $"{definition.Id}: shield impact and depletion feedback expires without a persistent bubble.", assertions);
                Require(checksum == SessionChecksum.Compute(sample.Session, 0),
                    $"{definition.Id}: shield transition sampling leaves synchronized state untouched in {(reduced ? "Reduced" : "Full")} effects.", assertions);
            }
            return CaptureReadabilityTransitionAtlas(rows, labels, font,
                $"SHIELD DEPLETION / {(reduced ? "REDUCED EFFECTS" : "FULL EFFECTS")}",
                ["FULL SHIELD", "25% / IMPACT", "EMPTY / RELEASE", "+210ms / FADING", "+500ms / BARE CHASSIS"], fileName);
        }
        finally { foreach (var row in rows) foreach (var texture in row) texture.Dispose(); }
    }

    private Color[] ReadabilityModelPixels(StatusReviewEnemy sample, float time, bool reduced,
        EnemyVisualFeedback feedback, bool bodyOnly = false)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 128, 128, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Transparent);
        var enemy = sample.Enemy;
        var position = new Vector2(64);
        var radius = EnemyModelArt.BodyRadius(enemy);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        EnemyModelArt.Draw(_batch, _primitives, enemy, position, sample.Heading, time, reduced,
            overdriveOpening: feedback.OverdriveOpening);
        if (!bodyOnly)
        {
            EnemyStatusArt.Draw(_batch, _primitives, enemy, position, radius, sample.Heading, time, reduced);
            EnemyTransitionArt.Draw(_batch, _primitives, enemy, position, radius, sample.Heading, feedback, reduced);
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[128 * 128];
        target.GetData(pixels);
        return pixels;
    }

    private Texture2D ReadabilityTexture(Color[] pixels)
    {
        var texture = new Texture2D(GraphicsDevice, 128, 128);
        texture.SetData(pixels);
        return texture;
    }

    private VisualVerificationScene CaptureReadabilityTransitionAtlas(IReadOnlyList<Texture2D[]> rows,
        IReadOnlyList<string> rowLabels, SpriteFont font, string title, IReadOnlyList<string> captions, string fileName)
    {
        const int width = 1620;
        const int top = 112;
        const int rowHeight = 180;
        const int labelWidth = 170;
        const int columnWidth = 288;
        using var target = new RenderTarget2D(GraphicsDevice, width, top + rows.Count * rowHeight + 12,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, title, new Vector2(24, 16), .78f);
        StatusLabel(font, "EACH CELL: 2.25x DETAIL LEFT / GAMEPLAY SIZE RIGHT / ACTUAL DAMAGE-DRIVEN TRANSITIONS",
            new Vector2(24, 50), .57f);
        for (var column = 0; column < captions.Count; column++)
            StatusLabel(font, captions[column], new Vector2(labelWidth + column * columnWidth + 12, 84), .52f);
        for (var row = 0; row < rows.Count; row++)
        {
            var y = top + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(12, y, width - 24, rowHeight - 6), ColorPalette.Panel);
            StatusLabel(font, rowLabels[row], new Vector2(24, y + 77), .63f);
            for (var column = 0; column < rows[row].Length; column++)
            {
                var x = labelWidth + column * columnWidth;
                _batch.Draw(rows[row][column], new Vector2(x + 91, y + 87), null, Color.White,
                    0, new Vector2(64), 2.25f, SpriteEffects.None, 0);
                _batch.Draw(rows[row][column], new Vector2(x + 225, y + 87), null, Color.White,
                    0, new Vector2(64), 1, SpriteEffects.None, 0);
            }
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }
}
