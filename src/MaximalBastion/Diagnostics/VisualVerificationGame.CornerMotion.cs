using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureCornerMotionReview(GameContent content, SpriteFont font)
    {
        const int fps = 60;
        const float lead = 1f / 120;
        var runnerReview = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RUNNER_CORNER") == "1";
        var frameCount = runnerReview ? 120 : 210;
        var columnWidth = runnerReview ? 640 : 320;
        var records = new List<object>();
        using var clip = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
        foreach (var mapId in new[] { "foundry_loop", "crosswind_basin" })
        {
            var session = new GameSession(content, mapId, "normal", "sandbox_lab");
            var families = runnerReview ? new[] { "t2_runner", "t2_runner" } :
                new[] { "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" };
            var points = session.Map.Definition.Path;
            var corner = points[1].ToVector2();
            var cornerDistance = Vector2.Distance(points[0].ToVector2(), corner);
            for (var index = 0; index < families.Length; index++)
            {
                session.SpawnEnemy(families[index], 1, 1, runnerReview && index == 1 ? "Boss" : "Standard");
                var enemy = session.Enemies[^1];
                enemy.AbsorbShield(enemy.Shield);
                enemy.SetSandboxPathDistance(cornerDistance - enemy.CurrentSpeed * (runnerReview ? 1 : 1.75f), session.Map.Path);
            }
            var path = EnemyLocomotionPath.For(session.Map);
            var directory = $"{(runnerReview ? "runner-corner-motion" : "corner-motion")}-{mapId}";
            Directory.CreateDirectory(Path.Combine(_outputDirectory, directory));
            var frames = new List<object>();
            var prior = new Dictionary<int, CornerMotionSample>();
            using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth,
                GameConstants.RenderHeight, false, SurfaceFormat.Color, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var presentation = PresentationFrame.Create(session, lead);
                var checksum = SessionChecksum.Compute(session, frame);
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
                StatusLabel(font, $"CORNER MOTION / {session.Map.Definition.DisplayName.ToUpperInvariant()} / 60 FPS",
                    new Vector2(24, 16), .85f);
                StatusLabel(font, "NATIVE SIZE / FIXED CAMERA ABOVE    2x DETAIL / BODY-FOLLOWING CAMERA BELOW",
                    new Vector2(24, 48), .48f);
                for (var index = 0; index < session.Enemies.Count; index++)
                    StatusLabel(font, session.Enemies[index].Definition.DisplayName.ToUpperInvariant() +
                        (runnerReview ? $" / {session.Enemies[index].Rank.ToString().ToUpperInvariant()}" : ""),
                        new Vector2(index * columnWidth + 20, 80), .60f);
                StatusLabel(font, $"{frame / (float)fps:0.00}s / ACTUAL MOVEMENT + HALF-TICK PRESENTATION LEAD",
                    new Vector2(24, 695), .47f);
                _batch.End();
                var samples = new List<object>();
                for (var index = 0; index < session.Enemies.Count; index++)
                {
                    var enemy = session.Enemies[index];
                    var distance = enemy.DistanceAlongPath + enemy.CurrentSpeed * presentation.LeadSeconds;
                    var position = presentation.EnemyPosition(enemy);
                    var forward = path.Forward(distance, EnemyLocomotion.MotionRadius(enemy), EnemyLocomotion.UsesTracks(enemy));
                    var heading = MathF.Atan2(forward.Y, forward.X);
                    DrawView(new Rectangle(index * columnWidth + 8, 108, columnWidth - 16, 278), corner, 1);
                    DrawView(new Rectangle(index * columnWidth + 8, 400, columnWidth - 16, 278), position, 2);
                    var contacts = EnemyLocomotion.ContactSlots(enemy).ToArray().Select(slot =>
                        EnemyLocomotion.Sample(enemy, position, heading, distance, slot.Side, slot.Leg, path)).ToArray();
                    var motionRadius = EnemyLocomotion.MotionRadius(enemy);
                    var before = path.Forward(distance - .01f, motionRadius, EnemyLocomotion.UsesTracks(enemy));
                    var after = path.Forward(distance + .01f, motionRadius, EnemyLocomotion.UsesTracks(enemy));
                    var speed = Vector2.Distance(path.VisualPosition(distance - .01f, motionRadius, EnemyLocomotion.UsesTracks(enemy)),
                        path.VisualPosition(distance + .01f, motionRadius, EnemyLocomotion.UsesTracks(enemy))) * enemy.CurrentSpeed / .02f;
                    var angularSpeed = MathHelper.WrapAngle(MathF.Atan2(after.Y, after.X) -
                        MathF.Atan2(before.Y, before.X)) * enemy.CurrentSpeed / .02f;
                    var acceleration = 0f;
                    var angularAcceleration = 0f;
                    var footStep = 0f;
                    var plantedDrift = 0f;
                    if (prior.TryGetValue(enemy.Id, out var previous))
                    {
                        speed = Vector2.Distance(position, previous.Position) * fps;
                        angularSpeed = MathHelper.WrapAngle(heading - previous.Heading) * fps;
                        acceleration = MathF.Abs(speed - previous.Speed) * fps;
                        angularAcceleration = MathF.Abs(angularSpeed - previous.AngularSpeed) * fps;
                        for (var leg = 0; leg < contacts.Length; leg++)
                        {
                            var current = contacts[leg];
                            var old = previous.Contacts[leg];
                            footStep = MathF.Max(footStep, Vector2.Distance(current.Pose.Foot, old.Pose.Foot));
                            if (current.Planted && old.Planted && current.StanceCycle == old.StanceCycle)
                                plantedDrift = MathF.Max(plantedDrift, Vector2.Distance(current.GroundPoint, old.GroundPoint));
                        }
                    }
                    prior[enemy.Id] = new(position, heading, speed, angularSpeed, contacts);
                    samples.Add(new { enemy.Id, family = enemy.Definition.Id, rank = enemy.Rank.ToString(), enemy.DistanceAlongPath,
                        authoritativeSpeed = enemy.CurrentSpeed, visualX = position.X, visualY = position.Y,
                        headingRadians = heading, speed, acceleration, angularSpeed, angularAcceleration,
                        maximumFootStep = footStep, maximumPlantedDrift = plantedDrift,
                        visualOffset = Vector2.Distance(position, enemy.Position) });

                    void DrawView(Rectangle bounds, Vector2 camera, float scale)
                    {
                        GraphicsDevice.ScissorRectangle = new Rectangle(bounds.X * GameConstants.RenderScale,
                            bounds.Y * GameConstants.RenderScale, bounds.Width * GameConstants.RenderScale,
                            bounds.Height * GameConstants.RenderScale);
                        var transform = Matrix.CreateTranslation(-camera.X, -camera.Y, 0) * Matrix.CreateScale(scale) *
                            Matrix.CreateTranslation(bounds.Center.X, bounds.Center.Y, 0) * Matrix.CreateScale(GameConstants.RenderScale);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                            null, clip, null, transform);
                        var theme = ColorPalette.Environment(session.Map.Definition.PathVisual.Style);
                        for (var segment = 1; segment < points.Count; segment++)
                        {
                            var first = points[segment - 1].ToVector2();
                            var last = points[segment].ToVector2();
                            _primitives.Line(_batch, first, last, theme.Edge, session.Map.Definition.PathWidth + 2);
                            _primitives.Line(_batch, first, last, theme.Lane, session.Map.Definition.PathWidth - 1);
                        }
                        for (var marker = MathF.Max(0, cornerDistance - 180); marker < cornerDistance + 180; marker += 20)
                            _primitives.Circle(_batch, session.Map.Path.GetPosition(marker), 1.1f, theme.Accent * .5f);
                        EnemyModelArt.Draw(_batch, _primitives, enemy, position, heading, presentation.TimeSeconds,
                            false, distance, movementPath: path);
                        _batch.End();
                    }
                }
                GraphicsDevice.SetRenderTarget(null);
                using var native = DownsampleEnemyLaneScene(target);
                var saved = SaveTierScene(native, $"{directory}/frame-{frame:000}.png");
                if (checksum != SessionChecksum.Compute(session, frame))
                    throw new InvalidOperationException("Corner motion review changed authoritative state.");
                frames.Add(new { frame, seconds = frame / (float)fps, saved.File, saved.Sha256, checksum, samples });
                session.Update(1f / fps);
            }
            records.Add(new { mapId, directory, frames });
            Console.WriteLine($"Corner motion: {mapId}, {frameCount} native frames captured.");
        }
        File.WriteAllText(Path.Combine(_outputDirectory, runnerReview ? "runner-corner-motion.json" : "corner-motion.json"), JsonSerializer.Serialize(new
        {
            width = 1280, height = 720, fps, frameCount,
            description = "Real 60 Hz movement at authored family speeds, sampled at a half-tick presentation lead; clockwise and counterclockwise corners. Native fixed camera and 2x body-following detail use the same path and grounded contacts as gameplay.",
            records
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record CornerMotionSample(Vector2 Position, float Heading, float Speed, float AngularSpeed,
        EnemyGroundContact[] Contacts);
}
