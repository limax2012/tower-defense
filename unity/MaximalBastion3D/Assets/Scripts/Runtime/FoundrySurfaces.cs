using System.Collections.Generic;
using MaximalBastion.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    public static class FoundrySurfaces
    {
        public static void Build(Transform root, MapDefinition map, VisualCatalog catalog)
        {
            var platform = catalog.Find("foundry_platform")?.prefab;
            foreach (var region in map.BuildableRegions)
            {
                float width = region.Width * Coordinates.Scale, depth = region.Height * Coordinates.Scale;
                Vector3 corner = Coordinates.World(new MaximalBastion.Gameplay.Vector2(region.X, region.Y), .1f);
                int nx = Mathf.Max(1, Mathf.RoundToInt(width / 4)), nz = Mathf.Max(1, Mathf.RoundToInt(depth / 4));
                for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                {
                    var tile = Object.Instantiate(platform, root);
                    tile.name = "Riveted build deck";
                    tile.transform.position = corner + new Vector3((x + .5f) * width / nx, 0, (z + .5f) * depth / nz);
                    tile.transform.localScale = new Vector3(width / nx / 4, 1, depth / nz / 4);
                }
                var collision = new GameObject("Build deck collision");
                collision.transform.SetParent(root); collision.layer = 8;
                collision.transform.position = corner + new Vector3(width / 2, -.05f, depth / 2);
                var box = collision.AddComponent<BoxCollider>(); box.size = new Vector3(width, .30f, depth);
            }
            var path = new Vector3[map.Path.Count];
            for (int i = 0; i < path.Length; i++) path[i] = Coordinates.World(map.Path[i].ToVector2(), .06f);
            float halfWidth = map.PathWidth * Coordinates.Scale * .5f;
            var road = new MeshBatch(); var copper = new MeshBatch(); var marks = new MeshBatch(); var seams = new MeshBatch();
            Vector3[] outerLeft = Offset(path, halfWidth), outerRight = Offset(path, -halfWidth);
            Vector3[] innerLeft = Offset(path, halfWidth - .10f), innerRight = Offset(path, -halfWidth + .10f);
            for (int i = 0; i < path.Length - 1; i++)
            {
                road.Quad(outerRight[i], outerRight[i + 1], outerLeft[i + 1], outerLeft[i]);
                road.Quad(outerLeft[i], outerLeft[i + 1], outerLeft[i + 1] - Vector3.up * .22f, outerLeft[i] - Vector3.up * .22f);
                road.Quad(outerRight[i] - Vector3.up * .22f, outerRight[i + 1] - Vector3.up * .22f, outerRight[i + 1], outerRight[i]);
                copper.Quad(outerLeft[i] + Vector3.up * .015f, innerLeft[i] + Vector3.up * .015f,
                    innerLeft[i + 1] + Vector3.up * .015f, outerLeft[i + 1] + Vector3.up * .015f);
                copper.Quad(innerRight[i] + Vector3.up * .015f, outerRight[i] + Vector3.up * .015f,
                    outerRight[i + 1] + Vector3.up * .015f, innerRight[i + 1] + Vector3.up * .015f);
                var direction = (path[i + 1] - path[i]).normalized;
                var side = Vector3.Cross(Vector3.up, direction);
                float length = Vector3.Distance(path[i], path[i + 1]);
                for (float d = halfWidth + .35f; d < length - halfWidth; d += 1.1f)
                {
                    Vector3 center = path[i] + direction * d + Vector3.up * .028f;
                    seams.FlatRect(center, side * (halfWidth - .20f), direction * .022f);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        Vector3 marker = center + side * (halfWidth - .23f) * sign + Vector3.up * .005f;
                        marks.FlatRect(marker, side * .045f, direction * .14f);
                    }
                }
            }
            road.Quad(outerLeft[0], outerLeft[0] - Vector3.up * .22f, outerRight[0] - Vector3.up * .22f, outerRight[0]);
            int last = path.Length - 1;
            road.Quad(outerRight[last], outerRight[last] - Vector3.up * .22f, outerLeft[last] - Vector3.up * .22f, outerLeft[last]);
            road.Create("Continuous transfer lane", root, catalog.road, true);
            copper.Create("Inset copper lane guides", root, catalog.copper);
            marks.Create("Transfer lane marker lamps", root, catalog.laneLights);
            seams.Create("Transfer lane expansion joints", root, catalog.steel);
        }

        private static Vector3[] Offset(Vector3[] points, float distance)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 incoming = (points[i] - points[Mathf.Max(0, i - 1)]).normalized;
                Vector3 outgoing = (points[Mathf.Min(points.Length - 1, i + 1)] - points[i]).normalized;
                if (i == 0) incoming = outgoing;
                if (i == points.Length - 1) outgoing = incoming;
                Vector3 normal = Vector3.Cross(Vector3.up, incoming);
                Vector3 miter = (normal + Vector3.Cross(Vector3.up, outgoing)).normalized;
                result[i] = points[i] + miter * (distance / Mathf.Max(.25f, Vector3.Dot(miter, normal)));
            }
            return result;
        }

        private sealed class MeshBatch
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _indices = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = _vertices.Count;
                _vertices.AddRange(new[] { a, b, c, d });
                foreach (var p in new[] { a, b, c, d }) _uvs.Add(new Vector2(p.x * .25f, p.z * .25f));
                _indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            public void FlatRect(Vector3 center, Vector3 side, Vector3 along) => Quad(center - side - along, center - side + along, center + side + along, center + side - along);
            public void Create(string name, Transform parent, Material material, bool collide = false)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(_vertices); mesh.SetUVs(0, _uvs); mesh.SetTriangles(_indices, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var obj = new GameObject(name); obj.transform.SetParent(parent); obj.layer = 8;
                obj.AddComponent<OwnedSurfaceMesh>().mesh = mesh;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (collide) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    public sealed class OwnedSurfaceMesh : MonoBehaviour
    {
        public Mesh mesh;
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
