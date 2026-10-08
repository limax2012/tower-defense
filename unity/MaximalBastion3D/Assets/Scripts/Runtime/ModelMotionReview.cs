using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Bastion3D
{
    // Isolated presentation fixture: articulated motion and contact checks, not gameplay evidence.
    public sealed class ModelMotionReview : MonoBehaviour
    {
        private BastionApplication _app;
        private ReviewCapture _capture;
        private string _directory;
        private readonly List<string> _report = new List<string>();
        private int _failures;

        public void Initialize(BastionApplication app)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--motion-review")) return;
            _app = app;
            int index = Array.IndexOf(args, "--capture-directory");
            _directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "MotionReview");
            Directory.CreateDirectory(_directory);
            _capture = ReviewCapture.Initialize(app.CameraRig.GetComponent<Camera>(), app.Interface);
            _capture.IncludeInterface = false;
            StartCoroutine(Run(args.Contains("--video")));
        }

        private IEnumerator Run(bool video)
        {
            yield return new WaitForSecondsRealtime(2);
            _app.Host.NewRun(); _app.Host.Paused = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Motion contact fixture"; floor.layer = 8;
            floor.transform.position = new Vector3(200, -.44f, 0);
            floor.transform.localScale = new Vector3(32, 1, 32);
            floor.GetComponent<Renderer>().sharedMaterial = _app.catalog.road;
            Physics.SyncTransforms();
            Process encoder = null;
            if (video)
            {
                string path = Path.Combine(_directory, "Model-motion-review.mp4");
                var info = new ProcessStartInfo("ffmpeg", $"-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size {_capture.Width}x{_capture.Height} -framerate 60 -i pipe:0 -vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \"{path}\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardError = true };
                encoder = new Process { StartInfo = info };
                encoder.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogWarning(e.Data); };
                encoder.Start(); encoder.BeginErrorReadLine();
            }
            Time.captureFramerate = 60;
            const float dt = 1f / 60;
            string[] enemies = { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" };
            foreach (string id in enemies)
            {
                var entry = _app.catalog.Find(id);
                var obj = Instantiate(entry.prefab);
                obj.name = id + " / 37";
                obj.transform.position = new Vector3(197, .11f, -3);
                var rig = new ModelRig(obj.transform, entry);
                float speed = _app.Host.Content.Enemies[id].Speed * Coordinates.Scale;
                float maxError = 0, maxSlip = 0, maxPenetration = 0;
                int samples = 0, minimumPlanted = 99, worstFrame = 0;
                for (int frame = 0; frame < 360; frame++)
                {
                    float distance = frame * dt * speed;
                    SamplePath(distance, out var position, out var direction);
                    obj.transform.position = new Vector3(200, .11f, 0) + position;
                    obj.transform.rotation = Quaternion.RotateTowards(obj.transform.rotation, Quaternion.LookRotation(direction), 520 * dt);
                    rig.AnimateEnemy(dt, speed, false);
                    if (frame > 60)
                    {
                        if (rig.Locomotion.ContactError > maxError) { maxError = rig.Locomotion.ContactError; worstFrame = frame; }
                        maxSlip = Mathf.Max(maxSlip, rig.Locomotion.FootSlip);
                        maxPenetration = Mathf.Max(maxPenetration, rig.Locomotion.FootPenetration);
                        minimumPlanted = Mathf.Min(minimumPlanted, rig.Locomotion.PlantedFeet);
                        samples++;
                    }
                    _app.CameraRig.SetView(obj.transform.position + Vector3.up * .65f, frame < 180 ? 75 : 150, frame < 180 ? 7 : 21, 7, true);
                    yield return new WaitForEndOfFrame();
                    if (frame == 105 || frame == 245) _capture.Capture(Path.Combine(_directory, id + (frame == 105 ? "-side.png" : "-turn.png")));
                    if (encoder != null)
                    {
                        byte[] pixels = _capture.ReadRgb24(); encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                    }
                }
                bool passed = maxError < .002f && maxPenetration < .002f && minimumPlanted >= (id == "t2_runner" ? 1 : 2);
                if (!passed) _failures++;
                _report.Add($"{(passed ? "PASS" : "FAIL")} {id}: {samples} moving samples, planted error={maxError:F5}m at frame{worstFrame}, horizontal error={maxSlip:F5}m, penetration={maxPenetration:F5}m, minimum supporting feet={minimumPlanted}");
                Destroy(obj); yield return null;
                CheckContactVariants(entry, speed);
            }
            foreach (string id in new[] { "needle_turret", "breaker_cannon", "siege_mortar", "prism_beam" })
            {
                var entry = _app.catalog.Find(id);
                var obj = Instantiate(entry.prefab, new Vector3(200, .06f, 0), Quaternion.identity);
                var rig = new ModelRig(obj.transform, entry);
                Vector3 rest = rig.Recoil.localPosition;
                float maximumStroke = 0;
                for (int frame = 0; frame < 180; frame++)
                {
                    float t = frame * dt;
                    rig.Aim(obj.transform.position + new Vector3(Mathf.Sin(t * .65f) * 7, 1, 12), dt);
                    if (frame % 60 == 12) rig.RecoilAmount = 1;
                    rig.AnimateTower(dt, false, false, false);
                    maximumStroke = Mathf.Max(maximumStroke, Vector3.Distance(rest, rig.Recoil.localPosition));
                    _app.CameraRig.SetView(obj.transform.position + Vector3.up * 1, 70, 13, 6, true);
                    yield return new WaitForEndOfFrame();
                    if (frame == 75) _capture.Capture(Path.Combine(_directory, id + "-recoil.png"));
                    if (encoder != null) { byte[] pixels = _capture.ReadRgb24(); encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length); }
                }
                float recovered = Vector3.Distance(rest, rig.Recoil.localPosition);
                bool passed = maximumStroke > 0 && maximumStroke < .32f && recovered < .008f;
                if (!passed) _failures++;
                _report.Add($"{(passed ? "PASS" : "FAIL")} {id}: recoil maximum={maximumStroke:F4}m, recovered displacement={recovered:F4}m");
                Destroy(obj); yield return null;
            }
            CheckMuzzlePorts();
            Time.captureFramerate = 0;
            if (encoder != null)
            {
                encoder.StandardInput.Close();
                if (!encoder.WaitForExit(15000) || encoder.ExitCode != 0) { _failures++; _report.Add("FAIL video encoder"); }
                encoder.Dispose();
            }
            File.WriteAllText(Path.Combine(_directory, "motion-verification.txt"), "Presentation-only fixture; no gameplay or performance claim.\n" + string.Join("\n", _report));
            Application.Quit(_failures == 0 ? 0 : 1);
        }

        private static void SamplePath(float distance, out Vector3 position, out Vector3 direction)
        {
            float p = Mathf.Repeat(distance, 24);
            if (p < 6) { position = new Vector3(-3, 0, -3 + p); direction = Vector3.forward; }
            else if (p < 12) { position = new Vector3(-3 + p - 6, 0, 3); direction = Vector3.right; }
            else if (p < 18) { position = new Vector3(3, 0, 3 - (p - 12)); direction = Vector3.back; }
            else { position = new Vector3(3 - (p - 18), 0, -3); direction = Vector3.left; }
        }

        private void CheckMuzzlePorts()
        {
            var entry = _app.catalog.Find("shard_fan");
            var obj = Instantiate(entry.prefab, new Vector3(200, .06f, 0), Quaternion.identity);
            var rig = new ModelRig(obj.transform, entry);
            CheckPorts(rig.FirePorts.Count == 3 && rig.FirePorts.Distinct().Count() == 3,
                "Shard has three distinct authored firing ports");
            foreach (float yaw in new[] { 0f, 83f, 214f })
            {
                rig.Yaw.rotation = Quaternion.Euler(0, yaw, 0);
                rig.Pitch.localRotation = Quaternion.Euler(-11, 0, 0);
                bool aligned = true;
                foreach (var port in rig.FirePorts)
                {
                    rig.BeginVolley();
                    var selected = rig.ProjectileMuzzle(port.position + port.forward * 12);
                    aligned &= selected == port;
                }
                CheckPorts(aligned, $"Shard port alignment at yaw {yaw:F0} and elevated aim");
            }
            foreach (int count in new[] { 3, 4, 7, 8 })
            {
                rig.BeginVolley();
                var target = rig.Muzzle.position + rig.Muzzle.forward * 12;
                var emitted = new List<Transform>();
                for (int i = 0; i < count; i++) emitted.Add(rig.ProjectileMuzzle(target));
                var uses = rig.FirePorts.Select(port => emitted.Count(p => p == port)).ToArray();
                CheckPorts(emitted[0] == rig.Muzzle && emitted.Take(3).Distinct().Count() == 3 &&
                    uses.Min() >= 1 && uses.Max() - uses.Min() <= 1 && uses.Sum() == count,
                    $"Shard volley {count}: center first, all ports used, balanced same-target emission");
                rig.BeginVolley();
                CheckPorts(rig.ProjectileMuzzle(target) == rig.Muzzle, $"Shard volley {count}: reset restores center-first aim");
            }
            Destroy(obj);
        }

        private void CheckPorts(bool passed, string description)
        {
            if (!passed) _failures++;
            _report.Add((passed ? "PASS " : "FAIL ") + description);
        }

        private void CheckContactVariants(VisualCatalog.Entry entry, float speed)
        {
            foreach (string variant in new[] { "boss-scale", "slowed", "three-times-clock" })
            {
                var obj = Instantiate(entry.prefab, new Vector3(197, .11f, -3), Quaternion.identity);
                obj.name = entry.id + " / 73";
                var rig = new ModelRig(obj.transform, entry);
                if (variant == "boss-scale") obj.transform.localScale *= 1.4f;
                float actualSpeed = speed * (variant == "slowed" ? .25f : variant == "boss-scale" ? 1.25f : 1);
                float step = variant == "three-times-clock" ? 3f / 60 : 1f / 60;
                int count = Mathf.CeilToInt(26 / (actualSpeed * step));
                float maximumError = 0;
                for (int frame = 0; frame < count; frame++)
                {
                    SamplePath(frame * step * actualSpeed, out var position, out var direction);
                    obj.transform.position = new Vector3(200, .11f, 0) + position;
                    obj.transform.rotation = Quaternion.RotateTowards(obj.transform.rotation, Quaternion.LookRotation(direction), 520 * step);
                    rig.AnimateEnemy(step, actualSpeed, false);
                    if (frame * step > 1) maximumError = Mathf.Max(maximumError, rig.Locomotion.ContactError);
                }
                bool passed = maximumError < .002f;
                if (!passed) _failures++;
                _report.Add($"{(passed ? "PASS" : "FAIL")} {entry.id}/{variant}: full26m loop, contact error={maximumError:F5}m");
                Destroy(obj);
            }
        }
        private void OnDestroy() { Time.captureFramerate = 0; }
    }
}
