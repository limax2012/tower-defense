using System.Diagnostics;
using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private sealed record CrowdedPilotSubject(EnemyInstance Enemy, string Condition, float InitialDistance);

    private void CaptureCrowdedEnemyPilot(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        var preview = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PILOT_PREVIEW") == "1";
        var bodyOnly = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PILOT_BODY_ONLY") == "1";
        var reduced = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LANE_REDUCED") == "1";
        var originalRenderer = _renderer;
        var assertions = new List<string>();
        var captures = new List<object>();
        var keyFrames = new HashSet<int> { 0, 29, 30, 45, 75, 120, 150, 179, 182, 210, 239 };
        try
        {
            foreach (var mapId in new[] { "foundry_loop", "crosswind_basin" })
            {
                _renderer = new GameRenderer { QuietEnemyHud = variant == "C", HideEnemyHud = bodyOnly, ReducedEffects = reduced };
                ApplyEnemyEvidenceOptions(_renderer);
                var session = new GameSession(content, mapId, "normal", "sandbox_lab");
                var sources = PlaceCrowdedPilotSources(session);
                const int preRollTicks = 270;
                for (var tick = 0; tick < preRollTicks; tick++) session.Update(1f / 60);
                Require(session.AnnouncementRemaining <= 0,
                    $"{mapId}: the crowded pilot starts after every setup announcement has cleared.", assertions);
                var subjects = CreateCrowdedPilotSubjects(session);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                Directory.CreateDirectory(Path.Combine(_outputDirectory, mapId));
                Directory.CreateDirectory(Path.Combine(_outputDirectory, "body-only", mapId));
                var frames = new List<object>();
                for (var frame = 0; frame < EnemyLaneFrameCount; frame++)
                {
                    if (frame == 30)
                        foreach (var subject in subjects) ApplyCrowdedPilotConditions(session, subject, sources);
                    var tick = preRollTicks + frame * 2;
                    var checksum = SessionChecksum.Compute(session, tick);
                    var before = GraphicsDevice.Metrics;
                    var timer = Stopwatch.StartNew();
                    using var scene = RenderScene(ui, GameState.Playing, session);
                    timer.Stop();
                    var metrics = GraphicsDevice.Metrics - before;
                    var enemyGlyphs = _renderer.LastEnemyGlyphCount;
                    var enemyHealthBars = _renderer.LastEnemyHealthBarCount;
                    var enemyShieldBars = _renderer.LastEnemyShieldBarCount;
                    var statusStrips = CaptureEnemyRailGeometry(session);
                    if (!bodyOnly)
                        Require(enemyGlyphs == 0,
                            $"{mapId} frame {frame}: active gameplay has no enemy condition icons.", assertions);
                    string? fileName = null;
                    string? hash = null;
                    string? bodyOnlyFile = null;
                    if (!preview || keyFrames.Contains(frame))
                    {
                        using var native = DownsampleEnemyLaneScene(scene);
                        fileName = $"{mapId}/frame-{frame:000}.png";
                        hash = SaveTierScene(native, fileName).Sha256;
                        if (keyFrames.Contains(frame))
                        {
                            var state = frame switch
                            {
                                0 => "neutral", 29 => "before-application", 30 => "first-application",
                                45 => "sustained", 75 => "between-pulses", 120 => "turning-and-overlap",
                                150 => "late-sustained", 179 => "last-active-frame", 182 => "timed-statuses-expired",
                                210 => "cleanup", _ => "settled"
                            };
                            File.Copy(Path.Combine(_outputDirectory, fileName),
                                Path.Combine(_outputDirectory, mapId, state + ".png"), true);
                            bodyOnlyFile = $"body-only/{mapId}/frame-{frame:000}.png";
                            if (bodyOnly)
                                File.Copy(Path.Combine(_outputDirectory, fileName), Path.Combine(_outputDirectory, bodyOnlyFile), true);
                            else
                            {
                                _renderer.HideEnemyHud = true;
                                using var bodyScene = RenderScene(ui, GameState.Playing, session);
                                using var bodyNative = DownsampleEnemyLaneScene(bodyScene);
                                SaveTierScene(bodyNative, bodyOnlyFile);
                                _renderer.HideEnemyHud = false;
                            }
                        }
                    }
                    Require(checksum == SessionChecksum.Compute(session, tick),
                        $"{mapId} frame {frame}: full and body-only crowded renders preserve the same simulation state.", assertions);
                    if (frame == 30)
                    {
                        Require(subjects.Count(subject => subject.Enemy.Definition.Id == "t3_brute") == 5 &&
                                subjects.Count(subject => subject.Enemy.Definition.Id == "t2_runner") == 5 &&
                                subjects.Count(subject => subject.Enemy.Definition.Id == "t4_aegis") == 5 &&
                                subjects.Count(subject => subject.Enemy.Definition.Id == "t5_regenerator") == 5,
                            $"{mapId}: twenty native-size Standard subjects cover all four pilot families.", assertions);
                        Require(subjects.Any(subject => subject.Condition == "burn-repair" &&
                                subject.Enemy.StatusEffects.IsBurning && subject.Enemy.DamagePauseTimer <= 0 && subject.Enemy.Shield > 0),
                            $"{mapId}: active repair and Burn coexist through a real shielded Regenerator state.", assertions);
                        Require(subjects.Where(subject => subject.Condition.Contains("expose", StringComparison.Ordinal))
                                .All(subject => subject.Enemy.StatusEffects.Active.Any(status => status.Type == StatusType.Exposed &&
                                    status.SourceId == sources[StatusType.Exposed])),
                            $"{mapId}: every Expose sample names the actual placed Prism source.", assertions);
                    }
                    if (frame == 182)
                        Require(subjects.All(subject => subject.Enemy.StatusEffects.Active.Count == 0),
                            $"{mapId}: all five-second temporary pilot statuses have expired through the normal update.", assertions);
                    frames.Add(new
                    {
                        frame, tick, seconds = frame / 30f, simulatedSeconds = session.Statistics.SimulatedSeconds,
                        checksum, fileName, hash, bodyOnlyFile,
                        renderSubmissionMilliseconds = timer.Elapsed.TotalMilliseconds,
                        metrics.DrawCount, metrics.SpriteCount, metrics.PrimitiveCount,
                        enemyGlyphs, enemyHealthBars, enemyShieldBars, statusStrips,
                        auraRegions = AuraRegionCounter?.GetValue(_renderer) as int?,
                        transientEffects = session.Effects.Effects.Count,
                        enemies = subjects.Select(subject => new
                        {
                            subject.Enemy.Id, family = subject.Enemy.Definition.Id, subject.Condition, subject.InitialDistance,
                            x = subject.Enemy.Position.X, y = subject.Enemy.Position.Y,
                            headingRadians = CrowdedPilotHeading(session, subject.Enemy),
                            bodyRadius = EnemyModelArt.BodyRadius(subject.Enemy),
                            visibleBounds = CaptureEnemyVisibleBounds(session, subject.Enemy),
                            subject.Enemy.DistanceAlongPath, subject.Enemy.Health, subject.Enemy.MaxHealth,
                            subject.Enemy.Shield, subject.Enemy.DamagePauseTimer, subject.Enemy.CurrentSpeed,
                            subject.Enemy.IsDead, subject.Enemy.HasEscaped,
                            expectedConditions = ExpectedEnemyConditions(subject.Enemy),
                            active = subject.Enemy.StatusEffects.Active.Select(status => new
                            {
                                type = status.Type.ToString(), status.SourceId, status.Magnitude, status.RemainingSeconds
                            }).ToArray()
                        }).ToArray()
                    });
                    for (var step = 0; step < 2; step++) session.Update(1f / 60);
                    ui.AdvanceVisualTime(1f / 30);
                }
                Require(subjects.All(subject => !subject.Enemy.IsDead && !subject.Enemy.HasEscaped),
                    $"{mapId}: every pilot subject remains available for the complete eight-second comparison.", assertions);
                captures.Add(new
                {
                    mapId, preRollTicks, initialPathDistance = 80, spacing = 44,
                    sources = sources.Select(source => new
                    {
                        status = source.Key.ToString(), towerInstanceId = source.Value,
                        towerDefinition = session.Towers.First(tower => tower.Id == source.Value).Definition.Id
                    }).ToArray(),
                    frames
                });
                Console.WriteLine($"Crowded enemy pilot {variant}: {mapId}, 240 matched simulation frames; {(preview ? "11 preview images" : "240 native images")} and body-only keyframes.");
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "crowded-pilot-manifest.json"), JsonSerializer.Serialize(new
        {
            variant, preview, bodyOnly, reducedEffects = reduced,
            richMaterialRequested = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICH_MATERIALS") == "1",
            fixture = "Controlled twenty-subject Standard-rank formation; authored body size, health and movement speed. Four disabled legal towers provide real condition SourceIds without shooting into the comparison.",
            seed = "The fixture uses no random source.", wave = 0, difficulty = "normal", challenge = "sandbox_lab",
            fps = 30, seconds = 8, fixedSimulationHz = 60, healthMultiplier = 1, movementMultiplier = 1,
            presentationLeadSeconds = 0, pointer = new { x = 0, y = 0 },
            resolution = new { width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440 },
            statusTiming = "Direct synthetic applications at 1s, duration5s: Burn2DPS, Slow35%, ArmorBreak4, Prism-sourced Expose10%. Temporary statuses expire at6s. Shield and repair follow their actual untimed mechanical state and are not falsely expired.",
            repairSetup = "Repair samples take240 health damage, then clear the existing one-second pause before rendering. Burn+repair has200 granted shield so periodic Burn hits do not pause health repair. All other shields are natural Aegis shields or explicitly depleted for a bare-shell control.",
            measurement = "CPU render submission excludes GPU completion, readback and PNG encoding. Metrics cover the normal game/UI pass; extra body-only keyframes are excluded.",
            assertions, captures
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static float CrowdedPilotHeading(GameSession session, EnemyInstance enemy)
    {
        var look = EnemyModelArt.BodyRadius(enemy) * .35f;
        var ahead = session.Map.Path.GetPosition(Math.Clamp(enemy.DistanceAlongPath + look, 0, session.Map.Path.TotalLength));
        var behind = session.Map.Path.GetPosition(Math.Clamp(enemy.DistanceAlongPath - look, 0, session.Map.Path.TotalLength));
        var direction = ahead - behind;
        return MathF.Atan2(direction.Y, direction.X);
    }

    private static Dictionary<StatusType, int> PlaceCrowdedPilotSources(GameSession session)
    {
        var sourceDefinitions = new[]
        {
            (StatusType.Burn, "ember_coil"), (StatusType.Slow, "frost_spire"),
            (StatusType.ArmorBreak, "breaker_cannon"), (StatusType.Exposed, "prism_beam")
        };
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
        for (var x = region.Left + 24; x < region.Right - 16; x += 24)
            candidates.Add(new Vector2(x, y));
        var sources = new Dictionary<StatusType, int>();
        foreach (var (status, definition) in sourceDefinitions)
        {
            if (!candidates.OrderByDescending(point => point.X + point.Y).Any(point => session.TryPlaceTower(definition, point)))
                throw new InvalidOperationException($"Cannot place a legal {definition} source in {session.Map.Definition.Id}.");
            var tower = session.SelectedTower!;
            if (!session.ToggleSandboxTower(tower.Id))
                throw new InvalidOperationException($"Cannot disable the {definition} fixture source.");
            sources.Add(status, tower.Id);
        }
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        return sources;
    }

    private static List<CrowdedPilotSubject> CreateCrowdedPilotSubjects(GameSession session)
    {
        var families = new[] { "t3_brute", "t2_runner", "t4_aegis", "t5_regenerator" };
        string[][] conditions =
        [
            ["untreated-neighbor", "burn", "slow", "burn-slow", "burn-armor-break"],
            ["untreated-neighbor", "burn", "slow", "burn-slow", "expose"],
            ["untreated-neighbor", "shielded", "slow", "burn-slow-shield", "expose"],
            ["untreated-neighbor", "repair-active", "burn-repair", "burn-armor-break", "expose"]
        ];
        var subjects = new List<CrowdedPilotSubject>();
        for (var group = 0; group < 5; group++)
        for (var family = 0; family < families.Length; family++)
        {
            session.SpawnEnemy(families[family], 1, 1);
            var enemy = session.Enemies[^1];
            var condition = conditions[family][group];
            var distance = 80 + subjects.Count * 44;
            enemy.SetSandboxPathDistance(distance, session.Map.Path);
            if (families[family] == "t4_aegis" && !condition.Contains("shield", StringComparison.Ordinal))
                enemy.AbsorbShield(enemy.Shield);
            subjects.Add(new(enemy, condition, distance));
        }
        return subjects;
    }

    private static void ApplyCrowdedPilotConditions(GameSession session, CrowdedPilotSubject subject,
        IReadOnlyDictionary<StatusType, int> sources)
    {
        var enemy = subject.Enemy;
        if (subject.Condition.Contains("repair", StringComparison.Ordinal))
        {
            session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 240, ArmorPierce = 1000, IgnoreShield = true });
            enemy.TickRuntimeTimers(1.1f);
            if (subject.Condition.Contains("burn", StringComparison.Ordinal)) enemy.GrantShield(200, 200);
        }
        if (subject.Condition.Contains("burn", StringComparison.Ordinal)) Apply(StatusType.Burn, 2, 4);
        if (subject.Condition.Contains("slow", StringComparison.Ordinal)) Apply(StatusType.Slow, .35f);
        if (subject.Condition.Contains("armor-break", StringComparison.Ordinal)) Apply(StatusType.ArmorBreak, 4);
        if (subject.Condition.Contains("expose", StringComparison.Ordinal)) Apply(StatusType.Exposed, .10f);

        void Apply(StatusType type, float magnitude, float armorPierce = 0) => enemy.ApplyStatus(new StatusApplication
        {
            Type = type, Duration = 5, Magnitude = magnitude, ArmorPierce = armorPierce, SourceId = sources[type]
        });
    }
}
