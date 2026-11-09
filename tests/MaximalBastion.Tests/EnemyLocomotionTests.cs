using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class EnemyLocomotionTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        GroundContacts(content);
        StopsAndResumes(content);
        TurnAndSwingContinuity(content);
        SizeHierarchyAndTrackEnvelope(content);
        TrackUpperRunDirection(content);
        TrackedCornerContinuity(content);
        CornerTravelQuality(content);
        RestingStance(content);
        BruteLegMotion(content);
        AllCornerContactContinuity(content);
    }

    private static void BruteLegMotion(GameContent content)
    {
        const int sampleCount = 96;
        const float heading = .73f;
        var center = new Vector2(133, 271);
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy("t3_brute", 1, 1, rank);
            var enemy = session.Enemies.Single();
            var stride = EnemyLocomotion.StrideDistance(enemy);
            foreach (var slot in EnemyLocomotion.ContactSlots(enemy))
            {
                var first = EnemyLocomotion.Sample(enemy, center, heading, 0, slot.Side, slot.Leg).Pose;
                var kneeTravel = 0f;
                var footTravel = 0f;
                var validDimensions = true;
                for (var sample = -1; sample < sampleCount; sample++)
                {
                    var pose = sample < 0
                        ? EnemyLocomotion.RestPose(enemy, center, heading, slot.Side, slot.Leg).Pose
                        : EnemyLocomotion.Sample(enemy, center, heading,
                            stride * sample / sampleCount, slot.Side, slot.Leg).Pose;
                    var upperLength = Vector2.Distance(pose.Root, pose.Knee);
                    var lowerLength = Vector2.Distance(pose.Knee, pose.Foot);
                    validDimensions &= float.IsFinite(upperLength) && upperLength > .01f &&
                        float.IsFinite(lowerLength) && lowerLength > .01f &&
                        float.IsFinite(pose.ContactRadius) && pose.ContactRadius > 0;
                    if (sample >= 0)
                    {
                        kneeTravel = MathF.Max(kneeTravel, Vector2.Distance(first.Knee, pose.Knee));
                        footTravel = MathF.Max(footTravel, Vector2.Distance(first.Foot, pose.Foot));
                    }
                }
                Check.True(validDimensions && kneeTravel > .25f && footTravel > .25f,
                    $"Brute {rank} leg {slot.Side}/{slot.Leg}: both leg segments remain finite and substantial, " +
                    $"with moving knees and feet through the stride ({kneeTravel:0.00}px knee, {footTravel:0.00}px foot)");
                var next = EnemyLocomotion.Sample(enemy, center, heading, stride, slot.Side, slot.Leg).Pose;
                Check.True(Vector2.Distance(first.Root, next.Root) < .01f &&
                    Vector2.Distance(first.Knee, next.Knee) < .01f && Vector2.Distance(first.Foot, next.Foot) < .01f,
                    $"Brute {rank} leg {slot.Side}/{slot.Leg}: body-local anatomy repeats continuously after one stride");
            }
        }
    }

    private static void AllCornerContactContinuity(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(enemy);
            var path = EnemyLocomotionPath.For(session.Map);
            var cornerDistance = 0f;
            var worstJump = 0f;
            var maximumCadence = 0f;
            for (var index = 1; index < map.Path.Count - 1; index++)
            {
                cornerDistance += Vector2.Distance(map.Path[index - 1].ToVector2(), map.Path[index].ToVector2());
                for (var offset = -radius * 2; offset <= radius * 2; offset += .1f)
                {
                    var distance = cornerDistance + offset;
                    var stride = EnemyLocomotion.StrideDistance(enemy);
                    var stepBoost = EnemyLocomotion.CornerStepBoost(enemy);
                    maximumCadence = MathF.Max(maximumCadence,
                        (path.Phase(distance + .05f, stride, radius, stepBoost) - path.Phase(distance - .05f, stride, radius, stepBoost)) * stride / .1f);
                    foreach (var slot in EnemyLocomotion.ContactSlots(enemy))
                    {
                        EnemyGroundContact At(float d)
                        {
                            var forward = path.Forward(d, radius);
                            return EnemyLocomotion.Sample(enemy, path.VisualPosition(d, radius),
                                MathF.Atan2(forward.Y, forward.X), d, slot.Side, slot.Leg, path);
                        }
                        var before = At(distance - .001f);
                        var after = At(distance + .001f);
                        worstJump = MathF.Max(worstJump, Vector2.Distance(before.Pose.Foot, after.Pose.Foot));
                        worstJump = MathF.Max(worstJump, Vector2.Distance(before.Pose.Knee, after.Pose.Knee));
                    }
                }
            }
            Check.True(worstJump < .075f,
                $"{map.Id} {family} {rank}: all corner feet and knees avoid nearest-segment snapping ({worstJump:0.0000}px across 0.002px travel)");
            Check.True(maximumCadence < 2.3f,
                $"{map.Id} {family} {rank}: corner cadence remains bounded without frantic footfalls ({maximumCadence:0.00}x)");
        }
    }

    private static void RestingStance(GameContent content)
    {
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            session.SpawnEnemy(family, 1, 1, rank);
            var first = session.Enemies[0];
            var second = session.Enemies[1];
            second.SetSandboxPathDistance(305, session.Map.Path);
            foreach (var slot in EnemyLocomotion.ContactSlots(first))
            {
                var left = EnemyLocomotion.RestPose(first, Vector2.Zero, 0, slot.Side, slot.Leg);
                var right = EnemyLocomotion.RestPose(first, Vector2.Zero, 0, -slot.Side, slot.Leg);
                var other = EnemyLocomotion.RestPose(second, Vector2.Zero, 0, slot.Side, slot.Leg);
                Check.True(left.Planted && left.Lift == 0 && left.Pose.Foot == left.GroundPoint,
                    $"{family} {rank}: resting contact is planted without a swing lift");
                Check.Equal(left, other, $"{family} {rank}: resting stance does not depend on identity or travel");
                foreach (var points in new[] { (left.Pose.Root, right.Pose.Root),
                             (left.Pose.Knee, right.Pose.Knee), (left.Pose.Foot, right.Pose.Foot) })
                    Check.True(Vector2.Distance(points.Item1, points.Item2 * new Vector2(1, -1)) < .0001f,
                        $"{family} {rank}: left and right resting anatomy is exactly mirrored");
            }
        }
    }

    private static void GroundContacts(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var path = new EnemyLocomotionPath(session.Map);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var slots = EnemyLocomotion.ContactSlots(enemy).ToArray();
            Check.Equal(family == "t1_crawler" ? 4 : family == "t3_brute" ? 6 : 8, slots.Length,
                $"{family} has its complete authored contact count");
            var previous = new EnemyGroundContact[slots.Length];
            var current = new EnemyGroundContact[slots.Length];
            var hasPrevious = false;
            var stancePairs = 0;
            var largestSlip = 0f;
            var minimumSide = float.MaxValue;
            var minimumOrder = float.MaxValue;
            var maximumReach = 0f;
            var maximumRoadOffset = 0f;
            var maximumRoadSample = string.Empty;
            var minimumContactGap = float.MaxValue;
            var minimumContactSample = string.Empty;
            var intersections = 0;
            var checksum = SessionChecksum.Compute(session, 0);
            for (var distance = 32f; distance < session.Map.Path.TotalLength - 32; distance += .5f)
            {
                var center = path.VisualPosition(distance, radius);
                var forward = path.Forward(distance, radius);
                var sideAxis = new Vector2(-forward.Y, forward.X);
                var heading = MathF.Atan2(forward.Y, forward.X);
                var index = 0;
                foreach (var (side, leg) in slots)
                {
                    var contact = EnemyLocomotion.Sample(enemy, center, heading, distance, side, leg, path);
                    if (hasPrevious && previous[index].Planted && contact.Planted &&
                        previous[index].StanceCycle == contact.StanceCycle)
                    {
                        largestSlip = MathF.Max(largestSlip,
                            Vector2.Distance(previous[index].GroundPoint, contact.GroundPoint));
                        stancePairs++;
                    }
                    foreach (var point in new[] { contact.Pose.Root, contact.Pose.Knee, contact.Pose.Foot })
                    {
                        var relative = point - center;
                        minimumSide = MathF.Min(minimumSide, Vector2.Dot(relative, sideAxis) * side);
                        if (leg != 0) minimumOrder = MathF.Min(minimumOrder, Vector2.Dot(relative, forward) * leg);
                        var roadOffset = session.Map.Path.DistanceToPath(point) + contact.Pose.ContactRadius;
                        if (roadOffset > maximumRoadOffset)
                        {
                            maximumRoadOffset = roadOffset;
                            maximumRoadSample = $"distance {distance:0.00}, slot {side}/{leg}, {(point == contact.Pose.Root ? "root" : point == contact.Pose.Knee ? "knee" : "foot")}";
                        }
                    }
                    maximumReach = MathF.Max(maximumReach,
                        Vector2.Distance(center, contact.Pose.Foot) + contact.Pose.ContactRadius);
                    current[index++] = contact;
                }
                for (var first = 0; first < current.Length; first++)
                for (var second = first + 1; second < current.Length; second++)
                {
                    var a = current[first].Pose;
                    var b = current[second].Pose;
                    var gap = Vector2.Distance(a.Foot, b.Foot) - a.ContactRadius - b.ContactRadius;
                    if (gap < minimumContactGap)
                    {
                        minimumContactGap = gap;
                        minimumContactSample = $"distance {distance:0.00}, slots {slots[first].Side}/{slots[first].Leg} and {slots[second].Side}/{slots[second].Leg}, " +
                            $"{(current[first].Planted ? "planted" : "swinging")}/{(current[second].Planted ? "planted" : "swinging")}";
                    }
                    if (Intersects(a.Root, a.Knee, b.Root, b.Knee) ||
                        Intersects(a.Root, a.Knee, b.Knee, b.Foot) ||
                        Intersects(a.Knee, a.Foot, b.Root, b.Knee) ||
                        Intersects(a.Knee, a.Foot, b.Knee, b.Foot)) intersections++;
                }
                Array.Copy(current, previous, current.Length);
                hasPrevious = true;
            }
            var label = $"{map.Id} {family} {rank}";
            Check.True(stancePairs > 100 && largestSlip < .01f,
                $"{label}: consecutive stance contacts stay planted in world space ({largestSlip:0.000}px drift)");
            Check.True(minimumSide > radius * .30f && minimumOrder > 0,
                $"{label}: roots, knees and feet retain separate side and front/rear corridors ({minimumSide:0.00}, {minimumOrder:0.00})");
            var roadLimit = map.PathWidth * .5f + (rank == "Standard" ? -2f : EnemyRankArtTests.OutlineExtension(map) - .25f);
            Check.True(maximumReach <= EnemyLocomotion.MaximumReach(enemy) && maximumRoadOffset <= roadLimit,
                $"{label}: contacts remain on the supporting deck ({maximumReach:0.00}px radial, {maximumRoadOffset:0.00}px path offset; {maximumRoadSample})");
            Check.True(intersections == 0 && minimumContactGap > .25f,
                $"{label}: legs never cross and contact pads remain separate ({intersections} crossings, {minimumContactGap:0.00}px clearance; {minimumContactSample})");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0),
                $"{label}: locomotion sampling is read-only");
        }
    }

    private static void TurnAndSwingContinuity(GameContent content)
    {
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            var path = EnemyLocomotionPath.For(session.Map);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var worstJump = 0f;
            var worstHeadingJump = 0f;
            for (var distance = 100f; distance < 210; distance += .05f)
            {
                var left = path.Forward(distance - .001f, radius);
                var right = path.Forward(distance + .001f, radius);
                worstHeadingJump = MathF.Max(worstHeadingJump, Vector2.Distance(left, right));
                foreach (var (side, leg) in EnemyLocomotion.ContactSlots(enemy))
                {
                    EnemyGroundContact At(float at)
                    {
                        var direction = path.Forward(at, radius);
                        return EnemyLocomotion.Sample(enemy, path.VisualPosition(at, radius),
                            MathF.Atan2(direction.Y, direction.X), at, side, leg, path);
                    }
                    worstJump = MathF.Max(worstJump, Vector2.Distance(At(distance - .001f).Pose.Foot,
                        At(distance + .001f).Pose.Foot));
                }
            }
            Check.True(worstJump < .075f && worstHeadingJump < .001f,
                $"{family}: feet and heading remain continuous through swing joins and corners ({worstJump:0.0000}px, {worstHeadingJump:0.00000})");
            var stance = EnemyLocomotion.StanceDuration(enemy);
            var stride = EnemyLocomotion.StrideDistance(enemy);
            for (var phase = 0; phase < 24; phase++)
            {
                var count = EnemyLocomotion.ContactSlots(enemy).ToArray().Count(slot =>
                    !EnemyLocomotion.Sample(enemy, Vector2.Zero, 0, (phase + .15f) / 24 * stride,
                        slot.Side, slot.Leg).Planted);
                Check.True(count <= (family == "t1_crawler" ? 1 : family == "t3_brute" ? 3 : 4),
                    $"{family}: each of the 24 gait phases retains its supporting group ({count} lifted)");
            }
        }
    }

    private static void SizeHierarchyAndTrackEnvelope(GameContent content)
    {
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            var previousArea = 0f;
            foreach (var family in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
            {
                session.SpawnEnemy(family, 1, 1, rank);
                var enemy = session.Enemies[^1];
                var radius = EnemyModelArt.BodyRadius(enemy);
                var area = 0f;
                for (var index = 0; index < EnemyModelArt.HullVertexCount(enemy); index++)
                {
                    var a = EnemyModelArt.HullVertex(enemy, index) * radius;
                    var b = EnemyModelArt.HullVertex(enemy, (index + 1) % EnemyModelArt.HullVertexCount(enemy)) * radius;
                    area += a.X * b.Y - b.X * a.Y;
                }
                area = MathF.Abs(area) * .5f;
                Check.True(area > previousArea * 1.2f,
                    $"{rank} {family}: armored footprint clearly exceeds the preceding family ({area:0}px2 / {previousArea:0}px2)");
                previousArea = area;
            }
        }
        foreach (var map in content.Maps.Values)
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy("t5_regenerator", 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(enemy);
            var path = EnemyLocomotionPath.For(session.Map);
            Check.Equal(0, EnemyLocomotion.ContactSlots(enemy).Length, "Tracked Regenerators have no walking contacts");
            var track = EnemyModelArt.TrackGeometry(enemy);
            var motionRadius = EnemyLocomotion.MotionRadius(enemy);
            var margin = rank == "Standard" ? 1.5f : .25f - EnemyRankArtTests.OutlineExtension(map);
            for (var distance = radius; distance < session.Map.Path.TotalLength - radius; distance += 1)
            {
                var center = path.VisualPosition(distance, motionRadius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy));
                var forward = path.Forward(distance, motionRadius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy));
                var lateral = new Vector2(-forward.Y, forward.X);
                for (var index = 0; index < EnemyModelArt.HullVertexCount(enemy); index++)
                {
                    var start = EnemyModelArt.HullVertex(enemy, index);
                    var end = EnemyModelArt.HullVertex(enemy, (index + 1) % EnemyModelArt.HullVertexCount(enemy));
                    for (var sample = 0; sample < 16; sample++)
                    {
                        var vertex = Vector2.Lerp(start, end, sample / 16f);
                        var point = center + (forward * vertex.X + lateral * vertex.Y) * radius;
                        Check.True(OnPavedLane(session, point, margin),
                            $"{map.Id} {rank}: tracked hull edge {index} stays inside the path outline at {distance:0}px");
                    }
                }
                for (var side = -1; side <= 1; side += 2)
                for (var end = -1; end <= 1; end += 2)
                for (var angle = 0; angle < 16; angle++)
                {
                    var a = angle * MathHelper.TwoPi / 16;
                    var point = center + (forward * (end * track.HalfLength + MathF.Cos(a) * track.HalfWidth) +
                        lateral * (side * track.LateralOffset + MathF.Sin(a) * track.HalfWidth)) * radius;
                    Check.True(OnPavedLane(session, point, margin),
                        $"{map.Id} {rank}: track endcaps retain a clean outline margin at {distance:0}px");
                }
            }
            var phase = EnemyLocomotion.TrackPhase(170, radius);
            enemy.SetSandboxPathDistance(170, session.Map.Path);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 1, Magnitude = 1 });
            enemy.UpdateMovement(.25f, session.Map.Path);
            Check.Nearly(phase, EnemyLocomotion.TrackPhase(enemy.DistanceAlongPath, radius),
                "A stopped tracked chassis also stops its treads");
            Check.Nearly(1, EnemyLocomotion.TrackPhase(170 + radius * .14f, radius) - phase,
                "Track tread movement follows traveled distance at one pitch per cycle");
        }
    }

    private static void TrackedCornerContinuity(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy("t5_regenerator", 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyLocomotion.MotionRadius(enemy);
            var path = EnemyLocomotionPath.For(session.Map);
            var initialDistance = enemy.DistanceAlongPath;
            var checksum = SessionChecksum.Compute(session, 0);
            var cornerDistance = 0f;
            for (var index = 1; index < map.Path.Count - 1; index++)
            {
                var incoming = Vector2.Distance(map.Path[index - 1].ToVector2(), map.Path[index].ToVector2());
                var outgoing = Vector2.Distance(map.Path[index].ToVector2(), map.Path[index + 1].ToVector2());
                cornerDistance += incoming;
                var window = MathF.Min(radius * 3.8f, MathF.Min(incoming, outgoing) * .48f);
                foreach (var join in new[] { cornerDistance - window, cornerDistance + window })
                {
                    const float step = .1f;
                    var at = path.VisualPosition(join, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy));
                    var before = (at - path.VisualPosition(join - step, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy))) / step;
                    var after = (path.VisualPosition(join + step, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy)) - at) / step;
                    Check.True(Vector2.Distance(before, after) < .025f &&
                        Vector2.Distance(before, path.Forward(join, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy))) < .025f,
                        $"{map.Id} {rank}: tracked corner joins preserve tangent and travel continuity");
                }
                for (var distance = cornerDistance - window; distance <= cornerDistance + window; distance += .5f)
                {
                    Check.True(Vector2.Distance(path.Forward(distance - .01f, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy)),
                        path.Forward(distance + .01f, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy))) < .003f,
                        $"{map.Id} {rank}: tracked heading changes continuously throughout the turn");
                    enemy.SetSandboxPathDistance(distance, session.Map.Path);
                    var authoritative = enemy.Position;
                    var sampleChecksum = SessionChecksum.Compute(session, 0);
                    var presented = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
                    Check.True(Vector2.Distance(presented, path.VisualPosition(distance, radius, tracked: true, heavyTracked: EnemyLocomotion.UsesHeavyTracks(enemy))) < .001f,
                        $"{map.Id} {rank}: presented tracked center uses the same rounding as its heading");
                    Check.Equal(authoritative, enemy.Position, "Tracked presentation preserves the authoritative position");
                    Check.Equal(sampleChecksum, SessionChecksum.Compute(session, 0),
                        "Tracked corner sampling preserves synchronized state");
                }
            }
            enemy.SetSandboxPathDistance(initialDistance, session.Map.Path);
            Check.Equal(checksum, SessionChecksum.Compute(session, 0),
                "Tracked corner sampling preserves the session checksum");
        }
    }

    private static void CornerTravelQuality(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        foreach (var family in new[] { "t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyLocomotion.MotionRadius(enemy);
            var tracked = family == "t5_regenerator";
            var path = EnemyLocomotionPath.For(session.Map);
            var initialDistance = enemy.DistanceAlongPath;
            var checksum = SessionChecksum.Compute(session, 0);
            var cornerDistance = 0f;
            for (var index = 1; index < map.Path.Count - 1; index++)
            {
                var incoming = map.Path[index].ToVector2() - map.Path[index - 1].ToVector2();
                var outgoing = map.Path[index + 1].ToVector2() - map.Path[index].ToVector2();
                var maximumWindow = MathF.Min(incoming.Length(), outgoing.Length()) * .45f;
                cornerDistance += incoming.Length();
                var window = MathF.Min(radius * 3.8f, MathF.Min(incoming.Length(), outgoing.Length()) * .48f);
                const float step = .1f;
                var minimumSpeed = float.MaxValue;
                var maximumSpeed = 0f;
                var maximumYawRate = 0f;
                var maximumVelocityChange = 0f;
                var maximumYawRateChange = 0f;
                var previousVelocity = Vector2.Zero;
                var previousYawRate = 0f;
                var samples = (int)MathF.Ceiling((window * 2 + 2) / step);
                for (var sample = 0; sample <= samples; sample++)
                {
                    var distance = cornerDistance - window - 1 + sample * step;
                    var velocity = (path.VisualPosition(distance + step, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy)) -
                        path.VisualPosition(distance - step, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy))) / (2 * step);
                    var before = path.Forward(distance - step, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy));
                    var after = path.Forward(distance + step, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy));
                    var yawRate = MathF.Atan2(before.X * after.Y - before.Y * after.X,
                        Vector2.Dot(before, after)) / (2 * step);
                    minimumSpeed = MathF.Min(minimumSpeed, velocity.Length());
                    maximumSpeed = MathF.Max(maximumSpeed, velocity.Length());
                    maximumYawRate = MathF.Max(maximumYawRate, MathF.Abs(yawRate));
                    if (sample > 0)
                    {
                        maximumVelocityChange = MathF.Max(maximumVelocityChange,
                            Vector2.Distance(velocity, previousVelocity));
                        maximumYawRateChange = MathF.Max(maximumYawRateChange,
                            MathF.Abs(yawRate - previousYawRate));
                    }
                    previousVelocity = velocity;
                    previousYawRate = yawRate;
                    if (family == "t2_runner" && sample % 10 == 0)
                    {
                        var center = path.VisualPosition(distance, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy));
                        var forward = path.Forward(distance, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy));
                        var lateral = new Vector2(-forward.Y, forward.X);
                        for (var vertexIndex = 0; vertexIndex < EnemyModelArt.HullVertexCount(enemy); vertexIndex++)
                        {
                            var vertex = EnemyModelArt.HullVertex(enemy, vertexIndex);
                            var point = center + (forward * vertex.X + lateral * vertex.Y) * EnemyModelArt.BodyRadius(enemy);
                            Check.True(OnPavedLane(session, point, 1.5f),
                                $"{map.Id} {rank}: Runner flight retains its lane margin at {distance:0.0}px");
                        }
                    }
                }
                var label = $"{map.Id} {family} {rank}, corner {index}";
                Check.True(minimumSpeed > .76f && maximumSpeed < 1.01f,
                    $"{label}: corner travel avoids a pronounced slowdown or catch-up surge ({minimumSpeed:0.000}-{maximumSpeed:0.000}x)");
                var turn = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(incoming),
                    Vector2.Normalize(outgoing)), -1, 1));
                var yawLimit = family == "t2_runner" ? MathF.Tan(turn * .5f) / (radius * .35f) :
                    1.5f * MathF.Tan(turn * .5f) / MathF.Min(radius * 1.3f, maximumWindow);
                Check.True(maximumYawRate <= yawLimit * 1.01f + .00001f,
                    $"{label}: heading spreads its rotation through the bend ({maximumYawRate:0.0000} radians per pixel)");
                Check.True(maximumVelocityChange < .012f && maximumYawRateChange < .004f,
                    $"{label}: successive travel samples preserve smooth velocity and yaw ({maximumVelocityChange:0.0000}, {maximumYawRateChange:0.00000})");
                foreach (var distance in new[] { cornerDistance - window - .1f, cornerDistance + window + .1f })
                    Check.True(Vector2.Distance(path.VisualPosition(distance, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy)), path.Position(distance)) < .001f,
                        $"{label}: visual offsets end on the straight path without accumulating drift");
                if (family == "t2_runner")
                    foreach (var fraction in new[] { -1f, -.5f, 0, .5f, 1f })
                    {
                        var distance = cornerDistance + window * fraction;
                        enemy.SetSandboxPathDistance(distance, session.Map.Path);
                        var authoritative = enemy.Position;
                        var sampleChecksum = SessionChecksum.Compute(session, 0);
                        foreach (var lead in new[] { 0, .008f })
                        {
                            var presentation = PresentationFrame.Create(session, lead);
                            var expected = path.VisualPosition(distance + enemy.CurrentSpeed * presentation.LeadSeconds, radius, tracked, EnemyLocomotion.UsesHeavyTracks(enemy));
                            Check.True(Vector2.Distance(presentation.EnemyPosition(enemy), expected) < .001f,
                                $"{label}: Runner interpolation follows its smooth visual flight path");
                        }
                        Check.Equal(authoritative, enemy.Position, "Runner presentation preserves its authoritative position");
                        Check.Equal(sampleChecksum, SessionChecksum.Compute(session, 0),
                            "Runner interpolation preserves synchronized state");
                    }
            }
            enemy.SetSandboxPathDistance(initialDistance, session.Map.Path);
            Check.Equal(checksum, SessionChecksum.Compute(session, 0),
                $"{map.Id} {family} {rank}: smooth corner sampling preserves synchronized state");
        }
    }

    private static void TrackUpperRunDirection(GameContent content)
    {
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        foreach (var (halfLength, count) in new[] { (.42f, 6), (.63f, 9), (.56f, 8) })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy("t5_regenerator", 1, 1, rank);
            var enemy = session.Enemies.Single();
            var radius = EnemyModelArt.BodyRadius(enemy);
            var pitch = halfLength * 2 / count;
            var cycleDistance = radius * pitch;
            var first = EnemyLocomotion.TrackCleat(count / 2, cycleDistance * .2f, radius, halfLength, count);
            var later = EnemyLocomotion.TrackCleat(count / 2, cycleDistance * .7f, radius, halfLength, count);
            Check.True(later.Along > first.Along,
                $"{rank}: visible top-run cleats advance toward the nose as the chassis moves forward");
            Check.Nearly(cycleDistance * .5f, (later.Along - first.Along) * radius,
                $"{rank}: upper-belt travel relative to the chassis matches actual travel distance");

            const float epsilon = .0001f;
            for (var slot = 0; slot < count - 1; slot++)
            {
                var beforeWrap = EnemyLocomotion.TrackCleat(slot, cycleDistance * (1 - epsilon), radius, halfLength, count);
                var afterWrap = EnemyLocomotion.TrackCleat(slot + 1, cycleDistance * (1 + epsilon), radius, halfLength, count);
                Check.True(MathF.Abs(afterWrap.Along - beforeWrap.Along) < .001f &&
                    MathF.Abs(afterWrap.Opacity - beforeWrap.Opacity) < .001f,
                    $"{rank}: visible cleats remain continuous when their cycle indices wrap");
            }
            var outgoing = EnemyLocomotion.TrackCleat(count - 1, cycleDistance * (1 - epsilon), radius, halfLength, count);
            var incoming = EnemyLocomotion.TrackCleat(0, cycleDistance * (1 + epsilon), radius, halfLength, count);
            Check.True(outgoing.Opacity < .001f && incoming.Opacity < .001f,
                $"{rank}: front and rear return rollers conceal cleat wrapping without a visible pop");

            enemy.SetSandboxPathDistance(170, session.Map.Path);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .5f, Magnitude = 1 });
            var checksum = SessionChecksum.Compute(session, 0);
            var stopped = Enumerable.Range(0, count).Select(slot => EnemyLocomotion.TrackCleat(slot,
                enemy.DistanceAlongPath, radius, halfLength, count)).ToArray();
            Check.Equal(checksum, SessionChecksum.Compute(session, 0), "Track sampling does not alter synchronized state");
            enemy.UpdateMovement(.2f, session.Map.Path);
            var held = Enumerable.Range(0, count).Select(slot => EnemyLocomotion.TrackCleat(slot,
                enemy.DistanceAlongPath, radius, halfLength, count)).ToArray();
            Check.True(stopped.SequenceEqual(held), $"{rank}: Stun freezes cleat positions and endcap fades exactly");
            enemy.StatusEffects.Update(.6f);
            enemy.UpdateMovement(.05f, session.Map.Path);
            var resumed = Enumerable.Range(0, count).Select(slot => EnemyLocomotion.TrackCleat(slot,
                enemy.DistanceAlongPath, radius, halfLength, count)).ToArray();
            Check.True(!resumed.SequenceEqual(held), $"{rank}: resumed movement advances the same distance-driven belt");
        }
    }

    private static bool OnPavedLane(GameSession session, Vector2 point, float margin)
    {
        var half = session.Map.Definition.PathWidth * .5f - margin;
        var points = session.Map.Definition.Path;
        foreach (var corner in points)
            if (MathF.Abs(point.X - corner.X) <= half && MathF.Abs(point.Y - corner.Y) <= half) return true;
        for (var index = 1; index < points.Count; index++)
        {
            var start = points[index - 1].ToVector2();
            var delta = points[index].ToVector2() - start;
            var direction = Vector2.Normalize(delta);
            var relative = point - start;
            var along = Vector2.Dot(relative, direction);
            if (along >= 0 && along <= delta.Length() &&
                MathF.Abs(Vector2.Dot(relative, new Vector2(-direction.Y, direction.X))) <= half) return true;
        }
        return false;
    }

    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 first, Vector2 second) => first.X * second.Y - first.Y * second.X;
        var first = Cross(b - a, c - a);
        var second = Cross(b - a, d - a);
        var third = Cross(d - c, a - c);
        var fourth = Cross(d - c, b - c);
        return first * second < -.00001f && third * fourth < -.00001f;
    }

    private static void StopsAndResumes(GameContent content)
    {
        foreach (var family in new[] { "t1_crawler", "t3_brute", "t4_aegis" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.SetSandboxPathDistance(170, session.Map.Path);
            var path = new EnemyLocomotionPath(session.Map);
            EnemyGroundContact[] At()
            {
                var forward = path.Forward(enemy.DistanceAlongPath, EnemyModelArt.BodyRadius(enemy));
                return EnemyLocomotion.ContactSlots(enemy).ToArray().Select(slot =>
                    EnemyLocomotion.Sample(enemy, path.VisualPosition(enemy.DistanceAlongPath, EnemyModelArt.BodyRadius(enemy)), MathF.Atan2(forward.Y, forward.X),
                        enemy.DistanceAlongPath, slot.Side, slot.Leg, path)).ToArray();
            }
            var distance = enemy.DistanceAlongPath;
            var fullSpeed = enemy.CurrentSpeed;
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = .5f });
            enemy.UpdateMovement(.1f, session.Map.Path);
            Check.Nearly(fullSpeed * .05f, enemy.DistanceAlongPath - distance,
                $"{family}: Slow advances contact phase at the actual reduced travel speed");
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .5f, Magnitude = 1 });
            var stopped = At();
            enemy.UpdateMovement(.25f, session.Map.Path);
            Check.True(stopped.SequenceEqual(At()),
                $"{family}: Stun stops every stance and swing without a presentation clock drifting the contacts");
            enemy.StatusEffects.Update(.6f);
            enemy.UpdateMovement(.1f, session.Map.Path);
            Check.True(!stopped.SequenceEqual(At()),
                $"{family}: resumed movement continues the distance-driven contact sequence");
        }
    }
}
