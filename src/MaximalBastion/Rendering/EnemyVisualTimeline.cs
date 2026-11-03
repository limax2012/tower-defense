using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

internal readonly record struct EnemyVisualFeedback(
    float OverdriveOpening, float OverdriveTransition, float ShieldImpact, float ShieldBreak,
    float FrostSpread = 1, float BurnIgnition = 0, float StunDischarge = 0,
    float MaterialTime = 0, float OrnamentScale = 1,
    IReadOnlyList<EnemyMaterialParticle>? Particles = null);

internal enum EnemyMaterialParticleKind { Ember, Vapor, IceFlake }

internal readonly record struct EnemyMaterialParticle(
    EnemyMaterialParticleKind Kind, Vector2 Origin, Vector2 Velocity, float BornAt, float Lifetime)
{
    internal float Progress(float time) => Math.Clamp((time - BornAt) / Lifetime, 0, 1);
    internal Vector2 Position(float time)
    {
        var age = Math.Clamp(time - BornAt, 0, Lifetime);
        var gravity = Kind == EnemyMaterialParticleKind.IceFlake ? new Vector2(0, 20) : Vector2.Zero;
        return Origin + Velocity * age + gravity * (age * age * .5f);
    }
}

/// <summary>Bounded local transitions inferred from observed enemy state and simulation presentation time.</summary>
internal sealed class EnemyVisualTimeline
{
    private readonly Dictionary<int, ObservedEnemy> _enemies = new();
    private readonly HashSet<int> _liveIds = new();
    private readonly Dictionary<(int X, int Y), int> _density = new();
    private GameSession? _session;
    private float _lastTime;
    private float _lastSimulationTime;

    internal int Count => _enemies.Count;
    internal int ParticleCount => _enemies.Values.Sum(state => state.Particles.Count);

