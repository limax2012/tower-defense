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
    private void AssertEnemyRanks(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var samples = content.Enemies.Values.SelectMany(definition => EnemyRoster.RanksFor(definition.Id).Select(rank =>
            EnemyRankSample(content, definition.Id, rank.ToString()))).ToArray();
        var checksums = samples.Select(sample => SessionChecksum.Compute(sample.Session, 0)).ToArray();
        scenes.Add(CaptureEnemyRankAtlas(samples, font, "ENEMY CONSTRUCTION / STANDARD, ELITE, BOSS",
            "59-enemy-rank-construction.png", alignRanks: true));
        for (var turn = 0; turn < 4; turn++)
            scenes.Add(CaptureEnemyRankAtlas(samples.Select(sample => sample with
                { Heading = turn * MathHelper.PiOver2 }).ToArray(), font,
                $"ENEMY CONSTRUCTION / {turn * 90} DEGREE HEADING", $"59a-enemy-rank-heading-{turn}.png", alignRanks: true));
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            var reduced = EnemyRankPixels(sample, .17f, true);
            Require(reduced.SequenceEqual(EnemyRankPixels(sample, .93f, true)),
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: Reduced Effects keeps the mechanical body steady.", assertions);
            var structure = EnemyRankPixels(sample, .17f, false, bodyOnly: true).Select(IsEnemyArmorPixel).ToArray();
            var laterStructure = EnemyRankPixels(sample, .93f, false, bodyOnly: true).Select(IsEnemyArmorPixel).ToArray();
            var structuralPixels = structure.Count(solid => solid);
            var laterStructuralPixels = laterStructure.Count(solid => solid);
            var changedStructure = structure.Zip(laterStructure, (first, second) => first != second).Count(changed => changed);
            Require(structuralPixels > 20 && changedStructure == 0,
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: reactor animation leaves the stationary non-emissive armor mask fixed ({structuralPixels}/{laterStructuralPixels} native pixels, {changedStructure} changed positions).", assertions);
            Require(checksums[index] == SessionChecksum.Compute(sample.Session, 0),
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: rank rendering leaves synchronized gameplay untouched.", assertions);
        }
        foreach (var definition in content.Enemies.Values)
        {
            var family = samples.Where(sample => sample.Enemy.Definition.Id == definition.Id).ToArray();
            for (var rank = 1; rank < family.Length; rank++)
                Require(EnemyRankPixels(family[rank - 1], .17f, true)
                        .Zip(EnemyRankPixels(family[rank], .17f, true), (a, b) => a != b).Count(value => value) > 40,
                    $"{definition.Id}: {family[rank].Enemy.Rank} has visible structural differences at gameplay scale.", assertions);
            var conditions = Enum.GetValues<EnemySignalRole>().SelectMany(role => EnemyRoster.RanksFor(definition.Id).Select(rank =>
                EnemyRankSample(content, definition.Id, rank.ToString(), role, stacked: true))).ToArray();
            scenes.Add(CaptureEnemyRankAtlas(conditions, font,
                $"{definition.DisplayName.ToUpperInvariant()} / SIGNALS WITH STACKED CONDITIONS",
                $"59b-enemy-rank-signals-{definition.Id}.png", alignRanks: true));
        }
        var unshielded = content.Enemies.Values.SelectMany(definition => EnemyRoster.RanksFor(definition.Id).Select(rank =>
            EnemyRankSample(content, definition.Id, rank.ToString()))).ToArray();
        foreach (var sample in unshielded) sample.Enemy.AbsorbShield(sample.Enemy.Shield);
        scenes.Add(CaptureEnemyRankAtlas(unshielded, font, "ENEMY CONSTRUCTION / SHIELDS DEPLETED",
            "59e-enemy-rank-unshielded.png", alignRanks: true));
        AssertEnemyRankFootSupport(content, assertions);
        CaptureEnemyRankPaths(content, font, display, assertions, scenes);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ENEMY_RANK_ANIMATION") != "1") return;
        for (var frame = 0; frame < 72; frame++)
            scenes.Add(CaptureEnemyRankAtlas(samples, font, "ENEMY CONSTRUCTION / STANDARD, ELITE, BOSS",
                $"enemy-rank-animation/construction-{frame:000}.png", frame / 24f, alignRanks: true));
    }

    // Gray material includes textured bevels and antialiased edges; emissive magenta remains separate.
    private static bool IsEnemyArmorPixel(Color pixel) => pixel.G >= 28 &&
        pixel.R <= pixel.G * 1.5f + 4 && pixel.B <= pixel.G * 2;

    private static StatusReviewEnemy EnemyRankSample(GameContent content, string id, string rank,
        EnemySignalRole role = EnemySignalRole.None, bool stacked = false)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        session.SpawnEnemy(id, 5, 1, rank, role);
        var enemy = session.Enemies.Single();
        enemy.SetSandboxPathDistance(430, session.Map.Path);
        if (stacked) ApplyEnemyRankConditions(enemy);
        var label = $"{enemy.Definition.DisplayName.ToUpperInvariant()} / {rank.ToUpperInvariant()}";
        if (stacked) label = $"{rank.ToUpperInvariant()} / {(role == EnemySignalRole.None ? "NO SIGNAL" : role.ToString().ToUpperInvariant())}";
        return new StatusReviewEnemy(label, session, enemy, Heading: -.48f);
    }

    private static void ApplyEnemyRankConditions(EnemyInstance enemy)
    {
        foreach (var status in new[] { StatusType.Slow, StatusType.Burn, StatusType.ArmorBreak, StatusType.Exposed })
            enemy.ApplyStatus(new StatusApplication { Type = status, Duration = 20,
                Magnitude = status is StatusType.Burn or StatusType.ArmorBreak ? 4 : .35f });
        enemy.GrantShield(30, 30);
        enemy.ApplyHealthDamage(enemy.MaxHealth * .15f);
        enemy.TickRuntimeTimers(1.1f);
    }

    private void DrawEnemyRankSample(StatusReviewEnemy sample, Vector2 position, float time, bool reduced)
    {
        var enemy = sample.Enemy;
        var radius = EnemyModelArt.BodyRadius(enemy);
        EnemyModelArt.Draw(_batch, _primitives, enemy, position, sample.Heading, time, reduced,
            travelDistance: reduced ? 0 : time * enemy.CurrentSpeed);
        EnemyDebuffAuraArt.Draw(_batch, _primitives, sample.Session, enemy, position, radius, time, reduced);
        EnemyStatusArt.DrawDurability(_batch, _primitives, enemy, position, radius, sample.Heading, time, reduced);
    }

    private static void AssertEnemyRankFootSupport(GameContent content, ICollection<string> assertions)
    {
        foreach (var map in content.Maps.Values)
        foreach (var definition in content.Enemies.Values.Where(definition => definition.Id != "t2_runner"))
        foreach (var rank in EnemyRoster.RanksFor(definition.Id))
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(definition.Id, 1, 1, rank.ToString());
            var enemy = session.Enemies.Single();
            var movementPath = new EnemyLocomotionPath(session.Map);
            var distances = new List<float>();
            for (var distance = 32f; distance < session.Map.Path.TotalLength - 32; distance += 32)
                distances.Add(distance);
            var cornerDistance = 0f;
            for (var vertex = 1; vertex < map.Path.Count - 1; vertex++)
            {
                cornerDistance += Vector2.Distance(map.Path[vertex - 1].ToVector2(), map.Path[vertex].ToVector2());
                foreach (var offset in new[] { -25f, -12, -3, -.1f, 0, .1f, 3, 12, 25 })
                    distances.Add(cornerDistance + offset);
            }
            var maximumEdgeDistance = 0f;
            var maximumRadialDistance = 0f;
            var sampleCount = 0;
            foreach (var distance in distances)
            for (var phase = 0; phase < 48; phase++)
            {
                var sampledDistance = Math.Clamp(distance + (phase - 24) * .25f, 0, session.Map.Path.TotalLength);
                var position = movementPath.VisualPosition(sampledDistance, EnemyLocomotion.MotionRadius(enemy), EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                var direction = movementPath.Forward(sampledDistance, EnemyLocomotion.MotionRadius(enemy), EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                var heading = MathF.Atan2(direction.Y, direction.X);
                foreach (var (side, leg) in EnemyLocomotion.ContactSlots(enemy))
                {
                    var pose = EnemyModelArt.LegPose(enemy, position, heading,
                        sampledDistance, side, leg, movementPath);
                    maximumEdgeDistance = MathF.Max(maximumEdgeDistance,
                        session.Map.Path.DistanceToPath(pose.Foot) + pose.ContactRadius);
                    maximumRadialDistance = MathF.Max(maximumRadialDistance,
                        Vector2.Distance(position, pose.Foot) + pose.ContactRadius);
                    sampleCount++;
                }
            }
            var supportingHalfWidth = rank == EnemyRank.Standard
                ? map.PathWidth * .5f - 2
                : map.PathWidth * .5f + (map.PathVisual.Style == "trail" ? 7 : 4) - .25f;
            Require(maximumEdgeDistance <= supportingHalfWidth + .01f,
                $"{map.Id} {definition.Id} {rank}: {sampleCount} animated foot contacts fit the {supportingHalfWidth * 2:0.#}px supporting deck, including corners (maximum edge distance {maximumEdgeDistance:0.00}px).", assertions);
            Require(maximumRadialDistance <= EnemyLocomotion.MaximumReach(enemy),
                $"{map.Id} {definition.Id} {rank}: contact reach remains bounded beside its chassis ({maximumRadialDistance:0.00}px).", assertions);
        }
    }

    private Color[] EnemyRankPixels(StatusReviewEnemy sample, float time, bool reduced, bool bodyOnly = false)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 128, 128, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        if (bodyOnly)
            EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, new Vector2(64), sample.Heading, time, reduced,
                travelDistance: 0);
        else DrawEnemyRankSample(sample, new Vector2(64), time, reduced);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureEnemyRankAtlas(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, string title, string fileName, float time = .43f, bool alignRanks = false)
    {
        const int columns = 3;
        const int cellWidth = 380;
        const int cellHeight = 275;
        var cells = new Point[samples.Count];
        var row = -1;
        var previousRank = columns;
        for (var index = 0; index < samples.Count; index++)
        {
            var column = alignRanks ? (int)samples[index].Enemy.Rank : index % columns;
            if (alignRanks && column <= previousRank) row++;
            cells[index] = new Point(column, alignRanks ? row : index / columns);
            previousRank = column;
        }
        var rows = cells.Length == 0 ? 0 : cells.Max(cell => cell.Y) + 1;
        using var target = new RenderTarget2D(GraphicsDevice, columns * cellWidth, 100 + rows * cellHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, title, new Vector2(24, 16), .78f);
        StatusLabel(font, "3x DETAIL / GAMEPLAY SIZE: FULL LEFT, REDUCED RIGHT", new Vector2(24, 55), .60f);
        for (var index = 0; index < samples.Count; index++)
        {
            var x = cells[index].X * cellWidth;
            var y = 100 + cells[index].Y * cellHeight;
            _primitives.FillRect(_batch, new Rectangle(x + 8, y, cellWidth - 16, cellHeight - 8), ColorPalette.Panel);
            StatusLabel(font, samples[index].Label, new Vector2(x + 18, y + 12), .53f);
        }
        _batch.End();
        for (var index = 0; index < samples.Count; index++)
        {
            var x = cells[index].X * cellWidth;
            var y = 100 + cells[index].Y * cellHeight;
            DrawAt(new Vector2(x + cellWidth * .5f, y + 125), 3, false);
            DrawAt(new Vector2(x + cellWidth * .32f, y + 231), 1, false);
            DrawAt(new Vector2(x + cellWidth * .68f, y + 231), 1, true);
            void DrawAt(Vector2 center, float scale, bool reduced)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X, center.Y, 0));
                DrawEnemyRankSample(samples[index], Vector2.Zero, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private void CaptureEnemyRankPaths(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var animate = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ENEMY_RANK_ANIMATION") == "1";
        if (animate) Directory.CreateDirectory(Path.Combine(_outputDirectory, "enemy-rank-animation"));
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var map in content.Maps.Values)
            {
                var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
                for (var step = 0; step < 270; step++) session.Update(1f / 60);
                var candidates = session.Map.BuildableRegions.SelectMany(region =>
                    Enumerable.Range(0, Math.Max(1, region.Width / 24)).SelectMany(x =>
                        Enumerable.Range(0, Math.Max(1, region.Height / 24)).Select(y =>
                            new Vector2(region.Left + 16 + x * 24, region.Top + 16 + y * 24))))
                    .OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 360))).ToArray();
                foreach (var towerId in new[] { "prism_beam", "ember_coil", "frost_spire", "breaker_cannon", "arc_relay" })
                    Require(candidates.Any(point => session.TryPlaceTower(towerId, point)),
                        $"{map.Id}: rank combat has a legal {towerId} emplacement.", assertions);
                var corners = new List<float>();
                var distance = 0f;
                for (var point = 1; point < map.Path.Count - 1; point++)
                {
                    distance += Vector2.Distance(map.Path[point - 1].ToVector2(), map.Path[point].ToVector2());
                    corners.Add(distance);
                }
                var roster = content.Enemies.Keys.SelectMany(id => EnemyRoster.RanksFor(id)
                    .Select(rank => (Id: id, Rank: rank))).ToArray();
                for (var index = 0; index < roster.Length; index++)
                {
                    var role = (EnemySignalRole)(index % 6);
                    session.SpawnEnemy(roster[index].Id, 100, 1, roster[index].Rank.ToString(), role);
                    var enemy = session.Enemies[^1];
                    var corner = corners[index % corners.Count];
                    var offset = index < corners.Count ? -24 : 40 + index * 3;
                    enemy.SetSandboxPathDistance(Math.Clamp(corner + offset, 64, session.Map.Path.TotalLength - 650), session.Map.Path);
                    enemy.ArmSignalAbility(0);
                }
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                var checksum = SessionChecksum.Compute(session, 0);
                foreach (var reduced in new[] { false, true })
                {
                    _renderer.ReducedEffects = reduced;
                    scenes.Add(Capture($"59c-enemy-rank-path-{map.Id}-{(reduced ? "reduced" : "full")}.png",
                        ui, GameState.Playing, session));
                }
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{map.Id}: every rank and family renders on the real route without mutating gameplay.", assertions);
                _renderer.ReducedEffects = false;
                var distancesBeforeMovement = session.Enemies.Select(enemy => enemy.DistanceAlongPath).ToArray();
                for (var step = 0; step < 6; step++) session.Update(1f / 60);
                Require(session.Enemies.Select((enemy, index) => enemy.DistanceAlongPath > distancesBeforeMovement[index]).All(moved => moved),
                    $"{map.Id}: all rank-review enemies advance through the actual Sandbox movement system.", assertions);
                if (animate)
                {
                    for (var frame = 0; frame < 96; frame++)
                    {
                        for (var step = 0; step < 5; step++) session.Update(1f / 120);
                        ui.AdvanceVisualTime(1f / 24);
                        scenes.Add(CaptureEnemyRankNative(session, ui,
                            $"enemy-rank-animation/{map.Id}-{frame:000}.png"));
                    }
                }
                foreach (var enemy in session.Enemies) ApplyEnemyRankConditions(enemy);
                for (var step = 0; step < 6; step++) session.Update(1f / 60);
                scenes.Add(Capture($"59d-enemy-rank-conditions-{map.Id}.png", ui, GameState.Playing, session));
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private VisualVerificationScene CaptureEnemyRankNative(GameSession session, UIManager ui, string fileName)
    {
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.LogicalWidth, GameConstants.LogicalHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _renderer.Draw(_batch, _primitives, session);
        ui.Draw(_batch, _primitives, GameState.Playing, session, Matrix.Identity);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }
}
