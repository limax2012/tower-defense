using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using UnityEngine;
using SimColor = MaximalBastion.Gameplay.Color;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public static class BeamLifecycleVerification
    {
        public static IEnumerator Run(BastionApplication app, Action<bool, string> require, string directory, ReviewCapture capture)
        {
            var checks = new List<string>();
            var errors = new List<string>();
            bool automatic = app.Host.AutoWave;
            void Check(bool condition, string label)
            {
                checks.Add((condition ? "PASS " : "FAIL ") + label);
                require(condition, label);
            }
            void CaptureError(string message, string stack, LogType type)
            {
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 64)
                    errors.Add(message + "\n" + stack);
            }
            Application.logMessageReceived += CaptureError;
            app.Host.AutoWave = false;
            try
            {
                foreach (string id in new[] { "prism_beam", "arc_relay" })
                {
                    app.Host.NewRun("easy", "sandbox_lab");
                    bool placed = app.Host.Command(GameCommandType.PlaceTower, choice: id, position: new SimVector(210, 175));
                    Check(placed, id + ": validated placement accepted");
                    if (!placed) continue;
                    var session = app.Host.Session;
                    var tower = session.Towers.Single();
                    float deadline = Time.realtimeSinceStartup + 2;
                    while (!app.Battlefield.TowerModels.ContainsKey(tower.Id) && Time.realtimeSinceStartup < deadline) yield return null;
                    app.Battlefield.TowerModels.TryGetValue(tower.Id, out var rig);
                    Check(rig != null && rig.Root && rig.Muzzle, id + ": live model and muzzle exist");
                    if (rig == null || !rig.Root || !rig.Muzzle) continue;
                    app.Host.Paused = true;
                    var style = id == "prism_beam" ? BeamStyle.Prism : BeamStyle.Standard;
                    session.Effects.AddBeam(tower.Position, tower.Position + new SimVector(120, 40),
                        id == "prism_beam" ? new SimColor(174, 121, 255) : new SimColor(38, 240, 217), 30, style, tower.Id);
                    var effect = session.Effects.Effects.Last();
                    app.Battlefield.Effects.CaptureStep();
                    app.CameraRig.SetView(rig.Root.position + Vector3.up, 30, 24, 11, true);
                    yield return new WaitForEndOfFrame();
                    string visualName = id == "prism_beam" ? "Prism discharge" : "Arc discharge";
                    var visual = GameObject.Find(visualName);
                    Check(visual && visual.GetComponentsInChildren<MeshRenderer>().Length >= 2 &&
                        visual.GetComponentsInChildren<Transform>().Any(t => t.name == "Energy core" && t.localScale.z > .1f),
                        id + ": live beam was rendered before sale");
                    capture?.Capture(Path.Combine(directory, "lifecycle-" + id + "-before.png"));
                    int errorsBeforeSale = errors.Count;
                    Check(app.Host.Command(GameCommandType.SellTower, tower.Id), id + ": validated sale accepted with a live beam");
                    app.Host.Paused = false;
                    deadline = Time.realtimeSinceStartup + 2;
                    while (rig.Root && Time.realtimeSinceStartup < deadline) yield return null;
                    // Destruction is deferred; cover later presentation frames while the effect is still active.
                    for (int frame = 0; frame < 3; frame++) { yield return null; yield return new WaitForEndOfFrame(); }
                    Check(!rig.Root && !rig.Muzzle && !app.Battlefield.TowerModels.ContainsKey(tower.Id),
                        id + ": Unity destroyed the emitter and its muzzle");
                    Check(effect.Remaining > 20 && session.Effects.Effects.Contains(effect),
                        id + ": effect remains alive beyond emitter destruction");
                    Check(!visual && !GameObject.Find(visualName), id + ": no stale discharge remains after deferred destruction");
                    Check(errors.Count == errorsBeforeSale, id + ": no presentation error during emitter retirement");
                    capture?.Capture(Path.Combine(directory, "lifecycle-" + id + "-after.png"));
                }
                app.Battlefield.Effects.Pulse(new Vector3(0, 1, 0), Color.cyan, 1);
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(light => light.name.StartsWith("Impact light ", StringComparison.Ordinal)).ToArray();
                Check(lights.Length > 0 && lights.Any(light => light.enabled), "Session clear: impact lighting is active before leaving");
                app.Host.LeaveRun();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Check(app.Host.Session == null && lights.All(light => !light || !light.enabled),
                    "Session clear: impact lights stay disabled on the menu");
            }
            finally
            {
                app.Host.AutoWave = automatic;
                app.Host.Paused = false;
                Application.logMessageReceived -= CaptureError;
                File.WriteAllText(Path.Combine(directory, "beam-lifecycle-verification.txt"),
                    "Presentation lifecycle fixture; validated commands with extended transient beam duration.\n" +
                    string.Join("\n", checks) + "\nErrors: " + errors.Count + "\n" + string.Join("\n", errors));
            }
        }
    }
}
