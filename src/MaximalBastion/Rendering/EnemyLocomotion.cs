using System.Runtime.CompilerServices;
using MaximalBastion.Enemies;
using MaximalBastion.Maps;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Presentation-only corner geometry and distance-driven ground contacts.</summary>
public sealed class EnemyLocomotionPath
{
    private static readonly ConditionalWeakTable<MapRuntime, EnemyLocomotionPath> Cache = new();
    private readonly Corner[] _corners;
    private readonly Dictionary<(float Radius, float StepBoost), PhaseProfile> _profiles = new();
    private float _lastDistance = float.NaN;
    private float _lastRadius;
    private bool _lastTracked;
    private Vector2 _lastPosition, _lastForward;
    public MapRuntime Map { get; }

    public static EnemyLocomotionPath For(MapRuntime map) => Cache.GetValue(map, static value => new(value));

    public EnemyLocomotionPath(MapRuntime map)
    {
        Map = map;
        var corners = new List<Corner>();
        var distance = 0f;
        var points = map.Definition.Path;
        for (var index = 1; index < points.Count - 1; index++)
        {
            var incoming = points[index].ToVector2() - points[index - 1].ToVector2();
            var outgoing = points[index + 1].ToVector2() - points[index].ToVector2();
            distance += incoming.Length();
            var first = Vector2.Normalize(incoming);
            var last = Vector2.Normalize(outgoing);
            var angle = MathF.Atan2(first.X * last.Y - first.Y * last.X, Vector2.Dot(first, last));
            if (MathF.Abs(angle) < .0001f) continue;
            corners.Add(new(distance, points[index].ToVector2(), first, last,
                MathF.Min(incoming.Length(), outgoing.Length()) * .48f, new TurnShape(angle), new TurnShape(angle, true)));
        }
        _corners = corners.ToArray();
    }

    public Vector2 Position(float distance)
    {
        var path = Map.Path;
        if (distance < 0) return path.GetPosition(0) + path.GetDirection(0) * distance;
        if (distance > path.TotalLength)
            return path.GetPosition(path.TotalLength) + path.GetDirection(path.TotalLength) * (distance - path.TotalLength);
        return path.GetPosition(distance);
    }

    public Vector2 VisualPosition(float distance, float radius, bool tracked = false)
    {
        SampleTurn(distance, radius, tracked, out var position, out _);
        return position;
    }

    public Vector2 Forward(float distance, float radius, bool tracked = false)
    {
        SampleTurn(distance, radius, tracked, out _, out var forward);
        return forward;
    }

    private void SampleTurn(float distance, float radius, bool tracked, out Vector2 position, out Vector2 forward)
    {
        if (distance == _lastDistance && radius == _lastRadius && tracked == _lastTracked)
        {
            position = _lastPosition;
            forward = _lastForward;
            return;
        }
        ComputeTurn(distance, radius, tracked, out position, out forward);
        _lastDistance = distance;
        _lastRadius = radius;
        _lastTracked = tracked;
        _lastPosition = position;
        _lastForward = forward;
    }

    private void ComputeTurn(float distance, float radius, bool tracked, out Vector2 position, out Vector2 forward)
    {
        foreach (var corner in _corners)
        {
            var window = MathF.Min(radius * 3.8f, corner.MaximumWindow);
            var delta = distance - corner.Distance;
            if (MathF.Abs(delta) >= window) continue;
            var shape = tracked ? corner.TrackedShape : corner.Shape;
            var reach = MathF.Min(radius * 1.8f, window);
            var length = shape.Length * reach;
            var deficit = reach * 2 - length;
            var t = (delta + window) / (window * 2);
            // Spread the shorter visual arc's timing over its approach and exit, then rejoin exactly.
            var arc = delta + window - deficit * EasedIntegral(t, .24f);
            var approach = window - reach;
            if (arc <= approach)
            {
                position = corner.Position + corner.Incoming * (arc - window);
                forward = corner.Incoming;
            }
            else if (arc >= approach + length)
            {
                position = corner.Position + corner.Outgoing * (arc - approach - length + reach);
                forward = corner.Outgoing;
            }
            else
            {
                var phase = (arc - approach) / length;
                shape.Sample(phase, out var local, out var tangent);
                var side = new Vector2(-corner.Incoming.Y, corner.Incoming.X);
                position = corner.Position + (corner.Incoming * (local.X - 1) + side * local.Y) * reach;
                forward = corner.Incoming * tangent.X + side * tangent.Y;
            }
            return;
        }
        position = Position(distance);
        forward = Map.Path.GetDirection(Math.Clamp(distance, 0, Map.Path.TotalLength));
    }

