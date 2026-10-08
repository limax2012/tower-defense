using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D
{
    [DefaultExecutionOrder(1000)]
    public sealed class ReviewCapture : MonoBehaviour
    {
        private RenderTexture _target;
        private RenderTexture _cameraTarget;
        private Camera _camera;
        private GameInterface _interface;
        private RenderTexture _previousTarget;
        private bool _previousCameraEnabled;
        private Texture2D _readback;
        private readonly RenderPipeline.StandardRequest _request = new RenderPipeline.StandardRequest();
        public int Width => _target.width;
        public int Height => _target.height;
        private bool _includeInterface = true;
        public bool IncludeInterface
        {
            get => _includeInterface;
            set
            {
                _includeInterface = value;
                if (_interface != null) _interface.SetCaptureTarget(value ? _target : null);
            }
        }

        public static ReviewCapture Initialize(Camera camera, GameInterface gameInterface, int width = 1920, int height = 1080)
        {
            var capture = camera.GetComponent<ReviewCapture>();
            if (capture != null) return capture;
            capture = camera.gameObject.AddComponent<ReviewCapture>();
            capture._camera = camera;
            capture._interface = gameInterface;
            capture._previousTarget = camera.targetTexture;
            capture._previousCameraEnabled = camera.enabled;
            capture._target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Review / camera and interface",
                antiAliasing = 1,
                useMipMap = false
            };
            capture._target.Create();
            capture._cameraTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Review / HDR camera",
                antiAliasing = UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.msaaSampleCount : 1,
                useMipMap = false
            };
            capture._cameraTarget.Create();
            camera.targetTexture = capture._cameraTarget;
            gameInterface.SetCaptureTarget(capture._target);
            capture._request.destination = capture._cameraTarget;
            capture._readback = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            ValidateColorConversion();
            return capture;
        }

        private void LateUpdate()
        {
            // Hidden players may skip swapchain rendering. Request the full URP camera stack explicitly.
            if (_camera == null || _cameraTarget == null) return;
            _camera.enabled = false;
            RenderPipeline.SubmitRenderRequest(_camera, _request);
            bool previousSrgb = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(_cameraTarget, _target);
            GL.sRGBWrite = previousSrgb;
        }

        // Capture after WaitForEndOfFrame so both the camera and UI panel have rendered.
        public void Capture(string path)
        {
            ReadTarget();
            var colors = _readback.GetPixels32();
            int minimum = 255, maximum = 0;
            long brightness = 0;
            for (int i = 0; i < colors.Length; i += 17)
            {
                int value = Mathf.Max(colors[i].r, Mathf.Max(colors[i].g, colors[i].b));
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
                brightness += value;
            }
            if (maximum - minimum < 12 || brightness < colors.Length / 17 * 3)
                throw new InvalidOperationException("Review image has no meaningful rendered content: " + path);
            byte[] png = _readback.EncodeToPNG();
            if (png == null || png.Length < 1024) throw new InvalidOperationException("Review image encoding failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, png);
            if (!File.Exists(path) || new FileInfo(path).Length != png.Length)
                throw new IOException("Review image was not saved: " + path);
            Debug.Log("Review captured " + path + " (" + png.Length + " bytes)");
        }

        // Packed sRGB RGB24 pixels, row zero at the bottom; call after WaitForEndOfFrame.
        public byte[] ReadRgb24()
        {
            ReadTarget();
            return _readback.GetRawTextureData<byte>().ToArray();
        }

        private void ReadTarget()
        {
            if (_target == null || !_target.IsCreated()) throw new InvalidOperationException("Review render target is unavailable.");
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                _readback.Apply(false, false);
            }
            finally { RenderTexture.active = previous; }
        }

        private static void ValidateColorConversion()
        {
            var previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            var linear = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var encoded = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(4, 4, TextureFormat.RGB24, false, false);
            try
            {
                RenderTexture.active = linear;
                GL.sRGBWrite = false;
                GL.Clear(false, true, new Color(.18f, .18f, .18f, 1));
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(linear, encoded);
                RenderTexture.active = encoded;
                pixels.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                pixels.Apply();
                int value = pixels.GetPixels32()[0].r;
                int expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? 118 : 46;
                if (Mathf.Abs(value - expected) > 4)
                    throw new InvalidOperationException("Review color conversion: 18% linear gray produced " + value + ", expected " + expected);
                Debug.Log("Review color calibration: 18% linear gray -> " + value + "/255 (expected " + expected + ").");
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(linear);
                RenderTexture.ReleaseTemporary(encoded);
                Destroy(pixels);
            }
        }

        private void OnDestroy()
        {
            if (_camera != null)
            {
                _camera.targetTexture = _previousTarget;
                _camera.enabled = _previousCameraEnabled;
            }
            if (_interface != null) _interface.SetCaptureTarget(null);
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_cameraTarget != null) { _cameraTarget.Release(); Destroy(_cameraTarget); }
            if (_readback != null) Destroy(_readback);
        }
    }
}