    internal void Observe(GameSession session, float time, bool detailedMaterials = true)
    {
        if (!ReferenceEquals(_session, session) || session.Statistics.SimulatedSeconds < _lastSimulationTime)
        {
            _enemies.Clear();
            _session = session;
            _lastTime = time;
        }
        // Pausing removes the fractional presentation lead without rewinding the simulation.
        time = MathF.Max(time, _lastTime);
        _lastTime = time;
        _lastSimulationTime = session.Statistics.SimulatedSeconds;
        _liveIds.Clear();
        _density.Clear();
        if (detailedMaterials)
        foreach (var enemy in session.Enemies)
            if (!enemy.IsDead && !enemy.HasEscaped)
            {
                var cell = Cell(enemy.Position);
                _density[cell] = _density.GetValueOrDefault(cell) + 1;
            }
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            _liveIds.Add(enemy.Id);
            if (!_enemies.TryGetValue(enemy.Id, out var previous))
            {
                _enemies.Add(enemy.Id, new ObservedEnemy(enemy, time)
                {
                    Fog = EnemyFogEnvelope.Restore(session, enemy, time)
                });
                continue;
            }
            previous.Fog.Observe(session, enemy, time);
            if (enemy.Shield < previous.Shield)
            {
                previous.ShieldHitAt = time;
                if (enemy.Shield <= 0) previous.ShieldBrokenAt = time;
            }
            if (enemy.BossPhaseActive && !previous.Overdrive) previous.OverdriveAt = time;
            previous.Shield = enemy.Shield;
            previous.Overdrive = enemy.BossPhaseActive;
            if (detailedMaterials) ObserveMaterials(session, enemy, previous, time);
            else previous.Particles.Clear();
            _enemies[enemy.Id] = previous;
        }
        if (_enemies.Count != _liveIds.Count)
            foreach (var id in _enemies.Keys.ToArray())
                if (!_liveIds.Contains(id)) _enemies.Remove(id);
    }

    internal EnemyFogPresentation FogFeedback(EnemyInstance enemy, float time) =>
        _enemies.TryGetValue(enemy.Id, out var state) ? state.Fog.At(MathF.Max(time, _lastTime)) : default;

    internal EnemyVisualFeedback Feedback(EnemyInstance enemy, float time)
    {
        if (!_enemies.TryGetValue(enemy.Id, out var state)) return new(1, 0, 0, 0);
        time = MathF.Max(time, _lastTime);
        var opening = float.IsNegativeInfinity(state.OverdriveAt)
            ? 1 : MathHelper.SmoothStep(0, 1, Math.Clamp((time - state.OverdriveAt) / .35f, 0, 1));
        return new(opening, Fade(state.OverdriveAt, .65f), Fade(state.ShieldHitAt, .22f),
            Fade(state.ShieldBrokenAt, .42f),
            float.IsNegativeInfinity(state.FrostAt) ? 1 :
                .32f + .68f * MathHelper.SmoothStep(0, 1, Math.Clamp((time - state.FrostAt) / .45f, 0, 1)),
            Fade(state.BurnAt, .24f), Fade(state.StunAt, .16f), time, state.OrnamentScale, state.Particles);

        float Fade(float start, float duration) => Math.Clamp(1 - (time - start) / duration, 0, 1);
    }

    private void ObserveMaterials(GameSession session, EnemyInstance enemy, ObservedEnemy state, float time)
    {
        var burning = enemy.StatusEffects.IsBurning;
        var frosted = enemy.StatusEffects.SlowFactor > 0;
        var stunned = enemy.StatusEffects.IsStunned;
        var cell = Cell(enemy.Position);
        var nearby = 0;
        for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                nearby += _density.GetValueOrDefault((cell.X + x, cell.Y + y));
        state.OrnamentScale = nearby > 7 ? .55f : nearby > 3 ? .75f : 1;
        for (var index = state.Particles.Count - 1; index >= 0; index--)
            if (time >= state.Particles[index].BornAt + state.Particles[index].Lifetime)
                state.Particles.RemoveAt(index);
        if (burning && !state.Burning) { state.BurnAt = time; state.NextEmberAt = time + .11f; }
        if (frosted && !state.Frosted) { state.FrostAt = time; state.NextVaporAt = time + .18f; }
        if (stunned && !state.Stunned) state.StunAt = time;
        if (!frosted && state.Frosted)
            for (var index = 0; index < 4; index++)
            {
                var anchors = EnemySurfaceAnchors.For(enemy);
                var local = Vector2.Lerp(anchors.IceRear, anchors.IceFront, .15f + index * .23f);
                if ((index & 1) != 0) local.Y = -local.Y;
                var origin = WorldAt(local, time);
                Add(new(EnemyMaterialParticleKind.IceFlake, origin,
                    new Vector2((index - 1.5f) * 4, -3 - index % 2), time, .30f));
            }
        if (burning && time >= state.NextEmberAt)
        {
            var born = MathF.Max(state.NextEmberAt, time - .10f);
            var anchors = EnemySurfaceAnchors.For(enemy);
            var serial = state.EmberSerial++;
            var origin = WorldAt(anchors.Vent + new Vector2(serial % 2 == 0 ? -.08f : .10f, 0), born);
            Add(new(EnemyMaterialParticleKind.Ember, origin,
                new Vector2(MathF.Sin(enemy.Id * 1.31f + serial * 2.4f) * 5, -10),
                born, .46f * state.OrnamentScale));
            state.NextEmberAt = born + (.36f + enemy.Id % 4 * .037f) / state.OrnamentScale;
        }
        if (frosted && time >= state.NextVaporAt)
        {
            var born = MathF.Max(state.NextVaporAt, time - .10f);
            var anchors = EnemySurfaceAnchors.For(enemy);
            var origin = WorldAt(Vector2.Lerp(anchors.IceRear, anchors.IceFront, .26f), born);
            Add(new(EnemyMaterialParticleKind.Vapor, origin, new Vector2(-2, -6),
                born, .58f * state.OrnamentScale));
            state.NextVaporAt = born + (.65f + enemy.Id % 3 * .071f) / state.OrnamentScale;
        }
        state.Burning = burning;
        state.Frosted = frosted;
        state.Stunned = stunned;
        state.Distance = enemy.DistanceAlongPath;
        state.ObservedAt = time;

        void Add(EnemyMaterialParticle particle)
        {
            if (state.Particles.Count == 8) state.Particles.RemoveAt(0);
            state.Particles.Add(particle);
        }
        Vector2 WorldAt(Vector2 local, float sampledTime)
        {
            var lead = Math.Clamp(time - session.Statistics.SimulatedSeconds, 0, .05f);
            var currentDistance = enemy.DistanceAlongPath + enemy.CurrentSpeed * lead;
            var progress = time > state.ObservedAt
                ? Math.Clamp((sampledTime - state.ObservedAt) / (time - state.ObservedAt), 0, 1) : 1;
            var distance = MathHelper.Lerp(state.Distance, currentDistance, progress);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var look = radius * .35f;
            var forward = session.Map.Path.GetPosition(distance + look) - session.Map.Path.GetPosition(distance - look);
            forward = forward.LengthSquared() > .001f ? Vector2.Normalize(forward) : Vector2.UnitX;
            var side = new Vector2(-forward.Y, forward.X);
            return session.Map.Path.GetPosition(distance) + (forward * local.X + side * local.Y) * radius;
        }
    }

    private static (int X, int Y) Cell(Vector2 position) =>
        ((int)MathF.Floor(position.X / 64), (int)MathF.Floor(position.Y / 64));

    private sealed class ObservedEnemy(EnemyInstance enemy, float time)
    {
        public float Shield = enemy.Shield;
        public bool Overdrive = enemy.BossPhaseActive;
        public bool Burning = enemy.StatusEffects.IsBurning;
        public bool Frosted = enemy.StatusEffects.SlowFactor > 0;
        public bool Stunned = enemy.StatusEffects.IsStunned;
        public float Distance = enemy.DistanceAlongPath;
        public float ObservedAt = time;
        public float FrostAt = float.NegativeInfinity;
        public float BurnAt = float.NegativeInfinity;
        public float StunAt = float.NegativeInfinity;
        public float NextEmberAt = time + .11f + enemy.Id % 4 * .037f;
        public float NextVaporAt = time + .18f + enemy.Id % 3 * .071f;
        public int EmberSerial;
        public float OrnamentScale = 1;
        public readonly List<EnemyMaterialParticle> Particles = new(8);
        public float OverdriveAt = float.NegativeInfinity;
        public float ShieldHitAt = float.NegativeInfinity;
        public float ShieldBrokenAt = float.NegativeInfinity;
        public EnemyFogEnvelope Fog;
    }
}

