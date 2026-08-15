using MinimalBastion.Data;

namespace MinimalBastion.Waves;

public sealed record WaveIntelInfo(
    int Wave,
    int ApproximateCount,
    string Archetype,
    string Briefing,
    IReadOnlyList<string> Threats)
{
    public string CompactThreats => Threats.Count == 0 ? "STANDARD" : string.Join("/", Threats.Take(3));
}

public sealed record CampaignIntelInfo(
    int TotalContacts,
    int PeakContacts,
    string OpeningThreats,
    float FinalHealthMultiplier,
    int BossWave)
{
    public string CompactSummary =>
        $"CAMPAIGN  OPEN {OpeningThreats}  |  {TotalContacts:N0} CONTACTS  |  PEAK {PeakContacts}  |  FINAL HEALTH x{FinalHealthMultiplier:0.00}  |  BOSS W{BossWave}";
}

public static class WaveIntel
{
    public static CampaignIntelInfo AnalyzeCampaign(WaveSetDefinition campaign, IReadOnlyDictionary<string, EnemyDefinition> enemies)
    {
        var waves = campaign.Waves;
        if (waves.Count == 0) return new CampaignIntelInfo(0, 0, "STANDARD", 1f, 0);
        var total = waves.Sum(wave => wave.Groups.Sum(group => group.Count));
        var peak = waves.Max(wave => wave.Groups.Sum(group => group.Count));
        var opening = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in waves.Take(Math.Min(5, waves.Count)).SelectMany(wave => wave.Groups))
        {
            if (!enemies.TryGetValue(group.EnemyId, out var enemy)) continue;
            var category = enemy.RegenerationPerSecond > 0 ? "REGEN"
                : enemy.Shield > 0 ? "SHIELD"
                : enemy.Speed >= 100 ? "FAST"
                : enemy.Armor > 0 ? "ARMOR"
                : "SWARM";
            opening[category] = opening.GetValueOrDefault(category) + group.Count;
        }
        var openingThreats = string.Join("/", opening.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(3).Select(pair => pair.Key));
        if (string.IsNullOrEmpty(openingThreats)) openingThreats = "STANDARD";
        var bossWave = waves.FirstOrDefault(wave => wave.Groups.Any(group => group.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)))?.Number
            ?? waves[^1].Number;
        return new CampaignIntelInfo(total, peak, openingThreats, waves[^1].HealthMultiplier, bossWave);
    }

    public static WaveIntelInfo Analyze(WaveDefinition wave, IReadOnlyDictionary<string, EnemyDefinition> enemies)
    {
        var count = wave.Groups.Sum(x => x.Count);
        var threatCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in wave.Groups)
        {
            if (!enemies.TryGetValue(group.EnemyId, out var enemy)) continue;
            if (group.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)) Add("BOSS", group.Count);
            else if (group.Rank.Equals("Elite", StringComparison.OrdinalIgnoreCase)) Add("ELITE", group.Count);
            if (enemy.MaxHealth <= 100) Add("SWARM", group.Count);
            if (enemy.Speed >= 100) Add("FAST", group.Count);
            if (enemy.Armor > 0) Add("ARMOR", group.Count);
            if (enemy.Shield > 0) Add("SHIELD", group.Count);
            if (enemy.RegenerationPerSecond > 0) Add("REGEN", group.Count);
        }

        var threats = threatCounts
            .Where(x => x.Key is "BOSS" or "ELITE" or "SHIELD" or "REGEN" || x.Value >= Math.Max(2, count / 6))
            .OrderBy(x => x.Key == "BOSS" ? 0 : x.Key == "ELITE" ? 1 : 2)
            .ThenByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .ToArray();
        return new WaveIntelInfo(wave.Number, RoundApproximate(count), wave.Archetype, wave.Briefing, threats);

        void Add(string key, int amount) => threatCounts[key] = threatCounts.GetValueOrDefault(key) + amount;
    }

    private static int RoundApproximate(int count)
    {
        if (count < 15) return count;
        return (int)(MathF.Round(count / 5f) * 5);
    }
}
