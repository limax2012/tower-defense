using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class EnemyStunPresentationTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        foreach (var family in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.SetSandboxPathDistance(430, session.Map.Path);
            Check.Equal(Vector2.Zero, EnemyStunMotion.BodyOffset(enemy, .23f, false),
                $"{family}: a healthy unstunned shell has no vibration");
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            var checksum = SessionChecksum.Compute(session, 0);
            var position = enemy.Position;
            var distance = enemy.DistanceAlongPath;
            var previous = EnemyStunMotion.BodyOffset(enemy, 0, false);
            var moved = false;
            for (var frame = 0; frame < 1000; frame++)
            {
                var time = frame / 1000f;
                var offset = EnemyStunMotion.BodyOffset(enemy, time, false);
                Check.True(offset.Length() <= EnemyStunMotion.MaximumDisplacement,
                    $"{family}: local vibration cannot move the shell more than its bounded displacement");
                Check.Equal(offset, EnemyStunMotion.BodyOffset(enemy, time, false),
                    $"{family}: a repeated presentation sample has identical vibration");
                Check.True(Vector2.Distance(previous, offset) < .2f,
                    $"{family}: shell vibration remains continuous between millisecond samples");
                Check.Equal(Vector2.Zero, EnemyStunMotion.BodyOffset(enemy, time, true),
                    $"{family}: Reduced Effects leaves the chassis still");
                Check.Nearly(offset.Length(), EnemyStunMotion.WorldOffset(enemy, 1.17f, time, false).Length(),
                    $"{family}: changing heading preserves the displacement bound");
                moved |= offset != previous;
                previous = offset;
            }
            Check.True(moved && enemy.CurrentSpeed == 0, $"{family}: the shell shudders while actual travel is stopped");
            Check.Equal(position, enemy.Position, $"{family}: vibration cannot change authoritative position");
            Check.Nearly(distance, enemy.DistanceAlongPath, $"{family}: vibration cannot advance the gait or tracks");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0), $"{family}: sampling leaves synchronized state untouched");
            var fullOffset = EnemyStunMotion.BodyOffset(enemy, .23f, false).Length();
            enemy.StatusEffects.Update(.54f);
            Check.True(EnemyStunMotion.BodyOffset(enemy, .23f, false).Length() < fullOffset * .15f,
                $"{family}: the shell settles near the end of the actual status");
            enemy.StatusEffects.Update(.02f);
            Check.Equal(Vector2.Zero, EnemyStunMotion.BodyOffset(enemy, .23f, false),
                $"{family}: an expired stun leaves no vibration");
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            enemy.ApplyHealthDamage(enemy.MaxHealth);
            Check.Equal(Vector2.Zero, EnemyStunMotion.BodyOffset(enemy, .23f, false),
                $"{family}: death ends the short-circuit state immediately");

            session.SpawnEnemy(family, 1, 1);
            var escaped = session.Enemies.Last();
            escaped.SetSandboxPathDistance(session.Map.Path.TotalLength, session.Map.Path);
            escaped.UpdateMovement(0, session.Map.Path);
            escaped.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            Check.Equal(Vector2.Zero, EnemyStunMotion.BodyOffset(escaped, .23f, false),
                $"{family}: escaped enemies cannot retain the short-circuit state");
        }
        GroundedShells(content);
    }

    private static void GroundedShells(GameContent content)
    {
        var minimumClearance = float.MaxValue;
        foreach (var map in content.Maps.Values)
        foreach (var family in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(enemy);
            var motionRadius = EnemyLocomotion.MotionRadius(enemy);
            var path = EnemyLocomotionPath.For(session.Map);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            var checksum = SessionChecksum.Compute(session, 0);
            var stableFeet = true;
            var rootsFollowShell = true;
            var reducedUnchanged = true;
            var clearance = float.MaxValue;
            for (var distance = radius; distance < session.Map.Path.TotalLength - radius; distance += 12)
            {
                var tracked = enemy.Definition.Id == "t5_regenerator";
                var center = path.VisualPosition(distance, motionRadius, tracked);
                var forward = path.Forward(distance, motionRadius, tracked);
                var heading = MathF.Atan2(forward.Y, forward.X);
                var lateral = new Vector2(-forward.Y, forward.X);
                for (var sample = 0; sample < 8; sample++)
                {
                    var time = sample / 60f;
                    var offset = EnemyStunMotion.WorldOffset(enemy, heading, time, false);
                    foreach (var (side, leg) in EnemyLocomotion.ContactSlots(enemy))
                    {
                        var contact = EnemyModelArt.LegPose(enemy, center, heading, distance, side, leg, path);
                        var displaced = EnemyModelArt.DisplaceLegPose(contact, offset);
                        stableFeet &= displaced.Foot == contact.Foot && displaced.ContactRadius == contact.ContactRadius;
                        rootsFollowShell &= Vector2.Distance(displaced.Root, contact.Root + offset) < .0001f;
                        reducedUnchanged &= EnemyModelArt.DisplaceLegPose(contact,
                            EnemyStunMotion.WorldOffset(enemy, heading, time, true)) == contact;
                    }
                    for (var vertex = 0; vertex < EnemyModelArt.HullVertexCount(enemy); vertex++)
                    {
                        var local = EnemyModelArt.HullVertex(enemy, vertex) * radius;
                        var point = center + forward * local.X + lateral * local.Y + offset;
                        clearance = MathF.Min(clearance, PavedClearance(session, point));
                    }
                }
            }
            var label = $"{map.Id} {family} {rank}";
            Check.True(stableFeet && rootsFollowShell && reducedUnchanged,
                $"{label}: rendered leg deformation moves roots with the shell while preserving exact foot contacts and Reduced Effects geometry");
            Check.True(clearance >= .75f,
                $"{label}: the shuddering hull retains a clean paved-lane margin ({clearance:0.000}px)");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0), $"{label}: stunned articulation is read-only");
            minimumClearance = MathF.Min(minimumClearance, clearance);
        }
        Console.WriteLine($"Stun geometry: sampled minimum hull-to-lane margin {minimumClearance:0.000}px; ground contacts unchanged.");
    }

    private static float PavedClearance(GameSession session, Vector2 point)
    {
        var half = session.Map.Definition.PathWidth * .5f;
        var clearance = float.NegativeInfinity;
        var points = session.Map.Definition.Path;
        foreach (var corner in points)
            clearance = MathF.Max(clearance, half - MathF.Max(MathF.Abs(point.X - corner.X), MathF.Abs(point.Y - corner.Y)));
        for (var index = 1; index < points.Count; index++)
        {
            var start = points[index - 1].ToVector2();
            var delta = points[index].ToVector2() - start;
            var direction = Vector2.Normalize(delta);
            var relative = point - start;
            var along = Vector2.Dot(relative, direction);
            if (along >= 0 && along <= delta.Length())
                clearance = MathF.Max(clearance, half - MathF.Abs(Vector2.Dot(relative, new Vector2(-direction.Y, direction.X))));
        }
        return clearance;
    }
}
