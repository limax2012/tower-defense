using MinimalBastion.Core;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Multiplayer;

public enum GameCommandType
{
    PlaceTower,
    UpgradeTower,
    ChooseDoctrine,
    SpecializeTower,
    OverdriveTower,
    ToggleAutoProtocol,
    SellTower,
    SetTargetMode,
    DeployEmergencyDefense,
    PlaceGenerator,
    UpgradeGenerator,
    SellGenerator,
    StartWave,
    ContinueEndless,
    SetSpeed,
    SetPaused
}

public sealed record GameCommand
{
    public long Sequence { get; init; }
    public long ClientRequestId { get; init; }
    public int PlayerId { get; init; }
    public GameCommandType Type { get; init; }
    public string TowerDefinitionId { get; init; } = "";
    public string SpecializationId { get; init; } = "";
    public string DoctrineId { get; init; } = "";
    public int EntityId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public TargetMode TargetMode { get; init; }
    public float Speed { get; init; } = 1f;
    public bool Paused { get; init; }
    public bool? EarlyStartEligible { get; init; }
    public Vector2 Position => new(X, Y);
}

public readonly record struct GameCommandResult(bool Accepted, string Reason)
{
    public static GameCommandResult Success => new(true, "Accepted");
    public static GameCommandResult Reject(string reason) => new(false, reason);
}

public static class GameCommandValidator
{
    public static bool IsStructurallyValid(GameCommand? command)
    {
        if (command is null || command.PlayerId is < 1 or > 2 || !Enum.IsDefined(command.Type) ||
            command.EntityId < 0 || command.TowerDefinitionId is null || command.TowerDefinitionId.Length > 128 ||
            command.SpecializationId is null || command.SpecializationId.Length > 128 ||
            command.DoctrineId is null || command.DoctrineId.Length > 128 ||
            !float.IsFinite(command.X) || !float.IsFinite(command.Y) || !Enum.IsDefined(command.TargetMode) ||
            !float.IsFinite(command.Speed) || command.Speed <= 0)
            return false;

        return command.Type switch
        {
            GameCommandType.PlaceTower => !string.IsNullOrWhiteSpace(command.TowerDefinitionId),
            GameCommandType.UpgradeTower or GameCommandType.OverdriveTower or GameCommandType.ToggleAutoProtocol or
                GameCommandType.SellTower or GameCommandType.SetTargetMode => command.EntityId > 0,
            GameCommandType.ChooseDoctrine => command.EntityId > 0 && !string.IsNullOrWhiteSpace(command.DoctrineId),
            GameCommandType.SpecializeTower => command.EntityId > 0 && !string.IsNullOrWhiteSpace(command.SpecializationId),
            GameCommandType.SetSpeed => command.Speed is 1f or 2f,
            GameCommandType.DeployEmergencyDefense or GameCommandType.PlaceGenerator or
                GameCommandType.UpgradeGenerator or GameCommandType.SellGenerator or GameCommandType.StartWave or
                GameCommandType.ContinueEndless or GameCommandType.SetPaused => true,
            _ => false
        };
    }
}

public static class GameCommandProcessor
{
    public static GameCommandResult Apply(GameSession session, GameCommand command)
    {
        if (!GameCommandValidator.IsStructurallyValid(command)) return GameCommandResult.Reject("Malformed command");
        // The pause command itself must remain available, and a StartWave that
        // both players committed before the pause may finish entering the
        // deterministic queue. Every mutable battlefield command is locked.
        if (session.IsCoOpPaused && command.Type is not (GameCommandType.SetPaused or GameCommandType.StartWave))
            return GameCommandResult.Reject("Shared pause locks battlefield commands");
        var accepted = command.Type switch
        {
            GameCommandType.PlaceTower => session.TryPlaceTower(command.TowerDefinitionId, command.Position, command.PlayerId, false),
            GameCommandType.UpgradeTower => session.TryUpgradeTower(command.EntityId, command.PlayerId),
            GameCommandType.ChooseDoctrine => session.TryChooseTowerDoctrine(command.EntityId, command.DoctrineId, command.PlayerId),
            GameCommandType.SpecializeTower => session.TrySpecializeTower(command.EntityId, command.SpecializationId, command.PlayerId),
            GameCommandType.OverdriveTower => session.TryOverdriveTower(command.EntityId, command.PlayerId),
            GameCommandType.ToggleAutoProtocol => session.TryToggleAutoProtocol(command.EntityId, command.PlayerId),
            GameCommandType.SellTower => session.TrySellTower(command.EntityId, command.PlayerId),
            GameCommandType.SetTargetMode => session.TrySetTargetMode(command.EntityId, command.TargetMode, command.PlayerId),
            GameCommandType.DeployEmergencyDefense => session.TryDeployEmergencyDefense(command.Position, command.PlayerId),
            GameCommandType.PlaceGenerator => session.TryPlaceGenerator(command.Position, command.PlayerId, false),
            GameCommandType.UpgradeGenerator => session.TryUpgradeGenerator(command.PlayerId),
            GameCommandType.SellGenerator => session.TrySellGenerator(command.PlayerId),
            GameCommandType.StartWave => session.StartNextWave(command.EarlyStartEligible),
            GameCommandType.ContinueEndless => session.BeginEndlessMode(),
            GameCommandType.SetSpeed => SetSpeed(session, command.Speed),
            GameCommandType.SetPaused => session.SetCoOpPaused(command.Paused, command.PlayerId),
            _ => false
        };
        return accepted ? GameCommandResult.Success : GameCommandResult.Reject("Command rejected by game rules");
    }

