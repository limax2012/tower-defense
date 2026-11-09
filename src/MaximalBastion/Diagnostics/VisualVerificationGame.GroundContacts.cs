using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureGroundedLocomotion(GameContent content, SpriteFont font, SpriteFont display)
    {
        var captures = new List<object>();
        foreach (var reduced in new[] { false, true })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            var families = new[] { "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" };
            var ranks = new[] { "Standard", "Elite", "Boss" };
            for (var family = 0; family < families.Length; family++)
            for (var rank = 0; rank < ranks.Length; rank++)
            {
                session.SpawnEnemy(families[family], 1, 1, ranks[rank]);
                var enemy = session.Enemies[^1];
                enemy.AbsorbShield(enemy.Shield);
                enemy.SetSandboxPathDistance(35 + family * 310 + rank * 65, session.Map.Path);
            }
            var initialDistances = session.Enemies.ToDictionary(enemy => enemy.Id, enemy => enemy.DistanceAlongPath);
            for (var step = 0; step < 270; step++) session.Update(1f / 60);
            foreach (var enemy in session.Enemies)
                enemy.SetSandboxPathDistance(initialDistances[enemy.Id], session.Map.Path);
            if (session.AnnouncementRemaining > 0)
                throw new InvalidOperationException("The ground-contact announcement pre-roll has not elapsed.");
            var path = new EnemyLocomotionPath(session.Map);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            ui.HandleGameplayInput(Pointer(0, 0), session);
            _renderer.ReducedEffects = reduced;
            _renderer.QuietEnemyHud = true;
            var directory = reduced ? "ground-contacts-reduced" : "ground-contacts-full";
            Directory.CreateDirectory(Path.Combine(_outputDirectory, directory));
            var frames = new List<object>();
            for (var frame = 0; frame < 240; frame++)
            {
                if (frame == 30)
                    foreach (var enemy in session.Enemies)
                        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = .45f });
                if (frame == 120)
                    foreach (var enemy in session.Enemies)
                        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .8f, Magnitude = 1 });
                var checksum = SessionChecksum.Compute(session, 270 + frame * 2);
                using var scene = RenderScene(ui, GameState.Playing, session);
                using var native = DownsampleEnemyLaneScene(scene);
                var saved = SaveTierScene(native, $"{directory}/frame-{frame:000}.png");
                frames.Add(new
                {
                    frame, seconds = frame / 30f, saved.File, saved.Sha256, checksum,
                    enemies = session.Enemies.Select(enemy =>
                    {
                        var radius = EnemyModelArt.BodyRadius(enemy);
                        var motionRadius = EnemyLocomotion.MotionRadius(enemy);
                        var center = path.VisualPosition(enemy.DistanceAlongPath, motionRadius, EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                        var forward = path.Forward(enemy.DistanceAlongPath, motionRadius, EnemyLocomotion.UsesTracks(enemy), EnemyLocomotion.UsesHeavyTracks(enemy));
                        var heading = MathF.Atan2(forward.Y, forward.X);
                        return new
                        {
                            enemy.Id, family = enemy.Definition.Id, rank = enemy.Rank.ToString(),
                            enemy.DistanceAlongPath, enemy.CurrentSpeed, bodyRadius = radius,
                            visualX = center.X, visualY = center.Y, headingRadians = heading,
                            trackPhase = EnemyLocomotion.TrackPhase(enemy.DistanceAlongPath, radius),
                            contactCount = EnemyLocomotion.ContactSlots(enemy).Length,
                            contacts = EnemyLocomotion.ContactSlots(enemy).ToArray().Select(pair =>
                            {
                                var contact = EnemyLocomotion.Sample(enemy, center, heading,
                                    enemy.DistanceAlongPath, pair.Item1, pair.Item2, path);
                                return new { side = pair.Item1, leg = pair.Item2, contact.Planted, contact.Lift, contact.StanceCycle,
                                    x = contact.GroundPoint.X, y = contact.GroundPoint.Y,
                                    contactRadius = contact.Pose.ContactRadius,
                                    rootX = contact.Pose.Root.X, rootY = contact.Pose.Root.Y,
                                    kneeX = contact.Pose.Knee.X, kneeY = contact.Pose.Knee.Y,
                                    footX = contact.Pose.Foot.X, footY = contact.Pose.Foot.Y };
                            }).ToArray()
                        };
                    }).ToArray()
                });
                if (checksum != SessionChecksum.Compute(session, 270 + frame * 2))
                    throw new InvalidOperationException("Ground-contact capture modified the authoritative session.");
                for (var step = 0; step < 2; step++) session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
            captures.Add(new { directory, reducedEffects = reduced, frames });
            Console.WriteLine($"Ground contacts: {directory}, 240 native frames captured.");
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "ground-contact-manifest.json"), JsonSerializer.Serialize(new
        {
            fixture = "Actual movement on Cinderworks; Crawler, Brute, Aegis and Regenerator at all three ranks traverse straight lanes and corners with shields depleted. Crawler has 4 legs, Brute 6, Aegis 8, and Regenerator has tracks. Direct legal Slow at 1s for 2s, Stun at 4s for 0.8s; rank resistance remains authoritative. Contact geometry uses the same rounded visual path center and heading as the renderer; simulation positions and checksums remain unchanged.",
            width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440,
            fps = 30, seconds = 8, simulationHz = 60, captures
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
