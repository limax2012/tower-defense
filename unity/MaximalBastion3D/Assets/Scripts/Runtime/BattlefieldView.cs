using System;
using System.Collections.Generic;
using System.Linq;
using MaximalBastion.Core;
using MaximalBastion.Enemies;
using MaximalBastion.Towers;
using UnityEngine;
using UnityEngine.Rendering;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public sealed class EntityPick : MonoBehaviour
    {
        public int id;
        public bool generator;
        public bool enemy;
    }

    public sealed class BattlefieldView : MonoBehaviour
    {
        public SimulationHost Host { get; private set; }
        public VisualCatalog Catalog { get; private set; }
        public CombatEffects Effects { get; private set; }
        public Camera ViewCamera;
        public Func<bool> PointerOverInterface;
        public readonly Dictionary<int, ModelRig> TowerModels = new Dictionary<int, ModelRig>();
        public readonly Dictionary<int, ModelRig> EnemyModels = new Dictionary<int, ModelRig>();
        private readonly Dictionary<int, SimVector> _previousEnemies = new Dictionary<int, SimVector>();
        private readonly Dictionary<int, ModelRig> _plates = new Dictionary<int, ModelRig>();
        private readonly List<int> _remove = new List<int>();
        private Transform _map, _units;
        private ModelRig _forge;
        private GameObject _ghost;
        private string _ghostId;
        private LineRenderer _range;
        private Material _validGhost, _invalidGhost;
        private SimVector _cursor;
        private Vector3 _leftStart;
        private float _time;
        private int _hoverEnemy;
        public int HoverEnemy => _hoverEnemy;

        public void Initialize(SimulationHost host, VisualCatalog catalog, Camera camera)
        {
            Host = host; Catalog = catalog; ViewCamera = camera;
            Effects = gameObject.AddComponent<CombatEffects>();
            Effects.Initialize(this);
            host.SessionChanged += Rebuild;
            host.BeforeStep += CapturePrevious;
            host.AfterStep += AfterStep;
            _range = NewLine("Selection range", new Color(.3f, .85f, .84f), .035f);
            _range.loop = true;
            _range.positionCount = 96;
            _validGhost = Effects.NewMaterial(new Color(.15f, .8f, .65f, .35f), false);
            _invalidGhost = Effects.NewMaterial(new Color(1, .15f, .2f, .35f), false);
            Rebuild();
        }

        private void Rebuild()
        {
            if (_units) Destroy(_units.gameObject);
            if (_map) Destroy(_map.gameObject);
            TowerModels.Clear(); EnemyModels.Clear(); _plates.Clear(); _previousEnemies.Clear();
            _forge = null;
            if (_ghost) Destroy(_ghost);
            _ghostId = null;
            Effects.Clear();
            _map = new GameObject("Cinderworks / presentation").transform;
            _units = new GameObject("Simulation views").transform;
            var map = Host.Session?.Map.Definition ?? Host.Content.Maps["foundry_loop"];
            Slab("Foundry foundation", new Vector3(0, -.55f, -2), new Vector3(66, 1, 60), Catalog.ground, true);
            FoundrySurfaces.Build(_map, map, Catalog);
            if (Catalog.dressing)
            {
                var dressing = Instantiate(Catalog.dressing, _map);
                foreach (var filter in dressing.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.transform.name.StartsWith("Rotor") || filter.transform.name.StartsWith("Fan")) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    filter.gameObject.layer = 8;
                }
            }
            foreach (var collider in _map.GetComponentsInChildren<Collider>()) collider.gameObject.layer = 8;
            if (Host.Session != null)
            {
                var session = Host.Session;
                session.EnemyKilled += e => { Effects.Defeat(EnemyPosition(e), EnemyColor(e), e.Radius * .05f); };
                session.EnemyEscaped += e => Effects.Pulse(Coordinates.World(e.Position, .3f), new Color(1, .12f, .15f), 2.2f);
                session.TowerPlaced += t => Effects.Pulse(Coordinates.World(t.Position, .3f), TowerColor(t), 1.2f);
                session.TowerUpgraded += (t, _) => Effects.Upgrade(Coordinates.World(t.Position, 1), TowerColor(t));
                session.TowerOverdriven += t => Effects.Upgrade(Coordinates.World(t.Position, 1), TowerColor(t));
                session.EmergencyDefenseTriggered += (p, _) => Effects.Pulse(Coordinates.World(p.Position, .2f), new Color(.2f, .9f, .85f), p.Definition.BlastRadius * .05f);
                AfterStep();
            }
        }

        private GameObject Slab(string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(_map); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Destroy(go.GetComponent<Collider>()); else go.layer = 8;
            return go;
        }

        private void CapturePrevious()
        {
            foreach (var e in Host.Session.Enemies) _previousEnemies[e.Id] = e.Position;
        }

        private void AfterStep()
        {
            var session = Host.Session;
            if (session == null) return;
            foreach (var t in session.Towers)
            {
                if (!TowerModels.TryGetValue(t.Id, out var rig))
                {
                    rig = CreateModel(t.Definition.Id, Coordinates.World(t.Position), t.Id, false);
                    TowerModels.Add(t.Id, rig);
                }
                if (rig != null)
                {
                    rig.SetTier(t.LevelIndex, t.IsApex);
                    if (t.RecoilAnimationRemaining > rig.LastRecoil + .001f)
                    {
                        var target = session.TargetSelector.Select(t.Position, session.GetEffectiveRange(t), t.TargetMode, session.Enemies);
                        if (target != null) rig.Aim(EnemyPosition(target), .25f);
                        rig.RecoilAmount = 1;
                        Effects.Fire(t, rig);
                    }
                    rig.LastRecoil = t.RecoilAnimationRemaining;
                }
            }
            Prune(TowerModels, id => session.Towers.Any(t => t.Id == id));
            foreach (var e in session.Enemies)
            {
                if (!EnemyModels.TryGetValue(e.Id, out var rig))
                {
                    rig = CreateModel(e.Definition.Id, Coordinates.World(e.Position, .11f), e.Id, true);
                    EnemyModels.Add(e.Id, rig);
                    if (rig != null)
                    {
                        rig.Root.localScale *= e.IsBoss ? 1.4f : e.IsElite ? 1.15f : 1;
                        AttachEnemyModule(rig, "RoleSocket", e.SignalRole.ToString());
                        if (e.IsBoss || e.IsElite) AttachEnemyModule(rig, "RankSocket", e.IsBoss ? "boss" : "elite");
                        rig.LastHealth = e.Health;
                        rig.LastShield = e.Shield;
                    }
                }
                if (rig != null)
                {
                    if (e.Health < rig.LastHealth || e.Shield < rig.LastShield)
                    {
                        rig.HitRemaining = .08f;
                        Effects.Hit(EnemyPosition(e), EnemyColor(e));
                    }
                    rig.LastHealth = e.Health;
                    rig.LastShield = e.Shield;
                }
            }
            Prune(EnemyModels, id => session.Enemies.Any(e => e.Id == id));
            foreach (var p in session.EmergencyDefenses)
                if (!_plates.ContainsKey(p.Id)) _plates[p.Id] = CreateModel("pulse_plate", Coordinates.World(p.Position, .11f), 0, false);
            Prune(_plates, id => session.EmergencyDefenses.Any(p => p.Id == id));
            if (session.Generator != null && _forge == null)
            {
                _forge = CreateModel("charge_forge", Coordinates.World(session.Generator.Position), 0, false);
                if (_forge != null) _forge.Root.GetComponentInChildren<EntityPick>().generator = true;
            }
            if (session.Generator == null && _forge != null) { Destroy(_forge.Root.gameObject); _forge = null; }
            Effects.CaptureStep();
        }

        private void Prune(Dictionary<int, ModelRig> models, Func<int, bool> exists)
        {
            _remove.Clear();
            foreach (var item in models) if (!exists(item.Key)) _remove.Add(item.Key);
            foreach (int id in _remove) { if (models[id] != null) Destroy(models[id].Root.gameObject); models.Remove(id); _previousEnemies.Remove(id); }
        }

        private ModelRig CreateModel(string id, Vector3 position, int entity, bool enemy)
        {
            var entry = Catalog.Find(id);
            if (entry == null || !entry.prefab) { Debug.LogError("Missing finished model: " + id); return null; }
            var obj = Instantiate(entry.prefab, position, Quaternion.identity, _units);
            obj.name = id + " / " + entity;
            var rig = new ModelRig(obj.transform, entry);
            var bounds = rig.Bounds();
            var pick = obj.AddComponent<EntityPick>(); pick.id = entity; pick.enemy = enemy;
            var box = obj.AddComponent<BoxCollider>();
            box.center = obj.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
            obj.layer = 9;
            return rig;
        }

        private void AttachEnemyModule(ModelRig rig, string socketName, string role)
        {
            var asset = Catalog.Find("enemy_role_" + role.ToLowerInvariant());
            if (asset == null || !asset.prefab) return;
            var socket = rig.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == socketName);
            if (socket) Instantiate(asset.prefab, socket, false);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var s = Host.Session;
            if (s == null) { _range.enabled = false; return; }
            float dt = Host.IsRunning ? Time.deltaTime * s.Speed : 0;
            foreach (var t in s.Towers)
            {
                if (!TowerModels.TryGetValue(t.Id, out var rig) || rig == null) continue;
                var target = s.TargetSelector.Select(t.Position, s.GetEffectiveRange(t), t.TargetMode, s.Enemies);
                if (target != null) rig.Aim(EnemyPosition(target), dt);
                rig.AnimateTower(dt, t.IsOverdriven, t.IsDisrupted, t.IsSuppressed);
            }
            foreach (var e in s.Enemies)
            {
                if (!EnemyModels.TryGetValue(e.Id, out var rig) || rig == null) continue;
                var prev = _previousEnemies.TryGetValue(e.Id, out var v) ? v : e.Position;
                var p = Host.IsRunning ? SimVector.Lerp(prev, e.Position, Host.Interpolation) : e.Position;
                rig.Root.position = Coordinates.World(p, .11f);
                var dir = s.Map.Path.GetDirection(e.DistanceAlongPath);
                var rotation = Quaternion.LookRotation(new Vector3(dir.X, 0, dir.Y));
                rig.Root.rotation = Quaternion.RotateTowards(rig.Root.rotation, rotation, 520 * dt);
                rig.AnimateEnemy(dt, e.CurrentSpeed * .05f, e.StatusEffects.IsStunned);
                rig.Status(e, _time);
            }
            _forge?.AnimateTower(dt, false, false, false);
            UpdateInteraction();
        }

        private void UpdateInteraction()
        {
            var s = Host.Session;
            var overUi = PointerOverInterface?.Invoke() ?? false;
            if (!Application.isFocused || !Host.HasFocus || overUi || Host.Paused) { if (_ghost) _ghost.SetActive(false); return; }
            if (Input.GetMouseButtonDown(0)) _leftStart = Input.mousePosition;
            Ray ray = ViewCamera.ScreenPointToRay(Input.mousePosition);
            bool onPlane = TryPlacementPoint(ray, out var point);
            if (onPlane) _cursor = Coordinates.Logical(point);
            bool click = Input.GetMouseButtonUp(0) && (Input.mousePosition - _leftStart).sqrMagnitude < 16;
            if (Host.IsPlacing && onPlane)
            {
                string id = Host.PlacementId ?? (Host.TacticalPlacement == TacticalPlacementKind.PulsePlate ? "pulse_plate" : "charge_forge");
                if (id != _ghostId)
                {
                    if (_ghost) Destroy(_ghost);
                    _ghostId = id;
                    var asset = Catalog.Find(id);
                    if (asset?.prefab) { _ghost = Instantiate(asset.prefab); foreach (var c in _ghost.GetComponentsInChildren<Collider>()) Destroy(c); }
                }
                bool valid = Host.ResolvePlacement(_cursor, out var resolved);
                var location = valid ? resolved : _cursor;
                if (_ghost)
                {
                    _ghost.SetActive(true); _ghost.transform.position = Coordinates.World(location);
                    foreach (var r in _ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(valid ? _validGhost : _invalidGhost, r.sharedMaterials.Length).ToArray();
                }
                ShowRange(location, Host.PlacementId != null ? s.Content.Towers[id].Levels[0].Range : 0);
                if (click) Host.Place(_cursor, Input.GetKey(KeyCode.LeftShift));
                if (Input.GetMouseButtonUp(1) && !ViewCamera.GetComponent<BattlefieldCamera>().IsDragging) Host.CancelPlacement();
            }
            else
            {
                if (_ghost) _ghost.SetActive(false);
                var selected = Host.SelectedTower;
                ShowRange(selected?.Position ?? default, selected == null ? 0 : s.GetEffectiveRange(selected));
                _hoverEnemy = 0;
                if (TryPick(ray, out var picked))
                {
                    if (picked.enemy) _hoverEnemy = picked.id;
                    else if (click) Host.Select(picked.id, picked.generator);
                }
                else if (click) Host.Select(0);
            }
        }

        public bool TryPick(Ray ray, out EntityPick picked)
        {
            picked = null;
            if (Physics.Raycast(ray, out var hit, 200, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
                picked = hit.collider.GetComponentInParent<EntityPick>();
            return picked != null;
        }

        public bool TryPlacementPoint(Ray ray, out Vector3 point)
        {
            point = default;
            var plane = new Plane(Vector3.up, new Vector3(0, .2f, 0));
            if (!plane.Raycast(ray, out float distance) || distance <= 0) return false;
            if (Physics.Raycast(ray, Mathf.Max(0, distance - .15f), 1 << 8, QueryTriggerInteraction.Ignore)) return false;
            point = ray.GetPoint(distance);
            return true;
        }

        private void ShowRange(SimVector center, float radius)
        {
            _range.enabled = radius > 0;
            if (!_range.enabled) return;
            Vector3 p = Coordinates.World(center, .225f);
            for (int i = 0; i < _range.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / _range.positionCount;
                _range.SetPosition(i, p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * .05f);
            }
        }

        public Vector3 EnemyPosition(EnemyInstance e)
        {
            if (EnemyModels.TryGetValue(e.Id, out var rig) && rig != null) return rig.Root.position + Vector3.up * rig.Entry.aimHeight * rig.Root.localScale.y;
            return Coordinates.World(e.Position, .9f);
        }
        public Color EnemyColor(EnemyInstance e) => Coordinates.Color(e.Definition.Visual.PrimaryColor);
        public Color TowerColor(TowerInstance t) => Catalog.Find(t.Definition.Id)?.energy ?? Coordinates.Color(t.Definition.Visual.PrimaryColor);
        public LineRenderer NewLine(string name, Color color, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(transform);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Effects.NewMaterial(color, true);
            line.startColor = line.endColor = Color.white;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 4; line.numCornerVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }
    }

    public sealed class ModelRig
    {
        public readonly Transform Root, Yaw, Pitch, Recoil, Muzzle;
        public readonly VisualCatalog.Entry Entry;
        public float RecoilAmount, LastRecoil, LastHealth, LastShield, HitRemaining;
        private readonly Transform[] _joints;
        private readonly Quaternion[] _jointRest;
        private readonly Transform[] _knees;
        private readonly Quaternion[] _kneeRest;
        private readonly Renderer[] _renderers;
        private readonly Material[][] _originalMaterials;
        private static readonly Dictionary<Material, Material[]> StatusMaterials = new Dictionary<Material, Material[]>();
        private readonly Vector3 _recoilRest;
        private readonly Quaternion _pitchRest;
        private float _stride;
        private int _tier = -1;
        private bool _apex;
        private float _statusUpdate;
        private int _statusStyle;
        private TowerStatusDisplay _towerStatus;
        private EnemyShieldDisplay _shield;

        public ModelRig(Transform root, VisualCatalog.Entry entry)
        {
            Root = root; Entry = entry;
            var children = root.GetComponentsInChildren<Transform>();
            Yaw = children.FirstOrDefault(t => t.name == "Yaw");
            Pitch = children.FirstOrDefault(t => t.name == "Pitch");
            Recoil = children.FirstOrDefault(t => t.name == "Recoil");
            Muzzle = children.FirstOrDefault(t => t.name == "Muzzle") ?? Pitch ?? Yaw ?? root;
            _recoilRest = Recoil ? Recoil.localPosition : Vector3.zero;
            _pitchRest = Pitch ? Pitch.localRotation : Quaternion.identity;
            _joints = children.Where(t => t.name.StartsWith("Leg_") || t.name.StartsWith("Rotor") || t.name.StartsWith("Track") || t.name.StartsWith("Fan") || t.name.StartsWith("Piston")).ToArray();
            _jointRest = _joints.Select(t => t.localRotation).ToArray();
            _knees = _joints.Select(t => t.GetComponentsInChildren<Transform>().FirstOrDefault(k => k.name.StartsWith("Knee"))).ToArray();
            _kneeRest = _knees.Select(t => t ? t.localRotation : Quaternion.identity).ToArray();
            _renderers = root.GetComponentsInChildren<Renderer>();
            _originalMaterials = _renderers.Select(r => r.sharedMaterials).ToArray();
        }

        public Bounds Bounds()
        {
            var b = new Bounds(Root.position, Vector3.zero);
            foreach (var r in _renderers) b.Encapsulate(r.bounds);
            return b;
        }

        public void Aim(Vector3 target, float dt)
        {
            if (!Yaw) return;
            Vector3 delta = target - Yaw.position;
            var rotation = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z));
            Yaw.rotation = Quaternion.RotateTowards(Yaw.rotation, rotation, 720 * dt);
            if (Pitch)
            {
                float elevation = -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
                Pitch.localRotation = Quaternion.Slerp(Pitch.localRotation, _pitchRest * Quaternion.Euler(Mathf.Clamp(elevation, -60, 35), 0, 0), 1 - Mathf.Exp(-24 * dt));
            }
        }

        public void SetTier(int tier, bool apex)
        {
            if (_tier == tier && _apex == apex) return;
            _tier = tier; _apex = apex;
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Tier2")) t.gameObject.SetActive(tier >= 1);
                if (t.name.StartsWith("Tier3")) t.gameObject.SetActive(tier >= 2);
                if (t.name.StartsWith("Apex")) t.gameObject.SetActive(apex);
            }
        }

        public void AnimateTower(float dt, bool overdrive, bool disrupted, bool suppressed)
        {
            if ((disrupted || suppressed) && !_towerStatus)
                _towerStatus = TowerStatusDisplay.Create(Root, (Bounds().max.y - Root.position.y) / Root.localScale.y + .55f);
            if (_towerStatus) _towerStatus.SetState(disrupted, suppressed);
            RecoilAmount = Mathf.MoveTowards(RecoilAmount, 0, dt * 7);
            if (Recoil) Recoil.localPosition = _recoilRest + Vector3.back * RecoilAmount * .14f;
            for (int i = 0; i < _joints.Length; i++)
                if (_joints[i].name.StartsWith("Rotor") || _joints[i].name.StartsWith("Fan")) _joints[i].Rotate(Vector3.up, dt * (overdrive ? 220 : 65), Space.Self);
        }

        public void AnimateEnemy(float dt, float speed, bool stunned)
        {
            if (!stunned) _stride += speed * dt * 3.3f;
            for (int i = 0; i < _joints.Length; i++)
            {
                var joint = _joints[i];
                if (joint.name.StartsWith("Leg_"))
                {
                    float swing = Mathf.Sin(_stride + (i % 2) * Mathf.PI) * 22;
                    joint.localRotation = _jointRest[i] * Quaternion.Euler(swing, 0, Mathf.Cos(_stride + i * Mathf.PI) * 5);
                    if (_knees[i]) _knees[i].localRotation = _kneeRest[i] * Quaternion.Euler(Mathf.Max(0, -swing) * .7f, 0, 0);
                }
                else joint.Rotate(Vector3.up, dt * 80, Space.Self);
            }
            HitRemaining = Mathf.Max(0, HitRemaining - dt);
        }

        public void Status(EnemyInstance enemy, float time)
        {
            if (enemy.Shield > 0 && !_shield) _shield = EnemyShieldDisplay.Create(Root, Bounds());
            if (_shield) _shield.SetState(enemy.Shield > 0, Mathf.Clamp01(HitRemaining / .08f));
            if (time < _statusUpdate) return;
            _statusUpdate = time + .04f;
            int style = HitRemaining > .035f ? 3 : enemy.StatusEffects.IsBurning ? 1 : enemy.StatusEffects.SlowFactor > 0 ? 2 :
                enemy.StatusEffects.ArmorReduction > 0 ? 4 : enemy.StatusEffects.DamageMultiplier > 1 ? 5 : 0;
            if (style == _statusStyle) return;
            _statusStyle = style;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (style == 0) { _renderers[i].sharedMaterials = _originalMaterials[i]; continue; }
                var materials = new Material[_originalMaterials[i].Length];
                for (int slot = 0; slot < materials.Length; slot++) materials[slot] = StatusMaterial(_originalMaterials[i][slot], style);
                _renderers[i].sharedMaterials = materials;
            }
        }

        private static Material StatusMaterial(Material original, int style)
        {
            if (!StatusMaterials.TryGetValue(original, out var variants)) StatusMaterials[original] = variants = new Material[6];
            if (variants[style]) return variants[style];
            var material = new Material(original) { name = original.name + " / status " + style, enableInstancing = true };
            Color tint = style == 1 ? new Color(1.30f, .65f, .50f) : style == 2 ? new Color(.65f, 1.15f, 1.5f) :
                style == 4 ? new Color(1.35f, 1.10f, .60f) : style == 5 ? new Color(1.10f, .85f, 1.35f) : Color.white;
            material.SetColor("_BaseColor", style == 3 ? Color.Lerp(original.GetColor("_BaseColor"), new Color(.78f, .86f, .91f), .6f) : original.GetColor("_BaseColor") * tint);
            if (original.IsKeywordEnabled("_EMISSION")) material.SetColor("_EmissionColor", original.GetColor("_EmissionColor") * tint);
            variants[style] = material;
            return material;
        }
    }
}
