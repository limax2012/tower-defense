using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    public sealed class EnemyWreckage : MonoBehaviour
    {
        private sealed class Piece
        {
            public Transform root;
            public Vector3 velocity, spin, scale;
            public Vector3[] support;
            public float age, duration, floorHeight;
            public bool grounded;
        }

        private const int Capacity = 48;
        private readonly List<Piece> _pieces = new List<Piece>(Capacity);
        private readonly List<MeshFilter> _parts = new List<MeshFilter>();
        private readonly Dictionary<Mesh, Material[]> _finishes = new Dictionary<Mesh, Material[]>();
        private readonly Dictionary<Mesh, Vector3[]> _supports = new Dictionary<Mesh, Vector3[]>();
        private readonly Dictionary<Material, Material> _cooled = new Dictionary<Material, Material>();
        private SimulationHost _host;
        private uint _seed = 8273;
        public int ActivePieces => _pieces.Count;

        public void Initialize(SimulationHost host, VisualCatalog catalog)
        {
            _host = host;
            foreach (var entry in catalog.enemies)
            {
                if (!entry.prefab) continue;
                foreach (var filter in entry.prefab.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !filter.sharedMesh || !filter.name.EndsWith("_Geometry")) continue;
                    if (!filter.sharedMesh.isReadable)
                    {
                        Debug.LogWarning("Chassis fragments require readable enemy mesh data: " + entry.id + "/" + filter.name);
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!_cooled.TryGetValue(materials[i], out var finish))
                        {
                            finish = new Material(materials[i]) { name = materials[i].name + " / cooled", enableInstancing = true };
                            if (finish.HasProperty("_EmissionColor")) finish.SetColor("_EmissionColor", Color.black);
                            finish.DisableKeyword("_EMISSION");
                            _cooled.Add(materials[i], finish);
                        }
                        materials[i] = finish;
                    }
                    _finishes[filter.sharedMesh] = materials;
                    if (!_supports.ContainsKey(filter.sharedMesh)) _supports[filter.sharedMesh] = SupportPoints(filter.sharedMesh);
                }
            }
        }

        public void BreakApart(Transform source)
        {
            if (!source) return;
            _parts.Clear();
            source.GetComponentsInChildren(false, _parts);
            _parts.RemoveAll(p => !p.sharedMesh || !_finishes.ContainsKey(p.sharedMesh));
            _parts.Sort((a, b) => b.sharedMesh.bounds.size.sqrMagnitude.CompareTo(a.sharedMesh.bounds.size.sqrMagnitude));
            int count = Mathf.Min(5, _parts.Count);
            for (int i = 0; i < count; i++)
            {
                if (_pieces.Count == Capacity) Remove(0);
                var part = _parts[i];
                var mesh = part.sharedMesh;
                var root = new GameObject("Disabled chassis fragment").transform;
                root.SetParent(transform, false);
                root.position = part.transform.TransformPoint(mesh.bounds.center);
                root.rotation = part.transform.rotation;
                root.localScale = part.transform.lossyScale;
                var geometry = new GameObject("Armor");
                geometry.transform.SetParent(root, false);
                geometry.transform.localPosition = -mesh.bounds.center;
                geometry.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = geometry.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = _finishes[mesh];
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                Vector3 radial = root.position - source.position;
                radial.y = 0;
                if (radial.sqrMagnitude < .01f) radial = new Vector3(Next() - .5f, 0, Next() - .5f);
                bool chassis = part.name.StartsWith("Body");
                Vector3 scale = root.localScale;
                _pieces.Add(new Piece
                {
                    root = root, scale = scale,
                    support = _supports[mesh],
                    velocity = source.forward * .6f + radial.normalized * (chassis ? .35f : .7f + Next() * .6f) + Vector3.up * (chassis ? .5f : 1.2f + Next() * .6f),
                    spin = new Vector3(Next() - .5f, Next() - .5f, Next() - .5f) * (chassis ? 70 : 220),
                    duration = chassis ? 1.15f : .9f + Next() * .25f,
                    floorHeight = .065f
                });
            }
        }

        private void LateUpdate()
        {
            if (_host.Session == null || !_host.IsRunning) return;
            float dt = Mathf.Min(Time.deltaTime * _host.Session.Speed, .06f);
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var piece = _pieces[i];
                piece.age += dt;
                if (piece.age >= piece.duration) { Remove(i); continue; }
                float retreat = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(piece.duration - .32f, piece.duration, piece.age));
                float size = 1 - retreat;
                piece.root.localScale = piece.scale * size;
                if (!piece.grounded)
                {
                    float previousHeight = piece.root.position.y;
                    piece.velocity += Vector3.down * (9.8f * dt);
                    piece.root.position += piece.velocity * dt;
                    piece.root.Rotate(piece.spin * dt, Space.World);
                    float support = FloorClearance(piece, out var contact) * size;
                    float rayHeight = Mathf.Max(previousHeight, piece.root.position.y) + .15f;
                    piece.floorHeight = Mathf.Max(SurfaceHeight(piece.root.position, rayHeight),
                        SurfaceHeight(piece.root.position + piece.root.rotation * Vector3.Scale(contact, piece.scale) * size, rayHeight));
                    if (piece.root.position.y < piece.floorHeight + support)
                    {
                        var p = piece.root.position; p.y = piece.floorHeight + support; piece.root.position = p;
                        piece.velocity = new Vector3(piece.velocity.x * .35f, Mathf.Abs(piece.velocity.y) * .16f, piece.velocity.z * .35f);
                        piece.spin *= .25f;
                        if (piece.velocity.y < .3f) piece.grounded = true;
                    }
                }
                if (piece.grounded)
                {
                    var p = piece.root.position; p.y = piece.floorHeight + FloorClearance(piece, out _) * size; piece.root.position = p;
                }
            }
        }

        public void Clear()
        {
            for (int i = _pieces.Count - 1; i >= 0; i--) Remove(i);
        }

        private void Remove(int index)
        {
            if (_pieces[index].root) Destroy(_pieces[index].root.gameObject);
            _pieces.RemoveAt(index);
        }

        private float Next()
        {
            _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
            return (_seed & 65535) / 65535f;
        }

        private static Vector3[] SupportPoints(Mesh mesh)
        {
            var vertices = mesh.vertices;
            if (vertices.Length == 0) throw new System.InvalidOperationException("Enemy chassis mesh has no support geometry: " + mesh.name);
            var points = new HashSet<Vector3>();
            foreach (var vertex in vertices) points.Add(vertex - mesh.bounds.center);
            var result = new Vector3[points.Count]; points.CopyTo(result); return result;
        }

        private static float FloorClearance(Piece piece, out Vector3 contact)
        {
            float depth = float.NegativeInfinity;
            Vector3 direction = Vector3.Scale(Quaternion.Inverse(piece.root.rotation) * Vector3.down, piece.scale);
            contact = Vector3.zero;
            foreach (var point in piece.support)
            {
                float candidate = Vector3.Dot(point, direction);
                if (candidate > depth) { depth = candidate; contact = point; }
            }
            return depth;
        }

        internal static float SurfaceHeight(Vector3 sample, float rayHeight)
        {
            // The cast starts just above the swept fragment center, below overhead structures.
            var origin = new Vector3(sample.x, rayHeight, sample.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, 6, 1 << 8, QueryTriggerInteraction.Ignore) &&
                hit.normal.y > .5f ? hit.point.y + .005f : .065f;
        }

        internal void VerifySupportGeometry(List<string> report, List<string> errors)
        {
            float maximum = 0;
            int poses = 0, vertices = 0;
            foreach (var pair in _supports)
            {
                var source = pair.Key.vertices;
                vertices += pair.Value.Length;
                for (int i = 0; i < 192; i++)
                {
                    var rotation = Quaternion.Euler(i * 137.508f, i * 53.171f, i * 29.327f);
                    var scale = new Vector3(1.4f, 1.4f, 1.4f);
                    Vector3 direction = Vector3.Scale(Quaternion.Inverse(rotation) * Vector3.down, scale);
                    float cached = float.NegativeInfinity, actual = float.PositiveInfinity;
                    foreach (var point in pair.Value) cached = Mathf.Max(cached, Vector3.Dot(point, direction));
                    foreach (var point in source) actual = Mathf.Min(actual, (rotation * Vector3.Scale(point - pair.Key.bounds.center, scale)).y);
                    maximum = Mathf.Max(maximum, Mathf.Abs(cached + actual));
                    poses++;
                }
            }
            if (maximum > .0001f || poses == 0) errors.Add("Rotated chassis support does not match actual geometry.");
            report.Add($"Exact support: meshes={_supports.Count}, unique vertices={vertices}, rotated poses={poses}, maximum error={maximum:F6}m");
        }

        internal int MeasureGroundedContact(out float maximumError)
        {
            maximumError = 0;
            int count = 0;
            foreach (var piece in _pieces)
            {
                if (!piece.grounded) continue;
                var geometry = piece.root.GetComponentInChildren<MeshFilter>();
                float bottom = float.PositiveInfinity;
                foreach (var vertex in geometry.sharedMesh.vertices)
                    bottom = Mathf.Min(bottom, geometry.transform.TransformPoint(vertex).y);
                maximumError = Mathf.Max(maximumError, Mathf.Abs(bottom - piece.floorHeight));
                count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            foreach (var material in _cooled.Values) if (material) Destroy(material);
        }
    }
}
