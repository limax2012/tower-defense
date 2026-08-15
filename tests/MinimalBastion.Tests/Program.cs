using MinimalBastion;
using MinimalBastion.Combat;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Economy;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
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
            ("tactical color palette", TacticalColorPalette),
            ("map roster and power nodes", MapRosterAndPowerNodes),
            ("difficulty profiles and persistence", DifficultyProfilesAndPersistence),
            ("power node tower intel", PowerNodeTowerIntel),
            ("pause UI glyph coverage", PauseUiGlyphCoverage),
            ("opening wave balance", OpeningWaveBalance),
            ("path progress", PathProgress),
            ("target modes", TargetModes),
            ("damage and armor", DamageAndArmor),
            ("damage over time floor", DamageOverTimeFloor),
            ("status effects", StatusEffects),
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
            ("defeat field inspection", DefeatFieldInspection),
            ("co-op shared control commands", CoOpOwnershipCommands),
            ("network deterministic commands", NetworkDeterministicCommands),
            ("co-op active state snapshot", CoOpActiveStateSnapshot),
            ("co-op wave ready", CoOpWaveReady),
            ("online co-op transport", CoOpLoopbackTransport),
            ("online co-op reconnect transport", CoOpReconnectTransport),
            ("co-op invalid code", CoOpInvalidCode),
            ("co-op incompatible build", CoOpIncompatibleBuild),
            ("online endpoint parsing", OnlineEndpointParsing),
            ("tower information", TowerInformation),
            ("tower library reference", TowerLibraryReference),
            ("tower specializations", TowerSpecializations),
            ("tower overdrive", TowerOverdrive),
            ("emergency pulse plates", EmergencyPulsePlates),
            ("charge forge production", ChargeForgeProduction),
            ("checkpoint round trip", CheckpointRoundTrip),
            ("independent solo and co-op save slots", IndependentSaveSlots),
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
    }

    private static void ContentCounts()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal(10, content.Towers.Count, "tower count");
        Check.Equal(5, content.Enemies.Count, "enemy count");
        Check.Equal(20, content.Waves.Waves.Count, "wave count");
        Check.Equal(4, content.Maps.Count, "map count");
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
        ui.HandleMainMenu(WorldInput(new Vector2(740, 390)) with { LeftPressed = true });
        Check.Equal("hard", ui.SelectedDifficultyId, "difficulty selector cycles profiles");
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
        Check.Equal(UiAction.CloseSettings,
            ui.HandleSettingsInput(WorldInput(Vector2.Zero) with { EscapePressed = true }),
            "escape closes settings safely");
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
        var mapUi = new UIManager(null!);
        mapUi.ConfigureMaps(content.Maps.Values);
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
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 5, SourceId = 1 });
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 7, SourceId = 2 });
        statuses.Apply(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 9, SourceId = 3 });
        Check.Equal(2, statuses.Active.Count(x => x.Type == StatusType.Burn), "burn cap");
        Check.Nearly(16, statuses.ConsumeBurnDamage(1), "burn tick");
        statuses.Update(2.1f);
        Check.Equal(0, statuses.Active.Count, "status expiry");
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
        var restoredStatistics = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame()).Statistics;
        Check.Nearly(100f / 3f, restoredStatistics.Towers.Single(metrics => metrics.TowerId == "beacon").SupportDamageEquivalent,
            "support contribution survives save restoration");

        var escaped = new EnemyInstance(2, session.Content.Enemies["armored"], session.Map.Path, 1, 1);
        session.OnEnemyEscaped(escaped);
        Check.Equal("armored", session.Statistics.GreatestLeakThreat!.EnemyId, "stats leak threat");
        Check.Equal(1, session.Statistics.GreatestLeakThreat.LivesLost, "stats lives lost");
        session.Update(0.05f);
        Check.Nearly(0.06f, session.Statistics.SimulatedSeconds, "stats defense time");
    }

    private static void DefeatFieldInspection()
    {
        var ui = new UIManager(null!);
        Check.Equal(UiAction.ViewField,
            ui.HandleResultInput(WorldInput(new Vector2(399, 603)) with { LeftPressed = true }, false),
            "defeat results expose the field-inspection action");
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

    private static void CoOpLoopbackTransport()
    {
        CoOpLoopbackTransportAsync().GetAwaiter().GetResult();
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
        Check.Nearly(first.Towers.Values.Sum(x => x.StatusEnemySeconds.Values.Sum()), second.Towers.Values.Sum(x => x.StatusEnemySeconds.Values.Sum()), "deterministic status uptime");
        Check.True(first.WaveReached >= 2, "headless bot reaches requested wave limit");
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
        Check.Equal(JsonSerializer.Serialize(wave25), JsonSerializer.Serialize(EndlessWaveGenerator.Create(25, 20, anchor)),
            "endless generation is deterministic");

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
        Check.True(strongerSource.TryUpgrade(), "stronger beacon reaches level two");
        var buffs = new BuffSystem();
        buffs.Update(new[] { recipient, source, strongerSource });
        var signalBuff = buffs.Get(recipient);
        Check.True(signalBuff.IsActive, "beacon aura reports an active tower buff");
        Check.Nearly(beacon.Levels[1].AuraAttackSpeedBonus, signalBuff.AttackSpeedBonus, "overlapping beacons use strongest rate instead of stacking");
        Check.Nearly(beacon.Levels[1].AuraRangeBonus, signalBuff.RangeBonus, "overlapping beacons use strongest range instead of stacking");
        var signalSummary = TowerInfo.SignalBeaconStatChange(recipient.Level, signalBuff);
        Check.True(signalSummary.Contains("SIGNAL BEACON", StringComparison.Ordinal), "beacon summary identifies its source");
        Check.True(signalSummary.Contains("RATE 2>2.5/s", StringComparison.Ordinal), "beacon summary exposes exact strongest rate change");
        Check.True(signalSummary.Contains("RANGE 125>145", StringComparison.Ordinal), "beacon summary exposes exact strongest range change");

        var effectiveUpgrade = TowerInfo.UpgradeSummary(needle, 0, signalBuff, default);
        Check.True(effectiveUpgrade.Contains("RATE 2.5>2.75", StringComparison.Ordinal), "upgrade comparison includes beacon rate");
        Check.True(effectiveUpgrade.Contains("RANGE 145>157", StringComparison.Ordinal), "upgrade comparison includes beacon range");
        var beaconUpgrade = TowerInfo.UpgradeSummary(beacon, 0);
        Check.True(beaconUpgrade.Contains("AURA 145>165", StringComparison.Ordinal), "beacon upgrade compares aura radius");
        Check.True(beaconUpgrade.Contains("RATE +15%>+25%", StringComparison.Ordinal), "beacon upgrade compares aura rate");
    }

    private static void TowerLibraryReference()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        var ui = new UIManager(null!);
        ui.ConfigureTowerLibrary(content.Towers.Values);
        Check.Equal(UiAction.TowerLibrary,
            ui.HandleMainMenu(WorldInput(new Vector2(712, 442)) with { LeftPressed = true }),
            "title screen opens tower library");
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
}
