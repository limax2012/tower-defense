using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using MaximalBastion.UI;

namespace MaximalBastion.Tests;

internal static class EffectiveSlowDisplayTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var frost = content.Towers["frost_spire"];
        AboveCapPreviewsStayNeutral(frost);
        BranchAndProtocolTextUseEffectiveSlow(frost);
        EnemyDetailUsesMovementCap(content);
    }

    private static void AboveCapPreviewsStayNeutral(TowerDefinition frost)
    {
        var chill = frost.Tier2Doctrines.Single(doctrine => doctrine.Id == "frost_deep_chill");
        var permafrost = frost.Specializations.Single(branch => branch.Id == "permafrost");
        var current = permafrost.Level.WithDoctrine(chill);
        var apex = current.WithApex(frost.Apex);
        Check.True(current.SlowPercent > EnemyInstance.MaximumSlowFraction && apex.SlowPercent > current.SlowPercent,
            "Deep Chill Permafrost and its Apex have distinct authored slows above the movement cap");
        var stats = TowerInfo.ComparisonStats(frost, current, apex);
        var slow = stats.Single(stat => stat.Label == "SLOW");
        var cap = EnemyInstance.MaximumSlowFraction.ToString("P0");
        Check.Equal(cap, slow.Value, "the current-to-Apex preview reports effective slow");
        Check.Equal(TowerStatDirection.Unchanged, slow.Direction,
            "authored growth above the cap does not imply an effective slow improvement");
        Check.True(slow.PreviousValue is null && TowerInfo.ComparisonStatValueText(slow) == cap,
            "an unchanged capped slow does not display a misleading arrow");
        Check.Equal($"SLOW CAP {cap}", TowerInfo.ComparisonStatLabelText(slow),
            "the current and preview grid disclose the movement cap");
        Check.Equal(TowerStatDirection.Increase, stats.Single(stat => stat.Label == "SLOW TIME").Direction,
            "Apex still reports its real duration improvement");
        Check.Equal(TowerStatDirection.Increase, stats.Single(stat => stat.Label == "AREA").Direction,
            "Apex still reports its real splash-area improvement");
        Check.True(current.SlowPercent > EnemyInstance.MaximumSlowFraction,
            "displaying effective slow does not modify the authored level");

        var below = new TowerLevelDefinition { SlowPercent = .59f, SlowDuration = 3 };
        var above = new TowerLevelDefinition { SlowPercent = .75f, SlowDuration = 3 };
        var crossing = TowerInfo.ComparisonStats(frost, below, above).Single(stat => stat.Label == "SLOW");
        Check.Equal(TowerStatDirection.Increase, crossing.Direction, "crossing the cap retains the real increase up to the cap");
        Check.Equal(cap, crossing.Value, "crossing the cap stops the displayed value at the cap");
        var decreasing = TowerInfo.ComparisonStats(frost, above, below).Single(stat => stat.Label == "SLOW");
        Check.Equal(TowerStatDirection.Decrease, decreasing.Direction, "a decrease below the cap remains visible");
        var cappedOnly = TowerInfo.ComparisonStats(frost,
            new TowerLevelDefinition { SlowPercent = .73f }, above).Single(stat => stat.Label == "SLOW");
        Check.Equal(TowerStatDirection.Unchanged, cappedOnly.Direction, "73 to 75 percent authored slow remains neutral");
        var baseSlow = TowerInfo.ComparisonStats(frost, frost.Levels[0]).Single(stat => stat.Label == "SLOW");
        Check.Equal(frost.Levels[0].SlowPercent.ToString("P0"), baseSlow.Value, "below-cap values remain unchanged");
    }

    private static void BranchAndProtocolTextUseEffectiveSlow(TowerDefinition frost)
    {
        var chill = frost.Tier2Doctrines.Single(doctrine => doctrine.Id == "frost_deep_chill");
        var permafrost = frost.Specializations.Single(branch => branch.Id == "permafrost");
        var current = frost.Levels[1].WithDoctrine(chill);
        var final = permafrost.Level.WithDoctrine(chill);
        var cap = EnemyInstance.MaximumSlowFraction.ToString("P0");
        var branch = TowerInfo.SpecializationSummary(frost, current, permafrost, chill);
        Check.True(branch.Contains(cap, StringComparison.Ordinal) && branch.Contains($"CAP {cap}", StringComparison.Ordinal) &&
                !branch.Contains(final.SlowPercent.ToString("P0"), StringComparison.Ordinal),
            "branch summaries show the effective strength and explain the cap");
        Check.True(TowerInfo.Special(frost, final).Contains($"slow {cap}", StringComparison.Ordinal) &&
                TowerInfo.Special(frost, final).Contains($"cap {cap}", StringComparison.Ordinal),
            "the descriptive attack summary uses the same effective cap");
        var unchangedBranch = new TowerSpecializationDefinition
        {
            Id = "capped-preview", DisplayName = "Capped preview", Summary = "Equal effective slow",
            Level = new TowerLevelDefinition { SlowPercent = .75f, SlowDuration = 4 }
        };
        var summary = TowerInfo.SpecializationSummary(frost,
            new TowerLevelDefinition { SlowPercent = .73f, SlowDuration = 4 }, unchangedBranch);
        Check.True(!summary.Contains("73", StringComparison.Ordinal) && !summary.Contains("75", StringComparison.Ordinal),
            "a branch with only ineffective slow growth does not promise that growth");

        var protocol = new TowerProtocolDefinition
        {
            DisplayName = "Slow pulse", BurstStatus = "Slow", BurstStatusMagnitude = .75f, BurstStatusDuration = 4,
            BurstRadius = 170, DurationSeconds = 6, CooldownSeconds = 18
        };
        var texts = new[]
        {
            TowerInfo.ProtocolBonuses(protocol), TowerInfo.ProtocolLiveSummary(protocol),
            TowerInfo.ProtocolEffectSummary(protocol, false), TowerInfo.ProtocolEffectSummary(protocol, true),
            TowerInfo.ProtocolSummary(new TowerDefinition { Protocol = protocol })
        }.Concat(TowerInfo.ProtocolBonusRows(protocol));
        Check.True(texts.All(text => text.Contains($"SLOW {cap}/", StringComparison.Ordinal) &&
                !text.Contains($"SLOW {protocol.BurstStatusMagnitude:P0}/", StringComparison.Ordinal)),
            "all Protocol summary surfaces cap an over-cap slow magnitude");
        Check.True(TowerInfo.ProtocolLiveSummary(frost.Protocol).Contains(frost.Protocol.BurstStatusMagnitude.ToString("P0"), StringComparison.Ordinal),
            "the authored below-cap Flash Freeze magnitude is unchanged");
    }

    private static void EnemyDetailUsesMovementCap(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        session.SpawnEnemy("t1_crawler", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Slow, Magnitude = .75f, Duration = 4 });
        Check.True(EnemyConditionPresentation.Detail(enemy, EnemyCondition.Slow)
                .Contains(EnemyInstance.MaximumSlowFraction.ToString("P0"), StringComparison.Ordinal),
            "enemy condition details and tower previews use the same movement cap");
        Check.Equal(1f - EnemyInstance.MaximumSlowFraction, enemy.SpeedMultiplier,
            "the displayed capped slow agrees with actual movement");
    }
}
