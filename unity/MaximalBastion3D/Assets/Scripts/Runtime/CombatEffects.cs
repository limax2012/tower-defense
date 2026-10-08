using System.Collections.Generic;
using System.Linq;
using MaximalBastion.Combat;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Towers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    public sealed class CombatEffects : MonoBehaviour
    {
        private sealed class Shot
        {
            public GameObject root;
            public Vector3 origin, offset, previous;
            public float distance, age;
            public Color color;
            public TrailRenderer trail;
        }
        private sealed class Transient
        {
            public GameObject root;
            public Vector3 velocity;
            public Vector3 initialScale;
            public float age, duration, expansion;
            public bool gravity;
            public Renderer renderer;
            public Color color;
            public MaterialPropertyBlock block = new MaterialPropertyBlock();
        }
        private sealed class Beam
        {
            public EffectInstance source;
            public GameObject root;
            public Transform[] cores, halos;
            public Vector2[] bends;
            public ModelRig emitter;
            public EnemyInstance originEnemy, target;
            public bool lightning;
            public float phase;
        }
        private BattlefieldView _view;
        private readonly Dictionary<ProjectileInstance, Shot> _shots = new Dictionary<ProjectileInstance, Shot>();
        private readonly Dictionary<EffectInstance, Beam> _beams = new Dictionary<EffectInstance, Beam>();
        private readonly HashSet<EffectInstance> _seen = new HashSet<EffectInstance>();
        private readonly List<Transient> _transients = new List<Transient>();
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly List<ProjectileInstance> _deadShots = new List<ProjectileInstance>();
        private readonly List<EffectInstance> _deadBeams = new List<EffectInstance>();
        private readonly Light[] _lights = new Light[8];
        private readonly float[] _lightTimes = new float[8];
        private Transform _root;
        private Mesh _sphere, _tube, _wedge, _debris, _ring;
        private CombatAudio _audio;
        private EnemyWreckage _wreckage;
        private uint _visualSeed = 197;
        private float Rand() { _visualSeed ^= _visualSeed << 13; _visualSeed ^= _visualSeed >> 17; _visualSeed ^= _visualSeed << 5; return (_visualSeed & 65535) / 65535f; }

        public void Initialize(BattlefieldView view)
        {
            _view = view;
            _root = new GameObject("Combat presentation").transform;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            _tube = TubeMesh(12);
            _wedge = WedgeMesh();
            _debris = WedgeMesh();
            _ring = RingMesh();
            for (int i = 0; i < _lights.Length; i++)
            {
                var go = new GameObject("Impact light " + i); go.transform.SetParent(_root);
                var light = go.AddComponent<Light>(); light.type = LightType.Point;
                light.shadows = LightShadows.None; light.enabled = false; light.range = 4;
                _lights[i] = light;
            }
            _audio = gameObject.AddComponent<CombatAudio>();
            _audio.Initialize();
            _wreckage = gameObject.AddComponent<EnemyWreckage>();
            _wreckage.Initialize(view.Host, view.Catalog);
        }

        public Material NewMaterial(Color color, bool additive)
        {
            if (additive && _materials.TryGetValue(color, out var cached)) return cached;
            var material = new Material(Shader.Find(additive ? "Bastion/Energy" : "Bastion/Ghost"));
            if (additive) material.SetVector("_BaseColor", color.linear);
            else material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            if (additive) _materials[color] = material;
            return material;
        }

        public void Clear()
        {
            foreach (var s in _shots.Values) if (s.root) Destroy(s.root);
            foreach (var b in _beams.Values) if (b.root) Destroy(b.root);
            foreach (var t in _transients) if (t.root) Destroy(t.root);
            _shots.Clear(); _beams.Clear(); _seen.Clear(); _transients.Clear();
            for (int i = 0; i < _lights.Length; i++)
            {
                _lightTimes[i] = 0;
                if (_lights[i]) _lights[i].enabled = false;
            }
            _wreckage.Clear();
        }

        public void CaptureStep()
        {
            var session = _view.Host.Session;
            foreach (var p in session.Projectiles.Projectiles)
            {
                if (_shots.ContainsKey(p)) continue;
                var source = session.Towers.Find(t => t.Id == p.Payload.SourceTowerId);
                var color = source != null ? _view.TowerColor(source) : Coordinates.Color(p.Color);
                Vector3 pos = Coordinates.World(p.Position, 1);
                Vector3 muzzle = pos;
                if (_view.TowerModels.TryGetValue(p.Payload.SourceTowerId, out var rig) && rig != null)
                {
                    Vector3 target = p.Target != null ? _view.EnemyPosition(p.Target) : Coordinates.World(p.AimPoint, 1);
                    muzzle = rig.ProjectileMuzzle(target).position;
                }
                var obj = MeshObject("Projectile", source?.Definition.Id == "breaker_cannon" ? _wedge : _sphere, Radiant(color, 2.5f));
                obj.transform.position = muzzle;
                float radius = Mathf.Clamp(p.Radius * .035f, .05f, .14f);
                obj.transform.localScale = new Vector3(radius, radius, radius * (source?.Definition.Id == "watchtower" ? 6 : 2.5f));
                var halo = MeshObject("Plasma glow", _sphere, new Color(color.r, color.g, color.b, .16f));
                halo.transform.SetParent(obj.transform, false); halo.transform.localScale = Vector3.one * 2.8f;
                var trail = obj.AddComponent<TrailRenderer>(); trail.sharedMaterial = NewMaterial(new Color(color.r, color.g, color.b, .7f), true);
                trail.startWidth = radius * .8f; trail.endWidth = 0; trail.time = .095f;
                trail.minVertexDistance = .06f; trail.numCapVertices = 3; trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.emitting = false;
                var shot = new Shot { root = obj, origin = pos, offset = muzzle - pos, previous = muzzle,
                    distance = Mathf.Max(.1f, Vector3.Distance(pos, Coordinates.World(p.AimPoint, 1))), color = color, trail = trail };
                _shots.Add(p, shot);
            }
            foreach (var e in session.Effects.Effects)
            {
                if (!_seen.Add(e)) continue;
                Color color = Coordinates.Color(e.Color);
                if (e.Kind == EffectKind.Beam)
                {
                    var emitter = session.Towers.Find(t => e.SourceTowerId != 0 ? t.Id == e.SourceTowerId :
                        MaximalBastion.Gameplay.Vector2.DistanceSquared(t.Position, e.Start) < 1);
                    bool lightning = e.BeamStyle != BeamStyle.Prism && (emitter?.Definition.Id == "arc_relay" ||
                        (color.g > .7f && color.b > .65f && color.r < .4f));
                    int segments = lightning ? 7 : 1;
                    var root = new GameObject(e.BeamStyle == BeamStyle.Prism ? "Prism discharge" : lightning ? "Arc discharge" : "Support link");
                    root.transform.SetParent(_root);
                    var beam = new Beam { source = e, root = root, cores = new Transform[segments], halos = new Transform[segments],
                        bends = new Vector2[segments + 1], lightning = lightning, target = NearestEnemy(e.End),
                        originEnemy = emitter == null ? NearestEnemy(e.Start) : null, phase = Rand() * 6.28f };
                    if (emitter != null) _view.TowerModels.TryGetValue(emitter.Id, out beam.emitter);
                    for (int i = 0; i < segments; i++)
                    {
                        var core = MeshObject("Energy core", _tube, Radiant(e.BeamStyle == BeamStyle.Prism ? new Color(.78f, .59f, 1f) : color, 2.5f));
                        var halo = MeshObject("Energy envelope", _tube, new Color(color.r, color.g, color.b, .18f));
                        core.transform.SetParent(root.transform); halo.transform.SetParent(root.transform);
                        beam.cores[i] = core.transform; beam.halos[i] = halo.transform;
                        if (i > 0) beam.bends[i] = new Vector2(Rand() - .5f, Rand() - .5f);
                    }
                    _beams[e] = beam;
                }
                else if (e.Kind == EffectKind.Splash) Pulse(Coordinates.World(e.Start, .5f), color, e.Radius * .05f);
            }
            if (_seen.Count > 512) _seen.RemoveWhere(e => !session.Effects.Effects.Contains(e));
        }

        public void Fire(TowerInstance tower, ModelRig rig)
        {
            if (tower.IsSupport) return;
            rig.BeginVolley();
            Color color = _view.TowerColor(tower);
            foreach (var port in rig.FirePorts)
                Particle(port.position, Vector3.zero, Radiant(Color.white, 2), .11f, .07f, .24f);
            LightAt(rig.Muzzle.position, color, 2, .08f);
            _audio.Play(tower.Definition.Id, rig.Muzzle.position);
        }
        public void Hit(Vector3 position, Color color)
        {
            Particle(position, Vector3.zero, Radiant(color, 2), .09f, .10f, .28f);
            for (int i = 0; i < 3; i++) Particle(position, new Vector3(Rand() - .5f, Rand(), Rand() - .5f) * 3, new Color(1, .7f, .3f), .035f, .22f, 0, true);
        }
        public void Defeat(Vector3 position, Color color, float radius, Transform model)
        {
            _wreckage.BreakApart(model);
            Pulse(position, color, radius * 1.2f);
            for (int i = 0; i < 4; i++) Particle(position, new Vector3(Rand() - .5f, .35f + Rand(), Rand() - .5f) * 3, color, .025f + Rand() * .045f, .35f, 0, true);
            _audio.Play("impact", position);
        }
        public void Upgrade(Vector3 position, Color color)
        {
            Pulse(position, color, 1.4f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                Particle(position + new Vector3(Mathf.Cos(a), -.5f, Mathf.Sin(a)), Vector3.up * 2, color, .05f, .7f, 0);
            }
            _audio.Play("upgrade", position);
        }
        public void Pulse(Vector3 position, Color color, float radius)
        {
            var obj = MeshObject("Impact shockwave", _ring, Radiant(new Color(color.r, color.g, color.b, .4f), 1.5f));
            obj.transform.position = position; obj.transform.localScale = Vector3.one * .12f;
            AddTransient(obj, Vector3.zero, color, .32f, radius * 2, false);
            Particle(position, Vector3.zero, Radiant(color, 1.8f), .18f, .16f, Mathf.Min(.6f, radius * .3f));
            LightAt(position, color, Mathf.Min(5, radius * 3), .18f);
        }
        private void Particle(Vector3 p, Vector3 velocity, Color color, float size, float life, float expand, bool gravity = false)
        {
            if (_transients.Count >= 350) return;
            var obj = MeshObject(gravity ? "Fragment" : "Energy", gravity ? _debris : _sphere, color);
            obj.transform.position = p; obj.transform.localScale = Vector3.one * size;
            AddTransient(obj, velocity, color, life, expand, gravity);
        }
        private void AddTransient(GameObject obj, Vector3 velocity, Color color, float life, float expand, bool gravity)
        {
            _transients.Add(new Transient { root = obj, velocity = velocity, initialScale = obj.transform.localScale,
                duration = life, expansion = expand, gravity = gravity, renderer = obj.GetComponent<Renderer>(), color = color });
        }
        private GameObject MeshObject(string name, Mesh mesh, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(_root);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = NewMaterial(color, true);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return obj;
        }
        private void LightAt(Vector3 position, Color color, float intensity, float life)
        {
            int index = 0;
            for (int i = 1; i < _lights.Length; i++) if (_lightTimes[i] < _lightTimes[index]) index = i;
            _lights[index].transform.position = position; _lights[index].color = color; _lights[index].intensity = intensity;
            _lights[index].enabled = true; _lightTimes[index] = life;
        }

        private void LateUpdate()
        {
            var session = _view.Host.Session;
            float dt = session != null && _view.Host.IsRunning ? Time.deltaTime * session.Speed : 0;
            _deadShots.Clear();
            foreach (var entry in _shots)
            {
                var p = entry.Key; var shot = entry.Value;
                if (p.IsExpired || session == null || !session.Projectiles.Projectiles.Contains(p)) { _deadShots.Add(p); continue; }
                shot.age += dt;
                var end = p.Kind == ProjectileKind.ImpactPoint ? Coordinates.World(p.AimPoint, .14f) :
                    p.Target != null && !p.Target.IsDead ? _view.EnemyPosition(p.Target) : Coordinates.World(p.AimPoint, .7f);
                var planar = Coordinates.World(p.Position, 1);
                float remaining = Vector2.Distance(new Vector2(planar.x, planar.z), new Vector2(end.x, end.z));
                float t = Mathf.Clamp01(1 - remaining / shot.distance);
                var pos = planar + shot.offset * (1 - t);
                pos.y = Mathf.Lerp(shot.origin.y + shot.offset.y, end.y, t);
                if (p.Kind == ProjectileKind.ImpactPoint) pos.y += Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(shot.distance * .28f, 1, 5);
                var direction = pos - shot.previous;
                if (direction.sqrMagnitude > .00001f) shot.root.transform.rotation = Quaternion.LookRotation(direction);
                shot.root.transform.position = pos;
                shot.trail.emitting = true;
                shot.previous = pos;
            }
            foreach (var p in _deadShots) { Destroy(_shots[p].root); _shots.Remove(p); }
            _deadBeams.Clear();
            foreach (var entry in _beams)
            {
                var e = entry.Key; var beam = entry.Value;
                if (e.Remaining <= 0 || session == null || !session.Effects.Effects.Contains(e) ||
                    (beam.emitter != null && (!beam.emitter.Root || !beam.emitter.Muzzle)))
                { _deadBeams.Add(e); continue; }
                Vector3 start = beam.emitter != null ? beam.emitter.Muzzle.position : beam.originEnemy != null ?
                    _view.EnemyPosition(beam.originEnemy) : Coordinates.World(e.Start, 1.1f);
                Vector3 end = beam.target != null ? _view.EnemyPosition(beam.target) : Coordinates.World(e.End, .8f);
                float radius = e.BeamStyle == BeamStyle.Prism ? .063f : .032f;
                Vector3 direction = (end - start).normalized;
                Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) > .95f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(side, direction);
                Vector3 previous = start;
                beam.phase += dt * 58;
                float bendScale = Mathf.Min(.5f, Vector3.Distance(start, end) * .10f);
                for (int i = 0; i < beam.cores.Length; i++)
                {
                    Vector3 next = Vector3.Lerp(start, end, (i + 1f) / beam.cores.Length);
                    Vector2 bend = beam.bends[i + 1];
                    next += (side * bend.x + up * bend.y) * bendScale * (1 + Mathf.Sin(beam.phase + i * 2.7f) * .22f);
                    SetTube(beam.cores[i], previous, next, radius);
                    SetTube(beam.halos[i], previous, next, radius * 2.6f);
                    previous = next;
                }
            }
            foreach (var e in _deadBeams) { Destroy(_beams[e].root); _beams.Remove(e); }
            for (int i = _transients.Count - 1; i >= 0; i--)
            {
                var t = _transients[i]; t.age += dt;
                if (t.age >= t.duration) { Destroy(t.root); _transients.RemoveAt(i); continue; }
                float life = t.age / t.duration;
                if (t.gravity) t.velocity += Vector3.down * (9 * dt);
                t.root.transform.position += t.velocity * dt;
                t.root.transform.localScale = t.initialScale + Vector3.one * t.expansion * life;
                if (t.gravity) t.root.transform.Rotate(new Vector3(170, 100, 270) * dt);
                t.block.SetFloat("_Fade", 1 - life); t.renderer.SetPropertyBlock(t.block);
            }
            for (int i = 0; i < _lights.Length; i++)
            {
                _lightTimes[i] -= dt;
                if (_lightTimes[i] <= 0) _lights[i].enabled = false;
            }
        }

        private EnemyInstance NearestEnemy(MaximalBastion.Gameplay.Vector2 point)
        {
            EnemyInstance found = null;
            float nearest = 24 * 24;
            foreach (var enemy in _view.Host.Session.Enemies)
            {
                float distance = MaximalBastion.Gameplay.Vector2.DistanceSquared(enemy.Position, point);
                if (distance < nearest) { nearest = distance; found = enemy; }
            }
            return found;
        }

        private static Color Radiant(Color color, float intensity)
        {
            // Keep the public color convention in sRGB while scaling energy in linear light.
            Color radiance = color.linear * intensity;
            radiance.a = color.a;
            return radiance.gamma;
        }

        private static Mesh RingMesh()
        {
            const int sides = 40, crossSections = 6;
            var vertices = new Vector3[sides * crossSections];
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
                for (int j = 0; j < crossSections; j++)
                {
                    float angle = i * Mathf.PI * 2 / sides, cross = j * Mathf.PI * 2 / crossSections;
                    float radius = .47f + Mathf.Cos(cross) * .025f;
                    vertices[i * crossSections + j] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(cross) * .018f, Mathf.Sin(angle) * radius);
                    int a = i * crossSections + j, b = ((i + 1) % sides) * crossSections + j;
                    int c = i * crossSections + (j + 1) % crossSections, d = ((i + 1) % sides) * crossSections + (j + 1) % crossSections;
                    triangles.AddRange(new[] { a, c, b, c, d, b });
                }
            var mesh = new Mesh { name = "Volumetric shock ring", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static void SetTube(Transform obj, Vector3 a, Vector3 b, float radius)
        {
            Vector3 delta = b - a;
            obj.position = a;
            if (delta.sqrMagnitude > .00001f) obj.rotation = Quaternion.LookRotation(delta);
            obj.localScale = new Vector3(radius, radius, Mathf.Max(.001f, delta.magnitude));
        }
        private static Mesh TubeMesh(int sides)
        {
            // Rounded caps prevent an abrupt rectangular aura at the barrel.
            float[] depths = { -.003f, 0, .006f, .994f, 1, 1.003f };
            float[] radii = { 0, .8f, 1, 1, .8f, 0 };
            var vertices = new Vector3[sides * depths.Length];
            var triangles = new List<int>();
            for (int r = 0; r < depths.Length; r++)
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    vertices[r * sides + s] = new Vector3(Mathf.Cos(a) * radii[r], Mathf.Sin(a) * radii[r], depths[r]);
                    if (r == depths.Length - 1) continue;
                    int n = (s + 1) % sides, i = r * sides + s, j = r * sides + n;
                    triangles.AddRange(new[] { i, j, i + sides, j, j + sides, i + sides });
                }
            var mesh = new Mesh { name = "Rounded energy volume", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh WedgeMesh()
        {
            var mesh = new Mesh { name = "Plasma wedge", vertices = new[] { new Vector3(0, 0, 1), new Vector3(-1, -.55f, -1),
                new Vector3(1, -.55f, -1), new Vector3(0, .8f, -.7f) }, triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 3, 1, 2, 3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
