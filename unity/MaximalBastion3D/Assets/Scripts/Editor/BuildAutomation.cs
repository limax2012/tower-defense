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
        [Serializable] private sealed class MaterialSpec { public string name; public string color; public string metallicSmoothnessTexture; public float metallic, roughness, emission; public float[] emissionLinear; }
        [Serializable] private sealed class AssetManifest { public RigNode[] hierarchy; }
        [Serializable] private sealed class RigNode { public string name, parent; public float[] worldPosition, worldForward, worldUp; }
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
            var graphicsSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").First());
            // Runtime-selected fog needs an explicit retained variant in the player.
            graphicsSettings.FindProperty("m_FogStripping").intValue = 1;
            graphicsSettings.FindProperty("m_FogKeepLinear").boolValue = false;
            graphicsSettings.FindProperty("m_FogKeepExp").boolValue = false;
            graphicsSettings.FindProperty("m_FogKeepExp2").boolValue = true;
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            PlayerSettings.companyName = "Bastion";
            PlayerSettings.productName = "Maximal Bastion 3D";
            PlayerSettings.bundleVersion = "0.1.0";
            ConfigureBranding();
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
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .010f;
            RenderSettings.fogColor = new Color(.045f, .070f, .105f);
            var app = new GameObject("Maximal Bastion / Cinderworks").AddComponent<BastionApplication>();
            app.catalog = catalog;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Bastion configured: " + catalog.towers.Length + " towers, " + catalog.enemies.Length + " enemies.");
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat");
        private static void ConfigureBranding()
        {
            const string path = "Assets/Resources/Brand/Prism.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new FileNotFoundException("The Prism brand mark is missing", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!texture || !sprite) throw new InvalidOperationException("The Prism brand mark could not be imported.");
            foreach (var target in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone })
            {
                int count = PlayerSettings.GetIconSizes(target, IconKind.Application).Length;
                if (count == 0) throw new InvalidOperationException("The target exposes no application icon slots: " + target.TargetName);
                PlayerSettings.SetIcons(target, Enumerable.Repeat(texture, count).ToArray(), IconKind.Application);
            }
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.backgroundPortrait = null;
            PlayerSettings.SplashScreen.backgroundColor = new Color(10 / 255f, 20 / 255f, 32 / 255f);
            PlayerSettings.SplashScreen.overlayOpacity = 0;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2, sprite) };
            if (PlayerSettings.SplashScreen.showUnityLogo)
                Debug.LogWarning("The active Unity license retains the Unity logo beside the project splash mark.");
        }
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
                    if (!importer) throw new FileNotFoundException("Missing surface finish texture", texturePath);
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false; importer.alphaIsTransparency = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
                    importer.mipmapEnabled = true; importer.anisoLevel = 4;
                    importer.SaveAndReimport();
                    mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    mat.SetFloat("_Smoothness", 1); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                else { mat.SetTexture("_MetallicGlossMap", null); mat.DisableKeyword("_METALLICSPECGLOSSMAP"); }
                if (spec.emission > 0)
                {
                    mat.EnableKeyword("_EMISSION");
                    var radiance = spec.emissionLinear != null && spec.emissionLinear.Length == 3
                        ? new Color(spec.emissionLinear[0], spec.emissionLinear[1], spec.emissionLinear[2], 1)
                        : color.linear * spec.emission;
                    // URP HDR color properties consume linear radiance; ordinary BaseColor is authored sRGB.
                    mat.SetColor("_EmissionColor", radiance);
                }
                else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
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
                importer.isReadable = assetPath.Contains("/Environment/") || assetPath.Contains("/Enemies/");
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
            var graphicsSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").First());
            if (graphicsSettings.FindProperty("m_FogStripping").intValue != 1 || !graphicsSettings.FindProperty("m_FogKeepExp2").boolValue)
                throw new InvalidOperationException("Exponential-squared fog must be retained for runtime lighting.");
            report.AppendLine("Exponential-squared fog variant retained explicitly.");
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
                VerifyRig(entry.id, obj.transform, report);
                if (catalog.enemies.Contains(entry))
                {
                    var meshes = obj.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).Where(mesh => mesh).ToArray();
                    if (meshes.Length == 0 || meshes.Any(mesh => !mesh.isReadable))
                        throw new InvalidOperationException("Enemy meshes must be readable for chassis support geometry: " + entry.id);
                    report.AppendLine("  readable chassis meshes: " + meshes.Length);
                }
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

        private static void VerifyRig(string id, Transform root, System.Text.StringBuilder report)
        {
            string path = "Assets/Art/Metadata/" + id + ".json";
            var manifest = JsonUtility.FromJson<AssetManifest>(File.ReadAllText(path));
            if (manifest.hierarchy == null || manifest.hierarchy.Length == 0)
                throw new InvalidOperationException("Missing authored rig hierarchy: " + id);
            var transforms = root.GetComponentsInChildren<Transform>(true);
            float maximumPositionError = 0, maximumDirectionError = 0;
            foreach (var node in manifest.hierarchy)
            {
                var matches = string.IsNullOrEmpty(node.parent) ? new[] { root } : transforms.Where(t => t.name == node.name).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Missing or ambiguous imported rig node: " + id + "/" + node.name);
                var actual = matches[0];
                Vector3 expectedPosition = RigVector(node.worldPosition, id, node.name);
                Vector3 expectedForward = RigVector(node.worldForward, id, node.name).normalized;
                Vector3 expectedUp = RigVector(node.worldUp, id, node.name).normalized;
                float positionError = Vector3.Distance(actual.position, expectedPosition);
                float directionError = Mathf.Max(Vector3.Distance(actual.forward, expectedForward), Vector3.Distance(actual.up, expectedUp));
                if (positionError > .002f || directionError > .001f || actual.localToWorldMatrix.determinant <= 0)
                    throw new InvalidOperationException($"Imported rig basis mismatch: {id}/{node.name}; expected position {expectedPosition:F6}, actual {actual.position:F6}, expected forward {expectedForward:F6}, actual {actual.forward:F6}, expected up {expectedUp:F6}, actual {actual.up:F6}, determinant {actual.localToWorldMatrix.determinant:F6}.");
                maximumPositionError = Mathf.Max(maximumPositionError, positionError);
                maximumDirectionError = Mathf.Max(maximumDirectionError, directionError);
            }
            report.AppendLine($"  authored rig: {manifest.hierarchy.Length} nodes; maximum position error {maximumPositionError:F6}m; maximum direction error {maximumDirectionError:F6}; positive handedness");
        }

        private static Vector3 RigVector(float[] value, string id, string node)
        {
            if (value == null || value.Length != 3 || value.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidOperationException("Invalid authored rig vector: " + id + "/" + node);
            return new Vector3(value[0], value[1], value[2]);
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
