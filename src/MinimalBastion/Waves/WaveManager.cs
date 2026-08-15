using MinimalBastion.Data;
using MinimalBastion.Core;
using MinimalBastion.Persistence;
using MinimalBastion.Multiplayer;

namespace MinimalBastion.Waves;

public sealed class WaveManager
{
    private readonly IReadOnlyList<WaveDefinition> _waves;
    private WaveDefinition? _activeDefinition;
    private int _groupIndex;
    private int _spawnedInGroup;
    private float _groupTimer;
    private float _delayRemaining;

    public int CurrentWaveNumber { get; private set; }
    public bool IsActive => _activeDefinition is not null;
    public bool CanStartNextWave => !IsActive && CurrentWaveNumber < int.MaxValue &&
        (CurrentWaveNumber < _waves.Count || EndlessModeEnabled);
    public float IntermissionRemaining { get; private set; }
    public bool IsFinalWaveCleared { get; private set; }
    public bool EndlessModeEnabled { get; private set; }
    public int TotalWaves => _waves.Count;
    public int QueuedEnemies { get; private set; }
    public WaveDefinition? ActiveWave => _activeDefinition;
    public WaveDefinition? NextWave => CurrentWaveNumber == int.MaxValue ? null : ResolveWave(CurrentWaveNumber + 1);

    public WaveManager(IReadOnlyList<WaveDefinition> waves)
    {
        _waves = waves;
        CurrentWaveNumber = 0;
    }

    public bool TryStartNextWave(MinimalBastion.GameSession session, bool? earlyStartEligible = null)
    {
        if (!CanStartNextWave) return false;
        // Co-op locks eligibility when the host receives the second ready signal,
        // so the fixed network input delay cannot erase a just-earned bonus.
        var calledEarly = CurrentWaveNumber > 0 && (earlyStartEligible ?? IntermissionRemaining > 0);
        _activeDefinition = ResolveWave(CurrentWaveNumber + 1);
        if (_activeDefinition is null) return false;
        CurrentWaveNumber = _activeDefinition.Number;
        _groupIndex = 0;
        _spawnedInGroup = 0;
        _groupTimer = 0;
        _delayRemaining = _activeDefinition.Groups[0].DelayBefore;
        QueuedEnemies = _activeDefinition.Groups.Sum(x => x.Count);
        if (calledEarly) session.Economy.AwardEarlyStart();
        IntermissionRemaining = 0;
        session.OnWaveStarted(_activeDefinition, calledEarly ? GameConstants.EarlyStartBonus : 0);
        return true;
    }

    public bool EnableEndlessMode()
    {
        if (EndlessModeEnabled || IsActive || !IsFinalWaveCleared || CurrentWaveNumber < _waves.Count) return false;
        EndlessModeEnabled = true;
        IntermissionRemaining = MathF.Max(IntermissionRemaining, GameConstants.IntermissionSeconds);
        return true;
    }

    public void Update(float deltaSeconds, MinimalBastion.GameSession session)
    {
        if (!IsActive || _activeDefinition is null) return;
        // The final group may have finished spawning on the previous frame. Keep the
        // wave active while live enemies finish, but never index beyond the group list.
        if (_groupIndex >= _activeDefinition.Groups.Count) return;
        if (_delayRemaining > 0)
        {
            _delayRemaining -= deltaSeconds;
            if (_delayRemaining > 0) return;
        }

        var group = _activeDefinition.Groups[_groupIndex];
        _groupTimer -= deltaSeconds;
        if (_spawnedInGroup < group.Count && _groupTimer <= 0)
        {
            session.SpawnEnemy(group.EnemyId, _activeDefinition.HealthMultiplier, _activeDefinition.SpeedMultiplier, group.Rank);
            _spawnedInGroup++;
            QueuedEnemies--;
            _groupTimer += group.SpawnInterval;
        }

        if (_spawnedInGroup >= group.Count)
        {
            _groupIndex++;
            if (_groupIndex >= _activeDefinition.Groups.Count) return;
            _spawnedInGroup = 0;
            _groupTimer = 0;
            _delayRemaining = _activeDefinition.Groups[_groupIndex].DelayBefore;
        }
    }

    public void TryComplete(bool noLiveEnemies, MinimalBastion.GameSession session)
    {
        if (!IsActive || _activeDefinition is null || _groupIndex < _activeDefinition.Groups.Count || !noLiveEnemies) return;
        var completedWave = CurrentWaveNumber;
        session.Economy.AwardWave(completedWave);
        session.OnWaveCompleted(completedWave);
        _activeDefinition = null;
        IntermissionRemaining = GameConstants.IntermissionSeconds;
        if (completedWave == _waves.Count) IsFinalWaveCleared = true;
    }

    public void UpdateIntermission(float deltaSeconds)
    {
        if (IsActive || IntermissionRemaining <= 0) return;
        IntermissionRemaining = MathF.Max(0, IntermissionRemaining - deltaSeconds);
    }

    public int EstimateRemainingIncludingLive(int liveCount) => QueuedEnemies + liveCount;