    private static float TravelRate(Corner corner, float distance, float radius)
    {
        var window = MathF.Min(radius * 3.8f, corner.MaximumWindow);
        var t = (distance - corner.Distance + window) / (window * 2);
        if (t <= 0 || t >= 1) return 1;
        var reach = MathF.Min(radius * 1.8f, window);
        var ramp = Math.Clamp(MathF.Min(t, 1 - t) / .24f, 0, 1);
        return 1 - reach * (2 - corner.Shape.Length) / (window * 2) * ramp * ramp * (3 - 2 * ramp) / .76f;
    }

    internal static float EasedIntegral(float phase, float shoulder)
    {
        if (phase < shoulder)
        {
            var t = phase / shoulder;
            return shoulder * t * t * t * (1 - .5f * t) / (1 - shoulder);
        }
        if (phase > 1 - shoulder)
        {
            var t = (1 - phase) / shoulder;
            return 1 - shoulder * t * t * t * (1 - .5f * t) / (1 - shoulder);
        }
        return (phase - shoulder * .5f) / (1 - shoulder);
    }

    private sealed class TurnShape
    {
        private const int Steps = 128;
        private const float Shoulder = .45f;
        private readonly float _angle;
        private readonly Vector2[] _positions = new Vector2[Steps + 1];
        private readonly Vector2[] _directions = new Vector2[Steps + 1];
        private readonly Vector2[] _tangents = new Vector2[Steps + 1];
        private readonly float[] _arcPositions = new float[Steps + 1];
        public float Length { get; }

        public TurnShape(float angle, bool tracked = false)
        {
            _angle = angle;
            _directions[0] = Vector2.UnitX;
            for (var index = 1; index <= Steps; index++)
            {
                _directions[index] = Heading(index / (float)Steps);
                _positions[index] = _positions[index - 1] +
                    (_directions[index - 1] + Heading((index - .5f) / Steps) * 4 + _directions[index]) / (Steps * 6);
            }
            var end = Vector2.UnitX + _directions[^1];
            var steeringLength = end.Length() / _positions[^1].Length();
            var outward = Vector2.UnitX - _directions[^1];
            var apexClearance = tracked ? .03f : .06f;
            var alongTurn = (Vector2.UnitX + _directions[^1]) * (tracked ? .02f : 0);
            for (var index = 0; index <= Steps; index++)
            {
                var angleAt = MathHelper.Pi * index / Steps;
                var sine = MathF.Sin(angleAt);
                var cosine = MathF.Cos(angleAt);
                var sineSquared = sine * sine;
                var sineFourth = sineSquared * sineSquared;
                var clearance = apexClearance * sineFourth * sineSquared;
                var clearanceRate = MathHelper.Pi * 6 * apexClearance * sineFourth * sine * cosine;
                // The chassis stays near the lane center while its facing eases through the planned turn.
                _positions[index] = _positions[index] * steeringLength + outward * clearance + alongTurn * (sineFourth * cosine);
                _tangents[index] = _directions[index] * steeringLength + outward * clearanceRate +
                    alongTurn * (MathHelper.Pi * (4 * sineSquared * sine * cosine * cosine - sineFourth * sine));
                if (index > 0)
                    _arcPositions[index] = _arcPositions[index - 1] + Vector2.Distance(_positions[index], _positions[index - 1]);
            }
            _positions[^1] = end;
            Length = _arcPositions[^1];
            for (var index = 1; index <= Steps; index++) _arcPositions[index] /= Length;
        }

        public void Sample(float phase, out Vector2 position, out Vector2 forward)
        {
            var index = Interval(phase, out var fraction, out var span);
            position = Vector2.Hermite(_positions[index], Vector2.Normalize(_tangents[index]) * (Length * span),
                _positions[index + 1], Vector2.Normalize(_tangents[index + 1]) * (Length * span), fraction);
            var parameter = MathHelper.Hermite(index / (float)Steps,
                Length / _tangents[index].Length() * span, (index + 1) / (float)Steps,
                Length / _tangents[index + 1].Length() * span, fraction);
            forward = Heading(parameter);
        }

        private Vector2 Heading(float phase)
        {
            // Eased curvature shoulders surround a steady central turn rate.
            var angle = _angle * EasedIntegral(phase, Shoulder);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        private int Interval(float phase, out float fraction, out float span)
        {
            phase = Math.Clamp(phase, 0, 1);
            var index = Array.BinarySearch(_arcPositions, phase);
            if (index < 0) index = ~index - 1;
            index = Math.Clamp(index, 0, Steps - 1);
            span = _arcPositions[index + 1] - _arcPositions[index];
            fraction = (phase - _arcPositions[index]) / span;
            return index;
        }
    }

