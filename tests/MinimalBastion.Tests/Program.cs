using MinimalBastion;
using MinimalBastion.Combat;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Diagnostics;
using MinimalBastion.Economy;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.Simulation;
using MinimalBastion.Towers;
using MinimalBastion.Tactics;
using MinimalBastion.UI;
using MinimalBastion.Waves;
using Microsoft.Xna.Framework;
using System.Text.Json;
using EconomyService = MinimalBastion.Economy.Economy;

namespace MinimalBastion.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Any(x => x.Equals("--simulate", StringComparison.OrdinalIgnoreCase) || x.Equals("--simulate-full", StringComparison.OrdinalIgnoreCase)))
        {
            var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
            return SimulationCli.Run(new ContentLoader(root).Load(), args, args.Any(x => x.Equals("--simulate-full", StringComparison.OrdinalIgnoreCase)));
        }

        if (args.Length > 0 && args.Any(x => x.Equals("--balance", StringComparison.OrdinalIgnoreCase)))
        {
            var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
            BalanceSimulation.Run(new ContentLoader(root).Load());
            return 0;
        }

        var tests = new (string Name, Action Test)[]
        {
            ("content counts", ContentCounts),
            ("high-resolution viewport", HighResolutionViewport),
            ("persistent display and audio settings", PersistentUserSettings),
            ("crash report fallback", CrashReportFallback),
            ("tactical color palette", TacticalColorPalette),
            ("map roster and power nodes", MapRosterAndPowerNodes),
            ("difficulty profiles and persistence", DifficultyProfilesAndPersistence),
            ("challenge directives and persistence", ChallengeDirectivesAndPersistence),
            ("power node tower intel", PowerNodeTowerIntel),
            ("pause UI glyph coverage", PauseUiGlyphCoverage),
            ("opening wave balance", OpeningWaveBalance),
            ("path progress", PathProgress),
            ("target modes", TargetModes),
            ("damage and armor", DamageAndArmor),
            ("damage over time floor", DamageOverTimeFloor),
            ("status effects", StatusEffects),
            ("effect budget", EffectBudget),
            ("elite and boss ranks", EliteAndBossRanks),
            ("economy", EconomyRules),
            ("placement rules", PlacementRules),
            ("wave final group", WaveFinalGroup),
            ("endless wave continuation", EndlessWaveContinuation),
            ("early wave call reward", EarlyWaveCallReward),
            ("mixed wave composition", MixedWaveComposition),
            ("arc relay chain", ArcRelayChain),
            ("frost area control", FrostAreaControl),
            ("mortar predictive aim", MortarPredictiveAim),
            ("economy telemetry", EconomyTelemetry),
            ("run statistics", RunStatistics),
            ("deep-run telemetry saturation", DeepRunTelemetrySaturation),
            ("sold tower utility persistence", SoldTowerUtilityPersistence),
            ("defeat field inspection", DefeatFieldInspection),
            ("co-op shared control commands", CoOpOwnershipCommands),
            ("network deterministic commands", NetworkDeterministicCommands),
            ("co-op command history bounds", CoOpCommandHistoryBounds),
            ("co-op buffered jitter commands", CoOpBufferedJitterCommands),
            ("co-op active state snapshot", CoOpActiveStateSnapshot),
            ("co-op malformed snapshot rejection", CoOpMalformedSnapshotRejection),
            ("co-op checksum coverage", CoOpChecksumCoverage),
            ("co-op reconnect combat soak", CoOpReconnectCombatSoak),
            ("co-op wave ready", CoOpWaveReady),
            ("co-op cursor presence", CoOpCursorPresence),
            ("online co-op transport", CoOpLoopbackTransport),
            ("online co-op framing bounds", CoOpFramingBounds),
            ("online co-op reconnect transport", CoOpReconnectTransport),
            ("co-op invalid code", CoOpInvalidCode),
            ("co-op incompatible build", CoOpIncompatibleBuild),
            ("build fingerprint content coverage", BuildFingerprintContentCoverage),
            ("content identity validation", ContentIdentityValidation),
            ("online endpoint parsing", OnlineEndpointParsing),
            ("tower information", TowerInformation),
            ("tower library reference", TowerLibraryReference),
            ("tower tier two doctrines", TowerTierTwoDoctrines),
            ("balance benchmark doctrine coverage", BalanceBenchmarkDoctrineCoverage),
            ("tower specializations", TowerSpecializations),
            ("tower overdrive", TowerOverdrive),
            ("emergency pulse plates", EmergencyPulsePlates),
            ("charge forge production", ChargeForgeProduction),
            ("checkpoint round trip", CheckpointRoundTrip),
            ("independent solo and co-op save slots", IndependentSaveSlots),
            ("save slot recovery backup", SaveSlotRecoveryBackup),
            ("persistent run history", PersistentRunHistory),
            ("headless simulation deterministic", HeadlessSimulationDeterministic),
            ("simulation campaign default", SimulationCampaignDefault)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Test();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
        return failed == 0 ? 0 : 1;
    }

    private static void SimulationCampaignDefault()
    {
        Check.Equal(20, SimulationCli.ResolveMaximumWave(["--simulate-full"], 20), "default simulation wave cap");
        Check.Equal(30, SimulationCli.ResolveMaximumWave(["--simulate-full", "--max-wave", "30"], 20), "explicit endless wave cap");
        Check.Equal(1, SimulationCli.ResolveMaximumWave(["--simulate", "--max-wave=0"], 20), "minimum explicit wave cap");
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var path = SimulationCli.ParseForcedBuild("siege_mortar:mortar_loader>quake_shell", content);
        Check.Equal("siege_mortar", path!.TowerId, "forced build parser tower");
        Check.Equal("mortar_loader", path.DoctrineId, "forced build parser doctrine");
        Check.Equal("quake_shell", path.SpecializationId, "forced build parser final role");
    }

    private static void CrashReportFallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MinimalBastionCrash-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "latest-crash.log");
        try
        {
            Check.Equal(path, CrashReporter.TryWrite(new InvalidOperationException("diagnostic sentinel"), path)!,
                "crash reporter returns the written destination");
            var report = File.ReadAllText(path);
            Check.True(report.Contains("MINIMAL BASTION CRASH REPORT", StringComparison.Ordinal) &&
                report.Contains("InvalidOperationException", StringComparison.Ordinal) &&
                report.Contains("diagnostic sentinel", StringComparison.Ordinal),
                "crash report contains build context and the complete exception");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void BalanceBenchmarkDoctrineCoverage()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal(70, BalanceSimulation.ValidateTierConfigurations(content),
            "balance benchmark covers L1, two doctrines, and four final combinations for every tower");
    }

    private static void ContentCounts()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal(10, content.Towers.Count, "tower count");
        Check.Equal(5, content.Enemies.Count, "enemy count");
        Check.Equal(20, content.Waves.Waves.Count, "wave count");
        Check.Equal(4, content.Maps.Count, "map count");
        Check.Equal(4, content.Challenges.Count, "challenge directive count");
        Check.Equal(1090, content.Waves.Waves.SelectMany(x => x.Groups).Sum(x => x.Count), "enemy count in waves");
        Check.True(content.Waves.Waves.SelectMany(x => x.Groups).Count(x => x.Rank.Equals("Elite", StringComparison.OrdinalIgnoreCase)) >= 5, "elite encounter groups");
        Check.Equal(1, content.Waves.Waves.SelectMany(x => x.Groups).Count(x => x.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)), "final boss group");
        Check.True(content.Towers.Values.Select(x => x.Visual.Primary).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 8, "tower palette");
        Check.True(content.Enemies.Values.Select(x => x.Visual.Primary).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 5, "enemy palette");
        Check.True(!content.Map.Background.Base.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase), "map palette");
        Check.Equal(2, content.Tactics.EmergencyDefense.Charges, "pulse plate charges");
        Check.Equal(60, content.Tactics.EmergencyDefense.PurchaseCost, "pulse plate direct cost");
        Check.Equal(15, content.Tactics.EmergencyDefense.DirectPurchaseCostIncrease, "pulse plate escalating direct cost");
        Check.Equal(16, content.Tactics.EmergencyDefense.MaximumActive, "pulse plate field capacity");
        Check.True(content.Tactics.EmergencyDefense.SlowPercent > 0, "pulse plate disruption slow");
        Check.True(content.Tactics.EmergencyDefense.KnockbackDistance <= content.Tactics.EmergencyDefense.MinimumSpacing,
            "pulse plate push cannot leap backward across a packed plate field");
        Check.True(content.Tactics.EmergencyDefense.KnockbackGraceSeconds >= 0.5f,
            "pulse plate anti-chain grace");
        Check.True(content.Tactics.EmergencyDefense.BossKnockbackMultiplier < content.Tactics.EmergencyDefense.EliteKnockbackMultiplier,
            "boss knockback resistance exceeds elite resistance");
        Check.Equal(3, content.Tactics.Generator.Levels.Count, "charge forge levels");
        Check.True(content.Tactics.Generator.Levels.Select(x => x.ProductionSeconds).SequenceEqual(new[] { 34f, 26f, 20f }),
            "charge forge production curve");
        Check.True(content.Towers["prism_beam"].Levels.Select(x => x.ArmorPierce).SequenceEqual(new[] { 3f, 5f, 8f }), "prism beam penetration curve");
        Check.Equal(20, content.Towers.Values.Sum(x => x.Specializations.Count), "specialization count");
        Check.True(content.Towers.Values.All(x => x.Specializations.Count == 2), "every tower has two final roles");
        Check.Equal(20, content.Towers.Values.Sum(x => x.Tier2Doctrines.Count), "tier two doctrine count");
        Check.True(content.Towers.Values.All(x => x.Tier2Doctrines.Count == 2), "every tower has two tier two doctrines");
        Check.True(content.Towers.Values.All(tower => tower.Tier2Doctrines.Any(x => x.AttackSpeedMultiplier > 1 || x.UtilityMultiplier > 1) &&
                                                     tower.Tier2Doctrines.Any(x => x.DamageMultiplier > 1 || x.RangeMultiplier > 1)),
            "doctrines offer distinct tempo/utility and power/reach tradeoffs");
        Check.Equal(10, content.Towers.Values.Select(x => x.Protocol.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "every tower has a distinct named protocol");
        Check.True(content.Towers["signal_beacon"].Protocol.AuraAttackSpeedBonus > 0 &&
            content.Towers["frost_spire"].Protocol.BurstStatus.Equals("Slow", StringComparison.OrdinalIgnoreCase),
            "support and control protocols expose thematic effects");

        var shard = content.Towers["shard_fan"];
        var watchtower = content.Towers["watchtower"];
        Check.True(TowerInfo.RawDps(shard.Levels[^1]) > TowerInfo.RawDps(watchtower.Levels[^1]),
            "short-range swarm tower earns higher peak slot DPS than Watchtower");
        Check.True(shard.Levels[^1].Range < watchtower.Levels[0].Range,
            "Watchtower retains a distinct range advantage");
        Check.True(shard.Levels.All(level => level.ArmorPierce > 0),
            "Shard Fan retains a short-range payoff into mixed armored crowds");

        var frost = content.Towers["frost_spire"];
        var permafrost = frost.Specializations.Single(x => x.Id == "permafrost").Level;
        var hail = frost.Specializations.Single(x => x.Id == "hail_lancer").Level;
        Check.True(permafrost.SlowPercent >= hail.SlowPercent + 0.20f && permafrost.SlowDuration >= hail.SlowDuration + 1f,
            "Permafrost owns sustained control");
        Check.True(TowerInfo.RawDps(hail) >= TowerInfo.RawDps(permafrost) * 3f,
            "Hail Lancer owns direct area damage");

        var ember = content.Towers["ember_coil"];
        var wildfire = ember.Specializations.Single(x => x.Id == "wildfire_matrix").Level;
        var searing = ember.Specializations.Single(x => x.Id == "searing_brand").Level;
        Check.True(wildfire.SplashRadius >= searing.SplashRadius + 40f, "Wildfire owns clustered burn");
        Check.True(searing.BurnDamagePerSecond >= wildfire.BurnDamagePerSecond * 2f && searing.ArmorPierce > 0,
            "Searing owns durable single-target burn");
        var shardBloom = shard.Specializations.Single(x => x.Id == "razor_bloom").Level;
        var shardLance = shard.Specializations.Single(x => x.Id == "lance_fan").Level;
        Check.True(shardBloom.PelletCount > shardLance.PelletCount && shardLance.ArmorPierce > shardBloom.ArmorPierce,
            "Shard branches separate crowd coverage from armor pressure");
        var mortar = content.Towers["siege_mortar"];
        var salvo = mortar.Specializations.Single(x => x.Id == "salvo_rack").Level;
        var quake = mortar.Specializations.Single(x => x.Id == "quake_shell").Level;
        Check.True(salvo.AttacksPerSecond > quake.AttacksPerSecond && quake.SplashRadius > salvo.SplashRadius && quake.SlowPercent > 0,
            "Mortar branches separate frequent shells from wide control");
        Check.True(mortar.Levels.Select(level => level.SplashTargetLimit).SequenceEqual(new[] { 6, 7, 8 }) &&
            salvo.SplashTargetLimit == 7 && quake.SplashTargetLimit == 10,
            "Mortar impact caps bound extreme crowd scaling while Quake owns wider control");
        var beacon = content.Towers["signal_beacon"];
        Check.True(beacon.Specializations.Any(x => x.Level.AuraAttackSpeedBonus >= 0.45f) &&
            beacon.Specializations.Any(x => x.Level.AuraRangeBonus >= 0.35f),
            "Beacon branches separate tempo from reach");
    }

    private static void DifficultyProfilesAndPersistence()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal(4, content.Difficulties.Count, "difficulty count");

        var hard = new GameSession(content, "foundry_loop", "hard");
        var normal = new GameSession(content, "foundry_loop", "normal");
        var easy = new GameSession(content, "foundry_loop", "easy");
        Check.Equal(400, hard.Economy.Credits, "hard preserves original starting credits");
        Check.Equal(20, hard.Economy.StartingLives, "hard preserves original starting lives");
        Check.Equal(450, normal.Economy.Credits, "normal starting credit allowance");
        Check.Equal(24, normal.Economy.StartingLives, "normal starting lives");
        Check.Equal(500, easy.Economy.Credits, "easy starting credit allowance");
        Check.Equal(30, easy.Economy.StartingLives, "easy starting lives");
        Check.True(content.Difficulties["normal"].ModifierSummary.Contains("ENEMY HP 90%") &&
            content.Difficulties["normal"].ModifierSummary.Contains("START CREDITS 112.5%") &&
            content.Difficulties["normal"].ModifierSummary.EndsWith("24 LIVES"),
            "difficulty selector exposes exact mechanical modifiers");

        hard.SpawnEnemy("t1_crawler", 1, 1);
        normal.SpawnEnemy("t1_crawler", 1, 1);
        Check.Nearly(70, hard.Enemies[0].MaxHealth, "hard keeps authored enemy health");
        Check.Nearly(63, normal.Enemies[0].MaxHealth, "normal applies its enemy health profile");
        Check.Nearly(68.6f, normal.Enemies[0].CurrentSpeed, "normal applies its enemy speed profile");

        var normalPersist = new GameSession(content, "foundry_loop", "normal");
        var normalSave = normalPersist.CaptureSaveGame();
        Check.Equal("normal", normalSave.DifficultyId, "save captures difficulty");
        Check.Equal("normal", GameSession.RestoreSaveGame(content, normalSave).DifficultyId, "save restores difficulty");
        var snapshot = normalPersist.CaptureCoOpState(4, 0, false);
        Check.Equal("normal", GameSession.RestoreCoOpState(content, snapshot, 2).DifficultyId, "co-op snapshot restores difficulty");

        normalSave.DifficultyId = "";
        Check.Equal("hard", GameSession.RestoreSaveGame(content, normalSave).DifficultyId, "legacy saves retain original hard rules");
        Check.True(SessionChecksum.Compute(hard, 0) != SessionChecksum.Compute(easy, 0), "difficulty identity contributes to checksum");

        var ui = new UIManager(null!);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        Check.Equal("normal", ui.SelectedDifficultyId, "new game UI defaults to normal");
        Check.Equal(UiAction.Play, ui.HandleMainMenu(WorldInput(Vector2.Zero) with { EnterPressed = true }),
            "title Enter starts the prominent new-game action");
        ui.HandleMainMenu(WorldInput(new Vector2(685, 390)) with { LeftPressed = true });
        Check.Equal("hard", ui.SelectedDifficultyId, "difficulty selector cycles profiles");
    }

    private static void ChallengeDirectivesAndPersistence()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var standard = new GameSession(content, "foundry_loop", "hard", "standard");
        var close = new GameSession(content, "foundry_loop", "hard", "close_quarters");
        var core = new GameSession(content, "foundry_loop", "hard", "core_six");
        var noReserves = new GameSession(content, "foundry_loop", "hard", "no_reserves");

        Check.Equal(400, standard.Economy.Credits, "standard directive preserves starting economy");
        Check.Equal(440, close.Economy.Credits, "close-quarters compensation is fixed at session start");
        Check.True(!close.IsTowerAvailable("watchtower") && !close.IsTowerAvailable("siege_mortar"),
            "close quarters removes the two remote artillery towers");
        Check.Equal(PlacementFailure.TowerUnavailable, close.ValidatePlacement("watchtower", new Vector2(50, 200)),
            "restricted towers report an explicit directive failure");
        Check.True(core.IsTowerAvailable("ember_coil") && !core.IsTowerAvailable("prism_beam"),
            "core-six roster retains its authored compact arsenal");
        Check.True(!noReserves.TacticalSystemsEnabled && noReserves.EmergencyInventory == 0,
            "no-reserves disables tactical inventory");
        Check.Equal(PlacementFailure.TacticalSystemsDisabled,
            noReserves.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(200, 30)),
            "no-reserves rejects pulse placement explicitly");

        var saved = close.CaptureSaveGame();
        Check.Equal("close_quarters", saved.ChallengeId, "save captures challenge directive");
        Check.Equal("close_quarters", GameSession.RestoreSaveGame(content, saved).ChallengeId, "save restores challenge directive");
        var snapshot = core.CaptureCoOpState(8, 0, false);
        Check.Equal("core_six", GameSession.RestoreCoOpState(content, snapshot, 2).ChallengeId,
            "co-op snapshot restores challenge directive");
        Check.True(SessionChecksum.Compute(standard, 0) != SessionChecksum.Compute(close, 0),
            "challenge identity contributes to deterministic checksum");

        var ui = new UIManager(null!);
        ui.ConfigureChallenges(content.Challenges.Values);
        Check.Equal("standard", ui.SelectedChallengeId, "challenge UI defaults to standard");
        ui.HandleMainMenu(WorldInput(new Vector2(790, 390)) with { LeftPressed = true });
        Check.Equal("close_quarters", ui.SelectedChallengeId, "challenge selector advances to close quarters");
    }

    private static void HighResolutionViewport()
    {
        Check.Equal(2560, GameConstants.RenderWidth, "supersampled render width");
        Check.Equal(1440, GameConstants.RenderHeight, "supersampled render height");
        var transform = new ViewportTransform();
        transform.Update(2048, 1125);
        Check.Equal(new Rectangle(24, 0, 2000, 1125), transform.DestinationRectangle, "centered fullscreen letterbox");
        Check.Nearly(0, transform.ScreenToLogical(new Point(24, 0)).X, "left canvas edge maps to logical zero");
        Check.Nearly(GameConstants.LogicalWidth, transform.ScreenToLogical(new Point(2024, 1125)).X, "right canvas edge maps to logical width");
    }

    private static void PersistentUserSettings()
    {
        var settings = new UserSettings { WindowWidth = 10, WindowHeight = 9000, SfxVolume = -2 };
        settings.Normalize();
        Check.Equal(960, settings.WindowWidth, "minimum output width");
        Check.Equal(2160, settings.WindowHeight, "maximum output height");
        Check.Nearly(0, settings.SfxVolume, "sound volume clamp");

        settings.CycleResolution();
        Check.Equal(1280, settings.WindowWidth, "unknown resolution enters first preset");
        Check.Equal(720, settings.WindowHeight, "first preset aspect ratio");
        settings.CycleResolution();
        Check.Equal(1600, settings.WindowWidth, "resolution preset cycles");

        var ui = new UIManager(null!);
        ui.ConfigureSettings(settings);
        Check.Equal(UiAction.Settings,
            ui.HandleMainMenu(WorldInput(new Vector2(730, 550)) with { LeftPressed = true }),
            "main menu settings button");
        Check.Equal(UiAction.ApplySettings,
            ui.HandleSettingsInput(WorldInput(new Vector2(500, 245)) with { LeftPressed = true }),
            "display mode changes apply immediately");
        Check.True(settings.Fullscreen, "settings UI toggles fullscreen");
        ui.HandleSettingsInput(WorldInput(Vector2.Zero) with { NavigateDownPressed = true });
        Check.Equal(1, ui.SelectedSettingsIndex, "settings Down selects the resolution control");
        Check.Equal(UiAction.ApplySettings,
            ui.HandleSettingsInput(WorldInput(Vector2.Zero) with { NavigateLeftPressed = true }),
            "settings Left applies a reverse adjustment");
        Check.Equal(1280, settings.WindowWidth, "settings reverse resolution navigation selects the previous preset");
        Check.Equal(UiAction.ApplySettings,
            ui.HandleSettingsInput(WorldInput(Vector2.Zero) with { EnterPressed = true }),
            "settings Enter activates the focused control");
        Check.Equal(1600, settings.WindowWidth, "settings Enter advances the focused resolution");
        Check.Equal(UiAction.CloseSettings,
            ui.HandleSettingsInput(WorldInput(Vector2.Zero) with { EscapePressed = true }),
            "escape closes settings safely");

        var directory = Path.Combine(Path.GetTempPath(), $"MinimalBastionSettings-{Guid.NewGuid():N}");
        try
        {
            var repository = new UserSettingsRepository(directory);
            repository.Save(new UserSettings { WindowWidth = 1600, WindowHeight = 900, SfxVolume = 0.25f });
            repository.Save(new UserSettings { WindowWidth = 1920, WindowHeight = 1080, SfxVolume = 0.75f });
            Check.Equal(1920, repository.Load().WindowWidth, "settings repository loads its current generation");
            File.WriteAllText(repository.SettingsPath, "{ interrupted");
            var recovered = repository.Load();
            Check.Equal(1600, recovered.WindowWidth, "corrupt settings recover from the previous valid generation");
            Check.Nearly(0.25f, recovered.SfxVolume, "settings recovery preserves audio choices");

            repository.Save(new UserSettings { WindowWidth = 2560, WindowHeight = 1440, SfxVolume = float.NaN });
            Check.Nearly(0.65f, repository.Load().SfxVolume, "nonfinite runtime volume normalizes before persistence");
            File.WriteAllText(repository.SettingsPath, "not json");
            Check.Equal(1600, repository.Load().WindowWidth,
                "saving after corruption does not overwrite the last known-good settings backup");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void TacticalColorPalette()
    {
        Check.Equal(new Color(21, 43, 70), ColorPalette.Navy, "deep navy HUD");
        Check.Equal(new Color(56, 78, 101), ColorPalette.Path, "muted slate road");
        Check.Equal(new Color(244, 245, 248), ColorPalette.Paper, "soft off-white surface");
        Check.Equal(new Color(33, 146, 170), ColorPalette.Cyan, "controlled cyan accent");
        Check.Equal(new Color(42, 194, 117), ColorPalette.Green, "controlled green accent");
        Check.True(ColorPalette.ContrastRatio(ColorPalette.Paper, ColorPalette.Berry) >= 4.5f,
            "map-selector berry supports readable light text");
        var paletteContent = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        foreach (var id in new[] { "breaker_cannon", "signal_beacon" })
        {
            var primary = paletteContent.Towers[id].Visual.PrimaryColor;
            var readable = ColorPalette.ReadableAccent(primary, ColorPalette.PanelAlt);
            Check.True(ColorPalette.ContrastRatio(readable, ColorPalette.PanelAlt) >= 4.49f,
                $"{id} library accent text meets readable contrast");
            Check.True(readable != ColorPalette.Ink,
                $"{id} library accent text retains tower color identity");
        }
        Check.Equal(new Color(232, 182, 55), ColorPalette.Gold, "controlled gold accent");
        Check.Equal(new Color(236, 80, 98), ColorPalette.Coral, "controlled coral accent");
        Check.Equal(new Color(124, 83, 218), ColorPalette.Violet, "controlled violet accent");
        Check.Equal(new Color(44, 122, 231), ColorPalette.Cobalt, "controlled blue accent");

        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        foreach (var map in content.Maps.Values)
            Check.Equal(new Color(21, 45, 54), map.Background.BaseColor, $"{map.Id} battlefield foundation");
    }

    private static void MapRosterAndPowerNodes()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var relay = content.Maps["relay_divide"];
        var prism = content.Maps["prism_circuit"];
        var crosswind = content.Maps["crosswind_basin"];
        Check.Equal(4, content.Maps.Values.Select(map => map.Background.Motif).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "each arena has a distinct background motif");
        Check.True(content.Maps.Values.All(map => !map.Background.Motif.Equals("none", StringComparison.OrdinalIgnoreCase)),
            "every arena opts into a visual identity motif");
        Check.Equal("conduit", prism.PathVisual.Style, "Prism uses a distinct conduit path");
        Check.Equal("channel", crosswind.PathVisual.Style, "Crosswind uses a distinct channel path");
        Check.Equal(0, crosswind.PowerNodes.Count, "Crosswind relies on crossfire geometry rather than power nodes");
        Check.Equal("crosswind_waves", crosswind.WaveSet, "Crosswind has its own campaign");
        Check.True(content.WaveSets[crosswind.WaveSet].Waves[1].Groups.Any(x => x.EnemyId == "t2_runner"),
            "Crosswind introduces its runner theme immediately");
        var crosswindSession = new GameSession(content, crosswind.Id, "hard");
        Check.True(crosswindSession.TryPlaceTower("needle_turret", new Vector2(250, 320)),
            "Crosswind interior island accepts a practical tower placement");
        var crosswindIntel = WaveIntel.AnalyzeCampaign(content.WaveSets[crosswind.WaveSet], content.Enemies);
        Check.Equal(1066, crosswindIntel.TotalContacts, "Crosswind campaign intel counts the authored roster");
        Check.Equal(118, crosswindIntel.PeakContacts, "Crosswind campaign intel identifies peak density");
        Check.True(crosswindIntel.OpeningThreats.Contains("FAST", StringComparison.Ordinal) && crosswindIntel.BossWave == 20,
            "Crosswind campaign intel exposes its opening identity and boss timing");
        var mapUi = new UIManager(null!);
        mapUi.ConfigureMaps(content.Maps.Values, content.WaveSets, content.Enemies);
        Check.Equal("foundry_loop", mapUi.SelectedMapId, "arena selector starts on Foundry");
        mapUi.HandleMainMenu(WorldInput(new Vector2(500, 370)) with { LeftPressed = true });
        Check.Equal("crosswind_basin", mapUi.SelectedMapId, "arena selector advances by challenge rating");
        Check.Equal(3, prism.PowerNodes.Count, "Prism has a restrained node roster");
        Check.Equal("prism_waves", prism.WaveSet, "Prism has its own campaign");
        Check.True(prism.ChallengeRating > content.Maps["foundry_loop"].ChallengeRating, "Prism challenge is above Foundry");
        Check.True(relay.ChallengeRating > prism.ChallengeRating, "Surge remains the hardest authored arena");
        Check.Equal("surge_waves", relay.WaveSet, "surge map has its own campaign");
        Check.True(content.WaveSets[relay.WaveSet].Waves[1].Groups.Any(x => x.EnemyId == "t2_runner"),
            "surge campaign introduces runners earlier than Foundry");
        Check.True(JsonSerializer.Serialize(content.WaveSets[relay.WaveSet].Waves) != JsonSerializer.Serialize(content.Waves.Waves),
            "map campaigns are authored independently");
        Check.Equal(9, relay.PowerNodes.Count, "surge node count");
        Check.True(relay.PowerNodes.All(x => x.Radius <= 42), "surge nodes stay compact");
        Check.True(relay.PowerNodes.Any(x => x.DamageBonus > 0), "damage node exists");
        Check.True(relay.PowerNodes.Any(x => x.ArmorPierceBonus > 0), "armor-pierce node exists");
        var session = new GameSession(content, relay.Id);
        Check.Equal("relay_divide", session.Map.Definition.Id, "selected map session");
        Check.True(session.TryPlaceTower("needle_turret", new Vector2(72, 285)), "tower on accelerator node");
        Check.Nearly(2.36f, session.GetEffectiveAttacksPerSecond(session.Towers[0]), "accelerator attack speed");
        Check.True(session.TryPlaceTower("needle_turret", new Vector2(285, 330)), "tower on amplifier node");
        Check.Nearly(9.2f, session.GetEffectiveDamage(session.Towers[1], 8), "amplifier damage");
        Check.True(session.TryPlaceTower("needle_turret", new Vector2(500, 330)), "tower on wideband node");
        Check.Nearly(137.5f, session.GetEffectiveRange(session.Towers[2]), "wideband range bonus");
        var defaultSession = new GameSession(content, "foundry_loop");
        Check.True(SessionChecksum.Compute(defaultSession, 0) != SessionChecksum.Compute(session, 0), "map identity contributes to checksum");
    }

    private static void PowerNodeTowerIntel()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var session = new GameSession(content, "relay_divide");
        var position = new Vector2(285, 330);
        var nodes = session.Map.GetPowerNodes(position);
        Check.Equal(1, nodes.Count, "amplifier overlap count");
        Check.Equal("Amplifier Node", nodes[0].DisplayName, "specific overlapping node");
        Check.Equal("DAMAGE +15%", TowerInfo.PowerNodeBonus(nodes[0]), "node bonus label");
        Check.Equal("DAMAGE 8>9.2", TowerInfo.PowerNodeStatChange(content.Towers["needle_turret"], content.Towers["needle_turret"].Levels[0], session.Map.GetPowerBuff(position)), "node stat delta");
        Check.Equal("NO COMPATIBLE COMBAT STAT CHANGE", TowerInfo.PowerNodeStatChange(content.Towers["signal_beacon"], content.Towers["signal_beacon"].Levels[0], session.Map.GetPowerBuff(position)), "support compatibility warning");
    }

    private static void PauseUiGlyphCoverage()
    {
        foreach (var canSave in new[] { false, true })
            Check.True(UIManager.PauseCheckpointStatus(canSave).All(character => character is >= ' ' and <= '~'), "pause status uses compiled ASCII glyphs");
        Check.Equal(UiAction.Resume,
            new UIManager(null!).HandlePausedInput(WorldInput(Vector2.Zero) with { EnterPressed = true }, Session()),
            "pause Enter resumes the match");
        var restartUi = new UIManager(null!);
        Check.Equal(UiAction.None,
            restartUi.HandlePausedInput(WorldInput(new Vector2(640, 467)) with { LeftPressed = true }, Session()),
            "first pause-menu restart click arms confirmation");
        Check.Equal(UiAction.Restart,
            restartUi.HandlePausedInput(WorldInput(new Vector2(640, 467)) with { LeftPressed = true }, Session()),
            "second pause-menu restart click confirms the reset");
    }

    private static void OpeningWaveBalance()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var session = new GameSession(new ContentLoader(root).Load());
        var positions = new[]
        {
            new Vector2(50, 200),
            new Vector2(220, 200),
            new Vector2(220, 350),
            new Vector2(460, 200)
        };

        foreach (var position in positions)
            Check.True(session.TryPlaceTower("needle_turret", position), $"opening placement at {position}");
        Check.Equal(40, session.Economy.Credits, "opening reserve");
        Check.True(session.StartNextWave(), "start opening wave");
        for (var elapsed = 0f; elapsed < 45f && !session.IsDefeat; elapsed += 0.02f)
            session.Update(0.02f);

        Check.Equal(0, session.Economy.EscapedEnemies, "opening wave leaks");

        var relay = new GameSession(new ContentLoader(root).Load(), "relay_divide");
        foreach (var position in new[] { new Vector2(50, 270), new Vector2(100, 350), new Vector2(285, 330), new Vector2(285, 410) })
            Check.True(relay.TryPlaceTower("needle_turret", position), $"relay opening placement at {position}");
        Check.Equal(0, relay.Economy.Credits, "relay opening budget uses four needles");
        Check.True(relay.StartNextWave(), "start relay opening wave");
        for (var elapsed = 0f; elapsed < 45f && !relay.IsDefeat; elapsed += 0.02f)
            relay.Update(0.02f);
        Check.Equal(0, relay.Economy.EscapedEnemies, "relay opening wave leaks");
    }

    private static void PathProgress()
    {
        var path = new PathRuntime(new[] { Point(0, 0), Point(100, 0), Point(100, 100) });
        Check.Nearly(0, path.GetProgress(0), "start progress");
        Check.Nearly(0.5f, path.GetProgress(100), "corner progress");
        Check.Nearly(0.75f, path.GetProgress(150), "second segment progress");
        Check.Nearly(50, path.GetPosition(150).Y, "position y");
        Check.True(path.DistanceToPath(new Vector2(50, 20)) <= 20.01f, "distance to segment");
    }

    private static void TargetModes()
    {
        var path = new PathRuntime(new[] { Point(0, 0), Point(500, 0) });
        var definition = Enemy("target", 100, 10, 1, 0, 0);
        var first = new EnemyInstance(1, definition, path, 1, 1);
        var second = new EnemyInstance(2, definition, path, 1, 1);
        second.UpdateMovement(20, path);
        var selector = new TargetSelector();
        var enemies = new[] { first, second };
        Check.Equal(2, selector.Select(Vector2.Zero, 500, TargetMode.First, enemies)!.Id, "first");
        Check.Equal(1, selector.Select(Vector2.Zero, 500, TargetMode.Last, enemies)!.Id, "last");
        Check.Equal(1, selector.Select(Vector2.Zero, 5, TargetMode.Nearest, enemies)!.Id, "nearest");
        var fast = new EnemyInstance(3, Enemy("fast", 100, 20, 1, 0, 0), path, 1, 1);
        var armored = new EnemyInstance(4, Enemy("armor", 100, 8, 1, 7, 0), path, 1, 1);
        Check.Equal(3, selector.Select(Vector2.Zero, 500, TargetMode.Fastest, new[] { first, fast, armored })!.Id, "fastest");
        Check.Equal(4, selector.Select(Vector2.Zero, 500, TargetMode.Armored, new[] { first, fast, armored })!.Id, "armored");
    }

    private static void DamageAndArmor()
    {
        var session = Session();
        var path = session.Map.Path;
        var enemy = new EnemyInstance(1, Enemy("armored", 100, 10, 1, 4, 0), path, 1, 1);
        session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 20, ArmorPierce = 0 });
        Check.Nearly(84, enemy.Health, "armor damage");
        session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 10, ArmorPierce = 4 });
        Check.Nearly(74, enemy.Health, "pierced damage");
        var shielded = new EnemyInstance(2, Enemy("shielded", 100, 10, 1, 0, 20), path, 1, 1);
        session.DamageResolver.Apply(shielded, new DamagePayload { Damage = 12 });
        Check.Nearly(100, shielded.Health, "shield prevents health damage");
        Check.Nearly(8, shielded.Shield, "shield remaining");

        var assisted = new EnemyInstance(3, Enemy("assisted", 100, 10, 1, 4, 0), path, 1, 1);
        assisted.StatusEffects.Apply(new StatusApplication { Type = StatusType.ArmorBreak, Duration = 2, Magnitude = 2, SourceId = 7 });
        assisted.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Duration = 2, Magnitude = 0.5f, SourceId = 8 });
        DamageReport? report = null;
        session.DamageResolver.DamageApplied += value => report = value;
        session.DamageResolver.Apply(assisted, new DamagePayload { Damage = 10, SourceTowerId = 9 });
        Check.Nearly(87, assisted.Health, "combined utility modifies actual damage once");
        Check.Equal(8, report!.Value.ExposeSourceTowerId, "damage report identifies expose source");
        Check.Nearly(5, report.Value.ExposeDamageEquivalent, "damage report measures marginal expose damage");
        Check.Equal(7, report.Value.ArmorBreakSourceTowerId, "damage report identifies armor-break source");
        Check.Nearly(2, report.Value.ArmorBreakDamageEquivalent, "damage report measures marginal armor-break damage without double counting");
    }

    private static void DamageOverTimeFloor()
    {
        var session = Session();
        var path = session.Map.Path;
        var enemy = new EnemyInstance(1, Enemy("armored_dot", 100, 10, 1, 4, 0), path, 1, 1);
        session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 0.5f, IsDamageOverTime = true });
        Check.Nearly(100, enemy.Health, "small DOT is absorbed by armor without the hit floor");
        enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 4, SourceId = 7 });
        Check.Nearly(2, enemy.EffectiveArmor, "burning temporarily scorches two armor");
        session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 4, IsDamageOverTime = true });
        Check.Nearly(98, enemy.Health, "scorched armor improves damage over time");
        session.DamageResolver.Apply(enemy, new DamagePayload { Damage = 0.5f });
        Check.Nearly(97, enemy.Health, "normal hit retains one damage floor");
    }

    private static void StatusEffects()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = 0.3f, SourceId = 1 });
        statuses.Apply(new StatusApplication { Type = StatusType.Slow, Duration = 1, Magnitude = 0.5f, SourceId = 2 });
        Check.Nearly(0.5f, statuses.SlowFactor, "strongest slow");
        Check.Equal(2, statuses.Active.Single(x => x.Type == StatusType.Slow).SourceId,
            "stronger status transfers utility attribution to its source");
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 5, SourceId = 1 });
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 7, SourceId = 2 });
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 9, SourceId = 3 });
        Check.Equal(2, statuses.Active.Count(x => x.Type == StatusType.Burn), "burn cap");
        Check.Nearly(16, statuses.ConsumeBurnDamage(1), "burn tick");
        statuses.Update(2.1f);
        Check.Equal(0, statuses.Active.Count, "status expiry");
    }

    private static void EffectBudget()
    {
        var effects = new EffectSystem();
        effects.AddPing(new Vector2(10, 10), Color.Cyan);
        for (var index = 0; index < EffectSystem.MaximumEffects * 2; index++)
            effects.AddBeam(Vector2.Zero, Vector2.One * index, Color.Coral, 0.2f);

        Check.Equal(EffectSystem.MaximumEffects, effects.Effects.Count, "dense transient effects remain hard-capped");
        Check.Equal(1, effects.Effects.Count(effect => effect.Kind == EffectKind.Ping),
            "co-op ping survives transient-effect pressure");
        effects.AddFlash(new Vector2(20, 20), Color.Gold, 0.4f, 80);
        Check.Equal(1, effects.Effects.Count(effect => effect.Kind == EffectKind.Flash),
            "large tactical flash displaces beam noise at capacity");

        for (var index = 0; index < EffectSystem.MaximumPings * 2; index++)
            effects.AddPing(new Vector2(index, index), Color.Cyan);
        Check.Equal(EffectSystem.MaximumPings, effects.Effects.Count(effect => effect.Kind == EffectKind.Ping),
            "co-op ping history has its own bounded budget");
        Check.True(effects.Effects.Count <= EffectSystem.MaximumEffects, "combined effect budget remains bounded");

        effects.Update(2f);
        Check.Equal(0, effects.Effects.Count, "expired effects are released after the budgeted burst");
    }

    private static void EliteAndBossRanks()
    {
        var path = new PathRuntime(new[] { Point(0, 0), Point(1000, 0) });
        var definition = Enemy("ranked", 100, 100, 10, 1, 0);
        var elite = new EnemyInstance(1, definition, path, 1, 1, "Elite");
        Check.True(elite.IsElite, "elite rank parsed");
        Check.Nearly(185, elite.MaxHealth, "elite health multiplier");
        Check.Nearly(3, elite.BaseArmor, "elite armor reinforcement");
        Check.Equal(20, elite.Reward, "elite reward");
        Check.Equal(2, elite.LivesLost, "elite leak penalty");

        var boss = new EnemyInstance(2, definition, path, 1, 1, "Boss");
        Check.True(boss.IsBoss, "boss rank parsed");
        Check.Nearly(450, boss.MaxHealth, "boss health multiplier");
        Check.Nearly(5, boss.BaseArmor, "boss armor reinforcement");
        Check.Equal(50, boss.Reward, "boss reward");
        Check.Equal(10, boss.LivesLost, "boss leak penalty");
        boss.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 10, Magnitude = 0.5f, SourceId = 1 });
        Check.Nearly(4, boss.StatusEffects.Active.Single().RemainingSeconds, "boss status resistance");
        var prePhaseSpeed = boss.CurrentSpeed;
        boss.ApplyHealthDamage(230);
        Check.True(boss.BossPhaseActive, "boss enters overdrive below half health");
        Check.True(boss.Shield >= boss.MaxHealth * 0.12f, "boss restores phase shield");
        Check.True(boss.CurrentSpeed > prePhaseSpeed, "boss overdrive accelerates");
        Check.True(boss.ConsumeBossPhasePulse(), "boss phase pulse emitted once");
        Check.True(!boss.ConsumeBossPhasePulse(), "boss phase pulse does not repeat");
    }

    private static void EconomyRules()
    {
        var economy = new EconomyService(300, 20);
        Check.True(economy.TrySpend(90), "purchase");
        Check.Equal(210, economy.Credits, "purchase balance");
        economy.AwardKill(8);
        economy.AwardWave(1);
        Check.Equal(268, economy.Credits, "rewards");
        economy.LoseLives(3);
        Check.Equal(17, economy.Lives, "lives");
        Check.Equal(1, economy.EscapedEnemies, "escape count");

        var saturated = new EconomyService(1, 20);
        saturated.AddCredits(int.MaxValue);
        saturated.AwardKill(int.MaxValue);
        saturated.AwardWave(int.MaxValue);
        saturated.RecoverSale(int.MaxValue);
        Check.Equal(int.MaxValue, saturated.Credits, "deep-run credits saturate instead of wrapping negative");
        Check.Equal(int.MaxValue, saturated.TotalCreditsEarned, "deep-run earned total saturates");
        Check.Equal(int.MaxValue, saturated.KillCreditsEarned, "deep-run kill income saturates");
        Check.Equal(int.MaxValue, saturated.WaveCreditsEarned, "deep-run wave income saturates");
        Check.Equal(int.MaxValue, saturated.SaleCreditsRecovered, "deep-run sale recovery saturates");
        Check.Equal(int.MaxValue, EconomyService.CalculateWaveReward(int.MaxValue), "extreme wave reward remains nonnegative");
    }

    private static void EconomyTelemetry()
    {
        var economy = new EconomyService(300, 20);
        Check.True(economy.TrySpend(90), "telemetry spend");
        economy.AwardKill(8);
        economy.AwardWave(2);
        economy.AwardEarlyStart();
        economy.RecoverSale(54);
        Check.Equal(90, economy.TotalCreditsSpent, "total spent");
        Check.Equal(8, economy.KillCreditsEarned, "kill income");
        Check.Equal(60, economy.WaveCreditsEarned, "wave income");
        Check.Equal(20, economy.EarlyStartCreditsEarned, "early income");
        Check.Equal(88, economy.TotalCreditsEarned, "earned excludes recycled sale credits");
        Check.Equal(54, economy.SaleCreditsRecovered, "sale recovery");
    }

    private static void RunStatistics()
    {
        var session = Session();
        session.Content.Towers["beacon"] = new TowerDefinition
        {
            Id = "beacon",
            DisplayName = "Test Beacon",
            Behavior = "aura",
            PurchaseCost = 0,
            Levels = new List<TowerLevelDefinition>
            {
                new() { AuraRange = 200, AuraAttackSpeedBonus = 0.5f, AuraRangeBonus = 0.1f }
            }
        };
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "stats tower placement");
        var tower = session.Towers[0];
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 90)), "second stats tower placement");
        var idleTower = session.Towers[1];
        Check.True(session.TryPlaceTower("beacon", new Vector2(110, 200)), "stats beacon placement");
        var beacon = session.Towers[2];
        session.Update(0.01f);
        var buff = session.GetSupportBuff(tower);
        Check.Equal(beacon.Id, buff.AttackSpeedSourceTowerId, "support buff identifies its attack-rate source");
        Check.Equal(beacon.Id, buff.RangeSourceTowerId, "support buff identifies its range source");
        var target = new EnemyInstance(1, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 120, SourceTowerId = tower.Id });
        var towerStats = session.Statistics.Towers.Single(metrics => metrics.TowerId == "tower");
        var beaconStats = session.Statistics.Towers.Single(metrics => metrics.TowerId == "beacon");
        Check.Equal(2, towerStats.Purchases, "stats purchases");
        Check.Equal(1, towerStats.Kills, "stats attributed kills");
        Check.Nearly(100, towerStats.Damage, "stats effective damage");
        Check.Nearly(100, tower.LifetimeDamage, "source tower tracks its own effective damage");
        Check.Equal(1, tower.LifetimeKills, "source tower tracks its own kill");
        Check.Nearly(0, idleTower.LifetimeDamage, "other instances do not inherit aggregate damage");
        Check.Equal(0, idleTower.LifetimeKills, "other instances do not inherit aggregate kills");
        Check.Nearly(100f / 3f, beaconStats.SupportDamageEquivalent, "beacon receives marginal attack-rate contribution credit");
        Check.Nearly(100f / 3f, beaconStats.ContributionDamage, "support contribution participates in run impact");
        Check.Nearly(100f / 3f, beacon.LifetimeSupportDamageEquivalent, "individual beacon retains its assisted damage");
        var utilityTarget = new EnemyInstance(4, session.Content.Enemies["armored"], session.Map.Path, 1, 1);
        utilityTarget.StatusEffects.Apply(new StatusApplication { Type = StatusType.ArmorBreak, Duration = 2, Magnitude = 2, SourceId = idleTower.Id });
        utilityTarget.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Duration = 2, Magnitude = 0.5f, SourceId = idleTower.Id });
        session.DamageResolver.Apply(utilityTarget, new DamagePayload { Damage = 10, SourceTowerId = tower.Id });
        Check.Nearly(5, idleTower.LifetimeExposeDamageEquivalent, "individual source retains expose assist damage");
        Check.Nearly(2, idleTower.LifetimeArmorBreakDamageEquivalent, "individual source retains armor-break assist damage");
        Check.Nearly(5, towerStats.ExposeDamageEquivalent, "run telemetry attributes expose assist damage");
        Check.Nearly(2, towerStats.ArmorBreakDamageEquivalent, "run telemetry attributes armor-break assist damage");
        var statusTarget = new EnemyInstance(3, session.Content.Enemies["armored"], session.Map.Path, 1, 1);
        statusTarget.StatusEffects.Apply(new StatusApplication
        {
            Type = StatusType.Slow,
            Duration = 1,
            Magnitude = 0.4f,
            SourceId = tower.Id
        });
        session.Enemies.Add(statusTarget);
        session.Statistics.Advance(0.2f);
        Check.Nearly(0.2f, towerStats.ControlSeconds, "run telemetry attributes source control uptime");
        Check.Nearly(0.2f, tower.LifetimeControlSeconds, "individual tower retains control uptime");
        session.Enemies.Remove(statusTarget);
        var beaconAssistBeforeSave = beacon.LifetimeSupportDamageEquivalent;
        var beaconAggregateBeforeSave = beaconStats.SupportDamageEquivalent;
        var restoredSession = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame());
        var restoredStatistics = restoredSession.Statistics;
        Check.Nearly(beaconAggregateBeforeSave, restoredStatistics.Towers.Single(metrics => metrics.TowerId == "beacon").SupportDamageEquivalent,
            "support contribution survives save restoration");
        Check.Nearly(beaconAssistBeforeSave, restoredSession.Towers.Single(candidate => candidate.Definition.Id == "beacon").LifetimeSupportDamageEquivalent,
            "individual support contribution survives save restoration");
        Check.Nearly(0.2f, restoredSession.Towers.Single(candidate => candidate.Id == tower.Id).LifetimeControlSeconds,
            "individual control uptime survives save restoration");
        Check.Nearly(5, restoredSession.Towers.Single(candidate => candidate.Id == idleTower.Id).LifetimeExposeDamageEquivalent,
            "individual expose assist survives save restoration");

        var escaped = new EnemyInstance(2, session.Content.Enemies["armored"], session.Map.Path, 1, 1);
        session.OnEnemyEscaped(escaped);
        Check.Equal("armored", session.Statistics.GreatestLeakThreat!.EnemyId, "stats leak threat");
        Check.Equal(1, session.Statistics.GreatestLeakThreat.LivesLost, "stats lives lost");
        session.Update(0.05f);
        Check.Nearly(0.26f, session.Statistics.SimulatedSeconds, "stats defense time");
    }

    private static void DeepRunTelemetrySaturation()
    {
        var session = Session();
        session.Statistics.RestoreSaveData(new RunStatisticsSaveData
        {
            SimulatedSeconds = float.MaxValue,
            EmergencyDeployments = int.MaxValue,
            EmergencyDirectPurchases = int.MaxValue,
            EmergencyTriggers = int.MaxValue,
            EmergencyHits = int.MaxValue,
            EmergencyKills = int.MaxValue,
            EmergencyDamage = float.MaxValue,
            GeneratedCharges = int.MaxValue,
            GeneratorPurchases = int.MaxValue,
            GeneratorUpgrades = int.MaxValue,
            Towers = new List<RunTowerStatisticsSaveData>
            {
                new()
                {
                    TowerId = "tower",
                    DisplayName = "Tower",
                    Purchases = int.MaxValue,
                    Upgrades = int.MaxValue,
                    Sales = int.MaxValue,
                    CreditsSpent = int.MaxValue,
                    CreditsRecovered = int.MaxValue,
                    Hits = int.MaxValue,
                    Kills = int.MaxValue,
                    Overdrives = int.MaxValue,
                    Damage = float.MaxValue,
                    SupportDamageEquivalent = float.MaxValue,
                    ExposeDamageEquivalent = float.MaxValue,
                    ArmorBreakDamageEquivalent = float.MaxValue,
                    ControlSeconds = float.MaxValue,
                    ExposeSeconds = float.MaxValue,
                    ArmorBreakSeconds = float.MaxValue,
                    ArmorAbsorbed = float.MaxValue,
                    Overkill = float.MaxValue,
                    Specializations = new Dictionary<string, int> { ["test"] = int.MaxValue }
                }
            },
            Enemies = new List<RunEnemyStatisticsSaveData>
            {
                new() { EnemyId = "enemy", DisplayName = "Enemy", Kills = int.MaxValue, Escapes = int.MaxValue, LivesLost = int.MaxValue }
            }
        }, Array.Empty<TowerInstance>());

        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "deep-run telemetry source placement");
        var tower = session.Towers.Single();
        var target = new EnemyInstance(1, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 120, SourceTowerId = tower.Id });
        session.OnEnemyEscaped(new EnemyInstance(2, session.Content.Enemies["enemy"], session.Map.Path, 1, 1));
        session.OnEmergencyDefenseTriggered(null!, int.MaxValue);
        session.OnEmergencyChargeProduced();
        session.Statistics.Advance(1);

        var towerMetrics = session.Statistics.Towers.Single(metrics => metrics.TowerId == "tower");
        var enemyMetrics = session.Statistics.Enemies.Single(metrics => metrics.EnemyId == "enemy");
        Check.Equal(int.MaxValue, towerMetrics.Purchases, "tower purchases saturate");
        Check.Equal(int.MaxValue, towerMetrics.Hits, "tower hits saturate");
        Check.Equal(int.MaxValue, towerMetrics.Kills, "tower kills saturate");
        Check.Equal(int.MaxValue, towerMetrics.CreditsSpent, "tower spending telemetry saturates");
        Check.True(float.IsFinite(towerMetrics.ContributionDamage) && towerMetrics.ContributionDamage == float.MaxValue,
            "tower contribution remains finite when all assist channels are saturated");
        Check.Equal(int.MaxValue, enemyMetrics.Kills, "enemy kills saturate");
        Check.Equal(int.MaxValue, enemyMetrics.Escapes, "enemy escapes saturate");
        Check.Equal(int.MaxValue, enemyMetrics.LivesLost, "enemy leak damage saturates");
        Check.Equal(int.MaxValue, session.Statistics.EmergencyTriggers, "plate trigger telemetry saturates");
        Check.Equal(int.MaxValue, session.Statistics.EmergencyHits, "plate hit telemetry saturates");
        Check.Equal(int.MaxValue, session.Statistics.GeneratedCharges, "forge output telemetry saturates");
        Check.True(float.IsFinite(session.Statistics.SimulatedSeconds) && session.Statistics.SimulatedSeconds == float.MaxValue,
            "defense time remains finite at its telemetry limit");

        var save = session.CaptureSaveGame();
        var savedTower = save.Towers.Single();
        savedTower.LifetimeDamage = float.MaxValue;
        savedTower.LifetimeKills = int.MaxValue;
        savedTower.LifetimeSupportDamageEquivalent = float.MaxValue;
        var restored = GameSession.RestoreSaveGame(session.Content, save);
        var restoredTower = restored.Towers.Single();
        var restoredTarget = new EnemyInstance(3, restored.Content.Enemies["enemy"], restored.Map.Path, 1, 1);
        restored.DamageResolver.Apply(restoredTarget, new DamagePayload { Damage = 120, SourceTowerId = restoredTower.Id });
        Check.Equal(int.MaxValue, restoredTower.LifetimeKills, "individual tower kills saturate");
        Check.True(float.IsFinite(restoredTower.LifetimeDamage) && restoredTower.LifetimeDamage == float.MaxValue,
            "individual tower damage remains finite");
        Check.True(float.IsFinite(restoredTower.LifetimeSupportDamageEquivalent) && restoredTower.LifetimeSupportDamageEquivalent == float.MaxValue,
            "individual tower assist remains finite");
    }

    private static void SoldTowerUtilityPersistence()
    {
        var original = Session();
        Check.True(original.TryPlaceTower("tower", new Vector2(50, 200)), "sold utility source placement");
        var sourceId = original.Towers.Single().Id;
        var lingering = new EnemyInstance(87, original.Content.Enemies["enemy"], original.Map.Path, 1, 1);
        lingering.StatusEffects.Apply(new StatusApplication
        {
            Type = StatusType.Slow,
            Duration = 1,
            Magnitude = 0.3f,
            SourceId = sourceId
        });
        original.Enemies.Add(lingering);
        Check.True(original.TrySellTower(sourceId), "sold utility source removal");
        Check.Equal(0, original.Statistics.TrackedTowerObjectCount, "sold tower object is released immediately");
        var savedStatistics = original.Statistics.CaptureSaveData();
        Check.Equal("tower", savedStatistics.TowerDefinitionByInstance[sourceId],
            "sold source definition is retained while its utility is active");

        var restored = Session();
        restored.Statistics.RestoreSaveData(savedStatistics, Array.Empty<TowerInstance>());
        var target = new EnemyInstance(88, restored.Content.Enemies["enemy"], restored.Map.Path, 1, 1);
        target.StatusEffects.Apply(new StatusApplication
        {
            Type = StatusType.Slow,
            Duration = 1,
            Magnitude = 0.3f,
            SourceId = sourceId
        });
        restored.Enemies.Add(target);
        restored.Statistics.Advance(0.25f);
        Check.Nearly(0.25f, restored.Statistics.Towers.Single(value => value.TowerId == "tower").ControlSeconds,
            "lingering sold-tower utility remains attributed after restoration");
        target.StatusEffects.Update(2);
        restored.Statistics.Advance(2.1f);
        Check.True(!restored.Statistics.TowerDefinitionByInstance.ContainsKey(sourceId),
            "expired sold-tower attribution is compacted");

        var churn = Session();
        for (var index = 0; index < 300; index++)
        {
            churn.Economy.AddCredits(100);
            Check.True(churn.TryPlaceTower("tower", new Vector2(50, 200)), "endless churn tower placement");
            Check.True(churn.TrySellTower(churn.Towers.Single().Id), "endless churn tower sale");
        }
        churn.Statistics.Advance(2.1f);
        Check.Equal(0, churn.Statistics.TowerDefinitionByInstance.Count,
            "historical source IDs do not grow under repeated sell/rebuild churn");
        Check.Equal(0, churn.Statistics.TrackedTowerObjectCount,
            "historical sold tower objects do not remain referenced");
    }

    private static void DefeatFieldInspection()
    {
        var ui = new UIManager(null!);
        Check.Equal(UiAction.ViewField,
            ui.HandleResultInput(WorldInput(new Vector2(399, 603)) with { LeftPressed = true }, false),
            "defeat results expose the field-inspection action");
        Check.Equal(UiAction.ContinueEndless,
            ui.HandleResultInput(WorldInput(Vector2.Zero) with { EnterPressed = true }, true),
            "victory Enter continues into endless defense");
        Check.Equal(UiAction.ViewField,
            ui.HandleResultInput(WorldInput(Vector2.Zero) with { EnterPressed = true }, false),
            "defeat Enter opens read-only field inspection");
        Check.Equal(UiAction.None,
            ui.HandleResultInput(WorldInput(new Vector2(621, 603)) with { LeftPressed = true }, false),
            "first result restart click arms confirmation");
        Check.Equal(UiAction.Restart,
            ui.HandleResultInput(WorldInput(new Vector2(621, 603)) with { LeftPressed = true }, false),
            "second result restart click confirms the reset");
        Check.Equal(UiAction.ViewResults,
            ui.HandleDefeatFieldInput(WorldInput(Vector2.Zero) with { EscapePressed = true }),
            "inspection escape returns to results");

        var session = Session();
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "inspection tower placement");
        var tower = session.Towers.Single();
        session.BeginPlacement("tower");

        session.HandleInspectionInput(WorldInput(tower.Position) with { LeftPressed = true });

        Check.True(session.PlacementTowerId is null, "inspection cancels placement tools");
        Check.Equal(tower.Id, session.SelectedTower!.Id, "inspection can select a placed tower");
        var credits = session.Economy.Credits;
        session.HandleInspectionInput(WorldInput(tower.Position) with { RightPressed = true });
        Check.True(session.SelectedTower is null, "inspection can clear tower selection");
        Check.Equal(credits, session.Economy.Credits, "inspection never mutates economy");
    }

    private static void CoOpOwnershipCommands()
    {
        var session = Session();
        var placement = GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 1,
            PlayerId = 2,
            Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "tower",
            X = 50,
            Y = 200
        });
        Check.True(placement.Accepted, "player two placement command");
        Check.Equal(2, session.Towers[0].OwnerPlayerId, "tower ownership");

        var target = GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 2,
            PlayerId = 1,
            Type = GameCommandType.SetTargetMode,
            EntityId = session.Towers[0].Id,
            TargetMode = TargetMode.Armored
        });
        Check.True(target.Accepted, "other player targeting command");
        Check.Equal(TargetMode.Armored, session.Towers[0].TargetMode, "shared targeting applied");
        Check.True(GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 3,
            PlayerId = 1,
            Type = GameCommandType.UpgradeTower,
            EntityId = session.Towers[0].Id
        }).Accepted, "other player upgrade command");
        Check.True(GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 4,
            PlayerId = 1,
            Type = GameCommandType.SpecializeTower,
            EntityId = session.Towers[0].Id,
            SpecializationId = "alpha"
        }).Accepted, "other player specialization command");
        Check.Equal("alpha", session.Towers[0].SpecializationId!, "shared branch synchronized");
        Check.True(GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 5,
            PlayerId = 2,
            Type = GameCommandType.OverdriveTower,
            EntityId = session.Towers[0].Id
        }).Accepted, "owner overdrive command");
        Check.True(GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 6,
            PlayerId = 1,
            Type = GameCommandType.ToggleAutoProtocol,
            EntityId = session.Towers[0].Id
        }).Accepted, "other player arms shared automatic protocol");
        Check.Equal(session.Towers[0].Id, session.AutoOverdriveTowerId, "automatic protocol selection is shared");
        Check.True(GameCommandProcessor.Apply(session, new GameCommand
        {
            Sequence = 7,
            PlayerId = 1,
            Type = GameCommandType.SellTower,
            EntityId = session.Towers[0].Id
        }).Accepted, "other player can sell tower");
        Check.Equal(0, session.Towers.Count, "shared sale removes tower");
        Check.Equal(0, session.AutoOverdriveTowerId, "selling armed tower clears automation");
        session.Economy.AddCredits(1_000);
        Check.True(session.TryPlaceGenerator(new Vector2(50, 200), 2), "player two places forge");
        Check.True(session.TryUpgradeGenerator(1), "other player upgrades forge");
        Check.True(session.TrySellGenerator(1), "other player sells forge");
        Check.True(session.Generator is null, "shared forge sale removes forge");

        var sequencedSession = Session();
        var host = new AuthoritativeCommandHost();
        var request = new GameCommand
        {
            ClientRequestId = 42,
            PlayerId = 2,
            Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "tower",
            X = 50,
            Y = 200
        };
        var accepted = host.Submit(sequencedSession, request);
        var duplicate = host.Submit(sequencedSession, request);
        Check.True(accepted.Accepted, "host accepts legal command");
        Check.Equal(1L, accepted.Command.Sequence, "host assigns sequence");
        Check.True(duplicate.Duplicate, "duplicate request identified");
        Check.Equal(1, sequencedSession.Towers.Count, "duplicate command not applied twice");
    }

    private static void NetworkDeterministicCommands()
    {
        var first = SessionWithWave();
        var second = SessionWithWave();
        var firstRunner = new DeterministicSessionRunner(first);
        var secondRunner = new DeterministicSessionRunner(second);
        var placement = new GameCommand
        {
            Sequence = 1,
            ClientRequestId = 1,
            PlayerId = 2,
            Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "tower",
            X = 50,
            Y = 200
        };
        var start = new GameCommand { Sequence = 2, ClientRequestId = 2, PlayerId = 1, Type = GameCommandType.StartWave };
        var auto = new GameCommand { Sequence = 3, ClientRequestId = 3, PlayerId = 1, Type = GameCommandType.ToggleAutoProtocol, EntityId = 1 };
        var overdrive = new GameCommand { Sequence = 4, ClientRequestId = 4, PlayerId = 2, Type = GameCommandType.OverdriveTower, EntityId = 1 };
        Check.True(firstRunner.Schedule(0, placement) && secondRunner.Schedule(0, placement), "schedule mirrored placement");
        Check.True(firstRunner.Schedule(1, start) && secondRunner.Schedule(1, start), "schedule mirrored wave");
        Check.True(firstRunner.Schedule(2, auto) && secondRunner.Schedule(2, auto), "schedule mirrored protocol automation");
        Check.True(firstRunner.Schedule(3, overdrive) && secondRunner.Schedule(3, overdrive), "schedule mirrored protocol activation");
        firstRunner.RunTicks(80);
        secondRunner.RunTicks(80);
        Check.Equal(SessionChecksum.Compute(first, firstRunner.Tick), SessionChecksum.Compute(second, secondRunner.Tick), "mirrored state checksum");
        Check.Equal(2, first.Towers[0].OwnerPlayerId, "mirrored ownership");
        Check.True(first.Towers[0].IsOverdriven, "mirrored active ability state");
        Check.True(first.OverdriveCooldownRemaining > 0, "mirrored cooldown state");
        Check.Equal(1, first.AutoOverdriveTowerId, "mirrored auto protocol state");
    }

    private static void CoOpCommandHistoryBounds()
    {
        var authority = new AuthoritativeCommandHost();
        for (var requestId = 1; requestId <= AuthoritativeCommandHost.ReceiptHistoryLimit + 300; requestId++)
            Check.True(authority.Sequence(new GameCommand { PlayerId = 1, ClientRequestId = requestId, Type = GameCommandType.SetSpeed }).Accepted,
                "sequential authoritative request accepted");
        Check.True(authority.ReceiptHistoryCount <= AuthoritativeCommandHost.ReceiptHistoryLimit,
            "authoritative receipt cache remains bounded");
        Check.True(authority.AcceptedCommands.Count <= AuthoritativeCommandHost.AcceptedCommandHistoryLimit,
            "accepted-command diagnostics remain bounded");
        var expired = authority.Sequence(new GameCommand { PlayerId = 1, ClientRequestId = 1, Type = GameCommandType.SetSpeed });
        Check.True(expired.Duplicate && !expired.Accepted, "evicted request IDs remain protected from replay");
        var recentId = AuthoritativeCommandHost.ReceiptHistoryLimit + 300L;
        var recent = authority.Sequence(new GameCommand { PlayerId = 1, ClientRequestId = recentId, Type = GameCommandType.SetSpeed });
        Check.True(recent.Duplicate && recent.Accepted, "recent duplicate returns its original receipt");

        var capacityRunner = new DeterministicSessionRunner(Session());
        for (var sequence = 1L; sequence <= DeterministicSessionRunner.MaximumPendingCommands; sequence++)
            Check.True(capacityRunner.Schedule(0,
                new GameCommand { Sequence = sequence, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 1 }),
                "pending command fits bounded authority window");
        Check.Equal(DeterministicSessionRunner.MaximumPendingCommands, capacityRunner.PendingCommandCount,
            "pending-command count reaches but does not exceed its bound");
        Check.True(!capacityRunner.Schedule(0, new GameCommand
        {
            Sequence = DeterministicSessionRunner.MaximumPendingCommands + 1L,
            PlayerId = 1,
            Type = GameCommandType.SetSpeed,
            Speed = 1
        }), "pending command overflow is rejected");

        var session = Session();
        var runner = new DeterministicSessionRunner(session);
        Check.True(!runner.Schedule(DeterministicSessionRunner.MaximumFutureTicks + 1,
            new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 2 }),
            "commands beyond the repair window are rejected");
        Check.True(runner.Schedule(0, new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 2 }),
            "near-term command schedules");
        Check.True(!runner.Schedule(1, new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 1 }),
            "pending duplicate sequence is rejected across ticks");
        runner.RunTicks(1);
        Check.True(!runner.Schedule(1, new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 1 }),
            "applied duplicate sequence is rejected");

        for (var sequence = 2L; sequence <= DeterministicSessionRunner.AppliedSequenceHistoryLimit + 300L; sequence++)
        {
            Check.True(runner.Schedule(runner.Tick,
                new GameCommand { Sequence = sequence, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = sequence % 2 == 0 ? 1 : 2 }),
                "rolling sequence command schedules");
            runner.RunTicks(1);
        }
        Check.True(runner.AppliedSequenceHistoryCount <= DeterministicSessionRunner.AppliedSequenceHistoryLimit,
            "applied sequence history remains bounded");
        Check.True(runner.ExpiredAppliedSequenceFloor > 0 &&
            !runner.Schedule(runner.Tick, new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 1 }),
            "compacted sequence floor still rejects ancient replay");
    }

    private static void CoOpBufferedJitterCommands()
    {
        var host = SessionWithWaves(2);
        var client = SessionWithWaves(2);
        host.ConfigureCoOp(1);
        client.ConfigureCoOp(2);
        host.Economy.AddCredits(1_500);
        client.Economy.AddCredits(1_500);
        var hostRunner = new DeterministicSessionRunner(host);
        var clientRunner = new DeterministicSessionRunner(client);
        var deliveries = new[]
        {
            (Target: 8L, Delay: 5L, Command: new GameCommand { Sequence = 1, ClientRequestId = 1, PlayerId = 2, Type = GameCommandType.PlaceTower, TowerDefinitionId = "tower", X = 50, Y = 200 }),
            (Target: 12L, Delay: 0L, Command: new GameCommand { Sequence = 2, ClientRequestId = 2, PlayerId = 1, Type = GameCommandType.PlaceTower, TowerDefinitionId = "tower", X = 50, Y = 90 }),
            (Target: 18L, Delay: 3L, Command: new GameCommand { Sequence = 3, ClientRequestId = 3, PlayerId = 1, Type = GameCommandType.UpgradeTower, EntityId = 1 }),
            (Target: 24L, Delay: 1L, Command: new GameCommand { Sequence = 4, ClientRequestId = 4, PlayerId = 2, Type = GameCommandType.SpecializeTower, EntityId = 1, SpecializationId = "alpha" }),
            (Target: 30L, Delay: 4L, Command: new GameCommand { Sequence = 5, ClientRequestId = 5, PlayerId = 1, Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.Armored }),
            (Target: 36L, Delay: 2L, Command: new GameCommand { Sequence = 6, ClientRequestId = 6, PlayerId = 2, Type = GameCommandType.OverdriveTower, EntityId = 1 }),
            (Target: 38L, Delay: 5L, Command: new GameCommand { Sequence = 7, ClientRequestId = 7, PlayerId = 2, Type = GameCommandType.ToggleAutoProtocol, EntityId = 2 }),
            (Target: 42L, Delay: 0L, Command: new GameCommand { Sequence = 8, ClientRequestId = 8, PlayerId = 1, Type = GameCommandType.StartWave }),
            (Target: 50L, Delay: 3L, Command: new GameCommand { Sequence = 9, ClientRequestId = 9, PlayerId = 2, Type = GameCommandType.SetSpeed, Speed = 2f }),
            (Target: 90L, Delay: 5L, Command: new GameCommand { Sequence = 10, ClientRequestId = 10, PlayerId = 1, Type = GameCommandType.SellTower, EntityId = 2 })
        };
        foreach (var delivery in deliveries)
            Check.True(hostRunner.Schedule(delivery.Target, delivery.Command), "host schedules buffered command");

        for (var tick = 0L; tick < 220; tick++)
        {
            foreach (var delivery in deliveries.Where(delivery => delivery.Target - delivery.Delay == tick))
                Check.True(clientRunner.Schedule(delivery.Target, delivery.Command), "jittered command arrives inside six-tick buffer");
            hostRunner.RunTicks(1);
            clientRunner.RunTicks(1);
        }

        Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(client, clientRunner.Tick),
            "zero-to-five-tick delivery jitter preserves authoritative state");
        Check.Equal(1, host.Towers.Count, "shared remote sale applied once");
        Check.Equal(2, host.Towers[0].OwnerPlayerId, "original placer survives shared cross-player operations");
        Check.Equal("alpha", host.Towers[0].SpecializationId!, "jittered specialization applied");
        Check.Equal(TargetMode.Armored, host.Towers[0].TargetMode, "jittered target mode applied");
        Check.Equal(0, host.AutoOverdriveTowerId, "selling the armed tower clears shared automation");

        var lateRunner = new DeterministicSessionRunner(Session());
        lateRunner.RunTicks(2);
        Check.True(!lateRunner.Schedule(1, new GameCommand { Sequence = 99, PlayerId = 2, Type = GameCommandType.SetSpeed, Speed = 2 }),
            "a command arriving after its authoritative tick is rejected for resynchronization");
    }

    private static void CoOpActiveStateSnapshot()
    {
        var host = SessionWithWave();
        host.ConfigureCoOp(1);
        var hostRunner = new DeterministicSessionRunner(host);
        var place = new GameCommand
        {
            Sequence = 1,
            ClientRequestId = 1,
            PlayerId = 2,
            Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "tower",
            X = 50,
            Y = 200
        };
        var start = new GameCommand { Sequence = 2, ClientRequestId = 2, PlayerId = 1, Type = GameCommandType.StartWave };
        Check.True(hostRunner.Schedule(0, place), "snapshot placement scheduled");
        Check.True(hostRunner.Schedule(1, start), "snapshot wave scheduled");
        hostRunner.RunTicks(8);
        Check.True(host.Waves.IsActive && host.Enemies.Count > 0, "snapshot captured during active wave");
        host.Enemies[0].ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = 0.25f, SourceId = 1 });
        Check.True(host.TryDeployEmergencyDefense(new Vector2(200, 30)), "snapshot stored plate deployment");
        Check.True(host.TryDeployEmergencyDefense(new Vector2(300, 30)), "snapshot direct plate deployment");
        Check.True(host.Enemies[0].TryApplyKnockback(4, 0.75f, host.Map.Path), "snapshot enemy knockback grace");
        Check.True(host.TryToggleAutoProtocol(host.Towers[0].Id, 2), "snapshot automatic protocol armed");

        var future = new GameCommand { Sequence = 3, ClientRequestId = 3, PlayerId = 2, Type = GameCommandType.SetSpeed, Speed = 2f };
        Check.True(hostRunner.Schedule(hostRunner.Tick + 3, future), "future command scheduled before snapshot");
        var state = host.CaptureCoOpState(hostRunner.Tick, 0b10, false);
        state.PendingCommands = hostRunner.CapturePendingCommands();
        var json = JsonSerializer.Serialize(state);
        var transferred = JsonSerializer.Deserialize<CoOpStateSnapshot>(json)!;
        var client = GameSession.RestoreCoOpState(host.Content, transferred, 2);
        var clientRunner = new DeterministicSessionRunner(client, transferred.Tick);
        clientRunner.RestorePendingCommands(transferred.PendingCommands);

        Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(client, clientRunner.Tick), "snapshot checksum matches immediately");
        Check.Equal(2, client.Towers[0].OwnerPlayerId, "snapshot preserves original placer");
        Check.Equal(host.Towers[0].Id, client.AutoOverdriveTowerId, "snapshot restores automatic protocol tower");
        Check.Equal(1, client.Enemies[0].StatusEffects.Active.Count, "snapshot restores status effects");
        Check.Equal(1, client.EmergencyDirectPurchasesThisWave, "snapshot restores escalating plate purchase count");
        Check.Nearly(host.Enemies[0].KnockbackGraceRemaining, client.Enemies[0].KnockbackGraceRemaining, "snapshot restores plate knockback grace");
        Check.Equal(1, clientRunner.CapturePendingCommands().Count, "snapshot restores future commands");
        hostRunner.RunTicks(20);
        clientRunner.RunTicks(20);
        Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(client, clientRunner.Tick), "restored sessions remain deterministic");
        Check.Nearly(2f, client.Speed, "restored future command executes");
    }

    private static void CoOpMalformedSnapshotRejection()
    {
        var session = SessionWithWave();

        var missingCollection = session.CaptureCoOpState(0, 0, false);
        missingCollection.Towers = null!;
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, missingCollection, 2),
            "missing snapshot collections fail with a data error");

        var duplicateEnemies = session.CaptureCoOpState(0, 0, false);
        duplicateEnemies.Enemies.Add(new EnemyRuntimeState { Id = 7, DefinitionId = "enemy" });
        duplicateEnemies.Enemies.Add(new EnemyRuntimeState { Id = 7, DefinitionId = "enemy" });
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, duplicateEnemies, 2),
            "duplicate network enemy identities are rejected before dictionary restoration");

        var staleIdentity = session.CaptureCoOpState(0, 0, false);
        staleIdentity.Enemies.Add(new EnemyRuntimeState { Id = staleIdentity.NextEnemyId, DefinitionId = "enemy" });
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, staleIdentity, 2),
            "stale next-entity identities are rejected instead of silently changing the client checksum");

        var invalidProgress = session.CaptureCoOpState(0, 0, false);
        invalidProgress.Enemies.Add(new EnemyRuntimeState
        {
            Id = 1,
            DefinitionId = "enemy",
            DistanceAlongPath = session.Map.Path.TotalLength + 1
        });
        invalidProgress.NextEnemyId = 2;
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, invalidProgress, 2),
            "enemy progress beyond the map path is rejected instead of clamped into divergence");

        var nonfiniteProjectile = session.CaptureCoOpState(0, 0, false);
        nonfiniteProjectile.Projectiles.Add(new ProjectileRuntimeState
        {
            X = float.NaN,
            Kind = (int)ProjectileKind.Straight,
            Speed = 100,
            Damage = 10,
            Radius = 2
        });
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, nonfiniteProjectile, 2),
            "nonfinite network combat state is rejected");

        var excessivePending = session.CaptureCoOpState(0, 0, false);
        excessivePending.PendingCommands = Enumerable.Range(1, DeterministicSessionRunner.MaximumPendingCommands + 1)
            .Select(sequence => new ScheduledCommandState
            {
                Tick = 1,
                Command = new GameCommand { Sequence = sequence, PlayerId = 1, Type = GameCommandType.SetSpeed, Speed = 1 }
            }).ToList();
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, excessivePending, 2),
            "oversized network command state is rejected before allocation into the runner");

        var missingStatistics = session.CaptureCoOpState(0, 0, false);
        missingStatistics.Statistics.Towers = null!;
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(session.Content, missingStatistics, 2),
            "missing nested telemetry state is rejected cleanly");
    }

    private static void CoOpChecksumCoverage()
    {
        var host = SessionWithWave();
        host.Content.Towers["tower"].Tier2Doctrines = new List<TowerDoctrineDefinition>
        {
            new() { Id = "tempo", DisplayName = "Tempo", ShortLabel = "TEMPO", Summary = "Fast", UpgradeCost = 50, AttackSpeedMultiplier = 1.1f },
            new() { Id = "focus", DisplayName = "Focus", ShortLabel = "FOCUS", Summary = "Hard", UpgradeCost = 50, DamageMultiplier = 1.1f }
        };
        Check.True(host.TryPlaceTower("tower", new Vector2(50, 200)), "checksum coverage places tower");
        Check.True(host.TryChooseTowerDoctrine(host.Towers[0].Id, "tempo"), "checksum coverage chooses doctrine");
        Check.True(host.StartNextWave(), "checksum coverage starts wave");
        host.Update(0.05f);
        Check.True(host.Enemies.Count > 0, "checksum coverage has active enemy");
        host.Projectiles.Add(new ProjectileInstance(
            new Vector2(40, 40),
            host.Enemies[0].Position,
            host.Enemies[0],
            120,
            ProjectileKind.Homing,
            0,
            new DamagePayload { Damage = 5, SourceTowerId = host.Towers[0].Id },
            Color.Cyan,
            3));
        var tick = 1L;
        var baseline = SessionChecksum.Compute(host, tick);

        var speedState = host.CaptureCoOpState(tick, 0, false);
        speedState.Enemies[0].SpeedMultiplier += 0.05f;
        var speedDrift = GameSession.RestoreCoOpState(host.Content, speedState, 2);
        Check.True(baseline != SessionChecksum.Compute(speedDrift, tick),
            "checksum detects hidden enemy speed-scale drift before positions diverge");

        var statisticsState = host.CaptureCoOpState(tick, 0, false);
        statisticsState.Statistics.GeneratedCharges++;
        var statisticsDrift = GameSession.RestoreCoOpState(host.Content, statisticsState, 2);
        Check.True(baseline != SessionChecksum.Compute(statisticsDrift, tick),
            "checksum detects run-analysis drift before the results screen");

        var identityState = host.CaptureCoOpState(tick, 0, false);
        identityState.NextTowerId += 10;
        var identityDrift = GameSession.RestoreCoOpState(host.Content, identityState, 2);
        Check.True(baseline != SessionChecksum.Compute(identityDrift, tick),
            "checksum detects latent future-entity identity drift");

        var investmentState = host.CaptureCoOpState(tick, 0, false);
        investmentState.Towers[0].InvestedCredits += 10;
        var investmentDrift = GameSession.RestoreCoOpState(host.Content, investmentState, 2);
        Check.True(baseline != SessionChecksum.Compute(investmentDrift, tick),
            "checksum detects latent sale-value drift");

        var doctrineState = host.CaptureCoOpState(tick, 0, false);
        doctrineState.Towers[0].DoctrineId = "focus";
        var doctrineDrift = GameSession.RestoreCoOpState(host.Content, doctrineState, 2);
        Check.True(baseline != SessionChecksum.Compute(doctrineDrift, tick),
            "checksum detects tier two doctrine drift");

        var deathState = host.CaptureCoOpState(tick, 0, false);
        deathState.Enemies[0].IsDead = true;
        var deathDrift = GameSession.RestoreCoOpState(host.Content, deathState, 2);
        Check.True(baseline != SessionChecksum.Compute(deathDrift, tick),
            "checksum detects enemy death-transition drift before cleanup");

        var escapeState = host.CaptureCoOpState(tick, 0, false);
        escapeState.Enemies[0].HasEscaped = true;
        var escapeDrift = GameSession.RestoreCoOpState(host.Content, escapeState, 2);
        Check.True(baseline != SessionChecksum.Compute(escapeDrift, tick),
            "checksum detects enemy escape-transition drift before cleanup");

        var bossPulseState = host.CaptureCoOpState(tick, 0, false);
        bossPulseState.Enemies[0].BossPhasePulsePending = true;
        var bossPulseDrift = GameSession.RestoreCoOpState(host.Content, bossPulseState, 2);
        Check.True(baseline != SessionChecksum.Compute(bossPulseDrift, tick),
            "checksum detects pending boss-phase feedback drift");

        var projectileVisualState = host.CaptureCoOpState(tick, 0, false);
        projectileVisualState.Projectiles[0].PackedColor = Color.Coral.PackedValue;
        var projectileVisualDrift = GameSession.RestoreCoOpState(host.Content, projectileVisualState, 2);
        Check.True(baseline != SessionChecksum.Compute(projectileVisualDrift, tick),
            "checksum detects projectile visual identity drift");

        var projectileRadiusState = host.CaptureCoOpState(tick, 0, false);
        projectileRadiusState.Projectiles[0].Radius += 1;
        var projectileRadiusDrift = GameSession.RestoreCoOpState(host.Content, projectileRadiusState, 2);
        Check.True(baseline != SessionChecksum.Compute(projectileRadiusDrift, tick),
            "checksum detects projectile radius drift");
    }

    private static void CoOpReconnectCombatSoak()
    {
        var host = SessionWithWaves(3);
        host.ConfigureCoOp(1);
        host.Economy.AddCredits(1_000);
        var hostRunner = new DeterministicSessionRunner(host);
        var commands = new[]
        {
            (Tick: 0L, Command: new GameCommand { Sequence = 1, ClientRequestId = 1, PlayerId = 1, Type = GameCommandType.PlaceTower, TowerDefinitionId = "tower", X = 50, Y = 200 }),
            (Tick: 1L, Command: new GameCommand { Sequence = 2, ClientRequestId = 2, PlayerId = 2, Type = GameCommandType.PlaceTower, TowerDefinitionId = "tower", X = 50, Y = 90 }),
            (Tick: 2L, Command: new GameCommand { Sequence = 3, ClientRequestId = 3, PlayerId = 2, Type = GameCommandType.UpgradeTower, EntityId = 1 }),
            (Tick: 3L, Command: new GameCommand { Sequence = 4, ClientRequestId = 4, PlayerId = 1, Type = GameCommandType.StartWave }),
            (Tick: 12L, Command: new GameCommand { Sequence = 5, ClientRequestId = 5, PlayerId = 2, Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.Strongest }),
            (Tick: 14L, Command: new GameCommand { Sequence = 6, ClientRequestId = 6, PlayerId = 1, Type = GameCommandType.ToggleAutoProtocol, EntityId = 2 }),
            (Tick: 90L, Command: new GameCommand { Sequence = 7, ClientRequestId = 7, PlayerId = 2, Type = GameCommandType.SetSpeed, Speed = 2f })
        };
        foreach (var scheduled in commands)
            Check.True(hostRunner.Schedule(scheduled.Tick, scheduled.Command), "host schedules reconnect soak command");

        hostRunner.RunTicks(35);
        Check.True(host.Waves.IsActive && host.Enemies.Count > 0, "reconnect soak snapshots active combat");
        var snapshot = host.CaptureCoOpState(hostRunner.Tick, 0b11, false);
        snapshot.PendingCommands = hostRunner.CapturePendingCommands();
        var transferred = JsonSerializer.Deserialize<CoOpStateSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var client = GameSession.RestoreCoOpState(host.Content, transferred, 2);
        var clientRunner = new DeterministicSessionRunner(client, transferred.Tick);
        clientRunner.RestorePendingCommands(transferred.PendingCommands);

        Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(client, clientRunner.Tick),
            "reconnect soak starts from identical authoritative state");
        hostRunner.RunTicks(500);
        clientRunner.RunTicks(500);
        Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(client, clientRunner.Tick),
            "reconnected peers remain identical through combat, kills, cooldowns, and a future command");
        Check.Equal(host.Economy.TotalKills, client.Economy.TotalKills, "reconnect soak preserves shared kills");
        Check.Equal(host.Statistics.Towers.Sum(value => value.Damage), client.Statistics.Towers.Sum(value => value.Damage),
            "reconnect soak preserves final tower telemetry");
    }

    private static void CoOpLoopbackTransport()
    {
        CoOpLoopbackTransportAsync().GetAwaiter().GetResult();
    }

    private static void CoOpCursorPresence()
    {
        var tracker = new CoOpCursorTracker();
        Check.True(tracker.TryCaptureLocal(new Vector2(300, 220), true, 0, out var first),
            "first local battlefield cursor sample sends immediately");
        Check.Equal(new Vector2(300, 220), first, "local cursor sample preserves logical coordinates");
        Check.True(!tracker.TryCaptureLocal(new Vector2(302, 222), true, 0, out _),
            "cursor traffic is rate limited between samples");
        tracker.Advance(CoOpCursorTracker.SendIntervalSeconds);
        Check.True(tracker.TryCaptureLocal(new Vector2(302, 222), true, 0, out _),
            "cursor sampling resumes at its bounded presence cadence");
        tracker.Advance(CoOpCursorTracker.SendIntervalSeconds);
        Check.True(!tracker.TryCaptureLocal(new Vector2(302, 222), true, 0, out _),
            "stationary cursor waits for its slower heartbeat");
        tracker.Advance(CoOpCursorTracker.SendIntervalSeconds);
        Check.True(tracker.TryCaptureLocal(new Vector2(302, 222), true, 42, out _),
            "tower selection changes send without waiting for the idle heartbeat");
        tracker.Advance(CoOpCursorTracker.IdleHeartbeatSeconds);
        Check.True(tracker.TryCaptureLocal(new Vector2(302, 222), true, 42, out _),
            "stationary cursor heartbeat keeps remote presence alive");
        Check.True(!tracker.TryCaptureLocal(new Vector2(GameConstants.MapWidth + 10, 220), true, 0, out _),
            "sidebar positions are excluded from battlefield presence traffic");

        Check.True(tracker.Receive(new Vector2(400, 300), 2, 17), "valid remote cursor is accepted");
        Check.Equal(new Vector2(400, 300), tracker.RemotePosition!.Value, "remote cursor position is exposed for drawing");
        Check.Equal(2, tracker.RemotePlayerId, "remote cursor retains player identity");
        Check.Equal(17, tracker.RemoteEntityId, "remote cursor retains selected tower context");
        Check.True(!tracker.Receive(new Vector2(float.NaN, 300), 2), "nonfinite remote cursor is ignored");
        tracker.Advance(CoOpCursorTracker.RemoteTimeoutSeconds + 0.01f);
        Check.True(tracker.RemotePosition is null && tracker.RemotePlayerId == 0 && tracker.RemoteEntityId == 0,
            "stale remote presence disappears instead of freezing on the battlefield");
    }

    private static async Task CoOpLoopbackTransportAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var host = new LanCoOpHost(0, "TEST42");
        host.Start();
        var acceptTask = host.AcceptPlayerAsync(timeout.Token);
        var client = await LanCoOpClient.ConnectAsync("localhost", host.Port, "test42", timeout.Token);
        await using var server = await acceptTask;
        var request = new GameCommand { ClientRequestId = 7, PlayerId = 2, Type = GameCommandType.StartWave };
        await client.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.CommandRequest, PlayerId = 2, Command = request }, timeout.Token);
        var received = await server.ReceiveAsync(timeout.Token);
        Check.Equal(CoOpMessageType.CommandRequest, received!.Type, "server receives command envelope");
        Check.Equal(7L, received.Command!.ClientRequestId, "command request id survives transport");
        await server.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.CommandReceipt, PlayerId = 2, Receipt = new CommandReceipt(request with { Sequence = 3 }, true, "Accepted", false) }, timeout.Token);
        var receipt = await client.ReceiveAsync(timeout.Token);
        Check.True(receipt!.Receipt!.Value.Accepted, "client receives accepted receipt");
        Check.Equal(3L, receipt.Receipt.Value.Command.Sequence, "authoritative sequence survives transport");
        await client.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.Cursor, PlayerId = 2, X = 321, Y = 222, EntityId = 9 }, timeout.Token);
        var cursor = await server.ReceiveAsync(timeout.Token);
        Check.Equal(CoOpMessageType.Cursor, cursor!.Type, "remote cursor update survives transport");
        Check.Nearly(321, cursor.X, "remote cursor x survives transport");
        Check.Nearly(222, cursor.Y, "remote cursor y survives transport");
        Check.Equal(9, cursor.EntityId, "remote tower selection survives presence transport");
        await client.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.RestartRequest, PlayerId = 2 }, timeout.Token);
        var restart = await server.ReceiveAsync(timeout.Token);
        Check.Equal(CoOpMessageType.RestartRequest, restart!.Type, "client restart request survives transport");
        Check.Equal(2, restart.PlayerId, "restart request preserves requesting player");
        var disconnect = server.ReceiveAsync(timeout.Token);
        await client.DisposeAsync();
        Check.True(await disconnect is null, "server observes graceful client disconnect");
    }

    private static void CoOpReconnectTransport()
    {
        CoOpReconnectTransportAsync().GetAwaiter().GetResult();
    }

    private static void CoOpFramingBounds()
    {
        CoOpFramingBoundsAsync().GetAwaiter().GetResult();
    }

    private static async Task CoOpFramingBoundsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var host = new LanCoOpHost(0, "BOUNDS");
        host.Start();
        var acceptTask = host.AcceptPlayerAsync(timeout.Token);
        using var rawClient = new System.Net.Sockets.TcpClient();
        await rawClient.ConnectAsync("localhost", host.Port, timeout.Token);
        var oversizedHeader = new byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(oversizedHeader,
            LanCoOpConnection.MaximumMessageBytes + 1);
        await rawClient.GetStream().WriteAsync(oversizedHeader, timeout.Token);
        var rejected = false;
        try { await acceptTask; }
        catch (InvalidDataException exception)
        {
            rejected = exception.Message.Contains("protocol limit", StringComparison.OrdinalIgnoreCase);
        }
        Check.True(rejected, "oversized frame is rejected before allocating its declared payload");
    }

    private static async Task CoOpReconnectTransportAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var host = new LanCoOpHost(0, "RETURN");
        host.Start();

        var firstAccept = host.AcceptPlayerAsync(timeout.Token);
        await using (var firstClient = await LanCoOpClient.ConnectAsync("localhost", host.Port, "return", timeout.Token))
        await using (var firstServer = await firstAccept)
        {
            await firstClient.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.Disconnect, PlayerId = 2 }, timeout.Token);
            Check.Equal(CoOpMessageType.Disconnect, (await firstServer.ReceiveAsync(timeout.Token))!.Type, "first connection is active");
        }

        var secondAccept = host.AcceptPlayerAsync(timeout.Token);
        await using var secondClient = await LanCoOpClient.ConnectAsync("localhost", host.Port, "return", timeout.Token);
        await using var secondServer = await secondAccept;
        var snapshot = SessionWithWave().CaptureCoOpState(7, 0b01, false);
        await secondServer.SendAsync(new CoOpEnvelope { Type = CoOpMessageType.StateSnapshot, PlayerId = 1, Tick = 7, State = snapshot }, timeout.Token);
        var received = await secondClient.ReceiveAsync(timeout.Token);
        Check.Equal(CoOpMessageType.StateSnapshot, received!.Type, "same listener accepts returning player");
        Check.Equal(7L, received.State!.Tick, "authoritative reconnect state survives transport");
    }

    private static void CoOpWaveReady()
    {
        var ready = new CoOpWaveReadyCoordinator();
        Check.True(ready.RegisterReady(2, true, true), "player two can ready");
        Check.True(ready.IsReady(2), "player two ready is visible");
        Check.True(!ready.StartQueued, "one player cannot start co-op wave");
        Check.True(!ready.RegisterReady(2, true, true), "duplicate ready is ignored");
        Check.True(ready.RegisterReady(1, true, true), "player one can ready");
        Check.True(ready.StartQueued, "both players queue the wave");
        Check.True(ready.EarlyBonusQueued, "second ready locks an available early bonus");
        Check.Equal("WAIT P2 | EARLY +20 | 8s",
            UIManager.CoOpWaveButtonLabel(1, 1, 0b01, false, false, 7.1f),
            "ready player retains the visible early timer");
        Check.Equal("JOIN P1 | EARLY +20 | 8s",
            UIManager.CoOpWaveButtonLabel(2, 1, 0b01, false, false, 7.1f),
            "joining player sees the same early timer");
        Check.Equal("P1 READY | P2 WAIT | EARLY +20 | 8s",
            UIManager.CoOpReadyStatusLabel(1, 0b01, false, false, 7.1f),
            "sidebar ready state retains the same early timer");
        Check.Equal("STARTING | +20 LOCKED",
            UIManager.CoOpWaveButtonLabel(1, 1, 0b11, true, true, 0),
            "queued start communicates the locked reward");
        ready.Reset();
        Check.Equal(0, ready.ReadyMask, "ready state resets for next wave");
        Check.True(!ready.EarlyBonusQueued, "early reward state resets for next wave");
        Check.True(!ready.RegisterReady(1, false, true), "ready is rejected outside preparation");

        Check.True(ready.RegisterReady(1, true, true), "first player can ready during early window");
        Check.True(ready.RegisterReady(2, true, false), "second player can ready after early window");
        Check.True(!ready.EarlyBonusQueued, "first ready alone cannot preserve an expired bonus");
    }

    private static void CoOpInvalidCode()
    {
        CoOpInvalidCodeAsync().GetAwaiter().GetResult();
    }

    private static async Task CoOpInvalidCodeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var host = new LanCoOpHost(0, "RIGHT1");
        host.Start();
        var acceptTask = host.AcceptPlayerAsync(timeout.Token);
        var clientRejected = false;
        try
        {
            await using var ignored = await LanCoOpClient.ConnectAsync(host.Port, "WRONG2", timeout.Token);
        }
        catch (InvalidDataException)
        {
            clientRejected = true;
        }
        var hostRejected = false;
        try
        {
            await acceptTask;
        }
        catch (InvalidDataException)
        {
            hostRejected = true;
        }
        Check.True(clientRejected, "client rejects invalid join code");
        Check.True(hostRejected, "host rejects invalid join code");
    }

    private static void CoOpIncompatibleBuild()
    {
        CoOpIncompatibleBuildAsync().GetAwaiter().GetResult();
    }

    private static async Task CoOpIncompatibleBuildAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var host = new LanCoOpHost(0, "MATCH1", "HOST-BUILD");
        host.Start();
        var acceptTask = host.AcceptPlayerAsync(timeout.Token);
        var clientRejected = false;
        try
        {
            await using var ignored = await LanCoOpClient.ConnectAsync("localhost", host.Port, "MATCH1", timeout.Token, "CLIENT-BUILD");
        }
        catch (InvalidDataException exception)
        {
            clientRejected = exception.Message.Contains("do not match", StringComparison.OrdinalIgnoreCase);
        }
        try { await acceptTask; } catch (InvalidDataException) { }
        Check.True(clientRejected, "client gets a clear incompatible-build rejection");
    }

    private static void OnlineEndpointParsing()
    {
        var defaultPort = OnlineHostEndpoint.Parse("203.0.113.10", 28741);
        Check.Equal("203.0.113.10", defaultPort.Host, "public IPv4 host parsed");
        Check.Equal(28741, defaultPort.Port, "default online port parsed");
        var named = OnlineHostEndpoint.Parse("friend.example:30123", 28741);
        Check.Equal("friend.example", named.Host, "DNS host parsed");
        Check.Equal(30123, named.Port, "explicit online port parsed");
        var ipv6 = OnlineHostEndpoint.Parse("[2001:db8::1]:28742", 28741);
        Check.Equal("2001:db8::1", ipv6.Host, "IPv6 host parsed");
        Check.Equal(28742, ipv6.Port, "IPv6 port parsed");

        var ui = new UIManager(null!);
        ui.HandleCoOpMenu(WorldInput(Vector2.Zero) with { TextEntered = "friend.example" });
        ui.HandleCoOpMenu(WorldInput(Vector2.Zero) with { TabPressed = true });
        ui.HandleCoOpMenu(WorldInput(Vector2.Zero) with { TextEntered = "AB12CD" });
        Check.Equal("friend.example", ui.JoinHostInput, "keyboard host entry remains in the address field");
        Check.Equal("AB12CD", ui.JoinCodeInput, "Tab switches keyboard entry to the join-code field");
        Check.Equal(UiAction.JoinCoOp, ui.HandleCoOpMenu(WorldInput(Vector2.Zero) with { EnterPressed = true }),
            "Enter joins after both keyboard fields are complete");
        ui.SetCoOpLobbyStatus("HOSTING ONLINE CO-OP", "Share this code with your friend.", "Q7M2XP");
        Check.Equal(UiAction.None, ui.HandleCoOpLobby(WorldInput(Vector2.Zero) with { CopyPressed = true }),
            "copying the host join code keeps the lobby open");
        Check.True(ui.CoOpLobbyCopyStatus is "JOIN CODE COPIED" or "CLIPBOARD UNAVAILABLE",
            "host lobby reports the join-code copy result");
        Check.Equal(UiAction.None, ui.HandleCoOpReconnect(WorldInput(Vector2.Zero) with { CopyPressed = true }),
            "copying the host rejoin code keeps the preserved session open");
        Check.True(ui.CoOpLobbyCopyStatus is "REJOIN CODE COPIED" or "CLIPBOARD UNAVAILABLE",
            "reconnect overlay reports the rejoin-code copy result");
        Check.Equal(UiAction.MainMenu, ui.HandleCoOpReconnect(WorldInput(Vector2.Zero) with { EscapePressed = true }),
            "reconnect Escape remains the explicit leave-session action");
    }

    private static void BuildFingerprintContentCoverage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MinimalBastionFingerprint-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "Maps"));
            File.WriteAllText(Path.Combine(directory, "Towers.json"), "{\"version\":1}");
            File.WriteAllText(Path.Combine(directory, "Maps", "Arena.json"), "{\"id\":\"arena\"}");
            var baseline = BuildFingerprint.Compute(directory);
            Check.Equal(64, baseline.Length, "build fingerprint uses a full SHA-256 digest");
            Check.Equal(baseline, BuildFingerprint.Compute(directory), "build fingerprint is repeatable");

            File.WriteAllText(Path.Combine(directory, "notes.txt"), "non-authoritative");
            Check.Equal(baseline, BuildFingerprint.Compute(directory), "non-JSON files do not affect gameplay compatibility");
            File.WriteAllText(Path.Combine(directory, "Maps", "Arena.json"), "{\"id\":\"changed\"}");
            Check.True(!baseline.Equals(BuildFingerprint.Compute(directory), StringComparison.Ordinal),
                "nested campaign content changes the compatibility fingerprint");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void ContentIdentityValidation()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var directory = Path.Combine(Path.GetTempPath(), $"MinimalBastionContent-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(source, directory);
            File.Copy(Path.Combine(directory, "Maps", "FoundryLoop.json"), Path.Combine(directory, "Maps", "DuplicateMap.json"));
            Check.Throws<InvalidDataException>(() => new ContentLoader(directory).Load(), "duplicate map IDs are rejected");
            File.Delete(Path.Combine(directory, "Maps", "DuplicateMap.json"));

            var foundryMapPath = Path.Combine(directory, "Maps", "FoundryLoop.json");
            var foundryMap = File.ReadAllText(foundryMapPath).Replace("\"foundry_waves\"", "\"prism_waves\"", StringComparison.Ordinal);
            File.WriteAllText(foundryMapPath, foundryMap);
            Check.Throws<InvalidDataException>(() => new ContentLoader(directory).Load(), "maps cannot bind another arena's campaign");

            File.WriteAllText(foundryMapPath, File.ReadAllText(Path.Combine(source, "Maps", "FoundryLoop.json"))
                .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));
            Check.Throws<InvalidDataException>(() => new ContentLoader(directory).Load(), "unknown map schemas fail clearly");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void HeadlessSimulationDeterministic()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var options = new MinimalBastion.Simulation.SimulationOptions
        {
            Strategy = MinimalBastion.Simulation.AutoPlayerStrategy.Adaptive,
            Seed = 424242,
            MaximumWave = 2,
            MaximumSimulatedSeconds = 240
        };
        var first = MinimalBastion.Simulation.HeadlessSimulation.Run(content, options);
        var second = MinimalBastion.Simulation.HeadlessSimulation.Run(content, options);
        Check.Equal(first.Result, second.Result, "deterministic result");
        Check.Equal(first.WaveReached, second.WaveReached, "deterministic wave");
        Check.Equal(first.LivesRemaining, second.LivesRemaining, "deterministic lives");
        Check.Equal(first.CreditsSpent, second.CreditsSpent, "deterministic spend");
        Check.Equal(first.Towers.Values.Sum(x => x.Purchases), second.Towers.Values.Sum(x => x.Purchases), "deterministic purchases");
        Check.Nearly(first.Towers.Values.Sum(x => x.SupportDamageEquivalent), second.Towers.Values.Sum(x => x.SupportDamageEquivalent), "deterministic support attribution");
        Check.Nearly(first.Towers.Values.Sum(x => x.ExposeDamageEquivalent), second.Towers.Values.Sum(x => x.ExposeDamageEquivalent), "deterministic expose attribution");
        Check.Nearly(first.Towers.Values.Sum(x => x.ArmorBreakDamageEquivalent), second.Towers.Values.Sum(x => x.ArmorBreakDamageEquivalent), "deterministic armor-break attribution");
        Check.Nearly(first.Towers.Values.Sum(x => x.StatusEnemySeconds.Values.Sum()), second.Towers.Values.Sum(x => x.StatusEnemySeconds.Values.Sum()), "deterministic status uptime");
        Check.True(first.WaveReached >= 2, "headless bot reaches requested wave limit");
        Check.True(first.Overdrives > 0 && first.ProtocolsEnabled, "default simulation exercises Protocol activations");

        var noProtocols = MinimalBastion.Simulation.HeadlessSimulation.Run(content, new MinimalBastion.Simulation.SimulationOptions
        {
            Strategy = options.Strategy,
            Seed = options.Seed,
            MaximumWave = options.MaximumWave,
            MaximumSimulatedSeconds = options.MaximumSimulatedSeconds,
            UseProtocols = false
        });
        Check.Equal(0, noProtocols.Overdrives, "Protocol-disabled control group records no activations");
        Check.True(!noProtocols.ProtocolsEnabled, "simulation report identifies Protocol-disabled control group");
    }

    private static void PlacementRules()
    {
        var session = Session();
        Check.Equal(PlacementFailure.BlocksPath, session.ValidatePlacement("tower", new Vector2(50, 78)), "path rejection");
        Check.Equal(PlacementFailure.OutsideBuildableRegion, session.ValidatePlacement("tower", new Vector2(700, 500)), "buildable rejection");
        Check.Equal(PlacementFailure.None, session.ValidatePlacement("tower", new Vector2(50, 200)), "valid placement");
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "place tower");
        Check.Equal(1, session.Towers.Count, "tower count");
        Check.Equal(PlacementFailure.OverlapsTower, session.ValidatePlacement("tower", new Vector2(75, 200)), "overlap rejection");
    }

    private static void WaveFinalGroup()
    {
        var session = SessionWithWave();
        Check.True(session.StartNextWave(), "start test wave");
        session.Update(0.01f);
        Check.Equal(1, session.Enemies.Count, "final group spawned");
        // This update used to throw after the manager advanced past its final group.
        session.Update(0.01f);
        Check.Equal(1, session.Enemies.Count, "final group remains active");
    }

    private static void EndlessWaveContinuation()
    {
        var session = SessionWithWave();
        Check.True(session.StartNextWave(), "start campaign finale");
        ResolveSingleEnemyWave(session);
        Check.True(session.IsVictory, "authored campaign still ends in victory");
        Check.True(!session.CanStartWave, "endless waves require an explicit continue choice");

        var frozenTime = session.Statistics.SimulatedSeconds;
        Check.True(session.BeginEndlessMode(), "victory can continue into endless mode");
        Check.True(!session.IsVictory && session.IsEndlessMode, "continue resumes the live session");
        Check.True(session.CanStartWave, "first endless wave is available during cooldown");
        Check.Equal(2, session.Waves.NextWave!.Number, "endless numbering follows the authored campaign");
        Check.True(session.Waves.NextWave.HealthMultiplier > 1f, "endless health pressure rises immediately");
        session.Update(0.1f);
        Check.True(session.Statistics.SimulatedSeconds > frozenTime, "battlefield simulation cools down after continuing");

        var save = session.CaptureSaveGame();
        Check.True(save.Waves.EndlessModeEnabled, "checkpoint records endless mode");
        var restored = GameSession.RestoreSaveGame(session.Content, save);
        Check.True(restored.IsEndlessMode && restored.CanStartWave, "checkpoint restores endless intermission");
        Check.Equal(2, restored.Waves.NextWave!.Number, "restored checkpoint regenerates the same next wave");

        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var anchor = content.Waves.Waves[^1];
        var wave21 = EndlessWaveGenerator.Create(21, 20, anchor);
        var wave25 = EndlessWaveGenerator.Create(25, 20, anchor);
        Check.True(wave25.HealthMultiplier > wave21.HealthMultiplier, "endless health scaling is monotonic");
        Check.True(wave25.Groups.Any(group => group.Rank == "Boss"), "boss returns every fifth endless wave");
        Check.True(wave21.Groups.All(group => group.Rank != "Boss"), "ordinary endless waves do not spam bosses");
        Check.True(Enumerable.Range(96, 5).Select(number => EndlessWaveGenerator.Create(number, 20, anchor))
            .Max(wave => wave.Groups.Sum(group => group.Count)) < 250, "endless roster growth remains performance bounded across every theme");
        var extreme = EndlessWaveGenerator.Create(int.MaxValue, 20, anchor);
        Check.True(float.IsFinite(extreme.HealthMultiplier) && float.IsFinite(extreme.SpeedMultiplier) &&
            extreme.Groups.Sum(group => group.Count) < 250,
            "extreme endless generation remains finite and density bounded");
        var terminalManager = new WaveManager(content.Waves.Waves);
        terminalManager.RestoreSaveData(new WaveSaveData
        {
            CurrentWaveNumber = int.MaxValue,
            IsFinalWaveCleared = true,
            EndlessModeEnabled = true
        });
        Check.True(!terminalManager.CanStartNextWave && terminalManager.NextWave is null,
            "maximum representable wave stops cleanly instead of overflowing negative");
        Check.Equal(JsonSerializer.Serialize(wave25), JsonSerializer.Serialize(EndlessWaveGenerator.Create(25, 20, anchor)),
            "endless generation is deterministic");

        var mapEndlessSignatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in content.Maps.Values)
        {
            var mapWaves = content.WaveSets[map.WaveSet].Waves;
            var mapAnchor = mapWaves[^1];
            var generated = EndlessWaveGenerator.Create(mapWaves.Count + 1, mapWaves.Count, mapAnchor);
            Check.True(generated.HealthMultiplier > mapAnchor.HealthMultiplier, $"{map.Id} endless health rises from its own finale");
            Check.True(generated.Groups.Select(group => group.EnemyId).Distinct(StringComparer.OrdinalIgnoreCase)
                .All(enemyId => mapAnchor.Groups.Any(group => group.EnemyId.Equals(enemyId, StringComparison.OrdinalIgnoreCase))),
                $"{map.Id} endless roster inherits authored arena contacts");
            mapEndlessSignatures.Add(string.Join('|', generated.Groups.Select(group =>
                $"{group.EnemyId}:{group.Rank}:{group.Count}:{group.SpawnInterval:0.000}")));
        }
        Check.Equal(content.Maps.Count, mapEndlessSignatures.Count,
            "each authored arena retains a distinct first endless formation");

        var mirroredHost = SessionWithWave();
        var mirroredClient = SessionWithWave();
        Check.True(mirroredHost.StartNextWave() && mirroredClient.StartNextWave(), "start mirrored finales");
        ResolveSingleEnemyWave(mirroredHost);
        ResolveSingleEnemyWave(mirroredClient);
        var command = new GameCommand { Sequence = 1, ClientRequestId = 1, PlayerId = 2, Type = GameCommandType.ContinueEndless };
        var hostRunner = new DeterministicSessionRunner(mirroredHost);
        var clientRunner = new DeterministicSessionRunner(mirroredClient);
        Check.True(hostRunner.Schedule(0, command) && clientRunner.Schedule(0, command), "schedule mirrored co-op continuation");
        hostRunner.RunTicks(1);
        clientRunner.RunTicks(1);
        Check.True(mirroredHost.IsEndlessMode && !mirroredHost.IsVictory, "co-op continuation command resumes the match");
        Check.Equal(SessionChecksum.Compute(mirroredHost, hostRunner.Tick), SessionChecksum.Compute(mirroredClient, clientRunner.Tick),
            "co-op endless continuation remains synchronized");
    }

    private static void EarlyWaveCallReward()
    {
        var session = SessionWithWaves(3);
        Check.True(session.StartNextWave(), "start first wave");
        Check.Equal(0, session.Economy.EarlyStartCreditsEarned, "first wave has no early reward");
        ResolveSingleEnemyWave(session);
        Check.True(session.IntermissionRemaining > 0, "intermission begins");
        Check.True(session.StartNextWave(), "call second wave early");
        Check.Equal(GameConstants.EarlyStartBonus, session.Economy.EarlyStartCreditsEarned, "early call reward");
        Check.True(session.AnnouncementSubtitle!.Contains("EARLY CALL", StringComparison.Ordinal), "early call announced");
        ResolveSingleEnemyWave(session);
        for (var index = 0; index < 101; index++) session.Update(0.1f);
        Check.True(session.StartNextWave(), "start third wave after waiting");
        Check.Equal(GameConstants.EarlyStartBonus, session.Economy.EarlyStartCreditsEarned, "waiting forfeits extra reward");

        var lockedBonus = SessionWithWaves(2);
        Check.True(lockedBonus.StartNextWave(), "start co-op bonus setup wave");
        ResolveSingleEnemyWave(lockedBonus);
        var lockedReady = new CoOpWaveReadyCoordinator();
        Check.True(lockedReady.RegisterReady(1, true, true), "first co-op player readies during timer");
        Check.True(lockedReady.RegisterReady(2, true, true), "second co-op player readies during timer");
        for (var index = 0; index < 101; index++) lockedBonus.Update(0.1f);
        Check.True(lockedBonus.StartNextWave(lockedReady.EarlyBonusQueued), "delayed network command starts locked early wave");
        Check.Equal(GameConstants.EarlyStartBonus, lockedBonus.Economy.EarlyStartCreditsEarned,
            "network input delay cannot erase a bonus locked by both ready players");

        var expiredBonus = SessionWithWaves(2);
        Check.True(expiredBonus.StartNextWave(), "start expired bonus setup wave");
        ResolveSingleEnemyWave(expiredBonus);
        var expiredReady = new CoOpWaveReadyCoordinator();
        Check.True(expiredReady.RegisterReady(1, true, true), "first player readies before expiration");
        for (var index = 0; index < 101; index++) expiredBonus.Update(0.1f);
        Check.True(expiredReady.RegisterReady(2, true, false), "second player readies after expiration");
        Check.True(expiredBonus.StartNextWave(expiredReady.EarlyBonusQueued), "late co-op readiness still starts wave");
        Check.Equal(0, expiredBonus.Economy.EarlyStartCreditsEarned,
            "one early ready cannot preserve the bonus after the second player waits too long");
    }

    private static void ResolveSingleEnemyWave(GameSession session)
    {
        session.Update(0.01f);
        Check.Equal(1, session.Enemies.Count, "single enemy spawned");
        session.DamageResolver.Apply(session.Enemies[0], new DamagePayload { Damage = 10_000 });
        session.Update(0.01f);
    }

    private static void MixedWaveComposition()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var tier = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["t1_crawler"] = 1,
            ["t2_runner"] = 2,
            ["t3_brute"] = 3,
            ["t4_aegis"] = 4,
            ["t5_regenerator"] = 5
        };
        foreach (var wave in content.Waves.Waves.Skip(2))
        {
            var order = wave.Groups.Select(x => tier[x.EnemyId]).ToArray();
            Check.True(order.Zip(order.Skip(1)).Any(pair => pair.First > pair.Second), $"wave {wave.Number} returns to a lower tier");
            Check.True(!string.IsNullOrWhiteSpace(wave.Archetype), $"wave {wave.Number} archetype");
            Check.True(!string.IsNullOrWhiteSpace(wave.Briefing), $"wave {wave.Number} briefing");
        }
        Check.True(content.Waves.Waves.Select(x => x.Archetype).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 15, "authored wave identities");
        var intel = MinimalBastion.Waves.WaveIntel.Analyze(content.Waves.Waves[13], content.Enemies);
        Check.True(intel.Threats.Contains("REGEN"), "regenerator warning");
        Check.True(intel.Threats.Contains("ARMOR"), "armor warning");
        var bossIntel = MinimalBastion.Waves.WaveIntel.Analyze(content.Waves.Waves[19], content.Enemies);
        Check.True(bossIntel.Threats.Contains("BOSS"), "boss warning");
        Check.Equal("BOSS", bossIntel.Threats[0], "boss warning has priority");
    }

    private static void ArcRelayChain()
    {
        var path = new PathRuntime(new[] { Point(0, 0), Point(1000, 0) });
        var towerDefinition = new TowerDefinition
        {
            Id = "arc_relay",
            DisplayName = "Arc Relay",
            PurchaseCost = 1,
            Behavior = "chain",
            Levels = new List<TowerLevelDefinition>
            {
                new() { Range = 200, Damage = 20, AttacksPerSecond = 1, ChainCount = 2, ChainDamage = 10, ChainRange = 90 }
            }
        };
        var enemyDefinition = Enemy("chain_target", 100, 10, 1, 0, 0);
        var session = new GameSession(new GameContent
        {
            Towers = new Dictionary<string, TowerDefinition> { [towerDefinition.Id] = towerDefinition },
            Enemies = new Dictionary<string, EnemyDefinition> { [enemyDefinition.Id] = enemyDefinition },
            Map = new MapDefinition { Id = "chain", Path = new List<PointData> { Point(0, 0), Point(1000, 0) }, Background = new BackgroundData() },
            Waves = new WaveSetDefinition { Waves = new List<WaveDefinition>() }
        });
        var tower = new TowerInstance(1, towerDefinition, new Vector2(100, 0));
        session.Towers.Add(tower);
        var first = new EnemyInstance(1, enemyDefinition, path, 1, 1);
        var second = new EnemyInstance(2, enemyDefinition, path, 1, 1);
        var third = new EnemyInstance(3, enemyDefinition, path, 1, 1);
        var outOfRange = new EnemyInstance(4, enemyDefinition, path, 1, 1);
        second.UpdateMovement(5, path); // x = 50
        third.UpdateMovement(10, path); // x = 100
        outOfRange.UpdateMovement(30, path); // x = 300, outside the second hop
        third.StatusEffects.Apply(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = 0.3f, SourceId = 99 });
        session.Enemies.AddRange(new[] { first, second, third, outOfRange });

        // The public runtime uses path positions, so use a compact path and select the first target
        // as the origin for the chain. The important regression is that each hop is unique and capped.
        var behavior = TowerBehaviorRegistry.Create("chain");
        behavior.Attack(new TowerInstanceContext { Tower = tower, Target = first, Session = session });

        Check.Nearly(80, first.Health, "chain primary damage");
        Check.Nearly(90, second.Health, "chain first hop");
        Check.Nearly(86.5f, third.Health, "chain slowed-target synergy");
        Check.Nearly(100, outOfRange.Health, "chain range limit");
        Check.Nearly(43.5f, 400 - first.Health - second.Health - third.Health - outOfRange.Health, "chain damage is bounded");
        Check.Equal(3, session.Effects.Effects.Count(x => x.Kind == EffectKind.Beam), "primary plus two chain beams");
    }

    private static void FrostAreaControl()
    {
        var session = Session();
        var level = new TowerLevelDefinition
        {
            Range = 200,
            Damage = 8,
            AttacksPerSecond = 1,
            ProjectileSpeed = 1000,
            SplashRadius = 35,
            SlowPercent = 0.4f,
            SlowDuration = 2
        };
        var definition = new TowerDefinition
        {
            Id = "frost",
            DisplayName = "Frost",
            Behavior = "slow_projectile",
            PurchaseCost = 1,
            Levels = new List<TowerLevelDefinition> { level }
        };
        var tower = new TowerInstance(9, definition, new Vector2(100, 100));
        var first = new EnemyInstance(10, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        var nearby = new EnemyInstance(11, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        var distant = new EnemyInstance(12, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        first.UpdateMovement(10, session.Map.Path);
        nearby.UpdateMovement(12, session.Map.Path);
        distant.UpdateMovement(20, session.Map.Path);
        session.Enemies.AddRange(new[] { first, nearby, distant });

        TowerBehaviorRegistry.Create("slow_projectile").Attack(new TowerInstanceContext { Tower = tower, Target = first, Session = session });
        session.Projectiles.Update(1, session);

        Check.Nearly(92, first.Health, "frost damages primary");
        Check.Nearly(92, nearby.Health, "frost damages nearby enemy");
        Check.Nearly(100, distant.Health, "frost area is bounded");
        Check.Nearly(0.4f, nearby.StatusEffects.SlowFactor, "frost applies area slow");
        var impact = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Flash);
        Check.Nearly(level.SplashRadius, impact.Radius, "splash effect communicates the actual impact radius");
    }

    private static void MortarPredictiveAim()
    {
        var session = Session();
        var definition = new TowerDefinition
        {
            Id = "mortar",
            DisplayName = "Mortar",
            Behavior = "splash_projectile",
            PurchaseCost = 1,
            Levels = new List<TowerLevelDefinition>
            {
                new() { Range = 300, Damage = 40, AttacksPerSecond = 1, ProjectileSpeed = 100, SplashRadius = 45, SplashTargetLimit = 3 }
            }
        };
        var tower = new TowerInstance(8, definition, new Vector2(100, 150));
        var target = new EnemyInstance(7, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        target.UpdateMovement(10, session.Map.Path);
        TowerBehaviorRegistry.Create("splash_projectile").Attack(new TowerInstanceContext { Tower = tower, Target = target, Session = session });
        var shell = session.Projectiles.Projectiles.Single();
        Check.True(shell.AimPoint.X > target.Position.X + 5, "mortar leads moving target");
        Check.Nearly(0, shell.Payload.Status?.Magnitude ?? 0, "mortar no longer hides a slow effect");
        Check.Equal(3, shell.CaptureCoOpState().SplashTargetLimit, "mortar cap survives active-projectile snapshots");

        var capSession = Session();
        var capDefinition = new TowerDefinition
        {
            Id = "capped_mortar",
            DisplayName = "Capped Mortar",
            Behavior = "splash_projectile",
            PurchaseCost = 1,
            Levels = new List<TowerLevelDefinition>
            {
                new() { Range = 300, Damage = 40, AttacksPerSecond = 1, ProjectileSpeed = 10_000, SplashRadius = 45, SplashTargetLimit = 3 }
            }
        };
        var capTower = new TowerInstance(12, capDefinition, new Vector2(100, 150));
        var crowded = Enumerable.Range(0, 5)
            .Select(index => new EnemyInstance(20 + index, capSession.Content.Enemies["enemy"], capSession.Map.Path, 1, 1))
            .ToArray();
        capSession.Enemies.AddRange(crowded);
        TowerBehaviorRegistry.Create("splash_projectile").Attack(new TowerInstanceContext
        {
            Tower = capTower,
            Target = crowded[0],
            Session = capSession
        });
        capSession.Projectiles.Update(1, capSession);
        Check.Equal(3, crowded.Count(enemy => enemy.Health < enemy.MaxHealth), "mortar damages only its nearest capped targets");
        Check.True(crowded.Take(3).All(enemy => enemy.Health < enemy.MaxHealth) && crowded.Skip(3).All(enemy => enemy.Health == enemy.MaxHealth),
            "equal-distance mortar cap resolves deterministically by enemy id");
    }

    private static void TowerInformation()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal("General", TowerInfo.ShortRole(content.Towers["needle_turret"]), "short general role");
        Check.Equal("Long range", TowerInfo.ShortRole(content.Towers["watchtower"]), "short long-range role");
        Check.Equal("Anti-armor", TowerInfo.ShortRole(content.Towers["breaker_cannon"]), "short armor role");
        Check.Equal("Chain", TowerInfo.ShortRole(content.Towers["arc_relay"]), "short chain role");
        Check.True(content.Towers.Values.All(x => TowerInfo.ShortRole(x).Length <= 10), "catalog roles fit compact cards");
        Check.True(content.Towers.Values.All(x => x.Visual.Marks == 1), "every tower begins with one level mark");
        Check.True(content.Towers.Values.All(x => x.Visual.Ring), "every tower has a consistent outer ring");

        var pelletLevel = content.Towers["shard_fan"].Levels[0];
        Check.Nearly(pelletLevel.Damage * pelletLevel.AttacksPerSecond * pelletLevel.PelletCount,
            TowerInfo.RawDps(pelletLevel), "pellet DPS includes every projectile");
        Check.True(TowerInfo.UpgradeSummary(content.Towers["needle_turret"], 0).Contains("DAMAGE", StringComparison.Ordinal),
            "upgrade summary exposes damage delta");

        var needle = content.Towers["needle_turret"];
        var beacon = content.Towers["signal_beacon"];
        var recipient = new TowerInstance(1, needle, new Vector2(100, 100));
        var source = new TowerInstance(2, beacon, new Vector2(140, 100));
        var strongerSource = new TowerInstance(3, beacon, new Vector2(100, 140));
        var amplifier = beacon.Tier2Doctrines.Single(x => x.Id == "beacon_amplifier");
        Check.True(strongerSource.TryChooseDoctrine(amplifier.Id), "stronger beacon reaches level two through a doctrine");
        var buffs = new BuffSystem();
        buffs.Update(new[] { recipient, source, strongerSource });
        var signalBuff = buffs.Get(recipient);
        Check.True(signalBuff.IsActive, "beacon aura reports an active tower buff");
        Check.Nearly(strongerSource.Level.AuraAttackSpeedBonus, signalBuff.AttackSpeedBonus, "overlapping beacons use strongest rate instead of stacking");
        Check.Nearly(strongerSource.Level.AuraRangeBonus, signalBuff.RangeBonus, "overlapping beacons use strongest range instead of stacking");
        var signalSummary = TowerInfo.SignalBeaconStatChange(recipient.Level, signalBuff);
        Check.True(signalSummary.Contains("SIGNAL BEACON", StringComparison.Ordinal), "beacon summary identifies its source");
        Check.True(signalSummary.Contains("RATE 2>2.56/s", StringComparison.Ordinal), "beacon summary exposes exact strongest rate change");
        Check.True(signalSummary.Contains("RANGE 125>147", StringComparison.Ordinal), "beacon summary exposes exact strongest range change");

        var effectiveUpgrade = TowerInfo.UpgradeSummary(needle, 0, signalBuff, default);
        Check.True(effectiveUpgrade.Contains("RATE 2.56>2.82", StringComparison.Ordinal), "upgrade comparison includes beacon rate");
        Check.True(effectiveUpgrade.Contains("RANGE 147>159", StringComparison.Ordinal), "upgrade comparison includes beacon range");
        var beaconUpgrade = TowerInfo.UpgradeSummary(beacon, 0);
        Check.True(beaconUpgrade.Contains("AURA 145>165", StringComparison.Ordinal), "beacon upgrade compares aura radius");
        Check.True(beaconUpgrade.Contains("RATE +15%>+25%", StringComparison.Ordinal), "beacon upgrade compares aura rate");
    }

    private static void TowerLibraryReference()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var ui = new UIManager(null!);
        ui.ConfigureMaps(content.Maps.Values, content.WaveSets, content.Enemies);
        ui.ConfigureTowerLibrary(content.Towers.Values, content.Enemies.Values);
        var firstTower = ui.SelectedLibraryTowerId;
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { NavigateDownPressed = true });
        Check.True(ui.SelectedLibraryTowerId != firstTower, "tower library Down selects the next tower");
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { NavigateUpPressed = true });
        Check.Equal(firstTower!, ui.SelectedLibraryTowerId!, "tower library Up returns to the previous tower");
        var tabUi = new UIManager(null!);
        tabUi.ConfigureMaps(content.Maps.Values, content.WaveSets, content.Enemies);
        tabUi.ConfigureTowerLibrary(content.Towers.Values, content.Enemies.Values);
        tabUi.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { TabPressed = true });
        Check.True(tabUi.LibraryShowsThreats, "Tactical Library Tab advances from towers to threats");
        tabUi.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { TabPressed = true });
        Check.True(tabUi.LibraryShowsCampaign, "Tactical Library Tab advances from threats to campaigns");
        tabUi.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { TabPressed = true });
        Check.True(!tabUi.LibraryShowsThreats && !tabUi.LibraryShowsCampaign,
            "Tactical Library Tab wraps from campaigns to towers");
        Check.Equal(UiAction.TowerLibrary,
            ui.HandleMainMenu(WorldInput(new Vector2(712, 442)) with { LeftPressed = true }),
            "title screen opens tower library");
        Check.Equal(UiAction.None,
            ui.HandleTitleTowerLibrary(WorldInput(new Vector2(827, 67)) with { LeftPressed = true }),
            "threat reference remains inside tactical library");
        Check.True(ui.LibraryShowsThreats, "tactical library switches to threat reference");
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { TowerHotkey = 3 });
        Check.Equal("t3_brute", ui.SelectedLibraryEnemyId!, "threat hotkeys select the health-ordered archetype");
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { NavigateUpPressed = true });
        Check.Equal("t1_crawler", ui.SelectedLibraryEnemyId!, "threat library supports health-ordered arrow selection");
        ui.HandleTitleTowerLibrary(WorldInput(new Vector2(982, 67)) with { LeftPressed = true });
        Check.True(ui.LibraryShowsCampaign, "tactical library switches to campaign reference");
        Check.Equal(20, ui.SelectedLibraryCampaignWaveCount, "campaign reference exposes all authored waves");
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { TowerHotkey = 2 });
        Check.Equal(20, ui.SelectedLibraryCampaignWaveCount, "campaign hotkey selects another complete arena roster");
        var secondMap = ui.SelectedLibraryCampaignMapId;
        ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { NavigateUpPressed = true });
        Check.True(ui.SelectedLibraryCampaignMapId != secondMap, "campaign library supports arrow selection");
        ui.HandleTitleTowerLibrary(WorldInput(new Vector2(672, 67)) with { LeftPressed = true });
        Check.True(!ui.LibraryShowsThreats, "tactical library returns to tower planning");
        Check.True(!ui.LibraryShowsCampaign, "tower planning closes campaign reference");
        Check.Equal(UiAction.MainMenu,
            ui.HandleTitleTowerLibrary(WorldInput(Vector2.Zero) with { EscapePressed = true }),
            "title library escape returns to title screen");
        foreach (var definition in content.Towers.Values)
        {
            Check.Equal(definition.PurchaseCost, TowerInfo.TotalCostToLevel(definition, 0), $"{definition.Id} level one total");
            if (definition.Levels.Count > 1)
                Check.Equal(definition.PurchaseCost + definition.Levels[0].UpgradeCost!.Value,
                    TowerInfo.TotalCostToLevel(definition, 1), $"{definition.Id} level two total");

            foreach (var level in definition.Levels)
            {
                var lines = TowerInfo.LibraryStatLines(definition, level);
                Check.True(lines.Count > 0, $"{definition.Id} level library stats");
                Check.True(lines.All(line => line.All(character => character is >= ' ' and <= '~')),
                    $"{definition.Id} library stats use compiled ASCII glyphs");
            }

            foreach (var specialization in definition.Specializations)
            {
                var expected = definition.PurchaseCost + definition.Levels[0].UpgradeCost!.Value + specialization.UpgradeCost;
                Check.Equal(expected, TowerInfo.TotalCostToSpecialization(definition, specialization),
                    $"{definition.Id} {specialization.Id} cumulative cost");
                Check.True(TowerInfo.LibraryStatLines(definition, specialization.Level).Count > 0,
                    $"{definition.Id} {specialization.Id} library stats");
            }

            foreach (var doctrine in definition.Tier2Doctrines)
            {
                Check.Equal(definition.PurchaseCost + doctrine.UpgradeCost,
                    TowerInfo.TotalCostToDoctrine(definition, doctrine), $"{definition.Id} {doctrine.Id} cumulative cost");
                var doctrineLevel = definition.Levels[1].WithDoctrine(doctrine);
                Check.True(TowerInfo.LibraryStatLines(definition, doctrineLevel).Count > 0,
                    $"{definition.Id} {doctrine.Id} library stats");
                foreach (var specialization in definition.Specializations)
                    Check.Equal(definition.PurchaseCost + doctrine.UpgradeCost + specialization.UpgradeCost,
                        TowerInfo.TotalCostToSpecialization(definition, doctrine, specialization),
                        $"{definition.Id} {doctrine.Id} {specialization.Id} cumulative cost");
            }
        }

        Check.True(TowerInfo.LibraryStatLines(content.Towers["arc_relay"], content.Towers["arc_relay"].Levels[1])
            .Any(line => line.Contains("MAX CHAIN DPS", StringComparison.Ordinal)), "library exposes maximum chain output");
        Check.True(TowerInfo.LibraryStatLines(content.Towers["signal_beacon"], content.Towers["signal_beacon"].Levels[0])
            .Any(line => line.Contains("ATTACK RATE", StringComparison.Ordinal)), "library exposes support aura strength");
    }

    private static void EmergencyPulsePlates()
    {
        var session = Session();
        var position = new Vector2(200, 30);
        Check.True(UIManager.PulsePlateButtonLabel(session).Contains("FIELD 0/16", StringComparison.Ordinal),
            "plate button always shows active field capacity");
        Check.Equal(PlacementFailure.None, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position), "road placement");
        Check.Equal(PlacementFailure.None, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(250, 61)), "visible road edge placement");
        Check.Equal(PlacementFailure.MustBeOnPath, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(200, 100)), "off-road rejection");
        Check.Equal(PlacementFailure.TooCloseToPathEndpoint, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(20, 30)), "endpoint rejection");

        session.BeginEmergencyPlacement();
        session.HandleWorldInput(WorldInput(new Vector2(200, 100)));
        Check.Equal(PlacementFailure.MustBeOnPath, session.PlacementFailure, "invalid preview reports immediately");
        session.HandleWorldInput(WorldInput(new Vector2(250, 61)));
        Check.Equal(PlacementFailure.None, session.PlacementFailure, "valid preview clears prior failure immediately");
        session.HandleWorldInput(WorldInput(new Vector2(20, 61)));
        Check.Equal(PlacementFailure.None, session.PlacementFailure, "endpoint cursor snaps to legal clearance");
        Check.Nearly(48, session.PlacementPreviewPosition.X, "endpoint preview uses first legal path position");
        var deploymentCommands = new List<GameCommand>();
        session.HandleWorldInput(WorldInput(new Vector2(20, 61)) with { LeftPressed = true }, deploymentCommands.Add);
        Check.Equal(1, deploymentCommands.Count, "assisted plate click emits one command");
        Check.Nearly(48, deploymentCommands[0].X, "network command uses the visible snapped position");

        Check.True(session.TryDeployEmergencyDefense(position), "deploy stored plate");
        Check.Equal(0, session.EmergencyInventory, "stored plate consumed");
        Check.Equal(300, session.Economy.Credits, "stored plate costs no credits");
        var placementSession = Session();
        placementSession.EmergencyDefenses.Add(new PulsePlateInstance(999, position, placementSession.Content.Tactics.EmergencyDefense));
        Check.Equal(PlacementFailure.None, placementSession.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(229, 30)), "near-adjacent plate placement");
        placementSession.BeginEmergencyPlacement();
        placementSession.HandleWorldInput(WorldInput(new Vector2(205, 61)));
        Check.Equal(PlacementFailure.None, placementSession.PlacementFailure, "occupied cursor snaps beside existing plate");
        Check.True(Vector2.Distance(placementSession.PlacementPreviewPosition, position) >= placementSession.Content.Tactics.EmergencyDefense.MinimumSpacing,
            "assisted placement respects plate spacing");
        placementSession.CancelPlacement();

        session.SpawnEnemy("enemy", 1, 1);
        session.Enemies[0].UpdateMovement(20, session.Map.Path);
        var system = new TacticalDefenseSystem();
        system.Update(0.4f, session);
        Check.Nearly(62, session.Enemies[0].Health, "first pulse damage");
        Check.Nearly(172, session.Enemies[0].DistanceAlongPath, "first pulse applies bounded knockback");
        Check.Nearly(0.30f, session.Enemies[0].StatusEffects.SlowFactor, "first pulse slows enemy");
        Check.Equal(1, session.EmergencyDefenses[0].ChargesRemaining, "one pulse remains");
        system.Update(0.01f, session);
        session.Enemies[0].StatusEffects.Update(2f);
        session.Enemies[0].UpdateMovement(4.8f, session.Map.Path);
        system.Update(0.2f, session);
        Check.Nearly(24, session.Enemies[0].Health, "durable enemy can trigger second pulse after re-crossing");
        Check.Equal(0, session.EmergencyDefenses.Count, "spent plate removed");

        Check.True(!session.TryDeployEmergencyDefense(new Vector2(300, 30)), "direct plate buying is unavailable between waves");

        var directSession = SessionWithWaves(2);
        Check.True(directSession.TryDeployEmergencyDefense(new Vector2(200, 30)), "deploy stored plate before wave");
        Check.True(directSession.StartNextWave(), "start wave for direct buying");
        Check.True(directSession.TryDeployEmergencyDefense(new Vector2(300, 30)), "first direct plate purchase");
        Check.Equal(240, directSession.Economy.Credits, "first direct plate keeps accessible base cost");
        Check.True(directSession.TryDeployEmergencyDefense(new Vector2(330, 30)), "second direct plate purchase");
        Check.Equal(165, directSession.Economy.Credits, "second direct plate pays escalating cost");
        Check.Equal(90, directSession.CurrentEmergencyDirectPurchaseCost, "next direct plate price is visible and deterministic");
        ResolveSingleEnemyWave(directSession);
        Check.True(directSession.StartNextWave(), "start next wave for direct-price reset");
        Check.Equal(60, directSession.CurrentEmergencyDirectPurchaseCost, "every wave starts at the same direct plate price");

        var chainSession = Session();
        var plateDefinition = chainSession.Content.Tactics.EmergencyDefense;
        chainSession.EmergencyDefenses.Add(new PulsePlateInstance(1, new Vector2(200, 30), plateDefinition));
        chainSession.EmergencyDefenses.Add(new PulsePlateInstance(2, new Vector2(172, 30), plateDefinition));
        chainSession.SpawnEnemy("enemy", 1, 1);
        chainSession.Enemies[0].UpdateMovement(20, chainSession.Map.Path);
        system.Update(0.4f, chainSession);
        Check.Nearly(172, chainSession.Enemies[0].DistanceAlongPath, "adjacent plate cannot chain a second knockback in the same moment");
        Check.Nearly(24, chainSession.Enemies[0].Health, "anti-chain plate still applies its damage and control");
        Check.True(chainSession.Enemies[0].KnockbackGraceRemaining > 0, "enemy receives a temporary knockback grace period");

        var bossSession = Session();
        Check.True(bossSession.TryDeployEmergencyDefense(new Vector2(200, 30)), "deploy boss test plate");
        bossSession.SpawnEnemy("enemy", 1, 1, "Boss");
        bossSession.Enemies[0].UpdateMovement(200f / bossSession.Enemies[0].CurrentSpeed, bossSession.Map.Path);
        system.Update(0.4f, bossSession);
        Check.Nearly(193, bossSession.Enemies[0].DistanceAlongPath, "boss receives only one quarter of standard plate knockback");

        var capSession = SessionWithWave();
        capSession.Content.Tactics.EmergencyDefense.MaximumActive = 3;
        capSession.Economy.AddCredits(1000);
        Check.True(capSession.StartNextWave(), "start wave for field-cap test");
        Check.True(capSession.TryDeployEmergencyDefense(new Vector2(100, 30)), "field plate one");
        Check.True(capSession.TryDeployEmergencyDefense(new Vector2(130, 30)), "field plate two");
        Check.True(capSession.TryDeployEmergencyDefense(new Vector2(160, 30)), "field plate three");
        Check.Equal(PlacementFailure.DefenseCapacityReached,
            capSession.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(190, 30)), "active plate field cap");
        Check.True(UIManager.PulsePlateButtonLabel(capSession).Contains("FIELD 3/3", StringComparison.Ordinal),
            "full plate button retains active field count");
    }

    private static void ChargeForgeProduction()
    {
        var session = SessionWithWave();
        session.Economy.AddCredits(100);
        var position = new Vector2(50, 200);
        Check.Equal(PlacementFailure.None, session.ValidateTacticalPlacement(TacticalPlacementKind.ChargeForge, position), "forge placement");
        Check.True(session.TryPlaceGenerator(position), "place forge");
        Check.Equal(80, session.Economy.Credits, "forge purchase cost");
        Check.Equal(1, session.EmergencyInventory, "initial inventory retained");
        Check.True(UIManager.PulsePlateButtonLabel(session).Contains("STORED 1/3", StringComparison.Ordinal) &&
            UIManager.PulsePlateButtonLabel(session).Contains("FIELD 0/16", StringComparison.Ordinal),
            "plate button distinguishes forge storage from active field capacity");

        var system = new TacticalDefenseSystem();
        var initialTimer = session.Generator!.ProductionRemaining;
        system.Update(20f, session);
        Check.Nearly(initialTimer, session.Generator.ProductionRemaining, "forge pauses between waves");
        Check.Equal(1, session.EmergencyInventory, "downtime cannot generate plates");
        Check.True(session.StartNextWave(), "start wave for forge production");
        system.Update(34.1f, session);
        Check.Equal(2, session.EmergencyInventory, "forge produces to capacity");
        session.Economy.AddCredits(210);
        Check.True(session.TryUpgradeGenerator(), "upgrade forge");
        Check.Equal(4, session.Generator!.Level.Capacity, "upgraded storage");
        Check.Nearly(0.15f, session.Generator.Level.DefenseDamageBonus, "upgraded pulse damage");
    }

    private static void CheckpointRoundTrip()
    {
        var session = SessionWithWaves(2);
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "checkpoint tower placement");
        var tower = session.Towers[0];
        Check.True(session.TryUpgradeTower(tower.Id), "checkpoint tower upgrade");
        Check.True(session.TrySetTargetMode(tower.Id, TargetMode.Armored), "checkpoint target mode");
        Check.True(session.TryToggleAutoProtocol(tower.Id), "checkpoint automatic protocol armed");
        Check.True(session.StartNextWave(), "start wave before checkpoint");
        ResolveSingleEnemyWave(session);
        var telemetryTarget = new EnemyInstance(99, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        session.DamageResolver.Apply(telemetryTarget, new DamagePayload { Damage = 120, SourceTowerId = tower.Id });
        Check.True(session.CanSaveCheckpoint, "checkpoint is available between waves");

        var restored = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame());
        Check.Equal(1, restored.CurrentWave, "saved wave restored");
        Check.Equal(session.Economy.Credits, restored.Economy.Credits, "saved credits restored");
        Check.Equal(session.Economy.TotalKills, restored.Economy.TotalKills, "saved kills restored");
        Check.Equal(1, restored.Towers.Count, "saved tower restored");
        Check.Equal(1, restored.Towers[0].LevelIndex, "saved tower level restored");
        Check.Equal(TargetMode.Armored, restored.Towers[0].TargetMode, "saved targeting restored");
        Check.Equal(restored.Towers[0].Id, restored.AutoOverdriveTowerId, "saved automatic protocol restored");
        Check.Nearly(tower.LifetimeDamage, restored.Towers[0].LifetimeDamage, "saved per-tower damage restored");
        Check.Equal(tower.LifetimeKills, restored.Towers[0].LifetimeKills, "saved per-tower kills restored");
        Check.Equal(1, restored.Statistics.Towers.Single().Purchases, "saved statistics restored");
        Check.True(restored.CanSaveCheckpoint, "restored state remains checkpoint-safe");
    }

    private static void IndependentSaveSlots()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MinimalBastion.Tests", Guid.NewGuid().ToString("N"));
        var legacyRoot = Path.Combine(Path.GetTempPath(), "MinimalBastion.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var solo = SessionWithWaves(2);
            Check.True(solo.StartNextWave(), "start solo slot wave");
            ResolveSingleEnemyWave(solo);

            var coOp = SessionWithWaves(2);
            coOp.ConfigureCoOp(1);
            Check.True(coOp.TryPlaceTower("tower", new Vector2(50, 200), 2), "place player-two tower before co-op save");
            Check.True(coOp.StartNextWave(), "start co-op slot wave");
            ResolveSingleEnemyWave(coOp);
            Check.True(coOp.CanSaveCheckpoint, "co-op host can save at a safe intermission");

            var repository = new SaveSlotRepository(testRoot);
            repository.Save(solo, 2);
            repository.Save(coOp, 4);
            var slots = repository.GetSlots();
            Check.Equal(3, slots.Count, "occupied saves plus the next available slot are enumerated");
            Check.True(slots.Single(slot => slot.Slot == 2).IsOccupied, "solo slot is occupied");
            Check.True(!slots.Single(slot => slot.Slot == 2).IsCoOp, "solo metadata remains solo");
            Check.True(slots.Single(slot => slot.Slot == 4).IsCoOp, "co-op metadata is identified");
            Check.Equal(1, repository.FindFirstEmptySlot()!.Value, "new runs claim an empty slot instead of overwriting");

            foreach (var slot in new[] { 1, 3, 5, 6, 7, 8 })
                repository.Save(slot % 2 == 0 ? coOp : solo, slot);
            slots = repository.GetSlots();
            Check.Equal(9, slots.Count, "dynamic save list expands beyond the old five-slot limit");
            Check.Equal(9, repository.FindFirstEmptySlot()!.Value, "full initial pages allocate a new slot instead of overwriting");
            Check.True(slots.Take(8).All(slot => slot.IsOccupied), "all existing saves remain occupied after expansion");

            var slotUi = new UIManager(null!);
            slotUi.ConfigureSaveSlots(slots, false);
            slotUi.HandleSaveSlots(WorldInput(Vector2.Zero) with { NavigateDownPressed = true });
            Check.Equal(2, slotUi.SelectedSaveSlot, "save browser Down selects the next slot");
            slotUi.HandleSaveSlots(WorldInput(Vector2.Zero) with { NavigateRightPressed = true });
            Check.Equal(6, slotUi.SelectedSaveSlot, "save browser Right advances to the next page");
            slotUi.HandleSaveSlots(WorldInput(Vector2.Zero) with { NavigateUpPressed = true });
            Check.Equal(5, slotUi.SelectedSaveSlot, "save browser Up crosses back to the previous page");
            slotUi.ConfigureSaveSlots(slots, false);
            Check.Equal(UiAction.ConfirmSaveSlot,
                slotUi.HandleSaveSlots(WorldInput(Vector2.Zero) with { EnterPressed = true }),
                "save browser Enter confirms the selected usable slot");
            Check.Equal(UiAction.None,
                slotUi.HandleSaveSlots(WorldInput(new Vector2(870, 600)) with { LeftPressed = true }),
                "next-page control changes pages without loading");
            Check.Equal(6, slotUi.SelectedSaveSlot, "second save page selects its first entry");
            Check.Equal(UiAction.None,
                slotUi.HandleSaveSlots(WorldInput(new Vector2(845, 543)) with { LeftPressed = true }),
                "first delete click arms confirmation");
            Check.Equal(UiAction.DeleteSaveSlot,
                slotUi.HandleSaveSlots(WorldInput(new Vector2(845, 543)) with { LeftPressed = true }),
                "second delete click confirms the selected save");

            Check.True(repository.Delete(6), "occupied dynamic slot can be deleted");
            Check.True(!File.Exists(repository.GetSlotPath(6)), "deleted save file is removed");
            Check.Equal(6, repository.FindFirstEmptySlot()!.Value, "deleted gap is reused before allocating a higher slot");
            Check.True(!repository.Delete(6), "deleting an already empty slot is harmless");

            var restoredSolo = repository.Load(solo.Content, 2);
            var restoredCoOp = repository.Load(coOp.Content, 4);
            Check.True(!restoredSolo.IsCoOp, "solo slot restores as solo");
            Check.True(restoredCoOp.IsCoOp, "co-op slot restores as a hosted co-op session");
            Check.Equal(2, restoredCoOp.Towers.Single().OwnerPlayerId, "co-op tower ownership survives slot restore");

            Directory.CreateDirectory(legacyRoot);
            var legacyRepository = new SaveSlotRepository(legacyRoot);
            File.WriteAllText(legacyRepository.LegacySavePath, JsonSerializer.Serialize(solo.CaptureSaveGame()));
            var migrated = legacyRepository.GetSlots();
            Check.True(migrated[0].IsOccupied, "legacy checkpoint migrates into slot one");
            Check.True(File.Exists(legacyRepository.LegacySavePath), "legacy checkpoint remains untouched after migration");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            if (Directory.Exists(legacyRoot)) Directory.Delete(legacyRoot, true);
        }
    }

    private static void SaveSlotRecoveryBackup()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MinimalBastion.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new SaveSlotRepository(testRoot);
            var session = Session();
            var originalCredits = session.Economy.Credits;
            repository.Save(session, 1);
            session.Economy.AddCredits(25);
            repository.Save(session, 1);
            Check.True(File.Exists(repository.GetSlotBackupPath(1)), "overwriting a slot retains one recovery generation");

            File.WriteAllText(repository.GetSlotPath(1), "{ not valid json");
            var recovered = repository.LoadData(1);
            Check.Equal(originalCredits, recovered.Economy.Credits, "corrupt primary transparently loads its recovery generation");
            Check.True(repository.GetSlots().Single(slot => slot.Slot == 1).Error is null,
                "backup recovery keeps slot metadata usable");

            var recoveryGeneration = File.ReadAllText(repository.GetSlotBackupPath(1));
            session.Economy.AddCredits(25);
            repository.Save(session, 1);
            Check.Equal(recoveryGeneration, File.ReadAllText(repository.GetSlotBackupPath(1)),
                "saving after primary corruption preserves the known-good recovery generation");
            File.WriteAllText(repository.GetSlotPath(1), "{ corrupt again");
            Check.Equal(originalCredits, repository.LoadData(1).Economy.Credits,
                "preserved recovery remains usable after another interrupted primary");

            File.WriteAllText(repository.GetSlotPath(1), "{\"schemaVersion\":1}");
            Check.Equal(originalCredits, repository.LoadData(1).Economy.Credits,
                "parseable but structurally empty primary falls back to recovery");
            var semanticRecoveryGeneration = File.ReadAllText(repository.GetSlotBackupPath(1));
            repository.Save(session, 1);
            Check.Equal(semanticRecoveryGeneration, File.ReadAllText(repository.GetSlotBackupPath(1)),
                "semantic corruption cannot replace a known-good save recovery generation");

            var nestedCorruption = session.CaptureSaveGame();
            nestedCorruption.Statistics.Towers.Add(new RunTowerStatisticsSaveData
            {
                TowerId = "tower",
                DisplayName = "Tower",
                Specializations = null!
            });
            File.WriteAllText(repository.GetSlotPath(1), JsonSerializer.Serialize(nestedCorruption));
            Check.Equal(originalCredits, repository.LoadData(1).Economy.Credits,
                "parseable nested telemetry corruption falls back to recovery");
            var nestedRecoveryGeneration = File.ReadAllText(repository.GetSlotBackupPath(1));
            repository.Save(session, 1);
            Check.Equal(nestedRecoveryGeneration, File.ReadAllText(repository.GetSlotBackupPath(1)),
                "nested corruption cannot rotate over a valid checkpoint backup");

            File.Delete(repository.GetSlotPath(1));
            Check.True(repository.GetSlots().Single(slot => slot.Slot == 1).IsOccupied,
                "backup-only interrupted slot remains discoverable");
            Check.True(repository.Delete(1), "deleting a recovered slot removes its remaining generation");
            Check.True(!File.Exists(repository.GetSlotBackupPath(1)), "slot recovery copy is deleted with the slot");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    private static void PersistentRunHistory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MinimalBastion.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new RunHistoryRepository(testRoot);
            var first = new RunHistoryEntry
            {
                RunId = "run-a",
                CompletedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                Victory = true,
                MapId = "foundry_loop",
                MapName = "Foundry Loop",
                DifficultyId = "normal",
                DifficultyName = "Normal",
                CurrentWave = 20,
                TotalWaves = 20,
                Lives = 8,
                StartingLives = 20,
                Kills = 1090,
                TopTowerName = "Needle Turret"
            };
            repository.Upsert(first);
            repository.Upsert(first with
            {
                CompletedAtUtc = first.CompletedAtUtc.AddHours(1),
                Victory = false,
                IsEndless = true,
                CurrentWave = 31,
                Lives = 0,
                Kills = 2500
            });
            var updated = repository.GetEntries().Single();
            Check.Equal(31, updated.CurrentWave, "endless continuation updates the original run record");
            Check.True(!updated.Victory && updated.IsEndless, "run conclusion changes from campaign clear to endless defeat");
            Check.True(File.Exists(repository.BackupPath), "run history retains a recovery generation");

            repository.Upsert(first with { RunId = "run-b", CompletedAtUtc = first.CompletedAtUtc.AddHours(2) });
            Check.Equal("run-b", repository.GetEntries()[0].RunId, "run history is newest first");
            var historyUi = new UIManager(null!);
            historyUi.ConfigureRunHistory(repository.GetEntries());
            historyUi.HandleRunHistory(WorldInput(Vector2.Zero) with { NavigateDownPressed = true });
            Check.Equal("run-a", historyUi.SelectedRunHistoryId!, "run history Down selects the next record");
            historyUi.HandleRunHistory(WorldInput(Vector2.Zero) with { NavigateUpPressed = true });
            Check.Equal("run-b", historyUi.SelectedRunHistoryId!, "run history Up returns to the previous record");
            Check.True(repository.Delete("run-a"), "individual history entries can be deleted");
            Check.Equal(1, repository.GetEntries().Count, "deleting history leaves unrelated records intact");

            var recoveryRepository = new RunHistoryRepository(Path.Combine(testRoot, "recovery"));
            recoveryRepository.Upsert(first);
            recoveryRepository.Upsert(first with { RunId = "run-b", CompletedAtUtc = first.CompletedAtUtc.AddHours(1) });
            File.WriteAllText(recoveryRepository.HistoryPath, "{ invalid history");
            recoveryRepository.Upsert(first with { RunId = "run-c", CompletedAtUtc = first.CompletedAtUtc.AddHours(2) });
            File.Delete(recoveryRepository.HistoryPath);
            Check.Equal("run-a", recoveryRepository.GetEntries().Single().RunId,
                "history update after primary corruption preserves its known-good recovery generation");

            var semanticRepository = new RunHistoryRepository(Path.Combine(testRoot, "semantic-recovery"));
            semanticRepository.Upsert(first);
            semanticRepository.Upsert(first with { RunId = "run-b", CompletedAtUtc = first.CompletedAtUtc.AddHours(1) });
            File.WriteAllText(semanticRepository.HistoryPath, "[{\"runId\":\"\"}]");
            semanticRepository.Upsert(first with { RunId = "run-c", CompletedAtUtc = first.CompletedAtUtc.AddHours(2) });
            File.Delete(semanticRepository.HistoryPath);
            Check.Equal("run-a", semanticRepository.GetEntries().Single().RunId,
                "semantic history corruption cannot replace a known-good recovery generation");

            var session = Session();
            var runId = session.RunId;
            var restoredSave = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame());
            Check.Equal(runId, restoredSave.RunId, "checkpoint restore preserves run identity");
            var restoredNetwork = GameSession.RestoreCoOpState(session.Content, session.CaptureCoOpState(4, 0, false), 2);
            Check.Equal(runId, restoredNetwork.RunId, "co-op resync preserves run identity");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    private static void TowerSpecializations()
    {
        var session = Session();
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200), 2), "player two places branch tower");
        var tower = session.Towers[0];
        Check.True(session.TryUpgradeTower(tower.Id, 2), "upgrade into branch point");
        Check.True(tower.RequiresSpecialization, "branch choice required at level two");
        Check.True(!session.TryUpgradeTower(tower.Id, 2), "linear final upgrade blocked");
        Check.True(session.TrySpecializeTower(tower.Id, "alpha", 1), "other player selects specialization");
        Check.Equal("alpha", tower.SpecializationId!, "specialization identity");
        Check.Equal(2, tower.LevelIndex, "specialization reaches final level");
        Check.Nearly(30, tower.Level.Damage, "specialization level stats active");
        Check.True(!session.TrySpecializeTower(tower.Id, "beta", 2), "branch choice is permanent");
        Check.Equal(1, session.Statistics.Towers.Single().Specializations["alpha"], "branch telemetry");
    }

    private static void TowerTierTwoDoctrines()
    {
        var session = Session();
        var definition = session.Content.Towers["tower"];
        definition.Tier2Doctrines = new List<TowerDoctrineDefinition>
        {
            new() { Id = "tempo", DisplayName = "Tempo Feed", ShortLabel = "TEMPO", Summary = "Faster", UpgradeCost = 50, DamageMultiplier = 0.9f, AttackSpeedMultiplier = 1.2f },
            new() { Id = "focus", DisplayName = "Focus Feed", ShortLabel = "FOCUS", Summary = "Harder", UpgradeCost = 50, DamageMultiplier = 1.2f, AttackSpeedMultiplier = 0.9f, RangeMultiplier = 1.1f }
        };
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200), 2), "place doctrine tower");
        var tower = session.Towers[0];
        Check.True(tower.RequiresDoctrine, "tier two requires an explicit doctrine");
        Check.True(!session.TryUpgradeTower(tower.Id, 1), "linear tier two upgrade is blocked");
        Check.True(session.TryChooseTowerDoctrine(tower.Id, "tempo", 1), "other player chooses shared doctrine");
        Check.Equal("tempo", tower.DoctrineId!, "doctrine identity");
        Check.Equal(1, tower.LevelIndex, "doctrine reaches tier two");
        Check.Nearly(10.8f, tower.Level.Damage, "doctrine damage tradeoff active");
        Check.Nearly(1.32f, tower.Level.AttacksPerSecond, "doctrine cadence tradeoff active");
        Check.True(tower.RequiresSpecialization, "final role follows doctrine");
        Check.True(session.TrySpecializeTower(tower.Id, "alpha", 2), "either player chooses final role");
        Check.Nearly(27, tower.Level.Damage, "doctrine persists into final role damage");
        Check.Nearly(1.44f, tower.Level.AttacksPerSecond, "doctrine persists into final role cadence");
        Check.Equal(1, session.Statistics.Towers.Single().Specializations["doctrine:tempo"], "doctrine telemetry");
        var restored = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame());
        Check.Equal("tempo", restored.Towers[0].DoctrineId!, "checkpoint preserves doctrine");
        Check.Nearly(tower.Level.Damage, restored.Towers[0].Level.Damage, "checkpoint restores doctrine stats");
        var commandSession = Session();
        commandSession.Content.Towers["tower"].Tier2Doctrines = definition.Tier2Doctrines;
        Check.True(commandSession.TryPlaceTower("tower", new Vector2(50, 200)), "place network doctrine tower");
        Check.True(GameCommandProcessor.Apply(commandSession, new GameCommand
        {
            PlayerId = 2,
            Type = GameCommandType.ChooseDoctrine,
            EntityId = commandSession.Towers[0].Id,
            DoctrineId = "focus"
        }).Accepted, "co-op doctrine command accepted");
        Check.Equal("focus", commandSession.Towers[0].DoctrineId!, "co-op doctrine command applied");

        var branchMetrics = new TowerRunMetrics { TowerId = definition.Id };
        var branchTower = new TowerInstance(9, definition, Vector2.Zero);
        Check.True(branchTower.TryChooseDoctrine("tempo"), "telemetry tower chooses doctrine");
        branchMetrics.RecordBranchUpgrade(branchTower);
        Check.True(branchTower.TrySpecialize("alpha"), "telemetry tower chooses final role");
        branchMetrics.RecordBranchUpgrade(branchTower);
        Check.Equal(1, branchMetrics.Doctrines["tempo"], "simulation telemetry records doctrine choice");
        Check.Equal(1, branchMetrics.Specializations["alpha"], "simulation telemetry records final choice");
        Check.Equal(1, branchMetrics.BuildPaths["tempo>alpha"], "simulation telemetry records completed cross-tree path");
    }

    private static void TowerOverdrive()
    {
        var session = Session();
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200), 2), "place overdrive tower");
        var tower = session.Towers[0];
        Check.True(session.TryOverdriveTower(tower.Id, 1), "other player activates overdrive");
        Check.Nearly(1.75f, session.GetEffectiveAttacksPerSecond(tower), "overdrive rate bonus");
        Check.Equal(1, session.Statistics.Towers.Single().Overdrives, "overdrive telemetry");
        Check.True(!session.TryOverdriveTower(tower.Id, 2), "overdrive cooldown enforced");
        for (var index = 0; index < 51; index++) session.Update(0.1f);
        Check.True(!tower.IsOverdriven, "overdrive duration expires");
        Check.True(session.OverdriveCooldownRemaining > 0, "cooldown outlasts effect");
        for (var index = 0; index < 130; index++) session.Update(0.1f);
        Check.True(session.TryOverdriveTower(tower.Id, 2), "overdrive recharges");

        var automatic = Session();
        automatic.Content.Towers["tower"].Levels[0].Range = 220;
        automatic.Content.Towers["tower"].Protocol = new TowerProtocolDefinition
        {
            DisplayName = "Test Pulse",
            Summary = "Test protocol",
            DurationSeconds = 4,
            CooldownSeconds = 10,
            AttackSpeedBonus = 0.5f,
            DamageBonus = 0.25f,
            AutoTriggerCount = 1,
            BurstRadius = 250,
            BurstDamage = 10
        };
        Check.True(automatic.TryPlaceTower("tower", new Vector2(50, 200)), "place automatic protocol tower");
        var automaticTower = automatic.Towers[0];
        Check.True(automatic.TryToggleAutoProtocol(automaticTower.Id, 2), "arm automatic protocol from player two");
        automatic.SpawnEnemy("enemy", 1, 1);
        automatic.Update(0.1f);
        Check.True(automaticTower.IsOverdriven, "automatic protocol activates under configured pressure");
        Check.Nearly(1.5f, automatic.GetEffectiveAttacksPerSecond(automaticTower), "protocol-specific rate bonus");
        Check.Nearly(12.5f, automatic.GetEffectiveDamage(automaticTower, 10), "protocol-specific damage bonus");
        Check.True(automatic.Enemies[0].Health < automatic.Enemies[0].MaxHealth, "protocol activation pulse applies damage");
    }

    private static GameSession Session()
    {
        var map = new MapDefinition
        {
            Id = "test",
            Path = new List<PointData> { Point(0, 30), Point(500, 30) },
            BuildableRegions = new List<RectangleData> { new() { X = 20, Y = 75, Width = 100, Height = 30 }, new() { X = 20, Y = 150, Width = 100, Height = 100 } },
            StartingCredits = 300,
            StartingLives = 20,
            Background = new BackgroundData()
        };
        var tower = new TowerDefinition
        {
            Id = "tower",
            DisplayName = "Test Tower",
            PurchaseCost = 90,
            Levels = new List<TowerLevelDefinition> { new() { Range = 100, Damage = 10, AttacksPerSecond = 1, UpgradeCost = 50 }, new() { Range = 110, Damage = 12, AttacksPerSecond = 1.1f, UpgradeCost = 80 }, new() { Range = 120, Damage = 15, AttacksPerSecond = 1.2f } },
            Specializations = new List<TowerSpecializationDefinition>
            {
                new() { Id = "alpha", DisplayName = "Alpha", ShortLabel = "ALPHA", Summary = "Damage", UpgradeCost = 80, Level = new TowerLevelDefinition { Range = 125, Damage = 30, AttacksPerSecond = 1.2f } },
                new() { Id = "beta", DisplayName = "Beta", ShortLabel = "BETA", Summary = "Speed", UpgradeCost = 75, Level = new TowerLevelDefinition { Range = 120, Damage = 18, AttacksPerSecond = 2f } }
            }
        };
        return new GameSession(new GameContent
        {
            Towers = new Dictionary<string, TowerDefinition> { [tower.Id] = tower },
            Enemies = new Dictionary<string, EnemyDefinition> { ["enemy"] = Enemy("enemy", 100, 10, 1, 0, 0), ["armored"] = Enemy("armored", 100, 10, 1, 4, 0), ["shielded"] = Enemy("shielded", 100, 10, 1, 0, 20) },
            Map = map,
            Waves = new WaveSetDefinition { Waves = new List<WaveDefinition>() }
        });
    }

    private static GameSession SessionWithWave()
    {
        return SessionWithWaves(1);
    }

    private static GameSession SessionWithWaves(int count)
    {
        var session = Session();
        return new GameSession(new GameContent
        {
            Towers = session.Content.Towers,
            Enemies = session.Content.Enemies,
            Map = session.Content.Map,
            Waves = new WaveSetDefinition
            {
                Waves = Enumerable.Range(1, count).Select(number => new WaveDefinition
                {
                    Number = number,
                    Groups = new List<WaveGroupDefinition> { new() { EnemyId = "enemy", Count = 1, SpawnInterval = 1f } }
                }).ToList()
            }
        });
    }

    private static EnemyDefinition Enemy(string id, float health, float speed, int reward, float armor, float shield) => new()
    {
        Id = id,
        DisplayName = id,
        MaxHealth = health,
        Speed = speed,
        Reward = reward,
        Armor = armor,
        Shield = shield,
        Visual = new EnemyVisualData { Radius = 10 }
    };

    private static PointData Point(float x, float y) => new() { X = x, Y = y };

    private static InputSnapshot WorldInput(Vector2 position) => new(
        MousePosition: position,
        LeftPressed: false,
        LeftReleased: false,
        RightPressed: false,
        PingPressed: false,
        EscapePressed: false,
        PausePressed: false,
        DebugKeyPressed: false,
        IsMouseOverLogicalCanvas: true,
        TowerHotkey: 0,
        UpgradePressed: false,
        SellPressed: false,
        TargetPressed: false,
        StartWavePressed: false,
        SpeedPressed: false,
        EmergencyPressed: false,
        GeneratorPressed: false,
        OverdrivePressed: false,
        TextEntered: "",
        BackspacePressed: false,
        EnterPressed: false);

}

internal static class Check
{
    public static void True(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {name}");
    }

    public static void Equal<T>(T expected, T actual, string name) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}: {name}");
    }

    public static void Nearly(float expected, float actual, string name)
    {
        if (MathF.Abs(expected - actual) > 0.02f) throw new InvalidOperationException($"Expected {expected:0.###}, got {actual:0.###}: {name}");
    }

    public static void Throws<TException>(Action action, string name) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}: {name}");
    }
}
