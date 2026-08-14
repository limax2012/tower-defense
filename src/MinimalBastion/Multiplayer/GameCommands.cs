using MinimalBastion.Core;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Multiplayer;

public enum GameCommandType
{
    PlaceTower,
    UpgradeTower,
    SpecializeTower,
    OverdriveTower,
    SellTower,
    SetTargetMode,
    DeployEmergencyDefense,
    PlaceGenerator,
    UpgradeGenerator,
    SellGenerator,
    StartWave,
    SetSpeed
}

public sealed record GameCommand
{
    public long Sequence { get; init; }
    public long ClientRequestId { get; init; }
    public int PlayerId { get; init; }
    public GameCommandType Type { get; init; }
    public string TowerDefinitionId { get; init; } = "";
    public string SpecializationId { get; init; } = "";
    public int EntityId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public TargetMode TargetMode { get; init; }
    public float Speed { get; init; } = 1f;
    public Vector2 Position => new(X, Y);
}

public readonly record struct GameCommandResult(bool Accepted, string Reason)
{
    public static GameCommandResult Success => new(true, "Accepted");
    public static GameCommandResult Reject(string reason) => new(false, reason);
}

public static class GameCommandProcessor
{
    public static GameCommandResult Apply(GameSession session, GameCommand command)
    {
        if (command.PlayerId is < 1 or > 2) return GameCommandResult.Reject("Unknown player");
        var accepted = command.Type switch
        {
            GameCommandType.PlaceTower => session.TryPlaceTower(command.TowerDefinitionId, command.Position, command.PlayerId, false),
            GameCommandType.UpgradeTower => session.TryUpgradeTower(command.EntityId, command.PlayerId),
            GameCommandType.SpecializeTower => session.TrySpecializeTower(command.EntityId, command.SpecializationId, command.PlayerId),
            GameCommandType.OverdriveTower => session.TryOverdriveTower(command.EntityId, command.PlayerId),
            GameCommandType.SellTower => session.TrySellTower(command.EntityId, command.PlayerId),
            GameCommandType.SetTargetMode => session.TrySetTargetMode(command.EntityId, command.TargetMode, command.PlayerId),
            GameCommandType.DeployEmergencyDefense => session.TryDeployEmergencyDefense(command.Position, command.PlayerId),
            GameCommandType.PlaceGenerator => session.TryPlaceGenerator(command.Position, command.PlayerId, false),
            GameCommandType.UpgradeGenerator => session.TryUpgradeGenerator(command.PlayerId),
            GameCommandType.SellGenerator => session.TrySellGenerator(command.PlayerId),
            GameCommandType.StartWave => session.StartNextWave(),
            GameCommandType.SetSpeed => SetSpeed(session, command.Speed),
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
    private readonly Dictionary<(int PlayerId, long RequestId), CommandReceipt> _receipts = new();
    private readonly List<GameCommand> _acceptedCommands = new();
    private long _nextSequence = 1;

    public long LastSequence => _nextSequence - 1;
    public IReadOnlyList<GameCommand> AcceptedCommands => _acceptedCommands;

    public CommandReceipt Submit(GameSession session, GameCommand request)
    {
        if (request.ClientRequestId <= 0)
            return new CommandReceipt(request, false, "Missing client request id", false);
        var key = (request.PlayerId, request.ClientRequestId);
        if (_receipts.TryGetValue(key, out var previous))
            return previous with { Duplicate = true };

        var result = GameCommandProcessor.Apply(session, request);
        var authoritative = result.Accepted ? request with { Sequence = _nextSequence++ } : request;
        var receipt = new CommandReceipt(authoritative, result.Accepted, result.Reason, false);
        _receipts[key] = receipt;
        if (result.Accepted) _acceptedCommands.Add(authoritative);
        return receipt;
    }

    public CommandReceipt Sequence(GameCommand request)
    {
        if (request.PlayerId is < 1 or > 2)
            return new CommandReceipt(request, false, "Unknown player", false);
        if (request.ClientRequestId <= 0)
            return new CommandReceipt(request, false, "Missing client request id", false);
        var key = (request.PlayerId, request.ClientRequestId);
        if (_receipts.TryGetValue(key, out var previous))
            return previous with { Duplicate = true };

        var authoritative = request with { Sequence = _nextSequence++ };
        var receipt = new CommandReceipt(authoritative, true, "Queued", false);
        _receipts[key] = receipt;
        _acceptedCommands.Add(authoritative);
        return receipt;
    }
}
