using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D
{
    public sealed class BastionApplication : MonoBehaviour
    {
        public VisualCatalog catalog;
        public SimulationHost Host { get; private set; }
        public BattlefieldView Battlefield { get; private set; }
        public BattlefieldCamera CameraRig { get; private set; }
        public GameInterface Interface { get; private set; }
        public string FatalError { get; private set; }
        private readonly System.Collections.Generic.List<string> _initializationErrors = new System.Collections.Generic.List<string>();

        private void Awake()
        {
            Application.targetFrameRate = 120;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            Application.logMessageReceived += CaptureInitializationError;
            try
            {
                if (!catalog) catalog = Resources.Load<VisualCatalog>("VisualCatalog");
                if (!catalog) throw new InvalidOperationException("VisualCatalog is missing. Configure the Unity project before running.");
                Host = gameObject.AddComponent<SimulationHost>();
                Host.Initialize();
                var cameraObject = new GameObject("Free camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.nearClipPlane = .08f; camera.farClipPlane = 250; camera.fieldOfView = 51;
                camera.allowHDR = true; camera.allowMSAA = true;
                camera.backgroundColor = new Color(.022f, .035f, .051f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                cameraObject.AddComponent<AudioListener>();
                var extra = camera.GetUniversalAdditionalCameraData();
                extra.renderPostProcessing = true;
                extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                extra.antialiasingQuality = AntialiasingQuality.High;
                CameraRig = cameraObject.AddComponent<BattlefieldCamera>();
                CameraRig.Host = Host;
                CameraRig.Sensitivity = PlayerPrefs.GetFloat("CameraSensitivity", 1);
                BuildLighting();
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--audio-diagnostics") >= 0)
                {
                    ReviewAudioCapture.StartProbe(gameObject);
                    return;
                }
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--render-diagnostics") >= 0)
                {
                    gameObject.AddComponent<RenderDiagnostics>().Initialize(camera);
                    return;
                }
                Battlefield = new GameObject("Battlefield").AddComponent<BattlefieldView>();
                Battlefield.Initialize(Host, catalog, camera);
                var reflections = new GameObject("Foundry reflections").AddComponent<ReflectionProbe>();
                reflections.transform.position = new Vector3(0, 4, 0);
                reflections.mode = ReflectionProbeMode.Custom;
                reflections.resolution = 128;
                reflections.size = new Vector3(80, 35, 70);
                reflections.boxProjection = true;
                reflections.hdr = true;
                reflections.cullingMask = (1 << 0) | (1 << 8);
                Interface = gameObject.AddComponent<GameInterface>();
                Interface.Initialize(Host, CameraRig);
                CameraRig.PointerOverInterface = Interface.PointerOverInterface;
                Battlefield.PointerOverInterface = Interface.PointerOverInterface;
                gameObject.AddComponent<FoundryAtmosphere>().Initialize(Host);
                gameObject.AddComponent<RuntimeVerification>().Initialize(this);
                gameObject.AddComponent<CombatReview>().Initialize(this);
                gameObject.AddComponent<CampaignReplay>().Initialize(this);
                gameObject.AddComponent<ModelMotionReview>().Initialize(this);
                if (_initializationErrors.Count > 0) throw new InvalidOperationException("Scene initialization reported errors: " + string.Join("\n", _initializationErrors));
            }
            catch (Exception e)
            {
                FatalError = e.ToString(); Debug.LogException(e);
                if (Host != null && Host.VerificationMode) Application.Quit(1);
            }
            finally { Application.logMessageReceived -= CaptureInitializationError; }
        }

        private void CaptureInitializationError(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && _initializationErrors.Count < 8)
                _initializationErrors.Add(message);
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.24f, .32f, .43f);
            RenderSettings.ambientEquatorColor = new Color(.12f, .16f, .23f);
            RenderSettings.ambientGroundColor = new Color(.07f, .05f, .055f);
            RenderSettings.ambientIntensity = 1;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.22f, .27f, .34f));
            ambient.AddDirectionalLight(Vector3.up, new Color(.33f, .40f, .51f), .45f);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .010f;
            RenderSettings.fogColor = new Color(.046f, .068f, .094f);
            var sun = new GameObject("Cool factory skylight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(.74f, .84f, 1f);
            sun.intensity = 1.25f; sun.shadows = LightShadows.Soft; sun.shadowBias = .025f; sun.shadowNormalBias = .12f;
            sun.transform.rotation = Quaternion.Euler(46, -32, 0);
            RenderSettings.sun = sun;
            var rim = new GameObject("Warm furnace bounce").AddComponent<Light>();
            rim.type = LightType.Directional; rim.color = new Color(1, .46f, .19f); rim.intensity = .18f;
            rim.transform.rotation = Quaternion.Euler(22, 148, 0); rim.shadows = LightShadows.None;
            var volume = new GameObject("Foundry grade").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = volume.profile.Add<Bloom>();
            bloom.threshold.Override(1.3f); bloom.intensity.Override(.28f); bloom.scatter.Override(.58f);
            var tone = volume.profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var adjust = volume.profile.Add<ColorAdjustments>();
            adjust.postExposure.Override(.15f); adjust.contrast.Override(8); adjust.saturation.Override(5);
            var vignette = volume.profile.Add<Vignette>(); vignette.intensity.Override(.17f); vignette.smoothness.Override(.45f);
        }

        private void OnGUI()
        {
            if (FatalError == null) return;
            GUI.color = Color.white;
            GUI.Box(new Rect(30, 30, Screen.width - 60, Screen.height - 60), "Cinderworks could not start");
            GUI.TextArea(new Rect(50, 75, Screen.width - 100, Screen.height - 125), FatalError);
        }
    }

}
