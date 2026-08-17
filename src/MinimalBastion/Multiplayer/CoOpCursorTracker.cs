using MinimalBastion.Core;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Multiplayer;

public readonly record struct CoOpCursorUpdate(Vector2 Position, bool HasPlacementPreview,
    Vector2 PlacementPreviewPosition, TacticalPlacementKind TacticalPlacement);

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
    private string _lastSentPlacementTowerId = "";
    private TacticalPlacementKind _lastSentTacticalPlacement;
    private bool _lastSentHasPlacementPreview;
    private Vector2 _lastSentPlacementPreviewPosition;

    public Vector2? RemotePosition { get; private set; }
    public int RemotePlayerId { get; private set; }
    public int RemoteEntityId { get; private set; }
    public string RemotePlacementTowerId { get; private set; } = "";
    public TacticalPlacementKind RemoteTacticalPlacement { get; private set; }
    public bool RemoteHasPlacementPreview { get; private set; }
    public Vector2 RemotePlacementPreviewPosition { get; private set; }

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
        => TryCaptureLocal(position, isMouseOverLogicalCanvas, selectedEntityId, "", out update);

    public bool TryCaptureLocal(Vector2 position, bool isMouseOverLogicalCanvas, int selectedEntityId,
        string placementTowerId, out Vector2 update)
    {
        var captured = TryCaptureLocal(position, isMouseOverLogicalCanvas, selectedEntityId,
            placementTowerId, TacticalPlacementKind.None, false, default, out var presence);
        update = presence.Position;
        return captured;
    }

    public bool TryCaptureLocal(Vector2 position, bool isMouseOverLogicalCanvas, int selectedEntityId,
        string placementTowerId, bool hasPlacementPreview, Vector2 placementPreviewPosition,
        out CoOpCursorUpdate update) =>
        TryCaptureLocal(position, isMouseOverLogicalCanvas, selectedEntityId, placementTowerId,
            TacticalPlacementKind.None, hasPlacementPreview, placementPreviewPosition, out update);

    public bool TryCaptureLocal(Vector2 position, bool isMouseOverLogicalCanvas, int selectedEntityId,
        string placementTowerId, TacticalPlacementKind tacticalPlacement, bool hasPlacementPreview,
        Vector2 placementPreviewPosition,
        out CoOpCursorUpdate update)
    {
        update = default;
        if (selectedEntityId < 0 || placementTowerId is null || placementTowerId.Length > 128 ||
            !isMouseOverLogicalCanvas || !IsBattlefieldPosition(position) ||
            (selectedEntityId > 0 && (!string.IsNullOrWhiteSpace(placementTowerId) ||
                                      tacticalPlacement != TacticalPlacementKind.None)) ||
            !IsValidPlacementContext(placementTowerId, tacticalPlacement, hasPlacementPreview,
                placementPreviewPosition)) return false;
        var contextChanged = !_hasSentPosition || selectedEntityId != _lastSentEntityId ||
            !string.Equals(placementTowerId, _lastSentPlacementTowerId, StringComparison.OrdinalIgnoreCase) ||
            tacticalPlacement != _lastSentTacticalPlacement ||
            hasPlacementPreview != _lastSentHasPlacementPreview;
        if (_sendRemaining > 0 && !contextChanged) return false;
        var changed = contextChanged ||
            Vector2.DistanceSquared(position, _lastSentPosition) >= 4f ||
            (hasPlacementPreview && Vector2.DistanceSquared(placementPreviewPosition,
                _lastSentPlacementPreviewPosition) >= 4f);
        if (!changed && _heartbeatRemaining > 0) return false;
        _sendRemaining = SendIntervalSeconds;
        _heartbeatRemaining = IdleHeartbeatSeconds;
        _lastSentPosition = position;
        _lastSentEntityId = selectedEntityId;
        _lastSentPlacementTowerId = placementTowerId;
        _lastSentTacticalPlacement = tacticalPlacement;
        _lastSentHasPlacementPreview = hasPlacementPreview;
        _lastSentPlacementPreviewPosition = placementPreviewPosition;
        _hasSentPosition = true;
        update = new CoOpCursorUpdate(position, hasPlacementPreview, placementPreviewPosition, tacticalPlacement);
        return true;
    }

    public bool Receive(Vector2 position, int playerId, int selectedEntityId = 0, string placementTowerId = "")
        => Receive(position, playerId, selectedEntityId, placementTowerId, TacticalPlacementKind.None, false, default);

    public bool Receive(Vector2 position, int playerId, int selectedEntityId, string placementTowerId,
        bool hasPlacementPreview, Vector2 placementPreviewPosition) =>
        Receive(position, playerId, selectedEntityId, placementTowerId, TacticalPlacementKind.None,
            hasPlacementPreview, placementPreviewPosition);

    public bool Receive(Vector2 position, int playerId, int selectedEntityId, string placementTowerId,
        TacticalPlacementKind tacticalPlacement, bool hasPlacementPreview, Vector2 placementPreviewPosition)
    {
        if (playerId is < 1 or > 2 || selectedEntityId < 0 || placementTowerId is null ||
            placementTowerId.Length > 128 || !IsBattlefieldPosition(position) ||
            (selectedEntityId > 0 && (!string.IsNullOrWhiteSpace(placementTowerId) ||
                                      tacticalPlacement != TacticalPlacementKind.None)) ||
            !IsValidPlacementContext(placementTowerId, tacticalPlacement, hasPlacementPreview,
                placementPreviewPosition)) return false;
        RemotePosition = position;
        RemotePlayerId = playerId;
        RemoteEntityId = selectedEntityId;
        RemotePlacementTowerId = placementTowerId;
        RemoteTacticalPlacement = tacticalPlacement;
        RemoteHasPlacementPreview = hasPlacementPreview;
        RemotePlacementPreviewPosition = placementPreviewPosition;
        _remoteRemaining = RemoteTimeoutSeconds;
        return true;
    }

    public void Reset()
    {
        _sendRemaining = 0;
        _heartbeatRemaining = 0;
        _lastSentPosition = default;
        _lastSentEntityId = 0;
        _lastSentPlacementTowerId = "";
        _lastSentTacticalPlacement = TacticalPlacementKind.None;
        _lastSentHasPlacementPreview = false;
        _lastSentPlacementPreviewPosition = default;
        _hasSentPosition = false;
        ClearRemote();
    }

    private void ClearRemote()
    {
        RemotePosition = null;
        RemotePlayerId = 0;
        RemoteEntityId = 0;
        RemotePlacementTowerId = "";
        RemoteTacticalPlacement = TacticalPlacementKind.None;
        RemoteHasPlacementPreview = false;
        RemotePlacementPreviewPosition = default;
        _remoteRemaining = 0;
    }

    private static bool IsBattlefieldPosition(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        position.X >= 0 && position.X < GameConstants.MapWidth &&
        position.Y >= GameConstants.TopBarHeight && position.Y < GameConstants.LogicalHeight;

    private static bool IsValidPlacementContext(string placementTowerId,
        TacticalPlacementKind tacticalPlacement, bool hasPlacementPreview, Vector2 placementPreviewPosition)
    {
        if (!Enum.IsDefined(tacticalPlacement)) return false;
        var hasTowerPlacement = !string.IsNullOrWhiteSpace(placementTowerId);
        var hasTacticalPlacement = tacticalPlacement != TacticalPlacementKind.None;
        if (hasTowerPlacement && hasTacticalPlacement) return false;
        return !hasPlacementPreview ||
               ((hasTowerPlacement || hasTacticalPlacement) && IsBattlefieldPosition(placementPreviewPosition));
    }
}
