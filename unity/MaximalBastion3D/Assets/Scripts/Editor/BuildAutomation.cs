using System;
using System.IO;
using System.Linq;
using Bastion3D;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D.Editor
{
    public static class BuildAutomation
    {
        [Serializable] private sealed class MaterialSet { public MaterialSpec[] materials; }
        [Serializable] private sealed class MaterialSpec { public string name; public string color; public string metallicSmoothnessTexture; public float metallic, roughness, emission; }
        private const string ScenePath = "Assets/Scenes/Cinderworks.unity";
        private const string CatalogPath = "Assets/Resources/VisualCatalog.asset";

        [MenuItem("Bastion/Configure project")]
        public static void ConfigureProject()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Art/Materials");
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/BastionPipeline.asset");
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/BastionRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/BastionPipeline.asset");
            }
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/BastionRenderer.asset");
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (!rendererData.postProcessData) throw new InvalidOperationException("URP postprocess shader data is missing.");
            var occlusion = rendererData.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (!occlusion)
            {
                occlusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                occlusion.name = "Mechanical contact shading";
                AssetDatabase.AddObjectToAsset(occlusion, rendererData);
                rendererData.rendererFeatures.Add(occlusion);
            }
            var occlusionSettings = new SerializedObject(occlusion);
            occlusionSettings.FindProperty("m_Settings.Intensity").floatValue = .75f;
            occlusionSettings.FindProperty("m_Settings.Radius").floatValue = .38f;
            occlusionSettings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .15f;
            occlusionSettings.FindProperty("m_Settings.Downsample").boolValue = true;
            occlusionSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(occlusion);
            EditorUtility.SetDirty(rendererData);
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1;
            pipeline.shadowDistance = 85;
            pipeline.shadowCascadeCount = 4;
            pipeline.maxAdditionalLightsCount = 8;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            PlayerSettings.companyName = "Bastion";
            PlayerSettings.productName = "Maximal Bastion 3D";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First());
            playerSettings.FindProperty("activeInputHandler").intValue = 0;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            BuildMaterials();
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<VisualCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.towers = Entries("Towers");
            catalog.enemies = Entries("Enemies");
            catalog.environment = Entries("Environment");
            catalog.tactics = Entries("Tactics");
            catalog.enemyModules = Entries("EnemyModules");
            catalog.dressing = catalog.environment.FirstOrDefault(e => e.id == "cinderworks_dressing")?.prefab;
            catalog.ground = BuildFloorMaterial();
            catalog.road = Material("Bastion_Recess");
            catalog.platform = Material("Foundry_Deck");
            catalog.steel = Material("Bastion_Graphite");
            catalog.trim = Material("Bastion_EdgeAlloy");
            catalog.energy = Material("Foundry_Heat");
            catalog.copper = Material("Foundry_Copper");
            catalog.laneLights = Material("Foundry_Hazard");
            catalog.interfaceFont = Resources.Load<Font>("InterfaceFont");
            EditorUtility.SetDirty(catalog);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("Maximal Bastion / Cinderworks").AddComponent<BastionApplication>();
            app.catalog = catalog;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Bastion configured: " + catalog.towers.Length + " towers, " + catalog.enemies.Length + " enemies.");
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat");
        private static Material BuildFloorMaterial()
        {
            const string path = "Assets/Art/Materials/FoundryFloor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Material("Foundry_Oxide")); AssetDatabase.CreateAsset(material, path); }
            foreach (var channel in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
            {
                string texturePath = "Assets/Art/Textures/FoundryFloor_" + channel + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                if (!importer) throw new FileNotFoundException("Missing floor texture", texturePath);
                importer.textureType = channel == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = channel == "BaseColor";
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8;
                importer.SaveAndReimport();
                string property = channel == "BaseColor" ? "_BaseMap" : channel == "Normal" ? "_BumpMap" : "_MetallicGlossMap";
                material.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            material.SetColor("_BaseColor", new Color(.25f, .22f, .21f));
            material.SetTextureScale("_BaseMap", new Vector2(8, 7.5f));
            material.SetFloat("_BumpScale", .6f); material.SetFloat("_Smoothness", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }
        private static void BuildMaterials()
        {
            var path = "Assets/Art/Metadata/materials.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Material catalog is missing", path);
            var set = JsonUtility.FromJson<MaterialSet>(File.ReadAllText(path));
            foreach (var spec in set.materials)
            {
                string target = "Assets/Art/Materials/" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(target);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, target); }
                ColorUtility.TryParseHtmlString(spec.color, out Color color);
                mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", spec.metallic); mat.SetFloat("_Smoothness", 1 - spec.roughness);
                if (!string.IsNullOrEmpty(spec.metallicSmoothnessTexture))
                {
                    string texturePath = "Assets/Art/Textures/" + spec.metallicSmoothnessTexture;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    if (!importer) throw new FileNotFoundException("Missing alloy surface texture", texturePath);
                    if (importer.sRGBTexture) { importer.sRGBTexture = false; importer.SaveAndReimport(); }
                    mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    mat.SetFloat("_Smoothness", 1); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                else { mat.SetTexture("_MetallicGlossMap", null); mat.DisableKeyword("_METALLICSPECGLOSSMAP"); }
                if (spec.emission > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * spec.emission); }
                else mat.DisableKeyword("_EMISSION");
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            foreach (string fbx in Directory.GetFiles("Assets/Art/Models", "*.fbx", SearchOption.AllDirectories))
            {
                var assetPath = fbx.Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.importAnimation = false;
                importer.isReadable = assetPath.Contains("/Environment/");
                importer.importCameras = false; importer.importLights = false;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Material>())
                {
                    var replacement = Material(asset.name);
                    if (replacement) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), asset.name), replacement);
                }
                importer.SaveAndReimport();
            }
        }

        private static VisualCatalog.Entry[] Entries(string category)
        {
            string folder = "Assets/Art/Models/" + category;
            if (!Directory.Exists(folder)) return Array.Empty<VisualCatalog.Entry>();
            return Directory.GetFiles(folder, "*.fbx").OrderBy(p => p).Select(path =>
            {
                string id = Path.GetFileNameWithoutExtension(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                var material = Material("Energy_" + id);
                return new VisualCatalog.Entry { id = id, prefab = prefab,
                    energy = material ? material.GetColor("_BaseColor") : new Color(.3f, .8f, .7f),
                    aimHeight = category == "Enemies" ? (id.Contains("crawler") || id.Contains("runner") ? .65f : 1.15f) : 1.5f };
            }).ToArray();
        }

        [MenuItem("Bastion/Verify assets")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (!catalog) throw new InvalidOperationException("Configure the project first.");
            var report = new System.Text.StringBuilder();
            foreach (var entry in catalog.towers.Concat(catalog.enemies).Concat(catalog.environment).Concat(catalog.tactics).Concat(catalog.enemyModules))
            {
                if (!entry.prefab) throw new InvalidOperationException("Missing model " + entry.id);
                var obj = UnityEngine.Object.Instantiate(entry.prefab);
                var renderers = obj.GetComponentsInChildren<Renderer>();
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    if (renderer.sharedMaterials.Any(m => !m || m.shader.name == "Hidden/InternalErrorShader")) throw new InvalidOperationException("Invalid material " + entry.id);
                }
                report.AppendLine(entry.id + " | bounds " + bounds + " | renderers " + renderers.Length);
                if (catalog.towers.Contains(entry) && entry.id != "signal_beacon")
                {
                    var muzzle = obj.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Muzzle");
                    if (!muzzle) throw new InvalidOperationException("Missing muzzle " + entry.id);
                    report.AppendLine("  muzzle: " + muzzle.position + " / forward: " + muzzle.forward);
                    if (muzzle.position.y < .2f || muzzle.position.y > 5) throw new InvalidOperationException("Incorrect muzzle scale/basis " + entry.id);
                }
                UnityEngine.Object.DestroyImmediate(obj);
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.artifacts/3d-development"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "unity-asset-verification.txt"), report.ToString());
            Debug.Log(report.ToString());
        }

        [MenuItem("Bastion/Build Windows (development)")]
        public static void BuildWindows() => BuildWindowsPlayer(true);

        [MenuItem("Bastion/Build Windows (release)")]
        public static void BuildWindowsRelease() => BuildWindowsPlayer(false);

        [Serializable]
        private sealed class PlayerBuildInfo
        {
            public string configuration, unityVersion, productVersion, builtUtc, executable;
        }

        private static void BuildWindowsPlayer(bool development)
        {
            ConfigureProject();
            Verify();
            string directory = development ? "Windows" : "Windows-Release";
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.artifacts/3d-development/" + directory + "/MaximalBastion3D.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-info.json"), JsonUtility.ToJson(new PlayerBuildInfo
            {
                configuration = development ? "Development" : "Release", unityVersion = Application.unityVersion,
                productVersion = PlayerSettings.bundleVersion, builtUtc = DateTime.UtcNow.ToString("O"), executable = Path.GetFileName(output)
            }, true));
            Debug.Log("Windows build: " + output + " | bytes: " + report.summary.totalSize);
        }
    }
}
