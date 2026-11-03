using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class EnemyMaterialTimelineTests
{
    internal static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        HeatAttachment(content);
        FogEnvelopes(content);
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t2_runner", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.SetSandboxPathDistance(100, session.Map.Path);
        var timeline = new EnemyVisualTimeline();
        timeline.Observe(session, 0);
        foreach (var type in new[] { StatusType.Burn, StatusType.Slow, StatusType.Stun })
            enemy.ApplyStatus(new StatusApplication { Type = type, Magnitude = .3f, Duration = 3 });
        var checksum = SessionChecksum.Compute(session, 0);
        timeline.Observe(session, .1f);
        var onset = timeline.Feedback(enemy, .1f);
        Check.Nearly(.32f, onset.FrostSpread, "frost has a visible coat on application, before its spread settles");
        Check.Nearly(1, onset.BurnIgnition, "first burn application starts a single ignition response");
        Check.Nearly(1, onset.StunDischarge, "first stun application bridges the arrested contacts briefly");
        timeline.Observe(session, .3f);
        var growing = timeline.Feedback(enemy, .3f);
        Check.True(growing.FrostSpread > onset.FrostSpread && growing.FrostSpread < 1,
            "frost spreads continuously across the existing plate surfaces");
        Check.Nearly(0, growing.StunDischarge, "the onset envelope expires while the underlying stun remains active");
        Check.Equal(checksum, SessionChecksum.Compute(session, 0),
            "onset, buildup and particle emission leave synchronized state unchanged");
        Check.True(growing.Particles!.Any(particle => particle.Kind == EnemyMaterialParticleKind.Ember),
            "sustained burn can release a bounded detached ember");
        var ember = growing.Particles!.First(particle => particle.Kind == EnemyMaterialParticleKind.Ember);
        var worldPoint = ember.Position(.35f);
        enemy.SetSandboxPathDistance(360, session.Map.Path);
        timeline.Observe(session, .35f);
        var retained = timeline.Feedback(enemy, .35f).Particles!.Single(particle => particle.Equals(ember));
        Check.True(Vector2.Distance(worldPoint, retained.Position(.35f)) < .0001f,
            "an emitted ember remains in world coordinates when its owner crosses a bend");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = .3f, Duration = 4 });
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .3f, Duration = 4 });
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Magnitude = .3f, Duration = 4 });
        timeline.Observe(session, .7f);
        var refreshed = timeline.Feedback(enemy, .7f);
        Check.Nearly(0, refreshed.BurnIgnition, "burn refresh does not replay the ignition flash");
        Check.Nearly(0, refreshed.StunDischarge, "stun refresh does not restart the discharge");
        Check.Nearly(1, refreshed.FrostSpread, "slow refresh retains the settled material rather than restarting buildup");
        for (var tick = 22; tick < 300; tick++) timeline.Observe(session, tick / 30f);
        Check.True(timeline.ParticleCount <= 8, "decorative material particles have a fixed per-owner bound");
        var beforeExpiry = SessionChecksum.Compute(session, 0);
        timeline.Observe(session, 10);
        Check.Equal(beforeExpiry, SessionChecksum.Compute(session, 0),
            "material timing and density reduction never mutate the authoritative enemy");
        enemy.StatusEffects.Update(5);
        timeline.Observe(session, 10.01f);
        Check.Equal(4, timeline.Feedback(enemy, 10.01f).Particles!.Count(particle =>
            particle.Kind == EnemyMaterialParticleKind.IceFlake), "actual slow expiry releases four brief flakes");
        timeline.Observe(session, 10.7f);
        Check.Equal(0, timeline.ParticleCount, "all detached remnants expire after the conditions end");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = .3f, Duration = 1 });
        timeline.Observe(session, 11);
        Check.Nearly(1, timeline.Feedback(enemy, 11).BurnIgnition, "a new burn after expiry can ignite again");
        timeline.Observe(session, 11.2f);
        enemy.UpdateMovement(1000, session.Map.Path);
        timeline.Observe(session, 11.3f);
        Check.Equal(0, timeline.Count, "an escaped owner leaves no material cache entry");
        Check.Equal(0, timeline.ParticleCount, "escape removes particles without inventing death debris");

        var next = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        next.SpawnEnemy("t2_runner", 1, 1);
        var nextEnemy = next.Enemies.Single();
        nextEnemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = .3f, Duration = 1 });
        var nextChecksum = SessionChecksum.Compute(next, 0);
        timeline.Observe(next, 0);
        Check.Nearly(0, timeline.Feedback(nextEnemy, 0).BurnIgnition,
            "a restored already-burning enemy does not invent an unobserved application");
        Check.Equal(0, timeline.ParticleCount, "a new session does not inherit detached particles from reused IDs");
        Check.Equal(nextChecksum, SessionChecksum.Compute(next, 0), "restored presentation is read-only");
    }

    private static void FogEnvelopes(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t4_aegis", 1, 1);
        var enemy = session.Enemies.Single();
        var timeline = new EnemyVisualTimeline();
        timeline.Observe(session, 0, false);
        Check.Equal(Vector4.Zero, timeline.FogFeedback(enemy, 0).Opacity, "An ordinary enemy has no presentation fog");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = .3f, Duration = 5 });
        var checksum = SessionChecksum.Compute(session, 0);
        timeline.Observe(session, .1f, false);
        Check.Equal(Vector4.Zero, timeline.FogFeedback(enemy, .1f).Opacity,
            "The first sustained debuff starts the whole cloud at transparent density");
        timeline.Observe(session, .15f, false);
        Check.Nearly(.5f, timeline.FogFeedback(enemy, .15f).Opacity.X, "The cloud is half visible after 50 ms");
        timeline.Observe(session, .2f, false);
        Check.Equal(new Vector4(1, 0, 0, 0), timeline.FogFeedback(enemy, .2f).Opacity,
            "The cloud settles after 100 ms");
        Check.Equal(checksum, SessionChecksum.Compute(session, 0), "Fog timing leaves synchronized state unchanged");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .3f, Duration = 1 });
        timeline.Observe(session, .22f, false);
        Check.Equal(new Vector4(1, 1, 0, 0), timeline.FogFeedback(enemy, .22f).Opacity,
            "Adding a second color changes its assignment immediately without replaying onset");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.ArmorBreak, Magnitude = .3f, Duration = 1 });
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Magnitude = .3f, Duration = 1 });
        timeline.Observe(session, .24f, false);
        Check.Equal(Vector4.One, timeline.FogFeedback(enemy, .24f).Opacity,
            "Additional active colors share the cloud's settled density");
        enemy.StatusEffects.Update(2);
        timeline.Observe(session, .26f, false);
        Check.Equal(new Vector4(1, 0, 0, 0), timeline.FogFeedback(enemy, .26f).Opacity,
            "Removing some debuffs changes colors without a density transition while Burn remains");
        enemy.StatusEffects.Update(4);
        timeline.Observe(session, .3f, false);
        Check.Equal(new Vector4(1, 0, 0, 0), timeline.FogFeedback(enemy, .3f).Opacity,
            "The last color is retained at the start of whole-cloud dissipation");
        timeline.Observe(session, .37f, false);
        Check.Nearly(.5f, timeline.FogFeedback(enemy, .37f).Opacity.X, "Dissipation reaches half density after 70 ms");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .3f, Duration = 1 });
        timeline.Observe(session, .37f, false);
        Check.True(Vector4.Distance(new Vector4(0, .5f, 0, 0), timeline.FogFeedback(enemy, .37f).Opacity) < .0001f,
            "Reapplication during dissipation replaces color while preserving current cloud density");
        timeline.Observe(session, .47f, false);
        Check.Equal(new Vector4(0, 1, 0, 0), timeline.FogFeedback(enemy, .47f).Opacity,
            "A reapplied cloud settles without retaining obsolete colors");
        enemy.StatusEffects.Update(2);
        timeline.Observe(session, .5f, false);
        timeline.Observe(session, .64f, false);
        Check.True(timeline.FogFeedback(enemy, .64f).Opacity.LengthSquared() < .00000001f,
            "The last cloud reaches transparent density after 140 ms");

        var pauseSession = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        pauseSession.SpawnEnemy("t1_crawler", 1, 1);
        var pauseEnemy = pauseSession.Enemies.Single();
        var pauseTimeline = new EnemyVisualTimeline();
        pauseTimeline.Observe(pauseSession, 0, false);
        pauseEnemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = .3f, Duration = 1 });
        pauseTimeline.Observe(pauseSession, 0, false);
        pauseTimeline.Observe(pauseSession, .012f, false);
        var beforePause = pauseTimeline.FogFeedback(pauseEnemy, .012f);
        pauseTimeline.Observe(pauseSession, 0, false);
        Check.Equal(beforePause, pauseTimeline.FogFeedback(pauseEnemy, 0),
            "Removing a fractional lead on pause preserves an in-flight cloud onset");
        pauseTimeline.Observe(pauseSession, .024f, false);
        Check.True(pauseTimeline.FogFeedback(pauseEnemy, .024f).Opacity.X > beforePause.Opacity.X,
            "Cloud onset resumes continuously when presentation time advances");
        pauseTimeline.Observe(pauseSession, .3f, false);
        pauseEnemy.StatusEffects.Update(2);
        pauseTimeline.Observe(pauseSession, .3f, false);
        pauseTimeline.Observe(pauseSession, .312f, false);
        var fadingBeforePause = pauseTimeline.FogFeedback(pauseEnemy, .312f);
        pauseTimeline.Observe(pauseSession, .3f, false);
        Check.Equal(fadingBeforePause, pauseTimeline.FogFeedback(pauseEnemy, .3f),
            "Lead rollback on pause preserves dissipation after the last debuff expires");
    }

    private static void HeatAttachment(GameContent content)
    {
        foreach (var family in new[] { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var vent = EnemySurfaceAnchors.For(enemy).Vent;
            var ends = EnemySurfaceAnchors.HeatEnds(enemy);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var halfWidth = EnemyStatusArt.HeatPlateWidth(radius) * .5f / radius;
            foreach (var end in new[] { ends.First, ends.Last })
            {
                var direction = Vector2.Normalize(end - vent);
                var normal = new Vector2(-direction.Y, direction.X);
                for (var sample = 0; sample <= 20; sample++)
                for (var edge = -1; edge <= 1; edge++)
                {
                    var point = Vector2.Lerp(vent, end, sample / 20f) + normal * (halfWidth * edge);
                    var inside = false;
                    var count = EnemyModelArt.HullVertexCount(enemy);
                    for (var index = 0; index < count; index++)
                    {
                        var first = EnemyModelArt.HullVertex(enemy, index);
                        var next = EnemyModelArt.HullVertex(enemy, (index + 1) % count);
                        if ((first.Y > point.Y) != (next.Y > point.Y) && point.X <
                            (next.X - first.X) * (point.Y - first.Y) / (next.Y - first.Y) + first.X)
                            inside = !inside;
                    }
                    Check.True(inside, $"{family}/{rank}: the heated plate stroke stays on steel, outside rear cutouts and service gaps");
                }
            }
        }
    }
}