    public Vector2 ConstrainContact(Vector2 point, float padRadius)
    {
        var projection = Map.Path.Project(point);
        var limit = Map.Definition.PathWidth * .5f - 3f - padRadius;
        if (projection.DistanceToPath <= limit) return point;
        var constrained = projection.Position + (point - projection.Position) * (limit / projection.DistanceToPath);
        foreach (var corner in _corners)
        {
            if (MathF.Abs(Vector2.Dot(corner.Incoming, corner.Outgoing)) > .001f) continue;
            var relative = point - corner.Position;
            var incoming = -Vector2.Dot(relative, corner.Incoming);
            var outgoing = Vector2.Dot(relative, corner.Outgoing);
            if (incoming <= 0 || outgoing <= 0 ||
                MathF.Abs(MathF.Min(incoming, outgoing) - projection.DistanceToPath) > .001f) continue;
            var difference = MathF.Abs(incoming - outgoing);
            if (difference >= 4) continue;
            // A radial contraction is shared by both segments at the inside-corner bisector.
            // Blending toward it removes the nearest-segment normal switch during a swing.
            var radial = corner.Position + relative * (limit / projection.DistanceToPath);
            return Vector2.Lerp(constrained, radial, 1 - MathHelper.SmoothStep(0, 1, difference / 4));
        }
        return constrained;
    }

    public float Phase(float distance, float stride, float radius, float stepBoost = 1.25f) =>
        Profile(radius, stepBoost).Coordinate(distance) / stride;
    public float Distance(float phase, float stride, float radius, float stepBoost = 1.25f) =>
        Profile(radius, stepBoost).Distance(phase * stride);

    private PhaseProfile Profile(float radius, float stepBoost)
    {
        var key = (radius, stepBoost);
        if (!_profiles.TryGetValue(key, out var profile))
            _profiles.Add(key, profile = new(_corners, Map.Path.TotalLength, radius, stepBoost));
        return profile;
    }

    private sealed class PhaseProfile
    {
        private readonly float[] _coordinates;
        public PhaseProfile(Corner[] corners, float length, float radius, float stepBoost)
        {
            _coordinates = new float[(int)MathF.Ceiling(length * 2) + 1];
            double coordinate = 0;
            var previousSlope = Slope(0);
            for (var index = 1; index < _coordinates.Length; index++)
            {
                var slope = Slope(index * .5f);
                coordinate += (previousSlope + slope) * .25;
                _coordinates[index] = (float)coordinate;
                previousSlope = slope;
            }
            float Slope(float distance)
            {
                var slope = 1f;
                var travel = 1f;
                foreach (var corner in corners)
                {
                    travel *= TravelRate(corner, distance, radius);
                    var u = MathF.Abs(distance - corner.Distance) / (radius * 1.35f);
                    if (u >= 1) continue;
                    var blend = 1 - u * u;
                    // Contact cadence follows visual travel with a modest, gradual turning adjustment.
                    slope += blend * blend * stepBoost;
                }
                return slope * travel;
            }
        }

        public float Coordinate(float distance)
        {
            if (distance <= 0) return distance;
            var sample = distance * 2;
            var index = (int)sample;
            return index >= _coordinates.Length - 1 ? _coordinates[^1] + distance - (_coordinates.Length - 1) * .5f :
                MathHelper.Lerp(_coordinates[index], _coordinates[index + 1], sample - index);
        }

        public float Distance(float coordinate)
        {
            if (coordinate <= 0) return coordinate;
            if (coordinate >= _coordinates[^1]) return (_coordinates.Length - 1) * .5f + coordinate - _coordinates[^1];
            var index = Array.BinarySearch(_coordinates, coordinate);
            if (index >= 0) return index * .5f;
            index = ~index - 1;
            return (index + (coordinate - _coordinates[index]) /
                (_coordinates[index + 1] - _coordinates[index])) * .5f;
        }
    }

