using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion;
using MaximalBastion.Core;
using UnityEngine;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public static class CameraInteractionVerification
    {
        public static IEnumerator Run(BastionApplication app, Action<bool, string> require, string directory)
        {
            var camera = app.CameraRig;
            var view = app.Battlefield;
            var lens = camera.GetComponent<Camera>();
            var checks = new List<string>();
            var fixtures = new List<GameObject>();
            bool paused = app.Host.Paused;
            var pointer = camera.PointerOverInterface;
            camera.PointerOverInterface = () => true;
            void Check(bool condition, string label)
            {
                checks.Add((condition ? "PASS " : "FAIL ") + label);
                require(condition, label);
            }
            GameObject Block(string name, Vector3 position, Vector3 size)
            {
                var block = new GameObject(name) { layer = 8 };
                block.transform.position = position;
                block.AddComponent<BoxCollider>().size = size;
                fixtures.Add(block);
                Physics.SyncTransforms();
                return block;
            }
            Ray RayTo(Vector3 position) => lens.ScreenPointToRay(lens.WorldToScreenPoint(position));

            try
            {
                float synchronizationDeadline = Time.realtimeSinceStartup + 1;
                while (view.TowerModels.Count < app.Host.Session.Towers.Count && Time.realtimeSinceStartup < synchronizationDeadline)
                    yield return null;
                app.Host.Paused = true;
                Physics.SyncTransforms();
                var tower = app.Host.Session.Towers.FirstOrDefault();
                Check(tower != null && view.TowerModels.ContainsKey(tower.Id), "Camera test has a live authored tower model");
                if (tower != null && view.TowerModels.TryGetValue(tower.Id, out var rig) && rig != null)
                {
                    var target = rig.Bounds().center;
                    int visible = 0, lowVisible = 0;
                    foreach (var angle in new[] { new Vector2(0, 7), new Vector2(180, 7), new Vector2(90, 70), new Vector2(270, 86) })
                    {
                        camera.SetView(target, angle.x, angle.y, 3.5f, true);
                        yield return null;
                        bool picked = view.TryPick(RayTo(target), out var entity);
                        if (picked && entity.id == tower.Id)
                        {
                            visible++;
                            if (angle.y < 10) lowVisible++;
                        }
                        checks.Add($"Selection yaw {angle.x}, pitch {angle.y}: {(picked ? "entity " + entity.id : "occluded")}");
                    }
                    Check(visible >= 3 && lowVisible >= 1, "Tower selection works from overhead, side and ground-level perspectives");
                    camera.SetView(target, 0, 70, 4, true);
                    yield return null;
                    var wall = Block("Verification / selection occluder", Vector3.Lerp(camera.transform.position, target, .5f), Vector3.one * .8f);
                    Check(!view.TryPick(RayTo(target), out _), "Solid environment blocks selection of a hidden tower");
                    UnityEngine.Object.Destroy(wall);
                    yield return null;
                }

                var probe = new GameSession(app.Host.Content, "foundry_loop", "easy", "standard");
                SimVector placement = default;
                bool found = false;
                foreach (var region in probe.Map.BuildableRegions)
                {
                    for (int y = region.Top + 25; y < region.Bottom - 25 && !found; y += 24)
                    for (int x = region.Left + 25; x < region.Right - 25 && !found; x += 24)
                    {
                        var candidate = new SimVector(x, y);
                        if (probe.ValidatePlacement("needle_turret", candidate) != PlacementFailure.None ||
                            app.Host.Session.Towers.Any(t => SimVector.Distance(t.Position, candidate) < 65)) continue;
                        placement = candidate; found = true;
                    }
                    if (found) break;
                }
                Check(found, "Camera test finds an authored legal build location");
                if (found)
                {
                    var point = Coordinates.World(placement);
                    int visible = 0, lowVisible = 0;
                    foreach (var angle in new[] { new Vector2(0, 7), new Vector2(180, 7), new Vector2(90, 65), new Vector2(270, 86) })
                    {
                        camera.SetView(point, angle.x, angle.y, 3.5f, true);
                        yield return null;
                        if (!view.TryPlacementPoint(RayTo(point), out var world))
                        {
                            checks.Add($"Placement yaw {angle.x}, pitch {angle.y}: occluded");
                            continue;
                        }
                        var logical = Coordinates.Logical(world);
                        Check(SimVector.Distance(logical, placement) < .05f &&
                            probe.ValidatePlacement("needle_turret", logical) == PlacementFailure.None,
                            $"Placement ray preserves authored coordinates at yaw {angle.x}, pitch {angle.y}");
                        visible++;
                        if (angle.y < 10) lowVisible++;
                    }
                    Check(visible >= 3 && lowVisible >= 1, "Placement works from overhead, side and ground-level perspectives");
                    camera.SetView(point, 0, 70, 4, true);
                    yield return null;
                    var wall = Block("Verification / placement occluder", Vector3.Lerp(camera.transform.position, point, .5f), Vector3.one * .8f);
                    Check(!view.TryPlacementPoint(RayTo(point), out _), "Solid environment blocks placement behind a wall");
                    UnityEngine.Object.Destroy(wall);
                    yield return null;
                }

                var collision = Block("Verification / camera collision", new Vector3(45, 3, 41), new Vector3(4, 4, 1));
                camera.SetView(new Vector3(45, 3, 45), 0, 0, 8, true);
                camera.SetView(new Vector3(45, 3, 45), 0, 0, 1);
                yield return new WaitForSecondsRealtime(.7f);
                Check(camera.transform.position.z > 38 && camera.transform.position.z < 40.3f &&
                    !Physics.CheckSphere(camera.transform.position, .18f, 1 << 8, QueryTriggerInteraction.Ignore),
                    "Orbit movement advances smoothly and stops with clearance before solid geometry");
                camera.SetView(new Vector3(45, 3, 45), 0, 0, 8, true);
                camera.SetFreeFlight(true);
                float until = Time.realtimeSinceStartup + .7f;
                while (Time.realtimeSinceStartup < until)
                {
                    camera.MoveFreeFlight(Vector3.forward * .7f);
                    yield return null;
                }
                Check(camera.FreeFlight && camera.transform.position.z > 38 && camera.transform.position.z < 40.3f &&
                    !Physics.CheckSphere(camera.transform.position, .18f, 1 << 8, QueryTriggerInteraction.Ignore),
                    "Free flight obeys the same solid-geometry clearance");
                UnityEngine.Object.Destroy(collision);
                yield return null;

                camera.SetView(new Vector3(45, .4f, 45), 0, 0, 1, true);
                camera.SetFreeFlight(true);
                until = Time.realtimeSinceStartup + .3f;
                while (Time.realtimeSinceStartup < until)
                {
                    camera.MoveFreeFlight(Vector3.down);
                    yield return null;
                }
                Check(camera.transform.position.y >= .349f, "Free flight cannot descend beneath the ground clearance");

                var overview = BattlefieldCamera.OverviewFocus - Quaternion.Euler(52, 210, 0) * Vector3.forward * 65;
                var resetWall = Block("Verification / reset occluder", Vector3.Lerp(camera.transform.position, overview, .15f), Vector3.one * 4);
                camera.Overview();
                yield return null;
                Check(!camera.FreeFlight && Vector3.Distance(camera.transform.position, overview) < .05f,
                    "Overview resets from ground-level free flight despite an intervening obstacle");
                Check(!Physics.CheckSphere(camera.transform.position, .18f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore),
                    "Overview reset lands outside solid geometry");
                bool routeVisible = true;
                foreach (var point in app.Host.Session.Map.Definition.Path)
                {
                    var screen = lens.WorldToViewportPoint(Coordinates.World(point.ToVector2()));
                    routeVisible &= screen.z > 0 && screen.x > .06f && screen.x < .94f && screen.y > .18f && screen.y < .86f;
                }
                Check(routeVisible, "Overview keeps every route corner clear of the top and bottom HUD bars");
                bool decksVisible = true;
                foreach (var region in app.Host.Session.Map.BuildableRegions)
                foreach (var point in new[] { new SimVector(region.Left, region.Top), new SimVector(region.Right, region.Top),
                    new SimVector(region.Left, region.Bottom), new SimVector(region.Right, region.Bottom) })
                {
                    var screen = lens.WorldToViewportPoint(Coordinates.World(point));
                    decksVisible &= screen.z > 0 && screen.x > .06f && screen.x < .94f && screen.y > .18f && screen.y < .86f;
                }
                Check(decksVisible, "Overview keeps all build decks inside the unobstructed battlefield");
                UnityEngine.Object.Destroy(resetWall);
                File.WriteAllText(Path.Combine(directory, "camera-interaction-verification.txt"), string.Join("\n", checks));
            }
            finally
            {
                foreach (var fixture in fixtures) if (fixture) UnityEngine.Object.Destroy(fixture);
                camera.PointerOverInterface = pointer;
                camera.Overview();
                app.Host.Paused = paused;
            }
        }
    }
}
