using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    [DefaultExecutionOrder(2000)]
    public sealed class PerformanceDiagnostics : MonoBehaviour
    {
        [Serializable]
        public sealed class Metric
        {
            public string name, unit;
            public int samples;
            public double mean, p50, p95, maximum;
        }

        [Serializable]
        public sealed class Report
        {
            public string label, graphicsApi, device, resolution;
            public bool frameTimingEnabled;
            public int sampledFrames;
            public double elapsedSeconds;
            public Metric[] metrics;
            public string[] unavailable;
        }

        private sealed class Counter
        {
            public string Name, Unit;
            public double Scale;
            public bool FloatingPoint;
            public ProfilerRecorder Recorder;
            public readonly List<double> Values = new List<double>(4096);
        }

        private static readonly HashSet<string> Requested = new HashSet<string>(StringComparer.Ordinal)
        {
            "Main Thread", "Render Thread", "GPU Frame Time", "CPU Total Frame Time",
            "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "CPU Main Thread Present Wait Time",
            "Draw Calls Count", "SetPass Calls Count", "Batches Count", "Triangles Count", "Vertices Count",
            "Shadow Casters Count", "GC Allocated In Frame", "GC Used Memory", "Total Used Memory",
            "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Gfx.WaitForGfxCommandsFromMainThread",
            "RenderPipelineManager.DoRenderLoop_Internal", "Camera.Render", "PlayerLoop", "GC.Collect"
        };

        private readonly List<Counter> _counters = new List<Counter>();
        private readonly Dictionary<string, List<double>> _timings = new Dictionary<string, List<double>>();
        private readonly FrameTiming[] _frameTiming = new FrameTiming[1];
        private readonly List<double> _wallFrames = new List<double>(4096);
        private readonly List<double> _cameraRenders = new List<double>(4096);
        private int _cameraRendersThisFrame;
        private bool _collecting;
        private string _label;
        private double _started;
        private ulong _lastFrameTimestamp;

        public static PerformanceDiagnostics Attach(GameObject owner)
        {
            var diagnostics = owner.GetComponent<PerformanceDiagnostics>();
            return diagnostics != null ? diagnostics : owner.AddComponent<PerformanceDiagnostics>();
        }

        // Begin after scene warmup; end before readback, encoding or screenshot work.
        public void BeginSample(string label)
        {
            DisposeRecorders();
            _label = label;
            _wallFrames.Clear();
            _cameraRenders.Clear();
            _cameraRendersThisFrame = 0;
            _timings.Clear();
            _lastFrameTimestamp = 0;
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var included = new HashSet<string>();
            foreach (var handle in handles)
            {
                var descriptor = ProfilerRecorderHandle.GetDescription(handle);
                if (!Requested.Contains(descriptor.Name) || !included.Add(descriptor.Name)) continue;
                string unit = descriptor.UnitType.ToString();
                double scale = unit == "TimeNanoseconds" ? .000001 : 1;
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default);
                recorder.Start();
                _counters.Add(new Counter
                {
                    Name = descriptor.Name, Unit = scale < 1 ? "ms" : unit, Scale = scale, Recorder = recorder,
                    FloatingPoint = descriptor.DataType == ProfilerMarkerDataType.Double || descriptor.DataType == ProfilerMarkerDataType.Float
                });
            }
            _started = Time.realtimeSinceStartupAsDouble;
            _collecting = true;
            RenderPipelineManager.endCameraRendering += CameraRendered;
        }

        private void LateUpdate()
        {
            if (!_collecting) return;
            _wallFrames.Add(Time.unscaledDeltaTime * 1000.0);
            _cameraRenders.Add(_cameraRendersThisFrame);
            _cameraRendersThisFrame = 0;
            foreach (var counter in _counters)
                if (counter.Recorder.Valid && counter.Recorder.Count > 0)
                {
                    double value = (counter.FloatingPoint ? counter.Recorder.LastValueAsDouble : counter.Recorder.LastValue) * counter.Scale;
                    if (counter.Name != "GPU Frame Time" || value > 0) counter.Values.Add(value);
                }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _frameTiming) == 0) return;
            var frame = _frameTiming[0];
            if (frame.frameStartTimestamp == _lastFrameTimestamp) return;
            _lastFrameTimestamp = frame.frameStartTimestamp;
            AddTiming("FrameTiming CPU frame", frame.cpuFrameTime);
            AddTiming("FrameTiming CPU main", frame.cpuMainThreadFrameTime);
            AddTiming("FrameTiming CPU render", frame.cpuRenderThreadFrameTime);
            AddTiming("FrameTiming present wait", frame.cpuMainThreadPresentWaitTime);
            AddTiming("FrameTiming GPU", frame.gpuFrameTime);
        }

        private void CameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (_collecting && camera.cameraType == CameraType.Game) _cameraRendersThisFrame++;
        }

        private void AddTiming(string name, double value)
        {
            // A missing GPU timing must not be represented as a zero-cost GPU frame.
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) return;
            if (!_timings.TryGetValue(name, out var values)) _timings.Add(name, values = new List<double>(4096));
            values.Add(value);
        }

        public Report EndSample(string outputPath = null)
        {
            _collecting = false;
            var metrics = new List<Metric>
            {
                Summarize("Wall frame", "ms", _wallFrames),
                Summarize("Game camera renders per frame", "Count", _cameraRenders)
            };
            metrics.AddRange(_counters.Where(c => c.Values.Count > 0).Select(c => Summarize(c.Name, c.Unit, c.Values)));
            metrics.AddRange(_timings.Select(pair => Summarize(pair.Key, "ms", pair.Value)));
            var unavailable = Requested.Where(name => !metrics.Any(metric => metric.name == name)).ToList();
            if (!_timings.ContainsKey("FrameTiming GPU")) unavailable.Add("FrameTiming GPU");
            var report = new Report
            {
                label = _label, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
                resolution = Screen.width + "x" + Screen.height, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(),
                sampledFrames = _wallFrames.Count, elapsedSeconds = Time.realtimeSinceStartupAsDouble - _started,
                metrics = metrics.ToArray(), unavailable = unavailable.ToArray()
            };
            DisposeRecorders();
            if (!string.IsNullOrEmpty(outputPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            }
            return report;
        }

        private static Metric Summarize(string name, string unit, List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return new Metric
            {
                name = name, unit = unit, samples = sorted.Length,
                mean = sorted.Length == 0 ? 0 : sorted.Average(),
                p50 = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .50)],
                p95 = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .95)],
                maximum = sorted.Length == 0 ? 0 : sorted[sorted.Length - 1]
            };
        }

        private void DisposeRecorders()
        {
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            foreach (var counter in _counters) counter.Recorder.Dispose();
            _counters.Clear();
        }

        private void OnDestroy() => DisposeRecorders();
    }
}