    private readonly record struct Corner(float Distance, Vector2 Position, Vector2 Incoming,
        Vector2 Outgoing, float MaximumWindow, TurnShape Shape, TurnShape TrackedShape);
}

public readonly record struct EnemyGroundContact(EnemyLegPose Pose, Vector2 GroundPoint, bool Planted, float Lift,
    int StanceCycle);

/// <summary>Family gait groups with fixed world-space stance and continuous shallow swing.</summary>
public static class EnemyLocomotion
{
    public const float StanceFraction = .64f;
    private static readonly (int Side, int Leg)[] FourContacts = [(-1, -1), (-1, 1), (1, -1), (1, 1)];
    private static readonly (int Side, int Leg)[] SixContacts = [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)];
    private static readonly (int Side, int Leg)[] EightContacts = [(-1, -3), (-1, -1), (-1, 1), (-1, 3), (1, -3), (1, -1), (1, 1), (1, 3)];

    public static bool UsesGroundedGait(EnemyInstance enemy) => enemy.Definition.Id is "t1_crawler" or "t3_brute" or "t4_aegis";

    /// <summary>Gives fast scouts more steering room while retaining clearance for full-width tracks.</summary>
    public static float MotionRadius(EnemyInstance enemy) => EnemyModelArt.BodyRadius(enemy) *
        (UsesTracks(enemy) ? .60f : enemy.Definition.Id == "t2_runner" ? 1.45f : 1);

    public static bool UsesTracks(EnemyInstance enemy) => enemy.Definition.Id == "t5_regenerator";

    public static ReadOnlySpan<(int Side, int Leg)> ContactSlots(EnemyInstance enemy) => enemy.Definition.Id switch
    {
        "t1_crawler" => FourContacts,
        "t3_brute" => SixContacts,
        "t4_aegis" => EightContacts,
        _ => []
    };

    public static float StrideDistance(EnemyInstance enemy) => EnemyModelArt.BodyRadius(enemy) *
        (enemy.Definition.Id == "t1_crawler" ? .95f : enemy.Definition.Id == "t3_brute" ? .72f : .66f);

    public static float CornerStepBoost(EnemyInstance enemy) => enemy.Definition.Id == "t1_crawler" ? .5f :
        enemy.Definition.Id == "t3_brute" ? .85f : 1.25f;

    public static float MaximumReach(EnemyInstance enemy)
    {
        var radius = EnemyModelArt.BodyRadius(enemy);
        var rank = enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0;
        var along = radius * (enemy.Definition.Id == "t1_crawler" ? .60f : enemy.Definition.Id == "t3_brute" ? .61f : .90f) +
            StrideDistance(enemy) * StanceDuration(enemy) * .5f;
        var spread = enemy.Definition.Id == "t1_crawler" ? radius * .83f :
            enemy.Definition.Id == "t3_brute" ? radius * .86f : 22.8f + rank * .15f;
        return MathF.Sqrt(along * along + spread * spread) + 4.5f;
    }

    public static float StanceDuration(EnemyInstance enemy) => enemy.Definition.Id == "t1_crawler" ? .76f : StanceFraction;

    public static float TrackPhase(float travelDistance, float radius) => travelDistance / (radius * .14f);

    /// <summary>A bilateral neutral stance for still portraits, independent of travel and gait phase.</summary>
    public static EnemyGroundContact RestPose(EnemyInstance enemy, Vector2 center, float heading, int side, int legIndex)
    {
        if (side is not (-1 or 1) || !ContactSlots(enemy).Contains((side, legIndex)))
            throw new ArgumentOutOfRangeException(nameof(legIndex));
        var radius = EnemyModelArt.BodyRadius(enemy);
        var crawler = enemy.Definition.Id == "t1_crawler";
        var brute = enemy.Definition.Id == "t3_brute";
        var rank = enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0;
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var lateral = new Vector2(-forward.Y, forward.X);
        var along = crawler || brute ? legIndex * radius * (crawler ? .60f : .61f) :
            Math.Sign(legIndex) * radius * (Math.Abs(legIndex) == 3 ? .90f : .34f);
        var spread = crawler ? radius * .83f : brute ? radius * .86f : 22.8f + rank * .15f;
        var rootAlong = legIndex * radius * (crawler ? .46f : brute ? .48f : .19f);
        var rootSpread = crawler ? .38f : brute ? .48f : .47f;
        var root = center + forward * rootAlong + lateral * (side * radius * rootSpread);
        var foot = center + forward * along + lateral * (side * spread);
        var knee = Vector2.Lerp(root, foot, .53f) - forward * (Math.Sign(legIndex) * radius * .02f);
        var pad = (crawler ? 1.15f : brute ? 1.35f : 1.1f) + rank * .10f;
        var pose = new EnemyLegPose(root, knee, foot, pad);
        return new(pose, foot, true, 0, 0);
    }

