using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using UnityEngine;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public sealed class CombatReview : MonoBehaviour
    {
        private BastionApplication _app;
        private ReviewCapture _capture;
        private string _directory;
        private readonly List<float> _frames = new List<float>();
        private readonly List<string> _errors = new List<string>();
        private int _peakEnemies, _peakTowers;
        private bool _measuring;
        private PerformanceDiagnostics _performance;

        public void Initialize(BastionApplication app)
        {
            if (!Environment.GetCommandLineArgs().Contains("--combat-review")) return;
            _app = app;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--capture-directory");
            _directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "CombatReview");
            Directory.CreateDirectory(_directory);
            _capture = ReviewCapture.Initialize(app.CameraRig.GetComponent<Camera>(), app.Interface);
            _performance = PerformanceDiagnostics.Attach(gameObject);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run(args.Contains("--video")));
        }

        private IEnumerator Run(bool video)
        {
            yield return new WaitForSecondsRealtime(2);
            var host = _app.Host;
            host.NewRun("easy", host.Content.Challenges.Values.First(c => c.IsSandbox).Id);
            string[] ids = { "needle_turret", "shard_fan", "watchtower", "frost_spire", "ember_coil", "breaker_cannon", "arc_relay", "siege_mortar", "prism_beam", "signal_beacon" };
            var positions = new[] { new SimVector(210,175), new SimVector(280,175), new SimVector(560,200), new SimVector(240,330), new SimVector(300,420),
                new SimVector(300,330), new SimVector(180,400), new SimVector(430,200), new SimVector(490,200), new SimVector(180,340) };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!host.Command(GameCommandType.PlaceTower, choice: ids[i], position: positions[i])) { _errors.Add("Cannot place " + ids[i]); continue; }
                var tower = host.Session.Towers.Last();
                host.Command(GameCommandType.ChooseDoctrine, tower.Id, tower.Definition.Tier2Doctrines.First().Id);
                host.Command(GameCommandType.SpecializeTower, tower.Id, tower.Definition.Specializations.First().Id);
                if (i % 3 == 0) host.Command(GameCommandType.UpgradeTower, tower.Id);
            }
            host.Select(0);
            host.Session.StartSandboxWave(30);
            yield return SpawnRoster();
            _capture.IncludeInterface = false;
            _app.CameraRig.SetView(new Vector3(0, .5f, -3), 205, 52, 65, true);
            yield return new WaitForSecondsRealtime(3);
            _measuring = !video;
            yield return Shot("01-combat-overhead");
            _app.CameraRig.SetView(new Vector3(-10, 1.2f, -6), 145, 10, 18, true);
            yield return new WaitForSecondsRealtime(5);
            yield return Shot("02-combat-ground");
            _app.CameraRig.SetView(new Vector3(-9, 1, -7), 295, 22, 23, true);
            yield return new WaitForSecondsRealtime(5);
            yield return Shot("03-combat-rear");
            _app.CameraRig.SetView(new Vector3(.5f, 1.4f, -8), 170, 12, 15, true);
            yield return new WaitForSecondsRealtime(5);
            yield return Shot("04-prism-mortar");
            _app.CameraRig.Overview();
            for (int i = 0; i < 12; i++)
            {
                var point = new SimVector(55 + i % 6 * 65, 520 + i / 6 * 75);
                if (!host.Command(GameCommandType.PlaceTower, choice: ids[i % ids.Length], position: point)) continue;
                var tower = host.Session.Towers.Last();
                host.Command(GameCommandType.ChooseDoctrine, tower.Id, tower.Definition.Tier2Doctrines.First().Id);
                host.Command(GameCommandType.SpecializeTower, tower.Id, tower.Definition.Specializations.First().Id);
            }
            yield return SpawnRoster();
            host.Session.StartSandboxWave(30);
            if (!video) _performance.BeginSample("Cinderworks dense combat / 22 towers");
            float until = Time.realtimeSinceStartup + 25;
            while (Time.realtimeSinceStartup < until) { _app.CameraRig.Yaw += 2 * Time.unscaledDeltaTime; yield return null; }
            if (!video) _performance.EndSample(Path.Combine(_directory, "performance-diagnostics.json"));
            yield return Shot("05-dense-combat");
            _measuring = false;
            if (video) yield return RecordVideo();
            var stats = host.Session.Towers.GroupBy(t => t.Definition.Id).Select(g => g.Key + ": damage=" + g.Sum(t => t.LifetimeDamage).ToString("F1") + ", kills=" + g.Sum(t => t.LifetimeKills) + ", support=" + g.Sum(t => t.LifetimeSupportDamageEquivalent).ToString("F1"));
            var sorted = _frames.OrderBy(x => x).ToArray();
            string timing = sorted.Length > 0 ? $"Mean frame ms: {sorted.Average():F3}\nP95 frame ms: {sorted[(int)((sorted.Length - 1) * .95f)]:F3}\n" : "Recording run; no performance claim.\n";
            File.WriteAllText(Path.Combine(_directory, "combat-review.txt"),
                $"Sandbox visual stress scenario; not campaign verification.\nGPU: {SystemInfo.graphicsDeviceName}\nRendered resolution: {_capture.Width}x{_capture.Height}\nFrames: {sorted.Length}\nPeak enemies: {_peakEnemies}\nPeak towers: {_peakTowers}\n" + timing + string.Join("\n", stats) + "\nErrors: " + _errors.Count + "\n" + string.Join("\n", _errors));
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }

        private IEnumerator SpawnRoster()
        {
            var session = _app.Host.Session;
            string[] ids = { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" };
            EnemySignalRole[] roles = { EnemySignalRole.Accelerator, EnemySignalRole.Restorer, EnemySignalRole.Bulwark, EnemySignalRole.Jammer, EnemySignalRole.Disruptor };
            for (int i = 0; i < ids.Length; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    session.SpawnSandboxTargets(ids[i], 1, i < 2 ? 18 : 4, i == 4 ? "Elite" : "Standard", false, roles[i]);
                    yield return new WaitForSeconds(.6f);
                }
            }
        }

        private IEnumerator Shot(string name)
        {
            bool measure = _measuring; _measuring = false;
            yield return new WaitForEndOfFrame();
            _capture.Capture(Path.Combine(_directory, name + ".png"));
            yield return null;
            _measuring = measure;
        }

        private IEnumerator RecordVideo()
        {
            string path = Path.Combine(_directory, "Cinderworks-combat-tour.mp4");
            var info = new ProcessStartInfo("ffmpeg", $"-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size {_capture.Width}x{_capture.Height} -framerate 30 -i pipe:0 -vf vflip -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"{path}\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardError = true };
            using (var process = new Process { StartInfo = info })
            {
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogWarning(e.Data); };
                if (!process.Start()) throw new InvalidOperationException("Video encoder did not start.");
                process.BeginErrorReadLine();
                var session = _app.Host.Session;
                session.ClearSandboxTargets();
                string[] enemies = { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" };
                // Sandbox spacing exposes every weapon along the authored path for the camera tour.
                for (int i = 0; i < 15; i++)
                {
                    session.SpawnSandboxTargets(enemies[i % enemies.Length], 1, 8, "Standard", false);
                    var enemy = session.Enemies.Last();
                    enemy.UpdateMovement((60 + i * 86 - enemy.DistanceAlongPath) / enemy.CurrentSpeed, session.Map.Path);
                }
                yield return new WaitForSecondsRealtime(2);
                Time.captureFramerate = 30;
                for (int frame = 0; frame < 900; frame++)
                {
                    if (frame % 45 == 0)
                        session.SpawnSandboxTargets(enemies[(frame / 45) % enemies.Length], 1, 8, "Standard", false);
                    float t = frame / 899f;
                    _app.CameraRig.SetView(new Vector3(-6, 1.5f, -6), 135 + t * 290, 12 + Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2) * 45, 23 + Mathf.Sin(t * Mathf.PI) * 24);
                    yield return new WaitForEndOfFrame();
                    byte[] pixels = _capture.ReadRgb24();
                    process.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                }
                Time.captureFramerate = 0;
                process.StandardInput.Close();
                if (!process.WaitForExit(15000) || process.ExitCode != 0) _errors.Add("Video encoding did not complete successfully.");
            }
        }

        private void Update()
        {
            if (!_measuring || _app.Host.Session == null) return;
            _frames.Add(Time.unscaledDeltaTime * 1000);
            _peakEnemies = Mathf.Max(_peakEnemies, _app.Host.Session.Enemies.Count);
            _peakTowers = Mathf.Max(_peakTowers, _app.Host.Session.Towers.Count);
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message + "\n" + stack); }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; Time.captureFramerate = 0; }
    }
}
