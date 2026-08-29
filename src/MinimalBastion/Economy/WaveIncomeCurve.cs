using MinimalBastion.Core;
using MinimalBastion.Data;

namespace MinimalBastion.Economy;

public static class WaveIncomeCurve
{
    public static float CalculateScale(
        WaveDefinition wave,
        WaveDefinition? anchorWave,
        IReadOnlyDictionary<string, EnemyDefinition> enemies)
    {
        if (wave.Number <= GameConstants.LateIncomeAnchorWave || anchorWave is null) return 1f;

        var baseIncome = CalculateBaseIncome(wave, enemies);
        if (baseIncome <= 0) return 1f;

        var anchorIncome = CalculateBaseIncome(anchorWave, enemies);
        var targetIncome = anchorIncome *
            (1f + GameConstants.LateIncomeGrowthPerWave * (wave.Number - GameConstants.LateIncomeAnchorWave));
        return Math.Max(0, targetIncome / baseIncome);
    }

    public static int CalculateBaseIncome(
        WaveDefinition wave,
        IReadOnlyDictionary<string, EnemyDefinition> enemies)
    {
        var total = (long)Economy.CalculateWaveReward(wave.Number);
        foreach (var group in wave.Groups)
        {
            if (!enemies.TryGetValue(group.EnemyId, out var enemy)) continue;
            total += (long)group.Count * Economy.CalculateKillReward(enemy.Reward, wave.Number);
        }

        return (int)Math.Min(int.MaxValue, total);
    }

    public static int CalculateScaledIncome(
        WaveDefinition wave,
        WaveDefinition? anchorWave,
        IReadOnlyDictionary<string, EnemyDefinition> enemies)
    {
        var scale = CalculateScale(wave, anchorWave, enemies);
        var total = (long)Economy.CalculateWaveReward(wave.Number, scale);
        foreach (var group in wave.Groups)
        {
            if (!enemies.TryGetValue(group.EnemyId, out var enemy)) continue;
            total += (long)group.Count * Economy.CalculateKillReward(enemy.Reward, wave.Number, scale);
        }

        return (int)Math.Min(int.MaxValue, total);
    }
}
