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
    private void CaptureEnemyConstructionReview(GameContent content, SpriteFont font)
    {
        var ranks = new[] { "Standard", "Elite", "Boss" };
        var samples = content.Enemies.Keys.SelectMany(id => ranks.Select(rank =>
            EnemyRankSample(content, id, rank) with { Heading = -MathHelper.PiOver2 })).ToArray();
        foreach (var sample in samples) sample.Enemy.AbsorbShield(sample.Enemy.Shield);
        var assertions = new List<string>();
        AssertEnemyRankFootSupport(content, assertions);
        AssertEnemyFamilySolidFootprints(content, samples, assertions);
        var scenes = new[]
        {
            CaptureEnemyFamilyAtlas(samples, font, "60-enemy-family-roster.png"),
            CaptureEnemyFamilySilhouettes(samples, font, "60a-enemy-family-silhouettes.png"),
            CaptureEnemyFamilySilhouettes(samples, font, "60aa-enemy-family-complete-silhouettes.png", fullModel: true),
            CaptureEnemyScaleAtlas(content, samples, font)
        };
        File.WriteAllText(Path.Combine(_outputDirectory, "construction-report.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                minimumRoadWidth = content.Maps.Values.Min(map => map.PathWidth),
                requestedLateralClearance = 2,
                families = samples.Select(sample => new
                {
                    family = sample.Enemy.Definition.Id, rank = sample.Enemy.Rank.ToString(),
                    bodyRadius = EnemyModelArt.BodyRadius(sample.Enemy),
                    contacts = EnemyLocomotion.ContactSlots(sample.Enemy).Length
                }).ToArray(), assertions, scenes
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Enemy construction verification passed: {scenes.Length} sheets, {assertions.Count} assertions.");
    }

    private void AssertEnemyFamilies(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ranks = new[] { "Standard", "Elite", "Boss" };
        var families = content.Enemies.Keys.ToArray();
        var samples = families.SelectMany(id => ranks.Select(rank =>
            EnemyRankSample(content, id, rank) with { Heading = -MathHelper.PiOver2 })).ToArray();
        var checksums = samples.Select(sample => SessionChecksum.Compute(sample.Session, 0)).ToArray();
        scenes.Add(CaptureEnemyFamilyAtlas(samples, font, "60-enemy-family-roster.png"));
        scenes.Add(CaptureEnemyFamilySilhouettes(samples, font, "60a-enemy-family-silhouettes.png"));
        scenes.Add(CaptureEnemyFamilySilhouettes(samples, font, "60aa-enemy-family-complete-silhouettes.png", fullModel: true));
        var masks = samples.Select(sample => EnemyFamilySilhouette(sample.Enemy, sample.Heading)).ToArray();
        for (var index = 0; index < samples.Length; index++)
        {
            var enemy = samples[index].Enemy;
            var vertices = Enumerable.Range(0, EnemyModelArt.HullVertexCount(enemy))
                .Select(vertex => EnemyModelArt.HullVertex(enemy, vertex)).ToArray();
            Require(vertices.Length >= 3 && vertices.All(vertex => float.IsFinite(vertex.X) && float.IsFinite(vertex.Y)),
                $"{enemy.Definition.Id} {enemy.Rank}: the material boundary is finite and closed.", assertions);
            Require(vertices.All(vertex => vertices.Any(mirror =>
                    Vector2.DistanceSquared(mirror, new Vector2(vertex.X, -vertex.Y)) < .00001f)),
                $"{enemy.Definition.Id} {enemy.Rank}: family armor has balanced port and starboard construction.", assertions);
            var twiceArea = vertices.Select((vertex, vertexIndex) =>
                vertex.X * vertices[(vertexIndex + 1) % vertices.Length].Y -
                vertex.Y * vertices[(vertexIndex + 1) % vertices.Length].X).Sum();
            Require(twiceArea > .1f,
                $"{enemy.Definition.Id} {enemy.Rank}: shield and frost follow a consistently wound family-specific material boundary.", assertions);
            Require(masks[index].Count(opaque => opaque) >= 60,
                $"{enemy.Definition.Id} {enemy.Rank}: the chassis has a substantial opaque silhouette at gameplay size.", assertions);
            Require(checksums[index] == SessionChecksum.Compute(samples[index].Session, 0),
                $"{enemy.Definition.Id} {enemy.Rank}: family comparison leaves synchronized state untouched.", assertions);
        }
        for (var family = 0; family < families.Length; family++)
        for (var rank = 1; rank < ranks.Length; rank++)
        {
            var difference = SilhouetteDifference(masks[family * 3 + rank - 1], masks[family * 3 + rank]);
            Require(difference >= .03f,
                $"{families[family]}: {ranks[rank]} changes {difference:P1} of the monochrome silhouette from {ranks[rank - 1]}.", assertions);
        }
        for (var rank = 0; rank < ranks.Length; rank++)
        for (var first = 0; first < families.Length; first++)
        for (var second = first + 1; second < families.Length; second++)
        {
            var difference = SilhouetteDifference(masks[first * 3 + rank], masks[second * 3 + rank]);
            Require(difference >= .05f,
                $"{ranks[rank]} {families[first]} and {families[second]} differ across {difference:P1} of their silhouettes without color or status cues.", assertions);
        }
        foreach (var rank in ranks)
        foreach (var heading in new[] { 0f, MathHelper.PiOver4, MathHelper.PiOver2 })
        {
            var heavy = new[] { "t3_brute", "t4_aegis", "t5_regenerator" }
                .Select(id => EnemyRankSample(content, id, rank)).ToArray();
            var normalizedMasks = heavy.Select(sample => EnemyFamilySilhouette(sample.Enemy, heading, 24)).ToArray();
            var completeMasks = heavy.Select(sample => EnemyFamilyModelSilhouette(sample.Enemy, heading, 24)).ToArray();
            for (var first = 0; first < heavy.Length; first++)
            for (var second = first + 1; second < heavy.Length; second++)
            {
                var overlap = 1 - SilhouetteDifference(normalizedMasks[first], normalizedMasks[second]);
                // Radial hulls share armor proportions; mounted limbs preserve the stronger outline separation.
                Require(overlap < .95f,
                    $"{rank} {heavy[first].Enemy.Definition.Id} and {heavy[second].Enemy.Definition.Id}: equal-radius hull overlap is {overlap:P1} at {MathHelper.ToDegrees(heading):0} degrees.", assertions);
                var completeOverlap = 1 - SilhouetteDifference(completeMasks[first], completeMasks[second]);
                Require(completeOverlap < .85f,
                    $"{rank} {heavy[first].Enemy.Definition.Id} and {heavy[second].Enemy.Definition.Id}: equal-radius complete model overlap is {completeOverlap:P1} at {MathHelper.ToDegrees(heading):0} degrees, including legs and mounted armor.", assertions);
            }
        }
        AssertEnemyFamilySolidFootprints(content, samples, assertions);
        AssertEnemyFamilySizeHierarchy(content, assertions);
        scenes.Add(CaptureEnemyScaleAtlas(content, samples, font));
        AssertEnemyFamilyShieldRecipients(content, font, assertions, scenes);
        CaptureEnemyFamilyBattlefields(content, font, display, assertions, scenes);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ENEMY_FAMILY_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "enemy-family-animation"));
        for (var frame = 0; frame < 72; frame++)
            scenes.Add(CaptureEnemyFamilyAtlas(samples, font,
                $"enemy-family-animation/roster-{frame:000}.png", frame / 24f));
    }

    private void AssertEnemyFamilySizeHierarchy(GameContent content, ICollection<string> assertions)
    {
        var families = new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" };
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        foreach (var heading in new[] { 0f, MathHelper.PiOver4, MathHelper.PiOver2 })
        {
            var previousArea = 0;
            foreach (var family in families)
            {
                var sample = EnemyRankSample(content, family, rank);
                var area = EnemyFamilyModelSilhouette(sample.Enemy, heading).Count(value => value);
                Require(area >= previousArea * 1.12f,
                    $"{rank} at {MathHelper.ToDegrees(heading):0} degrees: {family} solid silhouette area {area}px exceeds the preceding size tier {previousArea}px by at least 12%.", assertions);
                previousArea = area;
            }
        }
    }

    private bool[] EnemyFamilySilhouette(EnemyInstance enemy, float heading, float? radius = null)
    {
        const int size = 96;
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Transparent);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        EnemyModelArt.DrawHull(_batch, _primitives, enemy, new Vector2(size * .5f),
            radius ?? EnemyModelArt.BodyRadius(enemy), heading, ColorPalette.Paper);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[size * size];
        target.GetData(pixels);
        return pixels.Select(pixel => pixel.A >= 240).ToArray();
    }

    private void AssertEnemyFamilyShieldRecipients(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ranks = new[] { "Standard", "Elite", "Boss" };
        foreach (var definition in content.Enemies.Values)
        {
            var gallery = new List<StatusReviewEnemy>();
            foreach (var rank in ranks)
            {
                var baseline = EnemyShieldRecipient(content, definition.Id, rank);
                var shielded = EnemyShieldRecipient(content, definition.Id, rank, shielded: true);
                var stacked = EnemyShieldRecipient(content, definition.Id, rank, shielded: true, stacked: true);
                var expectedShield = MathF.Min(shielded.Enemy.MaxHealth * shielded.Session.Challenge.CounterShieldFraction,
                    definition.Shield + shielded.Enemy.MaxHealth * shielded.Session.Challenge.CounterShieldCapacityFraction);
                Require(shielded.Enemy.SignalRole == EnemySignalRole.None &&
                        MathF.Abs(shielded.Enemy.Shield - expectedShield) < .01f,
                    $"{definition.Id} {rank}: an actual Bulwark pulse grants {expectedShield:0.##} shield to the ordinary nonsignal recipient.", assertions);
                Require(shielded.Enemy.Health == baseline.Enemy.Health &&
                        shielded.Enemy.CurrentSpeed == baseline.Enemy.CurrentSpeed &&
                        shielded.Enemy.EffectiveArmor == baseline.Enemy.EffectiveArmor &&
                        shielded.Enemy.Radius == baseline.Enemy.Radius,
                    $"{definition.Id} {rank}: receiving shield leaves health, speed, armor, and hit radius unchanged.", assertions);
                foreach (var reduced in new[] { false, true })
                {
                    var clear = EnemyRankPixels(baseline, .43f, reduced);
                    var charged = EnemyRankPixels(shielded, .43f, reduced);
                    var combined = EnemyRankPixels(stacked, .43f, reduced);
                    Require(clear.Zip(charged, (a, b) => a != b).Count(changed => changed) >= 20,
                        $"{definition.Id} {rank}: the received shield is visible at native size in {(reduced ? "Reduced" : "Full")} effects.", assertions);
                    Require(charged.Zip(combined, (a, b) => a != b).Count(changed => changed) >= 20 &&
                            MathF.Abs(stacked.Enemy.Shield - expectedShield) < .01f,
                        $"{definition.Id} {rank}: shield and stacked conditions coexist without changing shield capacity.", assertions);
                    shielded.Enemy.AbsorbShield(shielded.Enemy.Shield);
                    Require(shielded.Enemy.Shield == 0 && clear.SequenceEqual(EnemyRankPixels(shielded, .43f, reduced)),
                        $"{definition.Id} {rank}: complete shield depletion restores the exact unshielded rendering in {(reduced ? "Reduced" : "Full")} effects.", assertions);
                    ActivateRecipientShield(shielded);
                    Require(charged.SequenceEqual(EnemyRankPixels(shielded, .43f, reduced)),
                        $"{definition.Id} {rank}: a later Bulwark pulse restores the shield presentation.", assertions);
                }
                Require(SignalModulePixels(shielded, .43f, false, 1, identityOnly: true)
                        .All(pixel => pixel == ColorPalette.Canvas),
                    $"{definition.Id} {rank}: receiving a shield does not add a signal identity module to an ordinary enemy.", assertions);
                gallery.Add(baseline);
                gallery.Add(shielded);
                gallery.Add(stacked);

                foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
                {
                    var carrier = EnemyShieldRecipient(content, definition.Id, rank, role: role);
                    var reference = new[] { false, true }.Select(reduced =>
                        SignalModulePixels(carrier, .43f, reduced, 1, identityOnly: true)).ToArray();
                    ActivateRecipientShield(carrier);
                    ApplyRecipientShieldConditions(carrier.Enemy);
                    foreach (var reduced in new[] { false, true })
                        Require(reference[reduced ? 1 : 0].SequenceEqual(
                                SignalModulePixels(carrier, .43f, reduced, 1, identityOnly: true)),
                            $"{definition.Id} {rank} {role}: received shields and debuffs preserve the underlying gem construction in {(reduced ? "Reduced" : "Full")} effects.", assertions);
                    var checksum = SessionChecksum.Compute(carrier.Session, 0);
                    _ = EnemyRankPixels(carrier, .43f, false);
                    Require(checksum == SessionChecksum.Compute(carrier.Session, 0),
                        $"{definition.Id} {rank} {role}: shield and signal presentation leaves gameplay state unchanged.", assertions);
                }
            }
            scenes.Add(CaptureEnemyRankAtlas(gallery, font,
                $"{definition.DisplayName.ToUpperInvariant()} / SHIELD RECEIVED FROM BULWARK",
                $"60d-shield-recipients-{definition.Id}.png"));
        }
    }

    private static StatusReviewEnemy EnemyShieldRecipient(GameContent content, string id, string rank,
        bool shielded = false, bool stacked = false, EnemySignalRole role = EnemySignalRole.None)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        session.SpawnEnemy("t1_crawler", 5, 1, "Standard", EnemySignalRole.Bulwark);
        session.SpawnEnemy(id, 5, 1, rank, role);
        session.Enemies[0].SetSandboxPathDistance(410, session.Map.Path);
        var recipient = session.Enemies[1];
        recipient.SetSandboxPathDistance(430, session.Map.Path);
        recipient.AbsorbShield(recipient.Shield);
        var condition = stacked ? "SHIELD + DEBUFFS" : shielded ? "SHIELDED" : "UNSHIELDED";
        var sample = new StatusReviewEnemy($"{rank.ToUpperInvariant()} / {condition}", session, recipient,
            Heading: -.48f);
        if (shielded) ActivateRecipientShield(sample);
        if (stacked) ApplyRecipientShieldConditions(recipient);
        return sample;
    }

    private static void ActivateRecipientShield(StatusReviewEnemy sample)
    {
        var source = sample.Session.Enemies[0];
        source.ArmSignalAbility(0);
        sample.Session.TryActivateEnemySignal(source);
    }

    private static void ApplyRecipientShieldConditions(EnemyInstance enemy)
    {
        foreach (var status in new[] { StatusType.Slow, StatusType.Burn, StatusType.ArmorBreak, StatusType.Exposed })
            enemy.ApplyStatus(new StatusApplication
            {
                Type = status,
                Duration = 12,
                Magnitude = status is StatusType.Burn or StatusType.ArmorBreak ? 4 : .35f
            });
    }

    private static float SilhouetteDifference(IReadOnlyList<bool> first, IReadOnlyList<bool> second)
    {
        var union = 0;
        var difference = 0;
        for (var pixel = 0; pixel < first.Count; pixel++)
        {
            if (first[pixel] || second[pixel]) union++;
            if (first[pixel] != second[pixel]) difference++;
        }
        return union == 0 ? 0 : difference / (float)union;
    }

    private bool[] EnemyFamilyModelSilhouette(EnemyInstance enemy, float heading,
        float? radius = null, float travelDistance = 0, int rasterScale = 1)
    {
        var size = 96 * rasterScale;
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Transparent);
        var scale = rasterScale * (radius.HasValue ? radius.Value / EnemyModelArt.BodyRadius(enemy) : 1);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(size * .5f, size * .5f, 0));
        EnemyModelArt.Draw(_batch, _primitives, enemy, Vector2.Zero, heading, .43f, true, travelDistance,
            castShadows: false);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[size * size];
        target.GetData(pixels);
        // Shadow-free opaque coverage includes dark recesses independently of surface lighting.
        return pixels.Select(pixel => pixel.A >= 240).ToArray();
    }

    private void AssertEnemyFamilySolidFootprints(GameContent content, IReadOnlyList<StatusReviewEnemy> samples,
        ICollection<string> assertions)
    {
        const int rasterScale = 2;
        const int size = 96 * rasterScale;
        var center = new Vector2(size * .5f);
        var pixelExtent = MathF.Sqrt(2) * .5f / rasterScale;
        var supportingHalfWidth = content.Maps.Values.Min(map => map.PathWidth * .5f - 2);
        foreach (var sample in samples)
        {
            var maximumAcross = 0f;
            var maximumAlong = 0f;
            var poses = 0;
            var rank = sample.Enemy.IsBoss ? 2 : sample.Enemy.IsElite ? 1 : 0;
            var grounded = EnemyLocomotion.UsesGroundedGait(sample.Enemy);
            var stride = grounded ? EnemyLocomotion.StrideDistance(sample.Enemy) : 12f - rank;
            var longitudinalLimit = grounded ? EnemyLocomotion.MaximumReach(sample.Enemy) :
                EnemyModelArt.BodyRadius(sample.Enemy) + 3;
            for (var direction = 0; direction < 8; direction++)
            for (var phase = 0; phase < 4; phase++)
            {
                var heading = direction * MathHelper.TwoPi / 8;
                var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
                var side = new Vector2(-forward.Y, forward.X);
                var mask = EnemyFamilyModelSilhouette(sample.Enemy, heading,
                    travelDistance: phase * stride * .25f, rasterScale: rasterScale);
                for (var index = 0; index < mask.Length; index++)
                {
                    if (!mask[index]) continue;
                    var pixelCenter = new Vector2(index % size + .5f, index / size + .5f);
                    var relative = (pixelCenter - center) / rasterScale;
                    maximumAcross = MathF.Max(maximumAcross, MathF.Abs(Vector2.Dot(relative, side)) + pixelExtent);
                    maximumAlong = MathF.Max(maximumAlong, MathF.Abs(Vector2.Dot(relative, forward)) + pixelExtent);
                }
                poses++;
            }
            Require(maximumAcross <= supportingHalfWidth && maximumAlong <= longitudinalLimit,
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: {poses} rendered solid poses retain 2px lateral clearance on the narrowest deck ({maximumAcross:0.00}px half-width, {maximumAlong:0.00}px fore/aft reach), including armor and feet but excluding shadows and exhaust.", assertions);
        }
    }

    private VisualVerificationScene CaptureEnemyScaleAtlas(GameContent content,
        IReadOnlyList<StatusReviewEnemy> samples, SpriteFont font)
    {
        const int width = 1280;
        const int top = 88;
        const int rowHeight = 172;
        const int columnWidth = 248;
        var roadWidth = content.Maps.Values.Min(map => map.PathWidth);
        using var target = new RenderTarget2D(GraphicsDevice, width, top + 3 * rowHeight + 24, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "ENEMY SCALE / NATIVE GAMEPLAY SIZE", new Vector2(22, 15), .76f);
        StatusLabel(font, $"{roadWidth:0}px NARROWEST TRACK / SHIELDS AND CONDITIONS OFF / VISUAL SIZE DOES NOT ALTER COLLISION",
            new Vector2(22, 49), .53f);
        for (var family = 0; family < samples.Count / 3; family++)
        for (var rank = 0; rank < 3; rank++)
        {
            var sample = samples[family * 3 + rank];
            var x = 20 + family * columnWidth;
            var y = top + rank * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(x, y, columnWidth - 8, rowHeight - 8), ColorPalette.Panel);
            StatusLabel(font, $"{sample.Enemy.Definition.DisplayName.ToUpperInvariant()} / {sample.Enemy.Rank.ToString().ToUpperInvariant()}",
                new Vector2(x + 10, y + 10), .48f);
            var center = new Vector2(x + (columnWidth - 8) * .5f, y + 76);
            _primitives.FillRect(_batch, new Rectangle(x + 10, (int)(center.Y - roadWidth * .5f), columnWidth - 28, (int)roadWidth),
                ColorPalette.Navy);
            foreach (var side in new[] { -1, 1 })
                _primitives.Line(_batch, new Vector2(x + 10, center.Y + side * roadWidth * .5f),
                    new Vector2(x + columnWidth - 18, center.Y + side * roadWidth * .5f), ColorPalette.Metal, 1);
            EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, center, 0, 0, true);
            var radius = EnemyModelArt.BodyRadius(sample.Enemy);
            var contacts = EnemyLocomotion.ContactSlots(sample.Enemy).Length;
            StatusLabel(font, $"BODY R{radius:0.#} / {(contacts == 0 ? sample.Enemy.Definition.Id == "t5_regenerator" ? "TRACKS" : "SKIMMER" : $"{contacts} CONTACTS")}",
                new Vector2(x + 10, y + 115), .47f);
            StatusLabel(font, "2px+ LATERAL SOLID MARGIN", new Vector2(x + 10, y + 139), .43f);
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, "60b-enemy-native-track-scale.png");
    }

    private VisualVerificationScene CaptureEnemyFamilyAtlas(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, string fileName, float time = .43f)
    {
        const int width = 1120;
        const int rowHeight = 126;
        const int top = 110;
        var families = samples.Count / 3;
        using var target = new RenderTarget2D(GraphicsDevice, width, top + rowHeight * families + 16,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "ENEMY FAMILIES / STANDARD, ELITE, BOSS", new Vector2(24, 16), .78f);
        StatusLabel(font, "EACH CELL: 2x DETAIL / GAMEPLAY SIZE / 65% SIZE", new Vector2(24, 52), .58f);
        for (var rank = 0; rank < 3; rank++)
            StatusLabel(font, new[] { "STANDARD", "ELITE", "BOSS" }[rank],
                new Vector2(210 + rank * 296, 83), .57f);
        for (var family = 0; family < families; family++)
        {
            var y = top + family * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(12, y, width - 24, rowHeight - 5), ColorPalette.Panel);
            StatusLabel(font, samples[family * 3].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(27, y + 49), .66f);
        }
        _batch.End();
        for (var index = 0; index < samples.Count; index++)
        {
            var x = 207 + index % 3 * 296;
            var y = top + index / 3 * rowHeight + 61;
            DrawAt(new Vector2(x + 45, y), 2);
            DrawAt(new Vector2(x + 160, y), 1);
            DrawAt(new Vector2(x + 230, y), .65f);
            void DrawAt(Vector2 position, float scale)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(position.X, position.Y, 0));
                DrawEnemyRankSample(samples[index], Vector2.Zero, time, false);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private VisualVerificationScene CaptureEnemyFamilySilhouettes(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font, string fileName, bool fullModel = false)
    {
        const int width = 1120;
        const int top = 110;
        const int rowHeight = 126;
        var textures = new List<Texture2D>();
        try
        {
            foreach (var sample in samples)
            {
                var mask = fullModel
                    ? EnemyFamilyModelSilhouette(sample.Enemy, sample.Heading)
                    : EnemyFamilySilhouette(sample.Enemy, sample.Heading);
                var texture = new Texture2D(GraphicsDevice, 96, 96);
                texture.SetData(mask.Select(opaque => opaque ? ColorPalette.Paper : Color.Transparent).ToArray());
                textures.Add(texture);
            }
            using var target = new RenderTarget2D(GraphicsDevice, width, top + rowHeight * (samples.Count / 3) + 16,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            StatusLabel(font, "FAMILY SILHOUETTES / NO COLOR OR STATUS CUES", new Vector2(24, 16), .78f);
            StatusLabel(font, fullModel ? "COMPLETE SOLID MODEL: 2x / 1x / 65%" : "CHASSIS CONTOURS: 2x / 1x / 65%",
                new Vector2(24, 52), .58f);
            for (var rank = 0; rank < 3; rank++)
                StatusLabel(font, new[] { "STANDARD", "ELITE", "BOSS" }[rank],
                    new Vector2(210 + rank * 296, 83), .57f);
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
                        0, new Vector2(48), scale, SpriteEffects.None, 0);
            }
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            return SaveTierScene(target, fileName);
        }
        finally { foreach (var texture in textures) texture.Dispose(); }
    }

    private void CaptureEnemyFamilyBattlefields(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var animate = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_ENEMY_FAMILY_ANIMATION") == "1";
        if (animate) Directory.CreateDirectory(Path.Combine(_outputDirectory, "enemy-family-animation"));
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
                var families = content.Enemies.Keys.ToArray();
                var ranks = new[] { "Standard", "Elite", "Boss" };
                for (var index = 0; index < 15; index++)
                {
                    session.SpawnEnemy(families[index % families.Length], 100, 1,
                        ranks[index / families.Length], (EnemySignalRole)(index % 6));
                    var enemy = session.Enemies[^1];
                    var distance = 200 + index * (session.Map.Path.TotalLength - 850) / 15;
                    enemy.SetSandboxPathDistance(distance, session.Map.Path);
                    enemy.ArmSignalAbility(0);
                }
                for (var step = 0; step < 300; step++) session.Update(1f / 60);
                Require(session.AnnouncementRemaining <= 0 && session.Enemies.Count == 15 &&
                        session.Enemies.All(enemy => !enemy.IsDead && !enemy.HasEscaped),
                    $"{map.Id}: all fifteen family and rank variants are present with a clear announcement area.", assertions);
                for (var index = 0; index < session.Enemies.Count; index++)
                    session.Enemies[index].SetSandboxPathDistance(
                        200 + index * (session.Map.Path.TotalLength - 850) / 15, session.Map.Path);
                foreach (var towerId in new[] { "prism_beam", "ember_coil", "frost_spire", "breaker_cannon", "arc_relay" })
                    Require(candidates.Any(point => session.TryPlaceTower(towerId, point)),
                        $"{map.Id}: mixed-family review includes a legal {towerId} emplacement.", assertions);
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                for (var step = 0; step < 24; step++) session.Update(1f / 60);
                var checksum = SessionChecksum.Compute(session, 0);
                foreach (var reduced in new[] { false, true })
                {
                    _renderer.ReducedEffects = reduced;
                    scenes.Add(CaptureEnemyRankNative(session, ui,
                        $"60b-enemy-families-{map.Id}-{(reduced ? "reduced" : "full")}.png"));
                }
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{map.Id}: full and reduced family rendering leave gameplay unchanged.", assertions);
                _renderer.ReducedEffects = false;
                foreach (var enemy in session.Enemies) ApplyEnemyRankConditions(enemy);
                for (var step = 0; step < 6; step++) session.Update(1f / 60);
                scenes.Add(CaptureEnemyRankNative(session, ui, $"60c-enemy-families-conditions-{map.Id}.png"));
                if (animate && map.Id == "foundry_loop")
                {
                    for (var frame = 0; frame < 72; frame++)
                    {
                        for (var step = 0; step < 5; step++) session.Update(1f / 120);
                        ui.AdvanceVisualTime(1f / 24);
                        scenes.Add(CaptureEnemyRankNative(session, ui,
                            $"enemy-family-animation/combat-{frame:000}.png"));
                    }
                }
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }
}