    private static bool SetSpeed(GameSession session, float speed)
    {
        session.SetSpeed(speed);
        return true;
    }
}

public readonly record struct CommandReceipt(GameCommand Command, bool Accepted, string Reason, bool Duplicate);

public sealed class AuthoritativeCommandHost
{
    public const int ReceiptHistoryLimit = 2048;
    public const int AcceptedCommandHistoryLimit = 2048;
    private readonly Dictionary<(int PlayerId, long RequestId), CommandReceipt> _receipts = new();
    private readonly Queue<(int PlayerId, long RequestId)> _receiptOrder = new();
    private readonly long[] _expiredRequestFloor = new long[3];
    private readonly List<GameCommand> _acceptedCommands = new();
    private long _nextSequence = 1;

    public long LastSequence => _nextSequence - 1;
    public int ReceiptHistoryCount => _receipts.Count;
    public IReadOnlyList<GameCommand> AcceptedCommands => _acceptedCommands;

    public void BeginRequestSession(int playerId)
    {
        if (playerId is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerId));
        foreach (var key in _receipts.Keys.Where(key => key.PlayerId == playerId).ToArray())
            _receipts.Remove(key);
        if (_receiptOrder.Count > 0)
        {
            var retained = _receiptOrder.Where(key => key.PlayerId != playerId).ToArray();
            _receiptOrder.Clear();
            foreach (var key in retained) _receiptOrder.Enqueue(key);
        }
        _expiredRequestFloor[playerId] = 0;
    }

    public CommandReceipt Submit(GameSession session, GameCommand request)
    {
        if (request.ClientRequestId <= 0)
            return new CommandReceipt(request, false, "Missing client request id", false);
        var key = (request.PlayerId, request.ClientRequestId);
        if (_receipts.TryGetValue(key, out var previous))
            return previous with { Duplicate = true };
        if (request.PlayerId is >= 1 and <= 2 && request.ClientRequestId <= _expiredRequestFloor[request.PlayerId])
            return new CommandReceipt(request, false, "Expired duplicate request", true);

        var result = GameCommandProcessor.Apply(session, request);
        var authoritative = result.Accepted ? request with { Sequence = _nextSequence++ } : request;
        var receipt = new CommandReceipt(authoritative, result.Accepted, result.Reason, false);
        RememberReceipt(key, receipt);
        if (result.Accepted) RememberAccepted(authoritative);
        return receipt;
    }

    public CommandReceipt Sequence(GameCommand request)
    {
        if (!GameCommandValidator.IsStructurallyValid(request))
            return new CommandReceipt(request, false, "Malformed command", false);
        if (request.ClientRequestId <= 0)
            return new CommandReceipt(request, false, "Missing client request id", false);
        var key = (request.PlayerId, request.ClientRequestId);
        if (_receipts.TryGetValue(key, out var previous))
            return previous with { Duplicate = true };
        if (request.ClientRequestId <= _expiredRequestFloor[request.PlayerId])
            return new CommandReceipt(request, false, "Expired duplicate request", true);

        var authoritative = request with { Sequence = _nextSequence++ };
        var receipt = new CommandReceipt(authoritative, true, "Queued", false);
        RememberReceipt(key, receipt);
        RememberAccepted(authoritative);
        return receipt;
    }

    private void RememberReceipt((int PlayerId, long RequestId) key, CommandReceipt receipt)
    {
        _receipts[key] = receipt;
        _receiptOrder.Enqueue(key);
        while (_receiptOrder.Count > ReceiptHistoryLimit)
        {
            var expired = _receiptOrder.Dequeue();
            _receipts.Remove(expired);
            if (expired.PlayerId is >= 1 and <= 2)
                _expiredRequestFloor[expired.PlayerId] = Math.Max(_expiredRequestFloor[expired.PlayerId], expired.RequestId);
        }
    }

    private void RememberAccepted(GameCommand command)
    {
        _acceptedCommands.Add(command);
        if (_acceptedCommands.Count > AcceptedCommandHistoryLimit)
            _acceptedCommands.RemoveRange(0, _acceptedCommands.Count - AcceptedCommandHistoryLimit);
    }
}
