using MinimalBastion;
using MinimalBastion.Combat;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Economy;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
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
            ("tactical color palette", TacticalColorPalette),
            ("map roster and power nodes", MapRosterAndPowerNodes),
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
            ("early wave call reward", EarlyWaveCallReward),
            ("mixed wave composition", MixedWaveComposition),
            ("arc relay chain", ArcRelayChain),
            ("frost area control", FrostAreaControl),
            ("mortar predictive aim", MortarPredictiveAim),
            ("economy telemetry", EconomyTelemetry),
            ("run statistics", RunStatistics),
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
            ("tower specializations", TowerSpecializations),
            ("tower overdrive", TowerOverdrive),
            ("emergency pulse plates", EmergencyPulsePlates),
            ("charge forge production", ChargeForgeProduction),
            ("checkpoint round trip", CheckpointRoundTrip),
            ("headless simulation deterministic", HeadlessSimulationDeterministic)
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

    private static void ContentCounts()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ContentData");
        var content = new ContentLoader(root).Load();
        Check.Equal(10, content.Towers.Count, "tower count");
        Check.Equal(5, content.Enemies.Count, "enemy count");
        Check.Equal(20, content.Waves.Waves.Count, "wave count");
        Check.Equal(2, content.Maps.Count, "map count");
        Check.Equal(1090, content.Waves.Waves.SelectMany(x => x.Groups).Sum(x => x.Count), "enemy count in waves");
        Check.True(content.Waves.Waves.SelectMany(x => x.Groups).Count(x => x.Rank.Equals("Elite", StringComparison.OrdinalIgnoreCase)) >= 5, "elite encounter groups");
        Check.Equal(1, content.Waves.Waves.SelectMany(x => x.Groups).Count(x => x.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)), "final boss group");
        Check.True(content.Towers.Values.Select(x => x.Visual.Primary).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 8, "tower palette");
        Check.True(content.Enemies.Values.Select(x => x.Visual.Primary).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 5, "enemy palette");
        Check.True(!content.Map.Background.Base.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase), "map palette");
        Check.Equal(2, content.Tactics.EmergencyDefense.Charges, "pulse plate charges");
        Check.Equal(3, content.Tactics.Generator.Levels.Count, "charge forge levels");
        Check.True(content.Towers["prism_beam"].Levels.Select(x => x.ArmorPierce).SequenceEqual(new[] { 3f, 5f, 8f }), "prism beam penetration curve");
        Check.Equal(8, content.Towers.Values.Sum(x => x.Specializations.Count), "specialization count");
        Check.True(new[] { "needle_turret", "frost_spire", "ember_coil", "breaker_cannon" }.All(id => content.Towers[id].Specializations.Count == 2), "branching tower roster");
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

    private static void TacticalColorPalette()
    {
        Check.Equal(new Color(21, 43, 70), ColorPalette.Navy, "deep navy HUD");
        Check.Equal(new Color(56, 78, 101), ColorPalette.Path, "muted slate road");
        Check.Equal(new Color(244, 245, 248), ColorPalette.Paper, "soft off-white surface");
        Check.Equal(new Color(33, 146, 170), ColorPalette.Cyan, "controlled cyan accent");
        Check.Equal(new Color(42, 194, 117), ColorPalette.Green, "controlled green accent");
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
        Check.Equal("DMG +15%", TowerInfo.PowerNodeBonus(nodes[0]), "node bonus label");
        Check.Equal("DMG 8>9.2", TowerInfo.PowerNodeStatChange(content.Towers["needle_turret"], content.Towers["needle_turret"].Levels[0], session.Map.GetPowerBuff(position)), "node stat delta");
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
        Check.True(session.TryPlaceTower("tower", new Vector2(50, 200)), "stats tower placement");
        var tower = session.Towers[0];
        var target = new EnemyInstance(1, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 120, SourceTowerId = tower.Id });
        var towerStats = session.Statistics.Towers.Single();
        Check.Equal(1, towerStats.Purchases, "stats purchases");
        Check.Equal(1, towerStats.Kills, "stats attributed kills");
        Check.Nearly(100, towerStats.Damage, "stats effective damage");

        var escaped = new EnemyInstance(2, session.Content.Enemies["armored"], session.Map.Path, 1, 1);
        session.OnEnemyEscaped(escaped);
        Check.Equal("armored", session.Statistics.GreatestLeakThreat!.EnemyId, "stats leak threat");
        Check.Equal(1, session.Statistics.GreatestLeakThreat.LivesLost, "stats lives lost");
        session.Update(0.05f);
        Check.Nearly(0.05f, session.Statistics.SimulatedSeconds, "stats defense time");
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
            Type = GameCommandType.SellTower,
            EntityId = session.Towers[0].Id
        }).Accepted, "other player can sell tower");
        Check.Equal(0, session.Towers.Count, "shared sale removes tower");
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
        var overdrive = new GameCommand { Sequence = 3, ClientRequestId = 3, PlayerId = 2, Type = GameCommandType.OverdriveTower, EntityId = 1 };
        Check.True(firstRunner.Schedule(0, placement) && secondRunner.Schedule(0, placement), "schedule mirrored placement");
        Check.True(firstRunner.Schedule(1, start) && secondRunner.Schedule(1, start), "schedule mirrored wave");
        Check.True(firstRunner.Schedule(2, overdrive) && secondRunner.Schedule(2, overdrive), "schedule mirrored overdrive");
        firstRunner.RunTicks(80);
        secondRunner.RunTicks(80);
        Check.Equal(SessionChecksum.Compute(first, firstRunner.Tick), SessionChecksum.Compute(second, secondRunner.Tick), "mirrored state checksum");
        Check.Equal(2, first.Towers[0].OwnerPlayerId, "mirrored ownership");
        Check.True(first.Towers[0].IsOverdriven, "mirrored active ability state");
        Check.True(first.OverdriveCooldownRemaining > 0, "mirrored cooldown state");
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
        Check.Equal(1, client.Enemies[0].StatusEffects.Active.Count, "snapshot restores status effects");
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
        Check.True(ready.RegisterReady(2, true), "player two can ready");
        Check.True(ready.IsReady(2), "player two ready is visible");
        Check.True(!ready.StartQueued, "one player cannot start co-op wave");
        Check.True(!ready.RegisterReady(2, true), "duplicate ready is ignored");
        Check.True(ready.RegisterReady(1, true), "player one can ready");
        Check.True(ready.StartQueued, "both players queue the wave");
        ready.Reset();
        Check.Equal(0, ready.ReadyMask, "ready state resets for next wave");
        Check.True(!ready.RegisterReady(1, false), "ready is rejected outside preparation");
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
                new() { Range = 300, Damage = 40, AttacksPerSecond = 1, ProjectileSpeed = 100, SplashRadius = 45 }
            }
        };
        var tower = new TowerInstance(8, definition, new Vector2(100, 150));
        var target = new EnemyInstance(7, session.Content.Enemies["enemy"], session.Map.Path, 1, 1);
        target.UpdateMovement(10, session.Map.Path);
        TowerBehaviorRegistry.Create("splash_projectile").Attack(new TowerInstanceContext { Tower = tower, Target = target, Session = session });
        var shell = session.Projectiles.Projectiles.Single();
        Check.True(shell.AimPoint.X > target.Position.X + 5, "mortar leads moving target");
        Check.Nearly(0, shell.Payload.Status?.Magnitude ?? 0, "mortar no longer hides a slow effect");
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
        Check.True(TowerInfo.UpgradeSummary(content.Towers["needle_turret"], 0).Contains("DMG", StringComparison.Ordinal),
            "upgrade summary exposes damage delta");
    }

    private static void EmergencyPulsePlates()
    {
        var session = Session();
        var position = new Vector2(200, 30);
        Check.Equal(PlacementFailure.None, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position), "road placement");
        Check.Equal(PlacementFailure.MustBeOnPath, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(200, 100)), "off-road rejection");
        Check.Equal(PlacementFailure.TooCloseToPathEndpoint, session.ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, new Vector2(20, 30)), "endpoint rejection");
        Check.True(session.TryDeployEmergencyDefense(position), "deploy stored plate");
        Check.Equal(0, session.EmergencyInventory, "stored plate consumed");
        Check.Equal(300, session.Economy.Credits, "stored plate costs no credits");

        session.SpawnEnemy("enemy", 1, 1);
        session.Enemies[0].UpdateMovement(20, session.Map.Path);
        var system = new TacticalDefenseSystem();
        system.Update(0.4f, session);
        Check.Nearly(68, session.Enemies[0].Health, "first pulse damage");
        Check.Equal(1, session.EmergencyDefenses[0].ChargesRemaining, "one pulse remains");
        system.Update(0.9f, session);
        Check.Nearly(68, session.Enemies[0].Health, "same enemy cannot waste second pulse");
        session.Enemies[0].UpdateMovement(10, session.Map.Path);
        session.SpawnEnemy("enemy", 1, 1);
        session.Enemies[1].UpdateMovement(20, session.Map.Path);
        system.Update(0.01f, session);
        Check.Nearly(68, session.Enemies[1].Health, "consecutive enemy reliably triggers second pulse");
        Check.Equal(0, session.EmergencyDefenses.Count, "spent plate removed");

        Check.True(session.TryDeployEmergencyDefense(new Vector2(300, 30)), "buy and deploy plate");
        Check.Equal(230, session.Economy.Credits, "direct plate purchase cost");
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

        var system = new TacticalDefenseSystem();
        var initialTimer = session.Generator!.ProductionRemaining;
        system.Update(20f, session);
        Check.Nearly(initialTimer, session.Generator.ProductionRemaining, "forge pauses between waves");
        Check.Equal(1, session.EmergencyInventory, "downtime cannot generate plates");
        Check.True(session.StartNextWave(), "start wave for forge production");
        system.Update(42.1f, session);
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
        Check.True(session.StartNextWave(), "start wave before checkpoint");
        ResolveSingleEnemyWave(session);
        Check.True(session.CanSaveCheckpoint, "checkpoint is available between waves");

        var restored = GameSession.RestoreSaveGame(session.Content, session.CaptureSaveGame());
        Check.Equal(1, restored.CurrentWave, "saved wave restored");
        Check.Equal(session.Economy.Credits, restored.Economy.Credits, "saved credits restored");
        Check.Equal(session.Economy.TotalKills, restored.Economy.TotalKills, "saved kills restored");
        Check.Equal(1, restored.Towers.Count, "saved tower restored");
        Check.Equal(1, restored.Towers[0].LevelIndex, "saved tower level restored");
        Check.Equal(TargetMode.Armored, restored.Towers[0].TargetMode, "saved targeting restored");
        Check.Equal(1, restored.Statistics.Towers.Single().Purchases, "saved statistics restored");
        Check.True(restored.CanSaveCheckpoint, "restored state remains checkpoint-safe");
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