    /// <summary>The visible upper belt travels toward the nose; cleats fade around its return rollers.</summary>
    public static (float Along, float Opacity) TrackCleat(int slot, float travelDistance, float radius,
        float halfLength = .42f, int count = 6)
    {
        if (count <= 0 || slot < 0 || slot >= count || radius <= 0 || halfLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var pitch = halfLength * 2 / count;
        var phase = travelDistance / (radius * pitch);
        var travel = phase - MathF.Floor(phase);
        var along = -halfLength + (slot + travel) * pitch;
        var fadeLength = MathF.Min(pitch * .7f, halfLength * .35f);
        var opacity = MathHelper.SmoothStep(0, 1,
            Math.Clamp((halfLength - MathF.Abs(along)) / fadeLength, 0, 1));
        return (along, opacity);
    }

    public static EnemyGroundContact Sample(EnemyInstance enemy, Vector2 center, float heading,
        float travelDistance, int side, int legIndex, EnemyLocomotionPath? path = null)
    {
        if (side is not (-1 or 1) || !ContactSlots(enemy).Contains((side, legIndex)))
            throw new ArgumentOutOfRangeException(nameof(legIndex));
        var radius = EnemyModelArt.BodyRadius(enemy);
        var crawler = enemy.Definition.Id == "t1_crawler";
        var brute = enemy.Definition.Id == "t3_brute";
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var lateral = new Vector2(-forward.Y, forward.X);
        var rank = enemy.IsBoss ? 2 : enemy.IsElite ? 1 : 0;
        var along = crawler || brute ? legIndex * radius * (crawler ? .60f : .61f) :
            Math.Sign(legIndex) * radius * (Math.Abs(legIndex) == 3 ? .90f : .34f);
        var rootAlong = legIndex * radius * (crawler ? .46f : brute ? .48f : .19f);
        var spread = crawler ? radius * .83f : brute ? radius * .86f : 22.8f + rank * .15f;
        var rootSpread = crawler ? .38f : brute ? .48f : .47f;
        var stride = StrideDistance(enemy);
        var stance = StanceDuration(enemy);
        var swingStart = crawler ? (side < 0 ? 0 : .5f) + (legIndex > 0 ? .25f : 0) :
            brute ? (side == (legIndex == 0 ? 1 : -1) ? 0 : .5f) :
            (((3 - legIndex) / 2 + (side > 0 ? 1 : 0)) % 2) * .5f;
        var offset = stance - swingStart;
        var stepBoost = CornerStepBoost(enemy);
        var phase = (path?.Phase(travelDistance, stride, radius, stepBoost) ?? travelDistance / stride) + offset;
        var cycle = MathF.Floor(phase);
        var progress = phase - cycle;
        var planted = progress < stance;
        var root = center + forward * rootAlong + lateral * (side * radius * rootSpread);
        var pad = (crawler ? 1.15f : brute ? 1.35f : 1.1f) + rank * .10f;

        Vector2 Contact(float contactPhase)
        {
            var distance = path?.Distance(contactPhase, stride, radius, stepBoost) ?? contactPhase * stride;
            var contactForward = path?.Forward(distance, radius) ?? forward;
            var contactSide = new Vector2(-contactForward.Y, contactForward.X);
            var contactCenter = path?.VisualPosition(distance, radius) ?? center + forward * (distance - travelDistance);
            var point = contactCenter + contactForward * along + contactSide * (side * spread);
            return path?.ConstrainContact(point, pad) ?? point;
        }

        var contactPhase = cycle - offset + stance * .5f;
        var ground = Contact(contactPhase);
        var lift = 0f;
        if (!planted)
        {
            var swing = (progress - stance) / (1 - stance);
            // A broad, steady swing avoids a fast mid-stride snap while easing both contact joins.
            var eased = EnemyLocomotionPath.EasedIntegral(swing, .25f);
            ground = Vector2.Lerp(ground, Contact(contactPhase + 1), eased);
            if (path is not null) ground = path.ConstrainContact(ground, pad);
            var arc = MathF.Sin(swing * MathHelper.Pi);
            lift = arc * arc;
        }
        var foot = Vector2.Lerp(ground, root, lift * .055f);
        if (!planted && path is not null) foot = path.ConstrainContact(foot, pad);
        var knee = Vector2.Lerp(root, foot, .53f) - forward * (Math.Sign(legIndex) * radius * .02f);
        var pose = new EnemyLegPose(root, knee, foot, pad);
        if (path is not null) pose = pose with { Knee = path.ConstrainContact(pose.Knee, pad) };
        return new(pose, ground, planted, lift, (int)cycle);
    }
}
