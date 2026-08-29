using MinimalBastion.Core;
using MinimalBastion.Persistence;

namespace MinimalBastion.Economy;

public sealed class Economy
{
    public int Credits { get; private set; }
    public int Lives { get; private set; }
    public int StartingCredits { get; }
    public int StartingLives { get; }
    public int TotalKills { get; private set; }
    public int EscapedEnemies { get; private set; }
    public int TotalCreditsSpent { get; private set; }
    public int KillCreditsEarned { get; private set; }
    public int WaveCreditsEarned { get; private set; }
    public int EarlyStartCreditsEarned { get; private set; }
    public int SaleCreditsRecovered { get; private set; }
    public float LateIncomeMultiplier { get; }
    public int TotalCreditsEarned => (int)Math.Min(int.MaxValue,
        (long)KillCreditsEarned + WaveCreditsEarned + EarlyStartCreditsEarned);
    public bool UnlimitedCredits { get; }
    public bool UnlimitedLives { get; }

    public Economy(int startingCredits, int startingLives, bool unlimitedCredits = false, bool unlimitedLives = false,
        float lateIncomeMultiplier = 1f)
    {
        StartingCredits = startingCredits;
        Credits = startingCredits;
        Lives = startingLives;
        StartingLives = startingLives;
        UnlimitedCredits = unlimitedCredits;
        UnlimitedLives = unlimitedLives;
        LateIncomeMultiplier = Math.Max(0, lateIncomeMultiplier);
    }

    public bool CanAfford(int amount) => amount >= 0 && (UnlimitedCredits || Credits >= amount);
    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        if (!UnlimitedCredits) Credits -= amount;
        TotalCreditsSpent = SaturatingAdd(TotalCreditsSpent, amount);
        return true;
    }

    public void AddCredits(int amount) => Credits = SaturatingAdd(Credits, amount);
    public void AwardKill(int reward) => AwardKill(reward, 1);

    public void AwardKill(int reward, int waveNumber)
    {
        TotalKills = SaturatingAdd(TotalKills, 1);
        var amount = CalculateKillReward(reward, waveNumber, LateIncomeMultiplier);
        KillCreditsEarned = SaturatingAdd(KillCreditsEarned, amount);
        AddCredits(amount);
    }

    public static float CalculateKillRewardMultiplier(int waveNumber)
    {
        var wavesPastOpening = Math.Max(0, waveNumber - GameConstants.FullKillRewardThroughWave);
        return Math.Max(GameConstants.MinimumKillRewardMultiplier,
            1f / (1f + GameConstants.KillRewardTaperPerWave * wavesPastOpening));
    }

    public static int CalculateKillReward(int reward, int waveNumber, float lateIncomeMultiplier = 1f)
    {
        if (reward <= 0) return 0;
        var incomeMultiplier = waveNumber > GameConstants.FullKillRewardThroughWave
            ? Math.Max(0, lateIncomeMultiplier)
            : 1f;
        return Math.Max(1, (int)Math.Round(
            reward * CalculateKillRewardMultiplier(waveNumber) * incomeMultiplier,
            MidpointRounding.AwayFromZero));
    }

    public float EffectiveKillRewardMultiplier(int waveNumber) =>
        CalculateKillRewardMultiplier(waveNumber) *
        (waveNumber > GameConstants.FullKillRewardThroughWave ? LateIncomeMultiplier : 1f);

    public void AwardWave(int waveNumber)
    {
        var amount = CalculateWaveReward(waveNumber, LateIncomeMultiplier);
        WaveCreditsEarned = SaturatingAdd(WaveCreditsEarned, amount);
        AddCredits(amount);
    }

    public static int CalculateWaveReward(int waveNumber, float lateIncomeMultiplier = 1f)
    {
        var baseReward = Math.Min(int.MaxValue, 40L + 10L * Math.Max(0, waveNumber));
        var incomeMultiplier = waveNumber > GameConstants.FullKillRewardThroughWave
            ? Math.Max(0, lateIncomeMultiplier)
            : 1f;
        return (int)Math.Min(int.MaxValue, Math.Round(baseReward * incomeMultiplier,
            MidpointRounding.AwayFromZero));
    }

    public int EffectiveWaveReward(int waveNumber) => CalculateWaveReward(waveNumber, LateIncomeMultiplier);

    public void AwardEarlyStart()
    {
        EarlyStartCreditsEarned = SaturatingAdd(EarlyStartCreditsEarned, GameConstants.EarlyStartBonus);
        AddCredits(GameConstants.EarlyStartBonus);
    }

    public void RecoverSale(int amount)
    {
        amount = Math.Max(0, amount);
        SaleCreditsRecovered = SaturatingAdd(SaleCreditsRecovered, amount);
        AddCredits(amount);
    }

    public void LoseLives(int amount)
    {
        if (amount <= 0 || UnlimitedLives) return;
        Lives = Math.Max(0, Lives - amount);
        EscapedEnemies = SaturatingAdd(EscapedEnemies, 1);
    }

    public EconomySaveData CaptureSaveData() => new()
    {
        Credits = Credits,
        Lives = Lives,
        TotalKills = TotalKills,
        EscapedEnemies = EscapedEnemies,
        TotalCreditsSpent = TotalCreditsSpent,
        KillCreditsEarned = KillCreditsEarned,
        WaveCreditsEarned = WaveCreditsEarned,
        EarlyStartCreditsEarned = EarlyStartCreditsEarned,
        SaleCreditsRecovered = SaleCreditsRecovered
    };

    public void RestoreSaveData(EconomySaveData data)
    {
        Credits = Math.Max(0, data.Credits);
        Lives = Math.Clamp(data.Lives, 0, StartingLives);
        TotalKills = Math.Max(0, data.TotalKills);
        EscapedEnemies = Math.Max(0, data.EscapedEnemies);
        TotalCreditsSpent = Math.Max(0, data.TotalCreditsSpent);
        KillCreditsEarned = Math.Max(0, data.KillCreditsEarned);
        WaveCreditsEarned = Math.Max(0, data.WaveCreditsEarned);
        EarlyStartCreditsEarned = Math.Max(0, data.EarlyStartCreditsEarned);
        SaleCreditsRecovered = Math.Max(0, data.SaleCreditsRecovered);
    }

    private static int SaturatingAdd(int current, int amount) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, current) + Math.Max(0, amount));
}
