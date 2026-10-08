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
            if (!Environment.GetCommandLineArgs().Contains("--combat-review") && !Environment.GetCommandLineArgs().Contains("--wreckage-review")) return;
            _app = app;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--capture-directory");
            _directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "CombatReview");
            Directory.CreateDirectory(_directory);
            _capture = ReviewCapture.Initialize(app.CameraRig.GetComponent<Camera>(), app.Interface);
            _performance = PerformanceDiagnostics.Attach(gameObject);
            Application.logMessageReceived += OnLog;
            StartCoroutine(args.Contains("--wreckage-review") ? ReviewWreckage() : Run(args.Contains("--video"), args.Contains("--art-review")));
        }

        private IEnumerator ReviewWreckage()
        {
            yield return new WaitForSecondsRealtime(2);
            _app.Host.NewRun();
            _capture.IncludeInterface = false;
            Vector3 origin = new Vector3(200, .11f, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Chassis response fixture";
            floor.layer = 8;
            floor.transform.position = new Vector3(200, -.44f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            floor.GetComponent<Renderer>().sharedMaterial = _app.catalog.road;
            Time.captureFramerate = 60;
            var report = new List<string>();
            var wreckage = _app.Battlefield.GetComponent<EnemyWreckage>();
            wreckage.VerifySupportGeometry(report, _errors);
            Physics.SyncTransforms();
            Process encoder = null;
            if (Environment.GetCommandLineArgs().Contains("--video"))
            {
                string videoPath = Path.Combine(_directory, "Chassis-response.mp4");
                var info = new ProcessStartInfo("ffmpeg", $"-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size {_capture.Width}x{_capture.Height} -framerate 60 -i pipe:0 -vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \"{videoPath}\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardError = true };
                encoder = new Process { StartInfo = info };
                encoder.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogWarning(e.Data); };
                encoder.Start(); encoder.BeginErrorReadLine();
            }
            foreach (string id in new[] { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" })
            {
                var entry = _app.catalog.Find(id);
                var body = Instantiate(entry.prefab, origin, Quaternion.Euler(0, 25, 0));
                _app.CameraRig.SetView(origin + Vector3.up * .65f, 140, 12, 6.5f, true);
                for (int i = 0; i < 12; i++) { yield return new WaitForEndOfFrame(); WriteWreckageFrame(encoder); }
                _app.Battlefield.Effects.Defeat(origin + Vector3.up, entry.energy, _app.Host.Content.Enemies[id].Visual.Radius * Coordinates.Scale, body.transform);
                int emitted = _app.Battlefield.GetComponent<EnemyWreckage>().ActivePieces;
                if (emitted == 0) _errors.Add(id + " did not emit visible chassis pieces.");
                Destroy(body);
                float contactError = 0;
                int groundedSamples = 0;
                for (int frame = 0; frame < 90; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    WriteWreckageFrame(encoder);
                    if (frame % 6 == 0)
                    {
                        groundedSamples += wreckage.MeasureGroundedContact(out float error);
                        contactError = Mathf.Max(contactError, error);
                    }
                    if (frame == 6 || frame == 24 || frame == 48)
                        _capture.Capture(Path.Combine(_directory, id + "-" + frame.ToString("D2") + ".png"));
                }
                int fragments = _app.Battlefield.GetComponentsInChildren<MeshRenderer>().Count(r => r.name == "Armor");
                if (fragments != 0) _errors.Add(id + " left disabled chassis fragments after their lifetime.");
                if (contactError > .002f || groundedSamples == 0) _errors.Add(id + " chassis did not settle against its visible support geometry.");
                report.Add(id + ": emitted=" + emitted + ", remaining fragments=" + fragments);
                report.Add($"{id} ground contact: samples={groundedSamples}, maximum error={contactError:F6}m");
            }
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Raised chassis contact fixture"; deck.layer = 8;
            deck.transform.position = new Vector3(200, .46f, 0);
            deck.transform.localScale = new Vector3(5, .8f, 5);
            deck.GetComponent<Renderer>().sharedMaterial = _app.catalog.road;
            var overhead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            overhead.name = "Overhead collision fixture"; overhead.layer = 8;
            overhead.transform.position = new Vector3(200, 4, 0);
            overhead.transform.localScale = new Vector3(5, .3f, 5);
            var excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.name = "Non-environment collision fixture"; excluded.layer = 9;
            excluded.transform.position = new Vector3(204, .4f, 0);
            excluded.transform.localScale = new Vector3(1, .2f, 1);
            Physics.SyncTransforms();
            float deckHeight = EnemyWreckage.SurfaceHeight(origin, 1.5f);
            float roadHeight = EnemyWreckage.SurfaceHeight(new Vector3(204, 0, 0), 1.5f);
            float fallback = EnemyWreckage.SurfaceHeight(new Vector3(10000, 0, 10000), 1.5f);
            if (Mathf.Abs(deckHeight - .865f) > .001f || Mathf.Abs(roadHeight - .065f) > .001f || Mathf.Abs(fallback - .065f) > .001f)
                _errors.Add("Chassis floor sampling did not respect the raised deck, overhead geometry, excluded entities or road fallback.");
            report.Add($"Surface sampling: deck={deckHeight:F4}m, road={roadHeight:F4}m, fallback={fallback:F4}m");
            Destroy(overhead); Destroy(excluded);
            var raisedEntry = _app.catalog.Find("t3_brute");
            var raisedOrigin = new Vector3(200, .91f, 0);
            var raisedBody = Instantiate(raisedEntry.prefab, raisedOrigin, Quaternion.Euler(13, 37, -9));
            _app.CameraRig.SetView(raisedOrigin + Vector3.up * .65f, 140, 12, 6.5f, true);
            _app.Battlefield.Effects.Defeat(raisedOrigin + Vector3.up, raisedEntry.energy, 1, raisedBody.transform);
            Destroy(raisedBody);
            int raisedSamples = 0; float raisedError = 0;
            for (int frame = 0; frame < 90; frame++)
            {
                yield return new WaitForEndOfFrame();
                WriteWreckageFrame(encoder);
                if (frame % 6 == 0)
                {
                    raisedSamples += wreckage.MeasureGroundedContact(out float error);
                    raisedError = Mathf.Max(raisedError, error);
                }
                if (frame == 24 || frame == 48) _capture.Capture(Path.Combine(_directory, "raised-brute-" + frame.ToString("D2") + ".png"));
            }
            if (raisedError > .002f || raisedSamples == 0 || wreckage.ActivePieces != 0) _errors.Add("Chassis fragments did not settle and clear on the raised deck.");
            report.Add($"Raised deck: grounded samples={raisedSamples}, maximum error={raisedError:F6}m, remaining fragments={wreckage.ActivePieces}");
            if (encoder != null)
            {
                encoder.StandardInput.Close();
                if (!encoder.WaitForExit(15000) || encoder.ExitCode != 0) _errors.Add("Chassis video encoder did not finish successfully.");
                encoder.Dispose();
            }
            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(_directory, "wreckage-review.txt"), "Isolated presentation fixture; not gameplay evidence.\n" + string.Join("\n", report) + "\nErrors: " + _errors.Count + "\n" + string.Join("\n", _errors));
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }

        private void WriteWreckageFrame(Process encoder)
        {
            if (encoder == null) return;
            byte[] pixels = _capture.ReadRgb24();
            encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
        }

        private IEnumerator Run(bool video, bool art)
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
            _measuring = !video && !art;
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
            if (!video && !art) _performance.BeginSample("Cinderworks dense combat / 22 towers");
            float until = Time.realtimeSinceStartup + (art ? 2 : 25);
            while (Time.realtimeSinceStartup < until) { _app.CameraRig.Yaw += 2 * Time.unscaledDeltaTime; yield return null; }
            if (!video && !art) _performance.EndSample(Path.Combine(_directory, "performance-diagnostics.json"));
            yield return Shot("05-dense-combat");
            _measuring = false;
            if (art)
            {
                _app.CameraRig.SetView(new Vector3(-15, 7, -27), 180, 0, 18, true);
                yield return new WaitForSecondsRealtime(1);
                yield return Shot("06-furnace-hall");
                _app.CameraRig.SetView(new Vector3(26, 1.3f, 13), 215, 14, 15, true);
                yield return new WaitForSecondsRealtime(1);
                yield return Shot("07-core-yard");
                _app.CameraRig.SetView(new Vector3(0, 1, 0), 24, 37, 25, true);
                yield return new WaitForSecondsRealtime(1);
                yield return Shot("08-opposite-works");
                _app.CameraRig.SetView(new Vector3(.5f, 1.6f, -8), 151, -4, 10, true);
                yield return new WaitForSecondsRealtime(1);
                yield return Shot("09-ground-under-fire");
            }
            if (video) yield return RecordVideo();
            var stats = host.Session.Towers.GroupBy(t => t.Definition.Id).Select(g => g.Key + ": damage=" + g.Sum(t => t.LifetimeDamage).ToString("F1") + ", kills=" + g.Sum(t => t.LifetimeKills) + ", support=" + g.Sum(t => t.LifetimeSupportDamageEquivalent).ToString("F1"));
            var sorted = _frames.OrderBy(x => x).ToArray();
            string timing = sorted.Length > 0 ? $"Mean frame ms: {sorted.Average():F3}\nP95 frame ms: {sorted[(int)((sorted.Length - 1) * .95f)]:F3}\n" : "Timing not measured; no performance claim.\n";
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
            var camera = _app.CameraRig.GetComponent<Camera>();
            if (Physics.CheckSphere(camera.transform.position, .15f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
                _errors.Add(name + " camera overlaps scene geometry.");
            string obstacle = Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f, .5f, 0)), out var hit, 250, (1 << 8) | (1 << 9)) ? hit.transform.name : "sky";
            File.AppendAllText(Path.Combine(_directory, "camera-poses.txt"), $"{name}: position={camera.transform.position:F3}, angles={camera.transform.eulerAngles:F3}, focus={_app.CameraRig.Focus:F3}, center={obstacle}\n");
            yield return null;
            _measuring = measure;
        }

        private IEnumerator RecordVideo()
        {
            string path = Path.Combine(_directory, "Cinderworks-combat-tour.mp4");
            string videoPath = Path.Combine(_directory, "Cinderworks-combat-tour.video.mp4");
            string audioPath = Path.Combine(_directory, "Cinderworks-combat-tour.wav");
            var info = new ProcessStartInfo("ffmpeg", $"-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size {_capture.Width}x{_capture.Height} -framerate 30 -i pipe:0 -vf vflip -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p \"{videoPath}\"")
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
                var focus = new Vector3(-6, 1.5f, -6);
                _app.CameraRig.SetView(focus, 135, 12, 22, true);
                yield return new WaitForSecondsRealtime(2);
                int previousFrameRate = Time.captureFramerate;
                var cameraPath = new System.Text.StringBuilder("frame,x,y,z,yaw,pitch,radius\n");
                try
                {
                    Time.captureFramerate = 30;
                    using (var audio = new ReviewAudioCapture(audioPath))
                    {
                        for (int frame = 0; frame < 900; frame++)
                        {
                            if (frame % 45 == 0)
                                session.SpawnSandboxTargets(enemies[(frame / 45) % enemies.Length], 1, 8, "Standard", false);
                            float t = frame / 899f;
                            float rise = Mathf.Sin(t * Mathf.PI);
                            float yaw = 135 + t * 360;
                            float distance = 22 + rise * 9;
                            float pitch = 12 + rise * rise * 45;
                            float north = Mathf.Cos(yaw * Mathf.Deg2Rad);
                            // Keep the recording camera in front of the furnace facade throughout the northern arc.
                            if (north > 0)
                            {
                                float minimumPitch = Mathf.Acos(Mathf.Clamp01((focus.z + 18) / (distance * north))) * Mathf.Rad2Deg;
                                pitch = Mathf.Max(pitch, minimumPitch);
                            }
                            _app.CameraRig.SetView(focus, yaw, pitch, distance);
                            yield return new WaitForEndOfFrame();
                            var position = _app.CameraRig.transform.position;
                            cameraPath.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0},{1:F5},{2:F5},{3:F5},{4:F5},{5:F5},{6:F5}\n", frame, position.x, position.y, position.z, yaw, pitch, distance);
                            audio.CaptureFrame();
                            byte[] pixels = _capture.ReadRgb24();
                            process.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                        }
                        audio.VerifyAndWriteReport(Path.Combine(_directory, "audio-verification.txt"), 900, 30);
                    }
                    File.WriteAllText(Path.Combine(_directory, "video-camera-path.csv"), cameraPath.ToString());
                    process.StandardInput.Close();
                    if (!process.WaitForExit(15000) || process.ExitCode != 0)
                        throw new InvalidOperationException("Video encoding did not complete successfully.");
                }
                finally
                {
                    Time.captureFramerate = previousFrameRate;
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); }
                }
            }
            // Unity's DSP blocks can end fractionally short of the final video frame.
            var muxInfo = new ProcessStartInfo("ffmpeg", $"-y -hide_banner -loglevel error -i \"{videoPath}\" -i \"{audioPath}\" -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -ac 2 -af apad -t 30 -movflags +faststart \"{path}\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true };
            using (var mux = new Process { StartInfo = muxInfo })
            {
                if (!mux.Start()) throw new InvalidOperationException("Audio/video muxer did not start.");
                var error = mux.StandardError.ReadToEndAsync();
                if (!mux.WaitForExit(15000))
                {
                    mux.Kill(); mux.WaitForExit(2000);
                    throw new InvalidOperationException("Audio/video muxer timed out.");
                }
                if (mux.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 10000)
                    throw new InvalidOperationException("Audio/video muxing failed: " + error.GetAwaiter().GetResult());
            }
        }

        private void Update()
        {
            if (_app?.Host.Session == null) return;
            if (_measuring) _frames.Add(Time.unscaledDeltaTime * 1000);
            _peakEnemies = Mathf.Max(_peakEnemies, _app.Host.Session.Enemies.Count);
            _peakTowers = Mathf.Max(_peakTowers, _app.Host.Session.Towers.Count);
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message + "\n" + stack); }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; Time.captureFramerate = 0; }
    }
}
