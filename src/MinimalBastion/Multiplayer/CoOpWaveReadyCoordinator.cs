namespace MinimalBastion.Multiplayer;

public sealed class CoOpWaveReadyCoordinator
{
    private const int BothPlayersMask = 0b11;

    public int ReadyMask { get; private set; }
    public bool StartQueued { get; private set; }

    public bool RegisterReady(int playerId, bool canStartWave)
    {
        if (playerId is < 1 or > 2 || !canStartWave || StartQueued) return false;
        var bit = 1 << (playerId - 1);
        if ((ReadyMask & bit) != 0) return false;
        ReadyMask |= bit;
        StartQueued = ReadyMask == BothPlayersMask;
        return true;
    }

    public bool IsReady(int playerId) => playerId is 1 or 2 && (ReadyMask & (1 << (playerId - 1))) != 0;

    public void ApplyState(int readyMask, bool startQueued)
    {
        ReadyMask = readyMask & BothPlayersMask;
        StartQueued = startQueued && ReadyMask == BothPlayersMask;
    }

    public void Reset()
    {
        ReadyMask = 0;
        StartQueued = false;
    }
}
