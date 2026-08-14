using MinimalBastion.Data;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Maps;

public readonly record struct PathProjection(Vector2 Position, float DistanceAlongPath, float DistanceToPath);
public readonly record struct MapPowerBuff(float AttackSpeedBonus, float RangeBonus, float DamageBonus, float ArmorPierceBonus)
{
    public bool IsPowered => AttackSpeedBonus > 0 || RangeBonus > 0 || DamageBonus > 0 || ArmorPierceBonus > 0;
}

public sealed class PathRuntime
{
    private readonly Vector2[] _points;
    private readonly float[] _cumulative;

    public float TotalLength { get; }

    public PathRuntime(IReadOnlyList<PointData> points)
    {
        _points = points.Select(x => x.ToVector2()).ToArray();
        _cumulative = new float[_points.Length];
        for (var i = 1; i < _points.Length; i++)
        {
            _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(_points[i - 1], _points[i]);
        }
        TotalLength = _cumulative[^1];
        if (TotalLength <= 0) throw new InvalidDataException("Path must have positive length.");
    }

    public Vector2 GetPosition(float distance)
    {
        distance = MathHelper.Clamp(distance, 0, TotalLength);
        var segment = FindSegment(distance);
        var startDistance = _cumulative[segment];
        var segmentLength = _cumulative[segment + 1] - startDistance;
        var t = segmentLength <= 0 ? 0 : (distance - startDistance) / segmentLength;
        return Vector2.Lerp(_points[segment], _points[segment + 1], t);
    }

    public Vector2 GetDirection(float distance)
    {
        var segment = FindSegment(MathHelper.Clamp(distance, 0, TotalLength - 0.001f));
        return Vector2.Normalize(_points[segment + 1] - _points[segment]);
    }

    public float GetProgress(float distance) => MathHelper.Clamp(distance / TotalLength, 0, 1);

    public float DistanceToPath(Vector2 point)
    {
        return Project(point).DistanceToPath;
    }

    public PathProjection Project(Vector2 point)
    {
        var bestDistance = float.MaxValue;
        var bestPosition = _points[0];
        var bestAlongPath = 0f;
        for (var i = 0; i < _points.Length - 1; i++)
        {
            var start = _points[i];
            var end = _points[i + 1];
            var delta = end - start;
            var lengthSquared = delta.LengthSquared();
            var t = lengthSquared <= 0.0001f ? 0 : MathHelper.Clamp(Vector2.Dot(point - start, delta) / lengthSquared, 0, 1);
            var candidate = start + delta * t;
            var distance = Vector2.Distance(point, candidate);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestPosition = candidate;
            bestAlongPath = _cumulative[i] + Vector2.Distance(start, candidate);
        }
        return new PathProjection(bestPosition, bestAlongPath, bestDistance);
    }

    private int FindSegment(float distance)
    {
        for (var i = 0; i < _cumulative.Length - 1; i++)
            if (distance <= _cumulative[i + 1]) return i;
        return _cumulative.Length - 2;
    }

}

public sealed class MapRuntime
{
    public MapDefinition Definition { get; }
    public PathRuntime Path { get; }
    public IReadOnlyList<Rectangle> BuildableRegions { get; }
    public IReadOnlyList<Rectangle> RestrictedRegions { get; }
    public Color BackgroundColor => Definition.Background.BaseColor;
    public Color AccentColor => Definition.Background.AccentColor;

    public MapRuntime(MapDefinition definition)
    {
        Definition = definition;
        Path = new PathRuntime(definition.Path);
        BuildableRegions = definition.BuildableRegions.Select(x => x.ToRectangle()).ToArray();
        RestrictedRegions = definition.RestrictedRegions.Select(x => x.ToRectangle()).ToArray();
    }

    public bool IsBuildable(Vector2 position)
    {
        return BuildableRegions.Any(region => region.Contains(position.ToPoint())) &&
               !RestrictedRegions.Any(region => region.Contains(position.ToPoint()));
    }

    public IReadOnlyList<PowerNodeData> GetPowerNodes(Vector2 position) => Definition.PowerNodes
        .Where(node => Vector2.DistanceSquared(position, node.Position.ToVector2()) <= node.Radius * node.Radius)
        .ToArray();

    public MapPowerBuff GetPowerBuff(Vector2 position)
    {
        var attackSpeed = 0f;
        var range = 0f;
        var damage = 0f;
        var armorPierce = 0f;
        foreach (var node in Definition.PowerNodes)
        {
            if (Vector2.DistanceSquared(position, node.Position.ToVector2()) > node.Radius * node.Radius) continue;
            attackSpeed = MathF.Max(attackSpeed, node.AttackSpeedBonus);
            range = MathF.Max(range, node.RangeBonus);
            damage = MathF.Max(damage, node.DamageBonus);
            armorPierce = MathF.Max(armorPierce, node.ArmorPierceBonus);
        }
        // Overlapping fields never add together. Each stat uses only its strongest
        // local node, keeping placement decisions useful without creating a stack exploit.
        return new MapPowerBuff(attackSpeed, range, damage, armorPierce);
    }
}
