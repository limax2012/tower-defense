namespace MinimalBastion.Multiplayer;

public sealed class CoOpHeartbeatMonitor
{
    public const float TimeoutSeconds = 15f;
    private float _silenceSeconds;

    public float SilenceSeconds => _silenceSeconds;

    public bool Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return false;
        // A debugger break, display reset, or resumed laptop should not turn a
        // single unusually large GameTime sample into an immediate disconnect.
        _silenceSeconds = MathF.Min(TimeoutSeconds, _silenceSeconds + MathF.Min(elapsedSeconds, 1f));
        return _silenceSeconds >= TimeoutSeconds;
    }

    public void MarkInboundActivity() => _silenceSeconds = 0;
    public void Reset() => _silenceSeconds = 0;
}
