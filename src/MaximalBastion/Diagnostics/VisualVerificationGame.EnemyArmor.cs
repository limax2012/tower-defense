using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyArmorPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var samples = AreaReviewFamilies.SelectMany(id => EnemyRoster.RanksFor(id).Select(rank =>
            EnemyRankSample(content, id, rank.ToString()))).ToArray();
        foreach (var sample in samples) sample.Enemy.AbsorbShield(sample.Enemy.Shield);
        foreach (var sample in samples)
        {
            var checksum = SessionChecksum.Compute(sample.Session, 0);
            var still = EnemyRankPixels(sample, .17f, true, bodyOnly: true);
            Require(still.SequenceEqual(EnemyRankPixels(sample, .93f, true, bodyOnly: true)),
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: reduced-effects armor and reactor remain stable at rest.", assertions);
            var metricsBefore = GraphicsDevice.Metrics;
            _ = EnemyRankPixels(sample, .43f, false, bodyOnly: true);
            var bodyDrawCalls = (GraphicsDevice.Metrics - metricsBefore).DrawCount;
            Require(bodyDrawCalls is > 0 and <= 10,
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: the full-effects standalone body uses {bodyDrawCalls} GPU draw batches, within its ten-batch budget.", assertions);
            Require(SessionChecksum.Compute(sample.Session, 0) == checksum,
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: model review leaves authoritative state unchanged.", assertions);
        }
        scenes.Add(CaptureEnemyArmorRoster(samples, font));
        scenes.Add(CaptureEnemyArmorRanks(samples, font));
        scenes.Add(CaptureEnemyArmorHeadings(samples, font));
        AssertBruteLegAttachments(samples, font, assertions, scenes);
        AssertEnemyGroundShadows(samples, font, assertions, scenes);
        CaptureEnemyArmorLane(content, font, display, assertions, scenes);
        AssertEnemyArmorCornerContainment(content, assertions);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ARMOR_ANIMATION") == "1")
        {
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "armor-tracks-animation"));
            for (var frame = 0; frame < 120; frame++)
                scenes.Add(CaptureEnemyArmorTracks(samples, font, frame));
        }
    }

    private VisualVerificationScene CaptureEnemyArmorRoster(IReadOnlyList<StatusReviewEnemy> samples, SpriteFont font)
    {
        var standards = samples.Where(sample => sample.Enemy.Rank == EnemyRank.Standard).ToArray();
        using var target = NewEnemyArmorTarget();
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "ENEMY FACTION / GRAPHITE ARMOR + MAGENTA REACTORS", new Vector2(30, 24), .78f);
        StatusLabel(font, "STANDARD RANK / THREE-TIMES DETAIL ABOVE / NATIVE GAMEPLAY SIZE BELOW", new Vector2(30, 61), .54f);
        for (var family = 0; family < 5; family++)
        {
            var x = 20 + family * 250;
            _primitives.FillRect(_batch, new Rectangle(x, 106, 240, 570), ColorPalette.Panel);
            StatusLabel(font, standards[family].Enemy.Definition.DisplayName.ToUpperInvariant(), new Vector2(x + 15, 129), .66f);
            StatusLabel(font, new[] { "SKIMMER", "4 LEGS", "6 LEGS", "8 LEGS", "CONTINUOUS TRACKS" }[family],
                new Vector2(x + 15, 168), .49f);
            _primitives.Line(_batch, new Vector2(x + 16, 463), new Vector2(x + 224, 463), ColorPalette.Metal, .6f);
            StatusLabel(font, "1x", new Vector2(x + 15, 493), .51f);
            StatusLabel(font, $"SIZE TIER {family + 1}", new Vector2(x + 15, 630), .51f);
        }
        _batch.End();
        for (var family = 0; family < 5; family++)
        {
            DrawEnemyArmorBody(standards[family], new Vector2(140 + family * 250, 325), 3, -MathHelper.PiOver2, .43f, 0);
            DrawEnemyArmorBody(standards[family], new Vector2(140 + family * 250, 550), 1, -MathHelper.PiOver2, .43f, 0);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(target);
        return SaveTierScene(native, "enemy-armor-roster.png");
    }

    private VisualVerificationScene CaptureEnemyArmorRanks(IReadOnlyList<StatusReviewEnemy> samples, SpriteFont font)
    {
        var families = samples.GroupBy(sample => sample.Enemy.Definition.Id).Select(group => group.ToArray()).ToArray();
        using var target = NewEnemyArmorTarget();
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "ENEMY RANKS / TWO-TIMES DETAIL + NATIVE SIZE", new Vector2(25, 25), .8f);
        for (var family = 0; family < 5; family++)
        {
            var x = 20 + family * 250;
            StatusLabel(font, families[family][0].Enemy.Definition.DisplayName.ToUpperInvariant(), new Vector2(x + 14, 78), .58f);
            for (var rank = 0; rank < families[family].Length; rank++)
            {
                var y = 112 + rank * 194;
                _primitives.FillRect(_batch, new Rectangle(x, y, 240, 184), ColorPalette.Panel);
                StatusLabel(font, families[family][rank].Enemy.Rank.ToString().ToUpperInvariant(), new Vector2(x + 14, y + 14), .49f);
            }
        }
        _batch.End();
        for (var family = 0; family < 5; family++)
        for (var rank = 0; rank < families[family].Length; rank++)
        {
            var sample = families[family][rank];
            DrawEnemyArmorBody(sample, new Vector2(95 + family * 250, 224 + rank * 194), 2, -MathHelper.PiOver2, .43f, 0);
            DrawEnemyArmorBody(sample, new Vector2(208 + family * 250, 224 + rank * 194), 1, -MathHelper.PiOver2, .43f, 0);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(target);
        return SaveTierScene(native, "enemy-armor-ranks.png");
    }

    private void CaptureEnemyArmorLane(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = BuildAreaReviewCrowd(content, 12, 3, FogReviewDebuffs);
        for (var index = 0; index < session.Enemies.Count; index++)
            session.Enemies[index].SetSandboxPathDistance(250 + index * 29, session.Map.Path);
        var originalRenderer = _renderer;
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        ui.HandleGameplayInput(Pointer(0, 0), session);
        var animate = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ARMOR_ANIMATION") == "1";
        if (animate) Directory.CreateDirectory(Path.Combine(_outputDirectory, "armor-lane-animation"));
        try
        {
            _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = false };
            for (var frame = 0; frame < (animate ? 120 : 1); frame++)
            {
                var checksum = SessionChecksum.Compute(session, frame * 2);
                using var scene = RenderScene(ui, GameState.Playing, session);
                using var native = DownsampleEnemyLaneScene(scene);
                var file = animate ? $"armor-lane-animation/frame-{frame:000}.png" : "enemy-armor-crowded-lane.png";
                scenes.Add(SaveTierScene(native, file));
                if (animate && frame == 0) scenes.Add(SaveTierScene(native, "enemy-armor-crowded-lane.png"));
                Require(SessionChecksum.Compute(session, frame * 2) == checksum,
                    $"Crowded armor frame {frame}: drawing does not mutate simulation state.", assertions);
                for (var tick = 0; tick < 2; tick++) session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer = originalRenderer; }
    }

    private VisualVerificationScene CaptureEnemyArmorHeadings(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font)
    {
        var standards = samples.Where(sample => sample.Enemy.Rank == EnemyRank.Standard).ToArray();
        const int width = 1280;
        const int height = 1140;
        var headings = new[] { -MathHelper.PiOver2, 0, MathHelper.PiOver2, MathHelper.Pi };
        var labels = new[] { "UP", "RIGHT", "DOWN", "LEFT" };
        using var target = new RenderTarget2D(GraphicsDevice, width * 2, height * 2, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "ENEMY ARMOR / CARDINAL HEADING REVIEW", new Vector2(22, 24), .78f);
        StatusLabel(font, "RESTING STANDARD RANK / THREE-TIMES DETAIL + NATIVE GAMEPLAY SIZE", new Vector2(22, 62), .52f);
        for (var heading = 0; heading < headings.Length; heading++)
            StatusLabel(font, labels[heading], new Vector2(154 + heading * 280, 100), .55f);
        for (var family = 0; family < 5; family++)
        {
            var y = 132 + family * 198;
            StatusLabel(font, standards[family].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(20, y + 83), .45f);
            for (var heading = 0; heading < headings.Length; heading++)
                _primitives.FillRect(_batch, new Rectangle(140 + heading * 280, y, 272, 188), ColorPalette.Panel);
        }
        _batch.End();
        for (var family = 0; family < 5; family++)
        for (var heading = 0; heading < headings.Length; heading++)
        {
            var sample = standards[family];
            var y = 226 + family * 198;
            DrawEnemyArmorBody(sample, new Vector2(240 + heading * 280, y), 3,
                headings[heading], .43f, 0, atRest: true);
            DrawEnemyArmorBody(sample, new Vector2(375 + heading * 280, y), 1,
                headings[heading], .43f, 0, atRest: true);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(native);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(target, new Rectangle(0, 0, width, height), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(native, "enemy-armor-headings.png");
    }

    private VisualVerificationScene CaptureEnemyArmorTracks(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, int frame)
    {
        var tracks = samples.Where(sample => sample.Enemy.Definition.Id == "t5_regenerator").ToArray();
        var time = frame / 30f;
        var distance = MathF.Min(time, 2) * 20 + MathF.Max(0, time - 3) * 20;
        using var target = NewEnemyArmorTarget();
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "REGENERATOR / UPPER TRACK RUN + STOP / START", new Vector2(28, 26), .78f);
        StatusLabel(font, "THREE-TIMES DETAIL ABOVE / NATIVE SIZE BELOW / NOSE POINTS RIGHT", new Vector2(28, 64), .55f);
        StatusLabel(font, time >= 2 && time < 3 ? "PAUSED" : "ADVANCING", new Vector2(28, 103), .62f);
        for (var rank = 0; rank < 3; rank++)
        {
            var x = 24 + rank * 420;
            _primitives.FillRect(_batch, new Rectangle(x, 158, 392, 508), ColorPalette.Panel);
            StatusLabel(font, tracks[rank].Enemy.Rank.ToString().ToUpperInvariant(), new Vector2(x + 18, 180), .65f);
            _primitives.Line(_batch, new Vector2(x + 110, 434), new Vector2(x + 270, 434), ColorPalette.Metal, 1);
            _primitives.Line(_batch, new Vector2(x + 270, 434), new Vector2(x + 260, 427), ColorPalette.Metal, 1);
            _primitives.Line(_batch, new Vector2(x + 270, 434), new Vector2(x + 260, 441), ColorPalette.Metal, 1);
            StatusLabel(font, "UPPER RUN TOWARD NOSE", new Vector2(x + 52, 464), .49f);
            StatusLabel(font, "1x", new Vector2(x + 18, 529), .51f);
        }
        _batch.End();
        for (var rank = 0; rank < 3; rank++)
        {
            DrawEnemyArmorBody(tracks[rank], new Vector2(220 + rank * 420, 324), 3, 0, time, distance);
            DrawEnemyArmorBody(tracks[rank], new Vector2(180 + rank * 420 + distance, 566), 1, 0, time, distance);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(target);
        return SaveTierScene(native, $"armor-tracks-animation/frame-{frame:000}.png");
    }

    private RenderTarget2D NewEnemyArmorTarget() => new(GraphicsDevice,
        GameConstants.RenderWidth, GameConstants.RenderHeight, false,
        SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

    private static float OuterGuardOverhang(EnemyInstance enemy)
    {
        if (!EnemyLocomotion.UsesHeavyTracks(enemy)) return 0;
        var track = EnemyModelArt.TrackGeometry(enemy);
        var halfWidth = EnemyModelContours.Shapes[EnemyModelContours.ArmorOutlineId(enemy)]
            .Max(point => MathF.Abs(point.Y));
        return MathF.Max(0, halfWidth - track.LateralOffset - track.HalfWidth) * EnemyModelArt.BodyRadius(enemy);
    }

    private void AssertEnemyArmorCornerContainment(GameContent content, ICollection<string> assertions)
    {
        const int rasterScale = 2;
        const int size = 192;
        var pixelExtent = MathF.Sqrt(2) * .5f / rasterScale;
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var pixels = new Color[size * size];
        var failures = new List<string>();
        foreach (var map in content.Maps.Values)
        foreach (var family in AreaReviewFamilies)
        foreach (var rank in EnemyRoster.RanksFor(family).Select(rank => rank.ToString()))
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(enemy);
            var motionRadius = EnemyLocomotion.MotionRadius(enemy);
            var movementPath = EnemyLocomotionPath.For(session.Map);
            var maximumAcross = 0f;
            var worstDistance = 0f;
            var worstPixel = Vector2.Zero;
            var worstCenter = Vector2.Zero;
            var worstHeading = 0f;
            var worstColor = Color.Transparent;
            Color[]? worstPixels = null;
            var cornerDistance = 0f;
            var poses = 0;
            for (var vertex = 1; vertex < Math.Min(3, map.Path.Count - 1); vertex++)
            {
                cornerDistance += Vector2.Distance(map.Path[vertex - 1].ToVector2(), map.Path[vertex].ToVector2());
                foreach (var offset in new[] { -1f, -.5f, 0, .5f, 1 })
                {
                    var distance = cornerDistance + offset * radius;
                    var position = movementPath.VisualPosition(distance, motionRadius, EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                    var forward = movementPath.Forward(distance, motionRadius, EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                    var heading = MathF.Atan2(forward.Y, forward.X);
                    GraphicsDevice.SetRenderTarget(target);
                    GraphicsDevice.Clear(Color.Transparent);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Matrix.CreateScale(rasterScale) *
                        Matrix.CreateTranslation(size * .5f - position.X * rasterScale,
                            size * .5f - position.Y * rasterScale, 0));
                    EnemyModelArt.Draw(_batch, _primitives, enemy, position, heading, .43f, false,
                        travelDistance: distance, movementPath: movementPath);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    target.GetData(pixels);
                    var previousMaximum = maximumAcross;
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        var pixel = pixels[index];
                        if (pixel.A < 240 || (pixel.R <= ColorPalette.Canvas.R + 1 &&
                            pixel.G <= ColorPalette.Canvas.G + 1 && pixel.B <= ColorPalette.Canvas.B + 1)) continue;
                        var relative = new Vector2(index % size + .5f - size * .5f,
                            index / size + .5f - size * .5f) / rasterScale;
                        var across = session.Map.Path.DistanceToPath(position + relative) + pixelExtent;
                        if (across <= maximumAcross) continue;
                        maximumAcross = across;
                        worstDistance = distance;
                        worstPixel = position + relative;
                        worstCenter = position;
                        worstHeading = heading;
                        worstColor = pixel;
                    }
                    if (maximumAcross > previousMaximum) worstPixels = (Color[])pixels.Clone();
                    poses++;
                }
            }
            var allowed = enemy.Rank == EnemyRank.Standard
                ? map.PathWidth * .5f - 2
                : map.PathWidth * .5f + (map.PathVisual.Style == "trail" ? 7 : 4) - .25f;
            // Tread support is checked independently; the outer guard may extend past that footprint.
            var armorOverhang = OuterGuardOverhang(enemy);
            allowed += armorOverhang;
            var footprint = armorOverhang > 0 ? $"the deck plus {armorOverhang:0.00}px of outer armor overhang" : "the supporting deck";
            var description = $"{map.Id} {family} {rank}: {poses} rendered corner poses keep every solid armor pixel inside {footprint} ({maximumAcross:0.00}px maximum centerline distance, {allowed:0.00}px allowed, at path {worstDistance:0.0}).";
            if (maximumAcross <= allowed)
            {
                Require(true, description, assertions);
                continue;
            }
            failures.Add(description);
            var directory = Path.Combine(_outputDirectory, "armor-containment");
            Directory.CreateDirectory(directory);
            var file = $"{map.Id}-{family}-{rank.ToLowerInvariant()}";
            var forwardAtFailure = new Vector2(MathF.Cos(worstHeading), MathF.Sin(worstHeading));
            var localPixel = new Vector2(Vector2.Dot(worstPixel - worstCenter, forwardAtFailure),
                Vector2.Dot(worstPixel - worstCenter, new Vector2(-forwardAtFailure.Y, forwardAtFailure.X))) / radius;
            File.WriteAllText(Path.Combine(directory, file + ".json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                map = map.Id, family, rank, radius, maximumAcross, allowed,
                worstDistance, worstHeading, worldPixel = new { x = worstPixel.X, y = worstPixel.Y },
                normalizedBodyPixel = new { x = localPixel.X, y = localPixel.Y },
                pixelColor = new { worstColor.R, worstColor.G, worstColor.B, worstColor.A },
                contacts = EnemyLocomotion.ContactSlots(enemy).ToArray().Select(slot =>
                {
                    var pose = EnemyModelArt.LegPose(enemy, worstCenter, worstHeading, worstDistance,
                        slot.Side, slot.Leg, movementPath);
                    return new { side = slot.Side, leg = slot.Leg,
                        root = new { x = pose.Root.X, y = pose.Root.Y },
                        knee = new { x = pose.Knee.X, y = pose.Knee.Y },
                        foot = new { x = pose.Foot.X, y = pose.Foot.Y }, pose.ContactRadius,
                        pixelToFoot = Vector2.Distance(worstPixel, pose.Foot),
                        pixelToKnee = Vector2.Distance(worstPixel, pose.Knee) };
                }).ToArray()
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            if (worstPixels is null) continue;
            using var texture = new Texture2D(GraphicsDevice, size, size);
            texture.SetData(worstPixels);
            using (var stream = File.Create(Path.Combine(directory, file + ".png")))
                texture.SaveAsPng(stream, size, size);
            for (var index = 0; index < worstPixels.Length; index++)
            {
                var pixel = worstPixels[index];
                if (pixel.A < 240 || (pixel.R <= ColorPalette.Canvas.R + 1 &&
                    pixel.G <= ColorPalette.Canvas.G + 1 && pixel.B <= ColorPalette.Canvas.B + 1)) continue;
                var relative = new Vector2(index % size + .5f - size * .5f,
                    index / size + .5f - size * .5f) / rasterScale;
                if (session.Map.Path.DistanceToPath(worstCenter + relative) + pixelExtent > allowed)
                    worstPixels[index] = ColorPalette.Burn;
            }
            texture.SetData(worstPixels);
            using (var stream = File.Create(Path.Combine(directory, file + "-mask.png")))
                texture.SaveAsPng(stream, size, size);
        }
        Require(failures.Count == 0, failures.Count == 0
            ? "Every sampled corner pose preserves the track's supporting margin."
            : string.Join(Environment.NewLine, failures), assertions);
    }

    private void DrawEnemyArmorBody(StatusReviewEnemy sample, Vector2 center, float scale,
        float heading, float time, float distance, bool atRest = false)
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale * 2) * Matrix.CreateTranslation(center.X * 2, center.Y * 2, 0));
        EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero, heading, time, true,
            travelDistance: distance, atRest: atRest);
        _batch.End();
    }
}
