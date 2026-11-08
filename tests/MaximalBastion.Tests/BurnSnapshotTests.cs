using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Tests;

internal static class BurnSnapshotTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        RetainedBurnRoundTrip(content);
        StatusIdentityValidation(content);
        StatusCapacityValidation(content);
    }

    private static void RetainedBurnRoundTrip(GameContent content)
    {
        var host = NewSession(content);
        var enemy = host.Enemies.Single();
        enemy.StatusEffects.Apply(new StatusApplication
        {
            Type = StatusType.Burn, SourceId = 0, Magnitude = 2, Duration = 5
        });
        enemy.StatusEffects.ConsumeBurnTicks(0.2f);
        enemy.StatusEffects.Update(0.2f);
        for (var source = 1; source < 12; source++)
            enemy.StatusEffects.Apply(new StatusApplication
            {
                Type = StatusType.Burn, SourceId = source, Magnitude = 10 + source,
                Duration = 0.6f, ArmorPierce = source % 3
            });
        foreach (var type in Enum.GetValues<StatusType>().Where(type => type != StatusType.Burn))
            enemy.StatusEffects.Apply(new StatusApplication
            {
                Type = type, SourceId = 0, Magnitude = 0.1f, Duration = 0.3f
            });

        var snapshot = host.CaptureCoOpState(17, 0, false);
        var envelope = new CoOpEnvelope { Type = CoOpMessageType.StateSnapshot, State = snapshot };
        var frame = CoOpFrameCodec.EncodeFrame(JsonSerializer.SerializeToUtf8Bytes(envelope));
        var transferred = JsonSerializer.Deserialize<CoOpEnvelope>(CoOpFrameCodec.DecodeFrame(frame))!.State!;
        var peer = GameSession.RestoreCoOpState(content, transferred, 2);
        var retained = peer.Enemies.Single().StatusEffects.Active;
        Check.Equal(16, retained.Count, "Reconnect retains twelve burn sources and all four other status types");
        Check.True(enemy.StatusEffects.Active.Where(status => status.Type == StatusType.Burn).Select(status => status.SourceId)
                .SequenceEqual(retained.Where(status => status.Type == StatusType.Burn).Select(status => status.SourceId)),
            "The network frame and reconstruction preserve burn application order");
        Check.Nearly(0.2f, retained.Single(status => status.Type == StatusType.Burn && status.SourceId == 0).TickProgress,
            "Inactive burn keeps its earned partial tick through the network frame");
        Check.True(peer.Enemies.Single().GetActiveBurns().All(status => status.SourceId != 0),
            "The restored partial burn remains inactive behind stronger sources");
        Check.Equal(SessionChecksum.Compute(host, 17), SessionChecksum.Compute(peer, 17),
            "Every retained source and its phase contributes to the restored checksum");
        var inactive = retained.Single(status => status.Type == StatusType.Burn && status.SourceId == 0);
        var inactiveProgress = inactive.TickProgress;
        var restoredChecksum = SessionChecksum.Compute(peer, 17);
        inactive.TickProgress += 0.01f;
        Check.True(restoredChecksum != SessionChecksum.Compute(peer, 17),
            "Inactive burn partial tick is checksum-covered");
        inactive.TickProgress = inactiveProgress;

        var reorderedSnapshot = host.CaptureCoOpState(17, 0, false);
        reorderedSnapshot.Enemies.Single().Statuses.Reverse();
        var reorderedPeer = GameSession.RestoreCoOpState(content, reorderedSnapshot, 2);
        Check.True(SessionChecksum.Compute(host, 17) != SessionChecksum.Compute(reorderedPeer, 17),
            "Burn resolution order contributes to checksums even when all status values match");

        var hostRunner = new DeterministicSessionRunner(host, 17);
        var peerRunner = new DeterministicSessionRunner(peer, 17);
        for (var tick = 0; tick < 90; tick++)
        {
            hostRunner.RunTicks(1);
            peerRunner.RunTicks(1);
            Check.Equal(SessionChecksum.Compute(host, hostRunner.Tick), SessionChecksum.Compute(peer, peerRunner.Tick),
                "Retained burn fallback remains deterministic after reconnect");
        }
        Check.Equal(1, peer.Enemies.Single().StatusEffects.Active.Count,
            "Expired suppressed burns disappear while the long source remains");
        Check.Equal(0, peer.Enemies.Single().GetActiveBurns().Single().SourceId,
            "A source without a live tower resumes when stronger sources expire");
    }

    private static void StatusIdentityValidation(GameContent content)
    {
        var snapshot = NewSession(content).CaptureCoOpState(0, 0, false);
        var statuses = snapshot.Enemies.Single().Statuses;
        statuses.Add(Burn(0));
        statuses.Add(Burn(700));
        CoOpSnapshotValidator.Validate(snapshot);
        statuses.Add(Burn(700));
        Check.Throws<InvalidDataException>(() => CoOpSnapshotValidator.Validate(snapshot),
            "Duplicate burn sources cannot manufacture additional stacks through reconnect");
        statuses.RemoveAt(statuses.Count - 1);
        foreach (var type in Enum.GetValues<StatusType>().Where(type => type != StatusType.Burn))
        {
            statuses.Add(new ActiveStatus { Type = type, SourceId = 10, Magnitude = 1, RemainingSeconds = 1 });
            CoOpSnapshotValidator.Validate(snapshot);
            statuses.Add(new ActiveStatus { Type = type, SourceId = 11, Magnitude = 1, RemainingSeconds = 1 });
            Check.Throws<InvalidDataException>(() => CoOpSnapshotValidator.Validate(snapshot),
                $"Duplicate {type} entries are invalid regardless of source");
            statuses.RemoveAt(statuses.Count - 1);
        }
    }

    private static void StatusCapacityValidation(GameContent content)
    {
        var snapshot = NewSession(content).CaptureCoOpState(0, 0, false);
        var statuses = snapshot.Enemies.Single().Statuses;
        for (var source = 0; source < 5_120; source++) statuses.Add(Burn(source));
        foreach (var type in Enum.GetValues<StatusType>().Where(type => type != StatusType.Burn))
            statuses.Add(new ActiveStatus { Type = type, Magnitude = 1, RemainingSeconds = 1 });
        CoOpSnapshotValidator.Validate(snapshot);
        statuses.Add(Burn(5_120));
        Check.Throws<InvalidDataException>(() => CoOpSnapshotValidator.Validate(snapshot),
            "An individual enemy cannot exceed the retained-status limit");

        snapshot.Enemies.Clear();
        for (var index = 0; index < 7; index++)
        {
            var enemy = new EnemyRuntimeState
            {
                Id = index + 1, DefinitionId = "t1_crawler", Health = 1
            };
            var count = index < 6 ? 5_120 : 2_048;
            for (var source = 0; source < count; source++) enemy.Statuses.Add(Burn(source));
            snapshot.Enemies.Add(enemy);
        }
        snapshot.NextEnemyId = 8;
        CoOpSnapshotValidator.Validate(snapshot);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CoOpEnvelope
        {
            Type = CoOpMessageType.StateSnapshot, State = snapshot
        });
        Check.True(payload.Length < CoOpFrameCodec.MaximumDecodedBytes,
            "The aggregate status budget fits the existing decoded frame limit in a dense snapshot");
        var frame = CoOpFrameCodec.EncodeFrame(payload);
        Check.Equal(payload.Length, CoOpFrameCodec.DecodeFrame(frame).Length,
            "A dense retained-burn snapshot survives compressed framing");
        snapshot.Enemies[^1].Statuses.Add(Burn(2_048));
        Check.Throws<InvalidDataException>(() => CoOpSnapshotValidator.Validate(snapshot),
            "Aggregate retained statuses remain bounded across enemies");
    }

    private static GameSession NewSession(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        session.ConfigureCoOp(1);
        session.SpawnEnemy("t1_crawler", 100, 0.01f);
        return session;
    }

    private static ActiveStatus Burn(int source) => new()
    {
        Type = StatusType.Burn, SourceId = source, Magnitude = 1, RemainingSeconds = 5
    };
}
