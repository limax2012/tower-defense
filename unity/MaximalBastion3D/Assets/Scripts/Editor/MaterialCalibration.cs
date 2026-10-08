using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D.Editor
{
    public static class MaterialCalibration
    {
        [MenuItem("Bastion/Calibrate material colors")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/BastionPipeline.asset");
            if (!pipeline) throw new InvalidOperationException("Configure the project before color calibration.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0;
            RenderSettings.reflectionIntensity = 0;
            var camera = new GameObject("Material calibration camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -2);
            camera.orthographic = true;
            camera.orthographicSize = .5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.enabled = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            camera.targetTexture = target;
            var pixels = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.localScale = Vector3.one * 2;
            var renderer = quad.GetComponent<Renderer>();
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var energy = new Material(Shader.Find("Bastion/Energy"));
            energy.SetFloat("_Fade", 1);
            lit.SetColor("_BaseColor", Color.black);
            lit.SetFloat("_Metallic", 0);
            lit.SetFloat("_Smoothness", 0);
            lit.EnableKeyword("_EMISSION");
            var report = new StringBuilder();
            var failures = new List<string>();
            report.AppendLine("Unity " + Application.unityVersion + " | " + QualitySettings.activeColorSpace + " | " + SystemInfo.graphicsDeviceName);
            report.AppendLine("Linear HDR readback, no lights/reflections/postprocessing; centre of a URP quad.");
            report.AppendLine("Base flags: " + unlit.shader.GetPropertyFlags(unlit.shader.FindPropertyIndex("_BaseColor")));
            report.AppendLine("Emission flags: " + lit.shader.GetPropertyFlags(lit.shader.FindPropertyIndex("_EmissionColor")));
            report.AppendLine("Energy flags: " + energy.shader.GetPropertyFlags(energy.shader.FindPropertyIndex("_BaseColor")));
            try
            {
                var srgb = new Color(.5f, .2f, .8f, 1);
                renderer.sharedMaterial = unlit;
                unlit.SetColor("_BaseColor", srgb);
                // Initialize the render pipeline and shader pass before measuring its target.
                Read();
                Record("Base / SetColor sRGB", unlit, "_BaseColor", srgb.linear, Read(), true);
                unlit.SetVector("_BaseColor", srgb.linear);
                Record("Base / SetVector linear", unlit, "_BaseColor", srgb.linear, Read());
                renderer.sharedMaterial = lit;
                Probe("Furnace", "#FF7934", 3);
                Probe("Prism", "#A58DE8", 1.35f);
                Probe("White", "#E3F7FF", 2.3f);
                renderer.sharedMaterial = energy;
                ProbeEnergy("Prism", "#A58DE8", 1.35f);
                ProbeEnergy("Furnace", "#FF7934", 3);
                renderer.sharedMaterial = unlit;
                ProbeBlock("Base / MPB SetColor sRGB", "_BaseColor", srgb, srgb.linear, false);
                ProbeBlock("Base / MPB SetVector linear", "_BaseColor", srgb.linear, srgb.linear, true);
                renderer.sharedMaterial = lit;
                ProbeBlock("Emission / MPB SetColor linear", "_EmissionColor", srgb.linear, srgb.linear, false);
                ProbeBlock("Emission / MPB SetVector linear", "_EmissionColor", srgb.linear, srgb.linear, true);
                renderer.sharedMaterial = energy;
                ProbeBlock("Energy / MPB SetColor linear", "_BaseColor", srgb.linear, srgb.linear, false);
                ProbeBlock("Energy / MPB SetVector linear", "_BaseColor", srgb.linear, srgb.linear, true);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(unlit);
                UnityEngine.Object.DestroyImmediate(lit);
                UnityEngine.Object.DestroyImmediate(energy);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(quad);
            }
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.artifacts/3d-development/material-calibration"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "color-space.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (failures.Count != 0) throw new InvalidOperationException("Color calibration failed: " + string.Join(", ", failures));

            void Probe(string name, string hex, float strength)
            {
                ColorUtility.TryParseHtmlString(hex, out var color);
                var radiance = color.linear * strength;
                lit.SetColor("_EmissionColor", color * strength);
                Record(name + " / SetColor sRGB x strength", lit, "_EmissionColor", radiance, Read());
                lit.SetColor("_EmissionColor", radiance.gamma);
                Record(name + " / SetColor encoded radiance", lit, "_EmissionColor", radiance, Read());
                lit.SetVector("_EmissionColor", radiance);
                Record(name + " / SetVector linear radiance", lit, "_EmissionColor", radiance, Read(), true);
                lit.SetColor("_EmissionColor", radiance);
                Record(name + " / SetColor linear radiance", lit, "_EmissionColor", radiance, Read(), true);
            }

            void ProbeEnergy(string name, string hex, float strength)
            {
                ColorUtility.TryParseHtmlString(hex, out var color);
                var radiance = color.linear * strength;
                radiance.a = 1;
                var raw = color * strength;
                raw.a = 1;
                energy.SetColor("_BaseColor", raw);
                Record("Energy " + name + " / SetColor sRGB x strength", energy, "_BaseColor", radiance, Read());
                energy.SetColor("_BaseColor", radiance);
                Record("Energy " + name + " / SetColor linear radiance", energy, "_BaseColor", radiance, Read(), true);
                energy.SetVector("_BaseColor", radiance);
                Record("Energy " + name + " / SetVector linear radiance", energy, "_BaseColor", radiance, Read(), true);
            }

            void ProbeBlock(string label, string property, Color input, Color expected, bool vector)
            {
                var block = new MaterialPropertyBlock();
                if (vector) block.SetVector(property, input);
                else block.SetColor(property, input);
                renderer.SetPropertyBlock(block);
                report.AppendLine(label + " | expected=" + Format(expected) + " | GPU=" + Format(Read()) +
                    " | stored=" + Format((Color)block.GetVector(property)));
                renderer.SetPropertyBlock(null);
            }

            Color Read()
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0, false);
                pixels.Apply(false, false);
                RenderTexture.active = null;
                return pixels.GetPixel(16, 16);
            }

            void Record(string label, Material material, string property, Color expected, Color actual, bool verify = false)
            {
                report.AppendLine(label + " | expected=" + Format(expected) + " | GPU=" + Format(actual) +
                    " | GetVector=" + Format((Color)material.GetVector(property)) + " | GetColor=" + Format(material.GetColor(property)));
                if (verify && Mathf.Max(Mathf.Abs(expected.r - actual.r), Mathf.Abs(expected.g - actual.g), Mathf.Abs(expected.b - actual.b)) > .003f)
                    failures.Add(label);
            }

            string Format(Color color) => string.Format(CultureInfo.InvariantCulture, "({0:F6},{1:F6},{2:F6})", color.r, color.g, color.b);
        }
    }
}
