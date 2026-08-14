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
    public int TotalCreditsEarned => KillCreditsEarned + WaveCreditsEarned + EarlyStartCreditsEarned;

    public Economy(int startingCredits, int startingLives)
    {
        StartingCredits = startingCredits;
        Credits = startingCredits;
        Lives = startingLives;
        StartingLives = startingLives;
    }

    public bool CanAfford(int amount) => amount >= 0 && Credits >= amount;
    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        Credits -= amount;
        TotalCreditsSpent += amount;
        return true;
    }

    public void AddCredits(int amount) => Credits += Math.Max(0, amount);
    public void AwardKill(int reward)
    {
        TotalKills++;
        var amount = Math.Max(0, reward);
        KillCreditsEarned += amount;
        AddCredits(amount);
    }

    public void AwardWave(int waveNumber)
    {
        var amount = 40 + 10 * waveNumber;
        WaveCreditsEarned += amount;
        AddCredits(amount);
    }

    public void AwardEarlyStart()
    {
        EarlyStartCreditsEarned += GameConstants.EarlyStartBonus;
        AddCredits(GameConstants.EarlyStartBonus);
    }

    public void RecoverSale(int amount)
    {
        amount = Math.Max(0, amount);
        SaleCreditsRecovered += amount;
        AddCredits(amount);
    }

    public void LoseLives(int amount)
    {
        if (amount <= 0) return;
        Lives = Math.Max(0, Lives - amount);
        EscapedEnemies++;
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
}
