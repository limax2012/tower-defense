using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation;
using UnityEngine;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public sealed class RuntimeVerification : MonoBehaviour
    {
        private BastionApplication _app;
        private AutoPlayer _player;
        private float _react;
        private bool _autoplay;
        private readonly List<float> _frames = new List<float>();
        private readonly List<string> _errors = new List<string>();
        private int _peakEnemies, _peakTowers;
        private string _directory;
        private bool _measuring;
        private ReviewCapture _capture;
        public void Initialize(BastionApplication app)
        {
            _app = app;
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--verify") && !args.Contains("--tour") && !args.Contains("--autoplay") && !args.Contains("--record-backend")) return;
            _directory = GetArgument(args, "--capture-directory") ?? Path.Combine(Application.persistentDataPath, "Verification");
            Directory.CreateDirectory(_directory);
            if (args.Contains("--record-backend")) { StartCoroutine(RecordBackend()); return; }
            _capture = ReviewCapture.Initialize(app.CameraRig.GetComponent<Camera>(), app.Interface);
            Application.logMessageReceived += CaptureLog;
            if (args.Contains("--beam-lifecycle-only")) { StartCoroutine(VerifyBeamLifecycle()); return; }
            _autoplay = args.Contains("--autoplay");
            StartCoroutine(Run(args.Contains("--tour")));
        }
        private static string GetArgument(string[] args, string key)
        {
            int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        private IEnumerator RecordBackend()
        {
            yield return null;
            try
            {
                string nativeFixture = Path.Combine(Application.streamingAssetsPath, "Verification/runtime-conformance.json");
                string reference = MaximalBastion.Gameplay.RuntimeConformance.RecordReference(_app.Host.Content, File.ReadAllText(nativeFixture));
                File.WriteAllText(Path.Combine(_directory, "runtime-conformance-unity.json"), reference);
                File.WriteAllText(Path.Combine(_directory, "backend-reference.txt"),
                    "Unity " + Application.unityVersion + " / " + SystemInfo.operatingSystem + " / " + Environment.Version + "\nRecorded by packaged player; verify in a separate process.\n");
                Application.Quit(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(_directory, "backend-error.txt"), e.ToString()); Debug.LogException(e); Application.Quit(1); }
        }
        private void CaptureLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message + "\n" + stack);
        }
        private IEnumerator VerifyBeamLifecycle()
        {
            yield return new WaitForSecondsRealtime(1);
            yield return BeamLifecycleVerification.Run(_app, Require, _directory, _capture);
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }
        private IEnumerator Run(bool tour)
        {
            yield return new WaitForSecondsRealtime(2);
            _capture.Capture(Path.Combine(_directory, "00-main-menu.png"));
            var host = _app.Host;
            string fixture = Path.Combine(Application.streamingAssetsPath, "Verification/runtime-conformance-unity.json");
            if (File.Exists(fixture))
            {
                var conformance = MaximalBastion.Gameplay.RuntimeConformance.Verify(host.Content, File.ReadAllText(fixture));
                File.WriteAllText(Path.Combine(_directory, "runtime-conformance.txt"),
                    $"Passed: {conformance.Passed}\nRuntime: {conformance.Runtime}\nScenarios: {conformance.ScenariosCompleted}\nTicks: {conformance.TicksRun}\nSamples: {conformance.SamplesChecked}\nSnapshots: {conformance.SnapshotsChecked}\n" + string.Join("\n", conformance.Failures));
                Require(conformance.Passed, "Same-backend gameplay conformance");
            }
            else Require(false, "Runtime conformance fixture exists");
            string nativeFixture = Path.Combine(Application.streamingAssetsPath, "Verification/runtime-conformance.json");
            if (File.Exists(nativeFixture))
            {
                var comparison = MaximalBastion.Gameplay.RuntimeBehaviorComparison.Compare(host.Content, File.ReadAllText(nativeFixture));
                File.WriteAllText(Path.Combine(_directory, "cross-runtime-comparison.txt"),
                    $"Final outcomes match: {comparison.FinalOutcomesMatch}\nRaw checksum mismatches: {comparison.ChecksumMismatches}\n" +
                    $"Intermediate discrete differences: {comparison.DiscreteDifferences}\nOut-of-tolerance numeric fields: {comparison.OutOfToleranceNumericFields}\n" +
                    "Cross-runtime lockstep is not supported.\n" + string.Join("\n", comparison.FinalOutcomeDifferences.Concat(comparison.Failures)));
                Require(comparison.FinalOutcomesMatch, "Native and Mono scenario final outcomes");
            }
            if (!tour && !_autoplay) yield return BeamLifecycleVerification.Run(_app, Require, _directory, _capture);
            host.NewRun("easy", "standard");
            if (_autoplay)
            {
                _player = new AutoPlayer(host.Session, AutoPlayerStrategy.Experienced, 7421, null, command => host.Command(command));
                _player.PrepareForWave(host.Session);
            }
            else
            {
                Require(host.Command(GameCommandType.PlaceTower, choice: "needle_turret", position: new SimVector(210, 175)), "Needle placement");
                Require(host.Command(GameCommandType.PlaceTower, choice: "shard_fan", position: new SimVector(280, 175)), "Shard placement");
                Require(host.Command(GameCommandType.PlaceTower, choice: "frost_spire", position: new SimVector(310, 330)), "Frost placement");
            }
            yield return CameraInteractionVerification.Run(_app, Require, _directory);
            host.Save(0, false);
            Require(host.Saves.GetSlots().Any(s => s.IsOccupied), "Checkpoint persisted");
            Require(host.Command(GameCommandType.StartWave), "Start wave");
            host.BeforeStep += Autoplay;
            yield return new WaitForSecondsRealtime(5);
            _measuring = true;
            _app.CameraRig.SetView(new Vector3(0, .5f, -3), 210, 52, 57, true);
            yield return Capture("01-overhead");
            _app.CameraRig.SetView(new Vector3(-10, 1, -8), 120, 8, 13, true);
            yield return Capture("02-ground-combat");
            _app.CameraRig.SetView(new Vector3(-10, 1, -8), 245, 20, 16, true);
            yield return Capture("03-rear-combat");
            _app.CameraRig.SetView(Vector3.zero, 180, 73, 44, true);
            yield return Capture("04-reverse-overhead");
            _app.CameraRig.Overview();
            if (tour || _autoplay)
            {
                float until = Time.realtimeSinceStartup + (_autoplay ? 2100 : 45);
                while (Time.realtimeSinceStartup < until && !host.Session.IsVictory && !host.Session.IsDefeat)
                {
                    if (tour) _app.CameraRig.Yaw += 6 * Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            yield return Capture("05-final");
            WriteReport();
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();
            _capture.Capture(Path.Combine(_directory, name + ".png"));
            yield return null;
        }
        private void Autoplay()
        {
            if (!_autoplay) return;
            var s = _app.Host.Session;
            if (s.IsVictory || s.IsDefeat) return;
            if (!s.Waves.IsActive && s.Enemies.Count == 0 && s.CanStartWave)
            {
                _player.PrepareForWave(s);
                _app.Host.Command(GameCommandType.StartWave);
            }
            if ((_react -= SimulationHost.Step) <= 0)
            {
                _react = .5f;
                _player.ReactDuringWave(s);
            }
        }
        private void Update()
        {
            if (!_measuring) return;
            _frames.Add(Time.unscaledDeltaTime * 1000);
            var s = _app.Host.Session;
            if (s == null) return;
            _peakEnemies = Mathf.Max(_peakEnemies, s.Enemies.Count);
            _peakTowers = Mathf.Max(_peakTowers, s.Towers.Count);
        }
        private void Require(bool condition, string name)
        {
            if (!condition) _errors.Add("FAILED: " + name);
            else Debug.Log("PASS: " + name);
        }
        private void WriteReport()
        {
            var sorted = _frames.OrderBy(x => x).ToArray();
            float avg = sorted.Length == 0 ? 0 : sorted.Average();
            float p95 = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .95f)];
            var s = _app.Host.Session;
            File.WriteAllText(Path.Combine(_directory, "runtime-report.txt"),
                $"GPU: {SystemInfo.graphicsDeviceName}\nRendered resolution: 1920x1080\n" +
                $"Frames: {sorted.Length}\nMean frame ms: {avg:F3}\nP95 frame ms: {p95:F3}\nMean FPS: {(avg > 0 ? 1000 / avg : 0):F1}\n" +
                $"Peak enemies: {_peakEnemies}\nPeak towers: {_peakTowers}\nWave: {s.CurrentWave}\nVictory: {s.IsVictory}\nDefeat: {s.IsDefeat}\n" +
                "Errors: " + _errors.Count + "\n" + string.Join("\n", _errors));
        }
        private void OnDestroy() => Application.logMessageReceived -= CaptureLog;
    }
}
