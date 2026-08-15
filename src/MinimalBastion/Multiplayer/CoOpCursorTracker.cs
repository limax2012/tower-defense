using MinimalBastion.Core;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Multiplayer;

public sealed class CoOpCursorTracker
{
    public const float SendIntervalSeconds = 0.125f;
    public const float IdleHeartbeatSeconds = 0.5f;
    public const float RemoteTimeoutSeconds = 0.75f;
    private float _sendRemaining;
    private float _heartbeatRemaining;
    private float _remoteRemaining;
    private Vector2 _lastSentPosition;
    private bool _hasSentPosition;
    private int _lastSentEntityId;

    public Vector2? RemotePosition { get; private set; }
    public int RemotePlayerId { get; private set; }
    public int RemoteEntityId { get; private set; }

    public void Advance(float elapsedSeconds)
    {
        elapsedSeconds = float.IsFinite(elapsedSeconds) ? MathF.Max(0, elapsedSeconds) : 0;
        _sendRemaining = MathF.Max(0, _sendRemaining - elapsedSeconds);
        _heartbeatRemaining = MathF.Max(0, _heartbeatRemaining - elapsedSeconds);
        if (RemotePosition is null) return;
        _remoteRemaining = MathF.Max(0, _remoteRemaining - elapsedSeconds);
        if (_remoteRemaining <= 0) ClearRemote();
    }

    public bool TryCaptureLocal(Vector2 position, bool isMouseOverLogicalCanvas, int selectedEntityId, out Vector2 update)
    {
        update = default;
        if (selectedEntityId < 0 || _sendRemaining > 0 || !isMouseOverLogicalCanvas || !IsBattlefieldPosition(position)) return false;
        var changed = !_hasSentPosition || selectedEntityId != _lastSentEntityId ||
            Vector2.DistanceSquared(position, _lastSentPosition) >= 4f;
        if (!changed && _heartbeatRemaining > 0) return false;
        _sendRemaining = SendIntervalSeconds;
        _heartbeatRemaining = IdleHeartbeatSeconds;
        _lastSentPosition = position;
        _lastSentEntityId = selectedEntityId;
        _hasSentPosition = true;
        update = position;
        return true;
    }

    public bool Receive(Vector2 position, int playerId, int selectedEntityId = 0)
    {
        if (playerId is < 1 or > 2 || selectedEntityId < 0 || !IsBattlefieldPosition(position)) return false;
        RemotePosition = position;
        RemotePlayerId = playerId;
        RemoteEntityId = selectedEntityId;
        _remoteRemaining = RemoteTimeoutSeconds;
        return true;
    }

    public void Reset()
    {
        _sendRemaining = 0;
        _heartbeatRemaining = 0;
        _lastSentPosition = default;
        _lastSentEntityId = 0;
        _hasSentPosition = false;
        ClearRemote();
    }

    private void ClearRemote()
    {
        RemotePosition = null;
        RemotePlayerId = 0;
        RemoteEntityId = 0;
        _remoteRemaining = 0;
    }

    private static bool IsBattlefieldPosition(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        position.X >= 0 && position.X < GameConstants.MapWidth &&
        position.Y >= GameConstants.TopBarHeight && position.Y < GameConstants.LogicalHeight;
}
