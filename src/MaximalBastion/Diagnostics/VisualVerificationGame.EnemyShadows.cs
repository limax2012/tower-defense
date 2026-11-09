using MaximalBastion.Core;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyGroundShadows(IReadOnlyList<StatusReviewEnemy> samples, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        scenes.Add(CaptureEnemyGroundShadows(samples, font));
        const int size = 192;
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var pixels = new Color[size * size];
        var contacts = new EnemyGroundContact[8];
        var measurements = new List<object>();
        foreach (var sample in samples)
        {
            var checksum = SessionChecksum.Compute(sample.Session, 0);
            for (var direction = 0; direction < 4; direction++)
            for (var phase = 0; phase < 4; phase++)
            {
                var center = new Vector2(size * .25f);
                var heading = direction * MathHelper.PiOver2;
                var distance = EnemyLocomotion.StrideDistance(sample.Enemy) * phase / 4;
                var slots = EnemyLocomotion.ContactSlots(sample.Enemy);
                EnemyModelArt.SampleGroundContacts(sample.Enemy, center, heading, distance, null, false,
                    contacts.AsSpan(0, slots.Length));
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(Color.Transparent);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(2));
                EnemyModelArt.DrawGroundShadow(_batch, _primitives, sample.Enemy, center, heading, .43f, true,
                    contacts.AsSpan(0, slots.Length));
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
                target.GetData(pixels);
                var maximumAlpha = pixels.Max(pixel => pixel.A);
                var covered = pixels.Count(pixel => pixel.A > 0);
                var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
                var lateral = new Vector2(-forward.Y, forward.X);
                var radius = EnemyModelArt.BodyRadius(sample.Enemy);
                var outline = EnemyModelContours.Shapes[EnemyModelContours.ArmorOutlineId(sample.Enemy)];
                var hull = new Vector2[outline.Length];
                for (var vertex = 0; vertex < hull.Length; vertex++)
                {
                    var local = outline[vertex];
                    hull[vertex] = center + new Vector2(1, 2) +
                        (forward * local.X + lateral * local.Y) * radius;
                }
                var outsideFootprint = 0;
                for (var pixel = 0; pixel < pixels.Length; pixel++)
                {
                    if (pixels[pixel].A == 0) continue;
                    var point = new Vector2((pixel % size + .5f) / 2, (pixel / size + .5f) / 2);
                    var inFootprint = ShadowHullContains(point, hull);
                    for (var contactIndex = 0; !inFootprint && contactIndex < slots.Length; contactIndex++)
                    {
                        var contact = contacts[contactIndex];
                        var offset = point - contact.GroundPoint;
                        var pad = contact.Pose.ContactRadius;
                        inFootprint = MathF.Abs(Vector2.Dot(offset, forward)) <=
                            pad * (contact.Planted ? 1.15f : .8f) + .75f &&
                            MathF.Abs(Vector2.Dot(offset, lateral)) <=
                            pad * (contact.Planted ? 1.5f : .9f) * .5f + .75f;
                    }
                    if (!inFootprint) outsideFootprint++;
                }
                var visibleContacts = 0;
                for (var contactIndex = 0; contactIndex < slots.Length; contactIndex++)
                {
                    var point = contacts[contactIndex].GroundPoint * 2;
                    var visible = false;
                    for (var y = (int)MathF.Floor(point.Y) - 1; y <= (int)MathF.Ceiling(point.Y) + 1; y++)
                    for (var x = (int)MathF.Floor(point.X) - 1; x <= (int)MathF.Ceiling(point.X) + 1; x++)
                        if (x >= 0 && x < size && y >= 0 && y < size && pixels[y * size + x].A > 0)
                            visible = true;
                    if (visible) visibleContacts++;
                }
                measurements.Add(new
                {
                    family = sample.Enemy.Definition.Id, rank = sample.Enemy.Rank.ToString(),
                    headingDegrees = direction * 90, phase = phase / 4f, maximumAlpha, coveredPixels = covered,
                    outsideFootprintPixels = outsideFootprint, visibleContacts, expectedContacts = slots.Length
                });
                Require(covered > 20 && maximumAlpha == 255 && outsideFootprint == 0 && visibleContacts == slots.Length,
                    $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}, heading {direction * 90}, phase {phase}/4: " +
                    $"ground shadow covers its outer armor and compact foot contacts without spreading outside them " +
                    $"({covered} pixels, alpha {maximumAlpha}/255, {outsideFootprint} outside, {visibleContacts}/{slots.Length} contacts).",
                    assertions);
            }
            Require(SessionChecksum.Compute(sample.Session, 0) == checksum,
                $"{sample.Enemy.Definition.Id} {sample.Enemy.Rank}: shadow sampling leaves synchronized state untouched.",
                assertions);
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "enemy-shadow-measurements.json"),
            System.Text.Json.JsonSerializer.Serialize(measurements,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool ShadowHullContains(Vector2 point, ReadOnlySpan<Vector2> hull)
    {
        const float rasterMargin = .75f;
        var inside = false;
        for (var index = 0; index < hull.Length; index++)
        {
            var a = hull[index];
            var b = hull[(index + 1) % hull.Length];
            var edge = b - a;
            var amount = Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
            if (Vector2.DistanceSquared(point, a + edge * amount) <= rasterMargin * rasterMargin) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private VisualVerificationScene CaptureEnemyGroundShadows(IReadOnlyList<StatusReviewEnemy> samples,
        SpriteFont font)
    {
        var standards = samples.Where(sample => sample.Enemy.Rank == EnemyRank.Standard).ToArray();
        const int width = 1280;
        const int height = 1030;
        var ground = ColorPalette.Rainline.Deck;
        var headings = new[] { -MathHelper.PiOver2, 0, MathHelper.PiOver4 };
        var labels = new[] { "UP / CYCLE START", "RIGHT / 1/3 CYCLE", "DIAGONAL / 2/3 CYCLE" };
        using var target = new RenderTarget2D(GraphicsDevice, width * 2, height * 2, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "ENEMY GROUND SHADOWS / HULLS + CONTACTS", new Vector2(20, 24), .78f);
        StatusLabel(font, "STANDARD RANK / TWO-TIMES DETAIL + NATIVE GAMEPLAY SIZE", new Vector2(20, 62), .52f);
        _primitives.FillRect(_batch, new Rectangle(140, 100, 1106, 80), ground);
        NightGridArt.Tower(_batch, _primitives, new Vector2(220, 140), 22, "breaker_cannon", 2, lights: false);
        StatusLabel(font, "TOWER REFERENCE / NATIVE SIZE", new Vector2(280, 116), .55f);
        StatusLabel(font, "SHARED GROUND SURFACE", new Vector2(280, 145), .44f);
        for (var direction = 0; direction < 3; direction++)
            StatusLabel(font, labels[direction], new Vector2(154 + direction * 370, 196), .49f);
        for (var family = 0; family < 5; family++)
        {
            var y = 224 + family * 156;
            StatusLabel(font, standards[family].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(20, y + 64), .45f);
            for (var direction = 0; direction < 3; direction++)
            {
                var x = 140 + direction * 370;
                _primitives.FillRect(_batch, new Rectangle(x, y, 366, 146), ground);
                for (var band = 0; band < 4; band++)
                    _primitives.Line(_batch, new Vector2(x, y + 18 + band * 36),
                        new Vector2(x + 366, y + 18 + band * 36), ColorPalette.Rainline.Edge * .10f, 1);
            }
        }
        _batch.End();
        for (var family = 0; family < 5; family++)
        for (var direction = 0; direction < 3; direction++)
        {
            var sample = standards[family];
            var y = 297 + family * 156;
            var distance = EnemyLocomotion.StrideDistance(sample.Enemy) * direction / 3;
            DrawEnemyArmorBody(sample, new Vector2(250 + direction * 370, y), 2, headings[direction], .43f, distance);
            DrawEnemyArmorBody(sample, new Vector2(425 + direction * 370, y), 1, headings[direction], .43f, distance);
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
        return SaveTierScene(native, "enemy-ground-shadows.png");
    }
}
