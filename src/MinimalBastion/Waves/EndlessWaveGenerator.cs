using MinimalBastion.Data;

namespace MinimalBastion.Waves;

public static class EndlessWaveGenerator
{
    private static readonly string[] Archetypes =
    [
        "Endless Escalation",
        "Velocity Spiral",
        "Iron Procession",
        "Regrowth Mesh",
        "Core Recurrence"
    ];

    private static readonly string[] Briefings =
    [
        "Combined arms return with reinforced integrity and an elite anchor.",
        "Runner-heavy screens compress the reaction window around an elite spearhead.",
        "Brutes and Aegis units form a denser armored procession.",
        "Regenerators thicken the formation around an elite sustain core.",
        "A reinforced Bastion Core returns at the end of the formation."
    ];

    public static WaveDefinition Create(int waveNumber, int campaignWaveCount, WaveDefinition anchor)
    {
        if (waveNumber <= campaignWaveCount) throw new ArgumentOutOfRangeException(nameof(waveNumber));
        if (anchor.Groups.Count == 0) throw new InvalidDataException("The endless-wave anchor has no groups.");

        var endlessStep = waveNumber - campaignWaveCount;
        var cycle = (endlessStep - 1) % Archetypes.Length;
        var healthGrowth = Math.Min(10_000d, 1d + 0.085d * endlessStep + 0.0045d * endlessStep * endlessStep);
        var countGrowth = MathF.Min(1.60f, 1f + 0.0125f * endlessStep);
        var cadenceScale = MathF.Max(0.80f, 1f - 0.0075f * endlessStep);
        var delayScale = MathF.Max(0.75f, 1f - 0.005f * endlessStep);

        var groups = anchor.Groups.Select(group => new WaveGroupDefinition
        {
            EnemyId = group.EnemyId,
            Rank = group.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)
                ? cycle == 4 ? "Boss" : "Standard"
                : "Standard",
            Count = Math.Max(1, (int)MathF.Round(group.Count * countGrowth * RosterWeight(group.EnemyId, cycle))),
            SpawnInterval = MathF.Max(0.14f, group.SpawnInterval * cadenceScale),
            DelayBefore = group.DelayBefore * delayScale
        }).ToList();

        var eliteCount = Math.Min(2, 1 + endlessStep / 20);
        switch (cycle)
        {
            case 0:
                InsertElite(groups, "t3_brute", eliteCount, groups.Count / 2);
                break;
            case 1:
                InsertElite(groups, "t2_runner", eliteCount, groups.Count / 3);
                InsertElite(groups, "t2_runner", eliteCount, groups.Count * 2 / 3);
                break;
            case 2:
                InsertElite(groups, "t3_brute", eliteCount, groups.Count / 3);
                InsertElite(groups, "t4_aegis", eliteCount, groups.Count * 2 / 3);
                break;
            case 3:
                InsertElite(groups, "t5_regenerator", eliteCount, groups.Count / 3);
                InsertElite(groups, "t5_regenerator", eliteCount, groups.Count * 2 / 3);
                break;
            case 4:
                InsertElite(groups, "t4_aegis", eliteCount, groups.Count / 2);
                break;
        }

        return new WaveDefinition
        {
            Number = waveNumber,
            Archetype = Archetypes[cycle],
            Briefing = Briefings[cycle],
            HealthMultiplier = anchor.HealthMultiplier * (float)healthGrowth,
            SpeedMultiplier = MathF.Min(1.30f, anchor.SpeedMultiplier + 0.006f * endlessStep),
            Groups = groups
        };
    }

    private static float RosterWeight(string enemyId, int cycle) => cycle switch
    {
        1 when enemyId is "t1_crawler" or "t2_runner" => 1.15f,
        2 when enemyId is "t3_brute" or "t4_aegis" => 1.12f,
        3 when enemyId == "t5_regenerator" => 1.22f,
        _ => 1f
    };

    private static void InsertElite(List<WaveGroupDefinition> groups, string preferredEnemyId, int count, int index)
    {
        var enemyId = groups.FirstOrDefault(group => group.EnemyId.Equals(preferredEnemyId, StringComparison.OrdinalIgnoreCase))?.EnemyId;
        if (enemyId is null) return;
        groups.Insert(Math.Clamp(index, 0, groups.Count), new WaveGroupDefinition
        {
            EnemyId = enemyId,
            Rank = "Elite",
            Count = count,
            SpawnInterval = 0.48f,
            DelayBefore = 0.22f
        });
    }
}
