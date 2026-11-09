using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.UI;
using MaximalBastion.Waves;

namespace MaximalBastion.Tests;

internal static class EnemyRosterTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        SandboxRanks(content);
        AuthoredRanks(content);
        LegacySnapshotRanks(content);
    }

    private static void SandboxRanks(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var definition in content.Enemies.Values)
        {
            var supported = EnemyRoster.RanksFor(definition.Id);
            Check.True(supported.SequenceEqual(definition.Id == "t5_regenerator"
                    ? new[] { EnemyRank.Standard, EnemyRank.Elite, EnemyRank.Boss }
                    : new[] { EnemyRank.Standard, EnemyRank.Elite }),
                $"{definition.Id}: available ranks match the playable roster");
            foreach (var rank in supported)
                Check.True(session.SpawnSandboxTargets(definition.Id, 1, 1, rank.ToString(), false),
                    $"{definition.Id} {rank}: supported Sandbox rank spawns");
            if (definition.Id == "t5_regenerator") continue;
            var checksum = SessionChecksum.Compute(session, 0);
            Check.True(!session.SpawnSandboxTargets(definition.Id, 1, 1, "Boss", false),
                $"{definition.Id}: unavailable Boss rank is rejected");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0),
                "Rejected Sandbox ranks leave gameplay and entity identities unchanged");
        }
        Check.True(!session.SpawnSandboxTargets("t5_regenerator", 1, 1, "999", false),
            "Numeric values outside EnemyRank are rejected");
        var boss = session.Enemies.Single(enemy => enemy.IsBoss);
        Check.Nearly(content.Enemies["t5_regenerator"].MaxHealth * 4.5f, boss.MaxHealth,
            "Regenerator Boss retains its authored health multiplier");

        var ordered = content.Enemies.Values.OrderBy(enemy => enemy.MaxHealth).ThenBy(enemy => enemy.Id).ToArray();
        var input = default(InputSnapshot) with { IsMouseOverLogicalCanvas = true, TextEntered = "" };
        for (var index = 0; index < ordered.Length; index++)
        {
            var ui = new UIManager(null!);
            session.ClearSandboxTargets();
            for (var move = 0; move < index; move++)
                ui.HandleGameplayInput(input with { SandboxEnemyNextPressed = true }, session);
            var ranks = EnemyRoster.RanksFor(ordered[index].Id);
            for (var cycle = 0; cycle <= ranks.Count; cycle++)
            {
                ui.HandleGameplayInput(input with { SandboxSpawnPressed = true }, session);
                var enemy = session.Enemies.Single();
                Check.Equal(ordered[index].Id, enemy.Definition.Id, "Sandbox spawn uses the selected family");
                Check.Equal(ranks[cycle % ranks.Count], enemy.Rank,
                    "Desktop rank cycling visits only the selected family's supported ranks");
                session.ClearSandboxTargets();
                ui.HandleGameplayInput(input with { SandboxRankPressed = true }, session);
            }
            if (ordered[index].Id != "t5_regenerator") continue;
            ui.HandleGameplayInput(input with { SandboxRankPressed = true }, session);
            ui.HandleGameplayInput(input with { SandboxEnemyPreviousPressed = true }, session);
            ui.HandleGameplayInput(input with { SandboxSpawnPressed = true }, session);
            Check.Equal(EnemyRank.Elite, session.Enemies.Single().Rank,
                "Leaving Regenerator with Boss selected selects the highest supported rank");
        }
    }

    private static void AuthoredRanks(GameContent content)
    {
        foreach (var map in content.Maps.Values)
        {
            var waves = content.WaveSets[map.WaveSet];
            Check.True(waves.Waves.SelectMany(wave => wave.Groups).All(group =>
                    Enum.TryParse<EnemyRank>(group.Rank, true, out var rank) && EnemyRoster.SupportsRank(group.EnemyId, rank)),
                $"{map.Id}: every campaign group uses a supported family/rank pair");
            for (var number = 31; number <= 35; number++)
                Check.True(EndlessWaveGenerator.Create(number, 30, waves.Waves[29]).Groups.All(group =>
                        Enum.TryParse<EnemyRank>(group.Rank, true, out var rank) && EnemyRoster.SupportsRank(group.EnemyId, rank)),
                    $"{map.Id}: Endless cycle {number} uses supported ranks");
        }

        var selectedMap = content.Maps["foundry_loop"];
        foreach (var definition in content.Enemies.Values.Where(enemy => enemy.Id != "t5_regenerator"))
        {
            var waves = JsonSerializer.Deserialize<WaveSetDefinition>(
                JsonSerializer.Serialize(content.WaveSets[selectedMap.WaveSet], ContentJson.Options), ContentJson.Options)!;
            waves.Waves[0].Groups[0].EnemyId = definition.Id;
            waves.Waves[0].Groups[0].Rank = "Boss";
            Check.Throws<InvalidDataException>(() => DataValidator.Validate(content.Towers.Values.ToList(),
                    content.Enemies.Values.ToList(), selectedMap, waves, content.Tactics),
                $"{definition.Id}: content validation rejects an unavailable Boss encounter");
        }
    }

    private static void LegacySnapshotRanks(GameContent content)
    {
        var host = new GameSession(content, "foundry_loop", "normal", "standard");
        host.ConfigureCoOp(1);
        foreach (var definition in content.Enemies.Values.Where(enemy => enemy.Id != "t5_regenerator"))
        {
            host.SpawnEnemy(definition.Id, 1, 1, "Boss");
            host.Enemies[^1].ApplyHealthDamage(host.Enemies[^1].MaxHealth * .6f);
        }
        var snapshot = JsonSerializer.Deserialize<CoOpStateSnapshot>(JsonSerializer.Serialize(host.CaptureCoOpState(12, 0, false)))!;
        var peer = GameSession.RestoreCoOpState(content, snapshot, 2);
        Check.True(peer.Enemies.All(enemy => enemy.IsBoss && enemy.BossPhaseActive),
            "Existing snapshots preserve every Boss rank and its active phase");
        Check.Equal(SessionChecksum.Compute(host, 12), SessionChecksum.Compute(peer, 12),
            "Existing snapshots preserve exact rank-adjusted combat state");
    }
}
