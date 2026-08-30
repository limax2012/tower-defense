using MinimalBastion.Data;
using MinimalBastion.Core;
using MinimalBastion.Enemies;
using EconomyService = MinimalBastion.Economy.Economy;

namespace MinimalBastion.Simulation;

public sealed record WavePressureMetrics(
    int EnemyCount,
    float TotalDurability,
    float ArmorAdjustedDemand,
    float PeakPacedDemand,
    float SpawnDurationSeconds,
    float RegenerationPerSecond,
    int ScheduledSignalCarriers,
    int KillCredits,
    int CompletionCredits)
{
    public int TotalCredits => KillCredits + CompletionCredits;
}

public static class WavePressureAnalysis
{
    public const float ReferenceHitDamage = 30f;
    public const float DefaultPressureWindowSeconds = 8f;

    public static WavePressureMetrics Analyze(
        WaveDefinition wave,
        IReadOnlyDictionary<string, EnemyDefinition> enemies,
        float difficultyHealthMultiplier = 1f,
        float difficultySpeedMultiplier = 1f,
        float pressureWindowSeconds = DefaultPressureWindowSeconds)
    {
        ArgumentNullException.ThrowIfNull(wave);
        ArgumentNullException.ThrowIfNull(enemies);
        if (!float.IsFinite(difficultyHealthMultiplier) || difficultyHealthMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyHealthMultiplier));
        if (!float.IsFinite(difficultySpeedMultiplier) || difficultySpeedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultySpeedMultiplier));
        if (!float.IsFinite(pressureWindowSeconds) || pressureWindowSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(pressureWindowSeconds));

        var enemyCount = 0;
        var totalDurability = 0f;
        var armorAdjustedDemand = 0f;
        var regenerationPerSecond = 0f;
        var signalCarriers = 0;
        var killCredits = 0;
        var spawnTime = 0f;
        var events = new List<SpawnPressureEvent>();

        for (var groupIndex = 0; groupIndex < wave.Groups.Count; groupIndex++)
        {
            var group = wave.Groups[groupIndex];
            if (!enemies.TryGetValue(group.EnemyId, out var enemy))
                throw new InvalidDataException($"Wave {wave.Number} references unknown enemy '{group.EnemyId}'.");

            var rank = ParseRank(group.Rank);
            var rankHealth = rank switch
            {
                EnemyRank.Elite => 1.85f,
                EnemyRank.Boss => 4.5f,
                _ => 1f
            };
            var rankSpeed = rank switch
            {
                EnemyRank.Elite => 1.07f,
                EnemyRank.Boss => 0.92f,
                _ => 1f
            };
            var rankArmor = rank switch
            {
                EnemyRank.Elite => 2f,
                EnemyRank.Boss => 4f,
                _ => 0f
            };
            var rankReward = rank switch
            {
                EnemyRank.Elite => 2f,
                EnemyRank.Boss => 5f,
                _ => 1f
            };

            var health = enemy.MaxHealth * wave.HealthMultiplier * difficultyHealthMultiplier * rankHealth;
            var shield = enemy.Shield + (rank == EnemyRank.Boss ? health * 0.12f : 0f);
            var armor = enemy.Armor + rankArmor;
            var perEnemyDemand = shield + health * ReferenceHitDamage /
                MathF.Max(1f, ReferenceHitDamage - armor);
            var pacedDemand = perEnemyDemand * wave.SpeedMultiplier * difficultySpeedMultiplier * rankSpeed;
            var reward = (int)MathF.Round(enemy.Reward * rankReward);

            enemyCount += group.Count;
            totalDurability += (health + shield) * group.Count;
            armorAdjustedDemand += perEnemyDemand * group.Count;
            regenerationPerSecond += enemy.RegenerationPerSecond * group.Count;
            killCredits += EconomyService.CalculateKillReward(reward, wave.Number) * group.Count;

            spawnTime += group.DelayBefore;
            for (var spawned = 0; spawned < group.Count; spawned++)
            {
                events.Add(new SpawnPressureEvent(spawnTime + spawned * group.SpawnInterval, pacedDemand));
                if (EnemySignalSchedule.Resolve(wave, groupIndex, spawned, group) != EnemySignalRole.None)
                    signalCarriers++;
            }
            if (group.Count > 0) spawnTime += (group.Count - 1) * group.SpawnInterval;
        }

        var peakPacedDemand = 0f;
        for (var start = 0; start < events.Count; start++)
        {
            var windowEnd = events[start].Time + pressureWindowSeconds;
            var windowDemand = 0f;
            for (var index = start; index < events.Count && events[index].Time < windowEnd; index++)
                windowDemand += events[index].Demand;
            peakPacedDemand = MathF.Max(peakPacedDemand, windowDemand);
        }

        return new WavePressureMetrics(
            enemyCount,
            totalDurability,
            armorAdjustedDemand,
            peakPacedDemand,
            spawnTime,
            regenerationPerSecond,
            signalCarriers,
            killCredits,
            EconomyService.CalculateWaveReward(wave.Number));
    }

    private static EnemyRank ParseRank(string rank) =>
        Enum.TryParse<EnemyRank>(rank, true, out var parsed) ? parsed : EnemyRank.Standard;

    private readonly record struct SpawnPressureEvent(float Time, float Demand);
}
