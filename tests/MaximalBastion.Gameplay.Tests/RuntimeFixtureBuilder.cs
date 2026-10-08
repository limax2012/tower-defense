extern alias legacy;
using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Gameplay;
using MaximalBastion.Multiplayer;
using Legacy = legacy::MaximalBastion;
using Xna = Microsoft.Xna.Framework;

internal static class RuntimeFixtureBuilder
{
    public static RuntimeConformanceFixture Create(Legacy.Data.GameContent content)
    {
        var fixture = new RuntimeConformanceFixture();
        foreach (var map in content.Maps.Keys.OrderBy(x => x))
        foreach (var challenge in new[] { "standard", "close_quarters" })
            fixture.Scenarios.Add(CreateScenario(content, map, challenge, false));
        fixture.Scenarios.Add(CreateScenario(content, "foundry_loop", "standard", true));
        return fixture;
    }

    private static RuntimeConformanceScenario CreateScenario(Legacy.Data.GameContent content,
        string map, string challenge, bool endless)
    {
        var session = new Legacy.GameSession(content, map, "normal", challenge);
        session.ConfigureCoOp(1);
        session.Economy.AddCredits(100000);
        var towerIndex = 0;
        foreach (var tower in content.Towers.Values)
        {
            var found = false;
            foreach (var region in session.Map.BuildableRegions)
            {
                for (var y = region.Top + 5; y < region.Bottom && !found; y += 20)
                for (var x = region.Left + 5; x < region.Right && !found; x += 20)
                {
                    if (!session.TryPlaceTower(tower.Id, new Xna.Vector2(x, y), towerIndex % 2 + 1, false)) continue;
                    var placed = session.Towers[^1];
                    session.TryChooseTowerDoctrine(placed.Id, tower.Tier2Doctrines[towerIndex % tower.Tier2Doctrines.Count].Id, placed.OwnerPlayerId);
                    session.TrySpecializeTower(placed.Id, tower.Specializations[towerIndex % tower.Specializations.Count].Id, placed.OwnerPlayerId);
                    found = true;
                }
                if (found) break;
            }
            if (!found) throw new Exception("No fixture placement for " + tower.Id);
            towerIndex++;
        }
        foreach (var region in session.Map.BuildableRegions)
        {
            if (session.TryPlaceGenerator(new Xna.Vector2(region.Center.X, region.Center.Y), 1, false)) break;
        }
        if (session.Generator is not null) session.TryUpgradeGenerator(1);
        session.TryDeployEmergencyDefense(session.Map.Path.GetPosition(session.Map.Path.TotalLength * .25f), 1);
        var initial = session.CaptureSaveGame();
        initial.RunId = "11111111222233334444555555555555";
        initial.SavedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        initial.Waves.CurrentWaveNumber = endless ? 30 : 20;
        initial.Waves.IsFinalWaveCleared = endless;
        initial.Waves.EndlessModeEnabled = endless;
        initial.Waves.IntermissionRemaining = 0;
        var scenario = new RuntimeConformanceScenario
        {
            Name = map + "/" + challenge + (endless ? "/endless" : "/wave21"),
            InitialSaveJson = JsonSerializer.Serialize(initial, Legacy.Data.ContentJson.Options),
            TickCount = 4800,
            SnapshotTicks = [299, 719, 1439, 2399, 3599]
        };
        long sequence = 0;
        void Command(int tick, GameCommandType type, int entityId = 0, int playerId = 1, float speed = 1, bool paused = false) =>
            scenario.Commands.Add(new RuntimeConformanceCommand { Tick = tick,
                Command = new GameCommand { Sequence = ++sequence, PlayerId = playerId, Type = type,
                    EntityId = entityId, Speed = speed, Paused = paused, TargetMode = MaximalBastion.Core.TargetMode.Strongest } });
        Command(0, GameCommandType.StartWave);
        Command(60, GameCommandType.ToggleAutoProtocol, 1);
        Command(180, GameCommandType.OverdriveTower, 2, 2);
        Command(360, GameCommandType.SetTargetMode, 1);
        Command(450, GameCommandType.SetSpeed, speed: 2);
        Command(600, GameCommandType.SetPaused, paused: true);
        Command(660, GameCommandType.SetPaused, paused: false);
        Command(800, GameCommandType.SetSpeed);
        foreach (var tower in session.Towers) Command(900 + tower.Id * 3, GameCommandType.UpgradeTower, tower.Id, tower.OwnerPlayerId);
        Command(1800, GameCommandType.SetSpeed, speed: 2);
        Command(2800, GameCommandType.SetSpeed);
        Command(4100, GameCommandType.StartWave);

        session = Legacy.GameSession.RestoreSaveGame(content, initial);
        var runner = new Legacy.Multiplayer.DeterministicSessionRunner(session);
        var commands = scenario.Commands.OrderBy(x => x.Tick).ThenBy(x => x.Command.Sequence).ToArray();
        var index = 0;
        while (runner.Tick < scenario.TickCount)
        {
            while (index < commands.Length && commands[index].Tick <= runner.Tick + 120)
            {
                var command = commands[index++];
                var native = JsonSerializer.Deserialize<Legacy.Multiplayer.GameCommand>(
                    JsonSerializer.Serialize(command.Command, ContentJson.Options), Legacy.Data.ContentJson.Options)!;
                if (!runner.Schedule(command.Tick, native)) throw new Exception("Fixture schedule failed.");
            }
            runner.RunTicks(1);
            if (runner.Tick % 12 == 0 || scenario.SnapshotTicks.Contains((int)runner.Tick))
                scenario.Samples.Add(new RuntimeConformanceSample { Tick = runner.Tick,
                    Checksum = Legacy.Multiplayer.SessionChecksum.Compute(session, runner.Tick) });
        }
        return scenario;
    }
}
