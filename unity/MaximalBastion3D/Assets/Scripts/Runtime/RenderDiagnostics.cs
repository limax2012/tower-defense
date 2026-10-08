using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D
{
    public sealed class RenderDiagnostics : MonoBehaviour
    {
        private Camera _camera;
        private string _directory;
        private readonly List<string> _report = new List<string>();
        private readonly Dictionary<string, int> _coverage = new Dictionary<string, int>();

        public void Initialize(Camera camera)
        {
            _camera = camera;
            var rig = camera.GetComponent<BattlefieldCamera>();
            if (rig) rig.enabled = false;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--capture-directory");
            _directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "RenderDiagnostics");
            Directory.CreateDirectory(_directory);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            try
            {
                _camera.enabled = false;
                _camera.transform.SetPositionAndRotation(new Vector3(0, 0, -2), Quaternion.identity);
                _camera.orthographic = true;
                _camera.orthographicSize = .7f;
                _camera.aspect = 1;
                _camera.cullingMask = 1 << 30;
                _camera.backgroundColor = Color.black;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.allowMSAA = true;
                var extra = _camera.GetUniversalAdditionalCameraData();
                extra.volumeLayerMask = 0;
                extra.renderPostProcessing = true;
                extra.antialiasingQuality = AntialiasingQuality.High;
                RenderSettings.fog = false;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0;
                RenderSettings.reflectionIntensity = 0;
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.layer = 30;
                quad.transform.localScale = new Vector3(1.15f, .3f, 1);
                quad.transform.rotation = Quaternion.Euler(0, 0, 17);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.black);
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", 0);
                material.EnableKeyword("_EMISSION");
                material.SetVector("_EmissionColor", Vector4.one);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                _report.Add("Unity " + Application.unityVersion + " | " + SystemInfo.graphicsDeviceName + " | pipeline MSAA=" + UniversalRenderPipeline.asset.msaaSampleCount);
                _report.Add("Same URP StandardRequest, HDR target and linear-to-sRGB blit as ReviewCapture; no volume effects.");
                Probe("01-no-aa", 1, AntialiasingMode.None);
                Probe("02-msaa4", 4, AntialiasingMode.None);
                Probe("03-smaa", 1, AntialiasingMode.SubpixelMorphologicalAntiAliasing);
                Probe("04-msaa4-smaa", 4, AntialiasingMode.SubpixelMorphologicalAntiAliasing);
                bool antialiasingRetained = _coverage["01-no-aa"] == 0 && _coverage["02-msaa4"] > 100 &&
                    _coverage["03-smaa"] > 100 && _coverage["04-msaa4-smaa"] > 100;
                _report.Add("AA coverage verified: " + antialiasingRetained);
                quad.transform.SetPositionAndRotation(new Vector3(0, 0, 78), Quaternion.identity);
                quad.transform.localScale = Vector3.one * 2;
                RenderSettings.fog = false;
                float clear = Probe("05-fog-off-80m", 1, AntialiasingMode.None);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = .01f;
                RenderSettings.fogColor = Color.black;
                float fog = Probe("06-fog-exp2-80m", 1, AntialiasingMode.None);
                bool fogRetained = fog / clear > .48f && fog / clear < .58f;
                _report.Add("Exp2 attenuation=" + (fog / clear).ToString("F6") + " expected approximately 0.528; retained=" + fogRetained);
                File.WriteAllLines(Path.Combine(_directory, "render-diagnostics.txt"), _report);
                Debug.Log(string.Join("\n", _report));
                Application.Quit(antialiasingRetained && fogRetained ? 0 : 1);
            }
            catch (Exception error)
            {
                _report.Add(error.ToString());
                File.WriteAllLines(Path.Combine(_directory, "render-diagnostics.txt"), _report);
                Debug.LogException(error);
                Application.Quit(1);
            }
        }

        private float Probe(string name, int samples, AntialiasingMode antialiasing)
        {
            const int size = 256;
            var extra = _camera.GetUniversalAdditionalCameraData();
            extra.antialiasing = antialiasing;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { antiAliasing = samples };
            var encoded = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var linearPixels = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(size, size, TextureFormat.RGB24, false, false);
            var resolved = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); encoded.Create(); resolved.Create();
            _camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            for (int frame = 0; frame < 3; frame++) RenderPipeline.SubmitRenderRequest(_camera, request);
            GL.sRGBWrite = false;
            Graphics.Blit(target, resolved);
            RenderTexture.active = resolved;
            linearPixels.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            linearPixels.Apply(false, false);
            var values = linearPixels.GetPixels();
            int fractional = 0;
            foreach (var color in values) if (color.r > .01f && color.r < .99f) fractional++;
            _coverage[name] = fractional;
            float centre = values[size / 2 * size + size / 2].r;
            GL.sRGBWrite = true;
            Graphics.Blit(target, encoded);
            RenderTexture.active = encoded;
            png.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            png.Apply(false, false);
            File.WriteAllBytes(Path.Combine(_directory, name + ".png"), png.EncodeToPNG());
            _report.Add(name + " | target samples=" + target.antiAliasing + " | fractional pixels=" + fractional + " | centre linear=" + centre.ToString("F6"));
            _camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release(); encoded.Release(); resolved.Release();
            Destroy(target); Destroy(encoded); Destroy(resolved); Destroy(linearPixels); Destroy(png);
            return centre;
        }
    }
}
