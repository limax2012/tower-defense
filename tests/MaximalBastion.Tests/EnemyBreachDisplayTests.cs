using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Mobile;
using MaximalBastion.Multiplayer;
using MaximalBastion.UI;

namespace MaximalBastion.Tests;

internal static class EnemyBreachDisplayTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var session = new GameSession(content, "foundry_loop", "bastion", "standard");
        foreach (var (id, expected) in new[]
        {
            ("t1_crawler", new[] { 1, 2, 10 }),
            ("t4_aegis", new[] { 3, 4, 10 }),
            ("t5_regenerator", new[] { 5, 6, 10 })
        })
        foreach (var rank in Enum.GetValues<EnemyRank>())
        {
            session.SpawnEnemy(id, 1, 1, rank.ToString());
            var enemy = session.Enemies[^1];
            var checksum = SessionChecksum.Compute(session, 0);
            var display = EnemyInfo.Breach(enemy);
            Check.Equal(expected[(int)rank], display.LivesLost, $"{id} {rank} displays the full breach cost even with one life left");
            Check.Equal(enemy.LivesLost, display.LivesLost, "Live breach Intel uses the enemy instance's authoritative life cost");
            Check.Equal(display, EnemyInfo.Breach(enemy.Definition, rank.ToString().ToLowerInvariant()),
                "Rank-aware preview and live breach details agree");
            Check.Equal(checksum, SessionChecksum.Compute(session, 0), "Breach inspection leaves gameplay unchanged");
        }
        Check.Equal("BREACH: LOSE 1 LIFE", EnemyInfo.Breach(session.Enemies[0]).Summary, "One-life breach uses singular wording");
        Check.Equal("BREACH: LOSE 10 LIVES", EnemyInfo.Breach(session.Enemies[^1]).Summary, "Boss breach states the life loss explicitly");
        var costly = new EnemyDefinition { Id = "costly", MaxHealth = 100, Speed = 1, LivesLost = 17 };
        foreach (var rank in Enum.GetValues<EnemyRank>())
        {
            var enemy = new EnemyInstance(100 + (int)rank, costly, session.Map.Path, 1, 1, rank.ToString());
            var expected = rank == EnemyRank.Elite ? 18 : 17;
            Check.Equal(expected, EnemyInfo.Breach(enemy).LivesLost, "Authored life costs above the boss minimum are preserved");
            Check.Equal(EnemyInfo.Breach(enemy), EnemyInfo.Breach(costly, rank.ToString()),
                "Preview preserves authored life costs above the boss minimum");
        }
        MobilePreview(content);
    }

    private static void MobilePreview(GameContent content)
    {
        var map = content.Maps["foundry_loop"];
        content.WaveSets[map.WaveSet].Waves[0].Groups =
        [
            new() { EnemyId = "t1_crawler", Rank = "Standard", Count = 3 },
            new() { EnemyId = "t4_aegis", Rank = "Elite", Count = 2 },
            new() { EnemyId = "t5_regenerator", Rank = "Boss", Count = 1 }
        ];
        var directory = Path.Combine(Path.GetTempPath(), "MaximalBastion.BreachDisplayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var controller = new MobileSessionController(content, directory);
            using var reply = JsonDocument.Parse(controller.HandleRequest(JsonSerializer.Serialize(new
            {
                protocol = 1, id = 1, action = "newRun",
                data = new { mapId = map.Id, difficultyId = "bastion", challengeId = "standard" }
            })));
            Check.True(reply.RootElement.GetProperty("ok").GetBoolean(), "Mobile preview creates an ordinary campaign");
            var checksum = SessionChecksum.Compute(controller.Session!, 0);
            using var state = JsonDocument.Parse(controller.GetStateJson());
            var groups = state.RootElement.GetProperty("state").GetProperty("wave").GetProperty("groups");
            var expected = new[] { 1, 4, 10 };
            for (var index = 0; index < groups.GetArrayLength(); index++)
            {
                var group = groups[index];
                var breach = group.GetProperty("breach");
                Check.Equal(expected[index], breach.GetProperty("livesLost").GetInt32(), "Mobile group preview carries per-enemy rank-adjusted breach cost");
                Check.Equal(EnemyInfo.Breach(content.Enemies[group.GetProperty("enemyId").GetString()!],
                        group.GetProperty("rank").GetString()!).Summary, breach.GetProperty("summary").GetString()!,
                    "Mobile preview receives shared breach wording without frontend rank arithmetic");
            }
            Check.Equal(checksum, SessionChecksum.Compute(controller.Session!, 0), "Mobile breach preview does not alter synchronized state");
        }
        finally { Directory.Delete(directory, true); }
    }
}
