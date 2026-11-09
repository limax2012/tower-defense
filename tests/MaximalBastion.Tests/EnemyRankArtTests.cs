using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class EnemyRankArtTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        OriginalGeometry(content);
        TierFootprints(content);
        BossTrackSupport(content);
        LegacyBossPresentation(content);
    }

    internal static float OutlineExtension(MapDefinition map) => map.PathVisual.Style == "trail" ? 7 : 4;

    private static void OriginalGeometry(GameContent content)
    {
        var shapes = new (string Family, string Rank, float Radius, Vector2[] Upper)[]
        {
            ("t2_runner", "Standard", 12,
                [new(.96f, 0), new(.12f, -.25f), new(-.48f, -.70f), new(-.32f, -.16f), new(-.65f, 0)]),
            ("t2_runner", "Elite", 15,
                [new(.96f, 0), new(.14f, -.40f), new(-.28f, -.76f), new(-.58f, -.76f), new(-.52f, -.32f), new(-.30f, 0)]),
            ("t1_crawler", "Standard", 16,
                [new(.70f, 0), new(.70f, -.40f), new(.47f, -.53f), new(-.12f, -.48f), new(-.54f, -.31f), new(-.64f, -.19f), new(-.64f, 0)]),
            ("t3_brute", "Standard", 20.5f,
                [new(.74f, 0), new(.37f, -.68f), new(-.37f, -.68f), new(-.74f, 0)]),
            ("t4_aegis", "Standard", 27,
                [new(.75f, 0), new(.75f, -.28f), new(.34f, -.62f), new(-.34f, -.62f), new(-.75f, -.28f), new(-.75f, 0)]),
            ("t5_regenerator", "Standard", 34,
                [new(.85f, 0), new(.85f, -.28f), new(.64f, -.45f), new(.48f, -.61f), new(.32f, -.70f), new(-.32f, -.70f), new(-.48f, -.61f), new(-.64f, -.45f), new(-.85f, -.28f), new(-.85f, 0)])
        };
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var shape in shapes)
        {
            session.SpawnEnemy(shape.Family, 1, 1, shape.Rank);
            var enemy = session.Enemies[^1];
            var expected = shape.Upper.Concat(shape.Upper.Where(v => v.Y < 0).Select(v => v * new Vector2(1, -1))).ToHashSet();
            var actual = Hull(enemy);
            var label = $"{shape.Family} {shape.Rank}";
            Check.Equal(shape.Radius, EnemyModelArt.BodyRadius(enemy), $"{label}: established presentation scale");
            Check.True(actual.Length == expected.Count && expected.SetEquals(actual), $"{label}: established authored silhouette");
            var coreRatio = shape.Family is "t2_runner" or "t1_crawler" ? .43f : .38f;
            Check.Equal(shape.Radius * coreRatio, EnemyModelArt.CoreRadius(enemy), $"{label}: established reactor scale");
            if (shape.Family == "t5_regenerator")
                Check.Equal((.29f, .11f, .59f), EnemyModelArt.TrackGeometry(enemy), "Standard Regenerator track footprint");
        }
    }

    private static void TierFootprints(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
        {
            session.SpawnEnemy(family, 1, 1, "Standard");
            var standard = session.Enemies[^1];
            session.SpawnEnemy(family, 1, 1, "Elite");
            var elite = session.Enemies[^1];
            Check.True(Area(elite) >= Area(standard) * 1.20f,
                $"{family}: Elite armor occupies at least twenty percent more screen area than Standard");
            Check.Equal(EnemyLocomotion.ContactSlots(standard).Length, EnemyLocomotion.ContactSlots(elite).Length,
                $"{family}: rank armor preserves the family locomotion anatomy");
            Check.True(EnemyModelArt.CoreRadius(elite) > EnemyModelArt.CoreRadius(standard) * 1.15f,
                $"{family}: the larger reactor remains readable at native size");
            var points = Hull(elite).ToHashSet();
            Check.True(points.All(v => points.Contains(v * new Vector2(1, -1))),
                $"{family}: reinforced hulls retain bilateral symmetry");
        }
        var eliteTank = session.Enemies[^1];
        session.SpawnEnemy("t5_regenerator", 1, 1, "Boss");
        var boss = session.Enemies[^1];
        Check.True(Area(boss) >= Area(eliteTank) * 1.20f,
            "Regenerator Boss has a clearly larger armored footprint than Elite before overdrive");
        Check.True(EnemyModelArt.TrackGeometry(boss).HalfLength * EnemyModelArt.BodyRadius(boss) >
            EnemyModelArt.TrackGeometry(eliteTank).HalfLength * EnemyModelArt.BodyRadius(eliteTank) * 1.25f,
            "Boss tracks support the extended five-module chassis");
    }

    private static void BossTrackSupport(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy("t5_regenerator", 1, 1, "Boss");
            var boss = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(boss);
            var track = EnemyModelArt.TrackGeometry(boss);
            var path = EnemyLocomotionPath.For(session.Map);
            var halfPathWidth = map.PathWidth * .5f + OutlineExtension(map);
            var minimumSupport = 1f;
            for (var distance = radius; distance < session.Map.Path.TotalLength - radius; distance += 1)
            {
                var center = path.VisualPosition(distance, EnemyLocomotion.MotionRadius(boss),
                    tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(boss));
                var forward = path.Forward(distance, EnemyLocomotion.MotionRadius(boss),
                    tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(boss));
                var lateral = new Vector2(-forward.Y, forward.X);
                for (var side = -1; side <= 1; side += 2)
                for (var station = 0; station <= 16; station++)
                {
                    var along = MathHelper.Lerp(-track.HalfLength, track.HalfLength, station / 16f);
                    var slice = center + (forward * along + lateral * (side * track.LateralOffset)) * radius;
                    var supported = 0;
                    const int widthSamples = 32;
                    for (var sample = 0; sample < widthSamples; sample++)
                    {
                        var offset = MathHelper.Lerp(-track.HalfWidth, track.HalfWidth,
                            (sample + .5f) / widthSamples) * radius;
                        if (session.Map.Path.DistanceToPath(slice + lateral * offset) <= halfPathWidth)
                            supported++;
                    }
                    minimumSupport = MathF.Min(minimumSupport, supported / (float)widthSamples);
                }
            }
            Check.True(minimumSupport >= .5f,
                $"{map.Id}: at least half of each Boss tread cross-section is supported by the path outline through turns ({minimumSupport:P1})");
        }
    }

    private static void LegacyBossPresentation(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var family in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis" })
        {
            session.SpawnEnemy(family, 1, 1, "Elite");
            var elite = session.Enemies[^1];
            session.SpawnEnemy(family, 1, 1, "Boss");
            var legacy = session.Enemies[^1];
            var checksum = SessionChecksum.Compute(session, 0);
            Check.Equal(EnemyModelArt.BodyRadius(elite), EnemyModelArt.BodyRadius(legacy), $"{family}: legacy Boss uses supported Elite scale");
            Check.Equal(EnemyModelArt.CoreRadius(elite), EnemyModelArt.CoreRadius(legacy), $"{family}: legacy Boss uses supported Elite reactor");
            Check.True(Hull(elite).SequenceEqual(Hull(legacy)), $"{family}: legacy Boss uses supported Elite silhouette");
            Check.True(legacy.IsBoss && legacy.MaxHealth > elite.MaxHealth,
                $"{family}: presentation fallback preserves legacy Boss gameplay state");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0), $"{family}: geometry sampling preserves synchronized state");
        }
        Check.True(EnemyModelContours.Shapes.Keys.Where(key => key.EndsWith("_boss", StringComparison.Ordinal))
            .SequenceEqual(["regenerator_boss"]), "Only Regenerator has authored Boss artwork");
    }

    private static Vector2[] Hull(EnemyInstance enemy) => Enumerable.Range(0, EnemyModelArt.HullVertexCount(enemy))
        .Select(index => EnemyModelArt.HullVertex(enemy, index)).ToArray();

    private static float Area(EnemyInstance enemy)
    {
        var hull = Hull(enemy);
        var area = 0f;
        for (var i = 0; i < hull.Length; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Length];
            area += a.X * b.Y - a.Y * b.X;
        }
        var radius = EnemyModelArt.BodyRadius(enemy);
        return MathF.Abs(area) * .5f * radius * radius;
    }
}