    public WaveSaveData CaptureSaveData()
    {
        if (IsActive) throw new InvalidOperationException("An active wave cannot be checkpointed.");
        return new WaveSaveData
        {
            CurrentWaveNumber = CurrentWaveNumber,
            IntermissionRemaining = IntermissionRemaining,
            IsFinalWaveCleared = IsFinalWaveCleared,
            EndlessModeEnabled = EndlessModeEnabled
        };
    }

    public void RestoreSaveData(WaveSaveData data)
    {
        if (data.CurrentWaveNumber < 0 || (!data.EndlessModeEnabled && data.CurrentWaveNumber > _waves.Count))
            throw new InvalidDataException("Saved wave number is outside the current wave set.");
        if (data.EndlessModeEnabled && (!data.IsFinalWaveCleared || data.CurrentWaveNumber < _waves.Count))
            throw new InvalidDataException("Saved endless mode does not follow a completed campaign.");
        _activeDefinition = null;
        _groupIndex = 0;
        _spawnedInGroup = 0;
        _groupTimer = 0;
        _delayRemaining = 0;
        QueuedEnemies = 0;
        CurrentWaveNumber = data.CurrentWaveNumber;
        IntermissionRemaining = MathF.Max(0, data.IntermissionRemaining);
        IsFinalWaveCleared = data.IsFinalWaveCleared;
        EndlessModeEnabled = data.EndlessModeEnabled;
    }

    public WaveRuntimeState CaptureCoOpState() => new()
    {
        CurrentWaveNumber = CurrentWaveNumber,
        ActiveWaveNumber = _activeDefinition?.Number ?? 0,
        GroupIndex = _groupIndex,
        SpawnedInGroup = _spawnedInGroup,
        GroupTimer = _groupTimer,
        DelayRemaining = _delayRemaining,
        IntermissionRemaining = IntermissionRemaining,
        IsFinalWaveCleared = IsFinalWaveCleared,
        EndlessModeEnabled = EndlessModeEnabled,
        QueuedEnemies = QueuedEnemies
    };

    public void RestoreCoOpState(WaveRuntimeState data)
    {
        if (data.CurrentWaveNumber < 0 || (!data.EndlessModeEnabled && data.CurrentWaveNumber > _waves.Count))
            throw new InvalidDataException("Network wave number is outside the current wave set.");
        if (data.EndlessModeEnabled && (!data.IsFinalWaveCleared || data.CurrentWaveNumber < _waves.Count))
            throw new InvalidDataException("Network endless mode does not follow a completed campaign.");

        _activeDefinition = data.ActiveWaveNumber <= 0
            ? null
            : ResolveWave(data.ActiveWaveNumber, data.EndlessModeEnabled)
                ?? throw new InvalidDataException($"Network wave {data.ActiveWaveNumber} is unavailable.");
        if (_activeDefinition is not null && (data.GroupIndex < 0 || data.GroupIndex > _activeDefinition.Groups.Count))
            throw new InvalidDataException("Network wave group index is invalid.");
        if (_activeDefinition is not null)
        {
            if (data.ActiveWaveNumber != data.CurrentWaveNumber)
                throw new InvalidDataException("Network active wave does not match current wave progress.");

            long expectedQueued = 0;
            if (data.GroupIndex < _activeDefinition.Groups.Count)
            {
                var currentGroup = _activeDefinition.Groups[data.GroupIndex];
                if (data.SpawnedInGroup > currentGroup.Count)
                    throw new InvalidDataException("Network wave group spawn progress is invalid.");
                expectedQueued = currentGroup.Count - data.SpawnedInGroup;
                for (var index = data.GroupIndex + 1; index < _activeDefinition.Groups.Count; index++)
                    expectedQueued += _activeDefinition.Groups[index].Count;
            }
            if (data.QueuedEnemies != expectedQueued)
                throw new InvalidDataException("Network queued-enemy count does not match wave progress.");
        }
        else if (data.QueuedEnemies != 0)
            throw new InvalidDataException("Network inactive wave cannot retain queued enemies.");

        CurrentWaveNumber = data.CurrentWaveNumber;
        _groupIndex = _activeDefinition is null ? 0 : data.GroupIndex;
        _spawnedInGroup = _activeDefinition is null ? 0 : Math.Max(0, data.SpawnedInGroup);
        _groupTimer = data.GroupTimer;
        _delayRemaining = data.DelayRemaining;
        IntermissionRemaining = MathF.Max(0, data.IntermissionRemaining);
        IsFinalWaveCleared = data.IsFinalWaveCleared;
        EndlessModeEnabled = data.EndlessModeEnabled;
        QueuedEnemies = Math.Max(0, data.QueuedEnemies);
    }

    private WaveDefinition? ResolveWave(int waveNumber, bool? endlessOverride = null)
    {
        if (waveNumber <= 0) return null;
        if (waveNumber <= _waves.Count) return _waves[waveNumber - 1];
        if (!(endlessOverride ?? EndlessModeEnabled) || _waves.Count == 0) return null;
        return EndlessWaveGenerator.Create(waveNumber, _waves.Count, _waves[^1]);
    }
}
