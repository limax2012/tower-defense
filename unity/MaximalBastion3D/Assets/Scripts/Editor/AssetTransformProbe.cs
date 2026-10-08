using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bastion3D.Editor
{
    public static class AssetTransformProbe
    {
        [Serializable] private sealed class Manifest { public Node[] hierarchy; }
        [Serializable] private sealed class Node { public string name; public float[] worldPosition; public float[] worldForward; }

        public static void Run()
        {
            var report = new StringBuilder();
            foreach (string id in new[] { "cinderworks_dressing", "prism_beam", "shard_fan", "siege_mortar", "t2_runner" })
            {
                string category = id == "cinderworks_dressing" ? "Environment" : id == "t2_runner" ? "Enemies" : "Towers";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Models/{category}/{id}.fbx");
                var instance = UnityEngine.Object.Instantiate(prefab);
                var nodes = instance.GetComponentsInChildren<Transform>();
                var root = instance.transform;
                report.AppendLine($"ASSET {id} root position={root.position:F6} rotation={root.eulerAngles:F6} scale={root.lossyScale:F6} determinant={root.localToWorldMatrix.determinant:F6}");
                var metadata = JsonUtility.FromJson<Manifest>(File.ReadAllText($"Assets/Art/Metadata/{id}.json"));
                foreach (var node in metadata.hierarchy.Where(n => n.name.StartsWith("CoreLight") || n.name.StartsWith("Heat_Furnace") || n.name.StartsWith("Exhaust_Stack") || n.name.StartsWith("Muzzle") || n.name.StartsWith("Leg_") || n.name == "Yaw" || n.name == "Pitch"))
                {
                    var actual = nodes.FirstOrDefault(t => t.name == node.name);
                    if (!actual) { report.AppendLine("MISSING " + node.name); continue; }
                    var expected = new Vector3(node.worldPosition[0], node.worldPosition[1], node.worldPosition[2]);
                    string forward = node.worldForward == null ? "none" : string.Join(",", node.worldForward.Select(v => v.ToString("F6")));
                    report.AppendLine($"{node.name} expected={expected:F6} actual={actual.position:F6} delta={(actual.position-expected):F6} expectedForward=({forward}) actualForward={actual.forward:F6} localRot={actual.localEulerAngles:F6} determinant={actual.localToWorldMatrix.determinant:F6}");
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }
            var output = Path.GetFullPath("../../.artifacts/3d-development/unity-transform-probe.txt");
            File.WriteAllText(output, report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