/// <summary>A reversible whole-cloud envelope; active color changes retain its density and geometry.</summary>
internal struct EnemyFogEnvelope
{
    internal const float RiseSeconds = .10f;
    internal const float FallSeconds = .14f;
    private float _from;
    private float _target;
    private float _changedAt;
    private int _displayMask;
    private Color _exposeColor;

    internal static EnemyFogEnvelope Restore(GameSession session, EnemyInstance enemy, float time)
    {
        var mask = EnemyDebuffAuraArt.Mask(enemy);
        return new()
        {
            _from = mask == 0 ? 0 : 1, _target = mask == 0 ? 0 : 1, _changedAt = time,
            _displayMask = mask, _exposeColor = EnemyConditionPresentation.ExposeAccent(session, enemy)
        };
    }

    internal EnemyFogPresentation At(float time)
    {
        var opacity = Density(time);
        return new(new Vector4((_displayMask & 1) != 0 ? opacity : 0,
            (_displayMask & 2) != 0 ? opacity : 0, (_displayMask & 4) != 0 ? opacity : 0,
            (_displayMask & 8) != 0 ? opacity : 0), _exposeColor);
    }

    internal void Observe(GameSession session, EnemyInstance enemy, float time)
    {
        var mask = EnemyDebuffAuraArt.Mask(enemy);
        var target = mask == 0 ? 0 : 1;
        if (target != _target)
        {
            _from = Density(time);
            _target = target;
            _changedAt = time;
        }
        if (mask == 0) return;
        _displayMask = mask;
        if ((mask & 8) != 0) _exposeColor = EnemyConditionPresentation.ExposeAccent(session, enemy);
    }

    private float Density(float time) => MathHelper.SmoothStep(_from, _target,
        Math.Clamp((time - _changedAt) / (_target > 0 ? RiseSeconds : FallSeconds), 0, 1));
}
