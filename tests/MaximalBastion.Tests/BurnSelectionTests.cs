using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;

namespace MaximalBastion.Tests;

internal static class BurnSelectionTests
{
    public static void Run()
    {
        OneAndTwoSourcesKeepIndependentTicks();
        DormantSourcesExpireAndProvideFallback();
        DormantSourcesKeepOnlyEarnedTickProgress();
        CurrentSourceRefreshReplacesPotency();
        ArmorAdjustedScoresUseRawDamageAndTickInterval();
        SelectionUsesDamagePerSecondAndStableTies();

        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        SelectionRespondsOnlyToArmor(session);
        ExpectedDamageEstimatesMatchResolver(session);
        EnemySystemUsesSelectedSources(session);
        ShieldAndExposeChangeDamageWithoutChangingSelection(session);
    }

    private static void OneAndTwoSourcesKeepIndependentTicks()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Burn(1, 10));
        Check.Equal(0, statuses.ConsumeBurnTicks(.25f).Count, "one source waits for its complete tick");
        statuses.Update(.25f);
        var first = statuses.ConsumeBurnTicks(.25f).Single();
        Check.Nearly(5, first.Damage, "one source retains its full nominal tick damage");
        Check.Equal(1, first.SourceId, "one source retains tick attribution");
        statuses.Update(.25f);

        statuses.Apply(Burn(2, 8, interval: .25f, pierce: 3));
        var ticks = statuses.ConsumeBurnTicks(.5f);
        Check.Equal(3, ticks.Count, "two sources retain independent tick intervals");
        Check.Nearly(9, ticks.Sum(tick => tick.Damage), "both sources contribute their complete damage");
        Check.Equal(2, ticks.Count(tick => tick.SourceId == 2), "the shorter interval ticks twice");
        Check.True(ticks.Where(tick => tick.SourceId == 2).All(tick => tick.ArmorPierce == 3),
            "ticks retain their source's armor pierce");
        Check.Equal(0, statuses.ConsumeBurnTicks(0).Count, "zero time creates no burn ticks");
    }

    private static void DormantSourcesExpireAndProvideFallback()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Burn(1, 10, duration: .5f));
        statuses.Apply(Burn(2, 8, duration: .75f));
        statuses.Apply(Burn(3, 4, duration: 3));
        statuses.Apply(Burn(4, 2, duration: .25f));
        Check.Equal(4, statuses.Active.Count, "every unexpired burn source is retained");
        AssertSources(statuses.GetActiveBurns(), "only the two strongest sources start ticking", 1, 2);

        Check.Equal(0, statuses.ConsumeBurnTicks(.25f).Count, "selected sources begin their first tick");
        statuses.Update(.25f);
        Check.True(statuses.Active.All(status => status.SourceId != 4), "an inactive source expires on schedule");
        Check.Nearly(2.75f, Source(statuses, 3).RemainingSeconds, "inactive duration continues to elapse");
        Check.Nearly(0, Source(statuses, 3).TickProgress, "a source earns no progress while inactive");
        Check.Nearly(9, statuses.ConsumeBurnTicks(.25f).Sum(tick => tick.Damage), "selected sources tick before expiry");
        statuses.Update(.25f);
        AssertSources(statuses.GetActiveBurns(), "the weaker long burn becomes active after expiry", 2, 3);

        Check.Equal(0, statuses.ConsumeBurnTicks(.25f).Count, "fallback starts without deferred ticks");
        statuses.Update(.25f);
        AssertSources(statuses.GetActiveBurns(), "the remaining long burn survives both stronger sources", 3);
        var fallback = statuses.ConsumeBurnTicks(.25f).Single();
        Check.Equal(3, fallback.SourceId, "the fallback retains its own attribution");
        Check.Nearly(2, fallback.Damage, "the fallback contributes only one earned tick");
    }

    private static void DormantSourcesKeepOnlyEarnedTickProgress()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Burn(1, 10));
        statuses.Apply(Burn(2, 8));
        statuses.ConsumeBurnTicks(.25f);
        statuses.Update(.25f);
        statuses.Apply(Burn(3, 12, duration: .5f));

        var activeTicks = statuses.ConsumeBurnTicks(.5f);
        Check.True(activeTicks.All(tick => tick.SourceId != 2), "a displaced source stops emitting ticks");
        Check.Nearly(.25f, Source(statuses, 2).TickProgress, "displacement freezes previously earned tick progress");
        statuses.Update(.5f);
        AssertSources(statuses.GetActiveBurns(), "expiry reactivates the displaced source", 1, 2);

        var resumed = statuses.ConsumeBurnTicks(.25f).Where(tick => tick.SourceId == 2).ToList();
        Check.Equal(1, resumed.Count, "reactivation resumes the partial tick without catching up");
        Check.Nearly(4, resumed[0].Damage, "reactivated damage excludes time spent inactive");
        Check.Nearly(0, Source(statuses, 2).TickProgress, "the resumed tick consumes its earned progress");
    }

    private static void CurrentSourceRefreshReplacesPotency()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Burn(1, 10, duration: 4, pierce: 5));
        statuses.Apply(Burn(2, 8));
        statuses.Apply(Burn(3, 7));
        statuses.ConsumeBurnTicks(.25f);
        statuses.Update(.25f);
        statuses.Apply(Burn(1, 2, duration: 1, interval: 1, pierce: 1));

        Check.Equal(3, statuses.Active.Count, "same-source refresh does not create another retained burn");
        var refreshed = Source(statuses, 1);
        Check.Nearly(2, refreshed.Magnitude, "a weaker refresh replaces the source's current potency");
        Check.Nearly(1, refreshed.TickInterval, "refresh replaces the source's tick interval");
        Check.Nearly(1, refreshed.ArmorPierce, "refresh replaces the source's armor pierce");
        Check.Nearly(3.75f, refreshed.RemainingSeconds, "a shorter refresh preserves remaining duration");
        Check.Nearly(.25f, refreshed.TickProgress, "refresh preserves earned tick progress");
        AssertSources(statuses.GetActiveBurns(), "weaker current potency changes source selection", 2, 3);

        statuses.Apply(Burn(1, 12, duration: 6));
        Check.Nearly(6, refreshed.RemainingSeconds, "a longer refresh extends the same source");
        AssertSources(statuses.GetActiveBurns(), "stronger current potency can reactivate its source", 1, 2);
    }

    private static void ArmorAdjustedScoresUseRawDamageAndTickInterval()
    {
        var burn = new ActiveStatus { Type = StatusType.Burn, Magnitude = 20, TickInterval = .5f };
        Check.Nearly(20, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn),
            "unarmored selection score is the raw burn damage rate");
        Check.Nearly(10, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 5),
            "selection subtracts one armor payment per tick from raw damage rate");
        burn.TickInterval = 1;
        Check.Nearly(15, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 5),
            "a longer interval reduces armor paid per second");
        burn.TickInterval = .25f;
        Check.Nearly(0, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 5),
            "fully absorbed burn damage scores zero");
        Check.Nearly(0, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 10),
            "armor-adjusted damage cannot become negative");
        burn.ArmorPierce = 4;
        Check.Nearly(16, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 5),
            "piercing reduces each tick's armor payment before converting to damage rate");
        burn.ArmorPierce = 10;
        Check.Nearly(20, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(burn, 5),
            "piercing beyond armor never increases damage above the raw rate");
    }

    private static void SelectionUsesDamagePerSecondAndStableTies()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Burn(1, 10, interval: 1));
        statuses.Apply(Burn(2, 12));
        statuses.Apply(Burn(3, 11));
        AssertSources(statuses.GetActiveBurns(), "selection compares damage rate rather than full tick size", 2, 3);
        AssertSources(statuses.GetActiveBurns(effectiveArmor: 4), "longer tick intervals pay armor less frequently", 1, 2);

        var rawTieBreak = new StatusEffectController();
        rawTieBreak.Apply(Burn(10, 10, pierce: 5));
        rawTieBreak.Apply(Burn(20, 14, pierce: 3));
        rawTieBreak.Apply(Burn(30, 13, pierce: 3.5f));
        Check.True(rawTieBreak.GetActiveBurns(5).Select(status => status.SourceId).SequenceEqual(new[] { 20, 30 }),
            "equal armor-adjusted scores prefer higher raw damage before source identity");
        Check.True(rawTieBreak.GetActiveBurns(100).Select(status => status.SourceId).SequenceEqual(new[] { 20, 30 }),
            "fully absorbed sources still use raw damage to break zero-score ties");
        Check.True(rawTieBreak.ConsumeBurnTicks(.5f, 5).Select(tick => tick.SourceId).Order().SequenceEqual(new[] { 20, 30 }),
            "tick selection uses the same raw damage tie-break as the query");

        foreach (var ids in new[] { new[] { 30, 10, 20 }, new[] { 20, 10, 30 } })
        {
            var tied = new StatusEffectController();
            foreach (var id in ids) tied.Apply(Burn(id, 10));
            Check.True(tied.GetActiveBurns().Select(status => status.SourceId).SequenceEqual(new[] { 10, 20 }),
                "equal scores and raw damage use ascending source identity independently of insertion order");
            AssertSources(tied.GetActiveBurns(effectiveArmor: 100), "zero damage ties have the same stable selection", 10, 20);
            Check.True(tied.ConsumeBurnTicks(.5f).Select(tick => tick.SourceId).Order().SequenceEqual(new[] { 10, 20 }),
                "tick emission uses the same tie winners as the selection query");
        }
    }

    private static void SelectionRespondsOnlyToArmor(GameSession session)
    {
        var enemy = Enemy(session, armor: 7);
        ApplyCompetingBurns(enemy);
        Check.Nearly(5, enemy.EffectiveArmor, "selection includes the shared burning armor reduction");
        AssertSources(enemy.GetActiveBurns(), "piercing sources win against armor", 2, 3);
        enemy.GrantShield(30, 30);
        AssertSources(enemy.GetActiveBurns(), "adding shields leaves armor-only selection unchanged", 2, 3);
        enemy.AbsorbShield(30);
        AssertSources(enemy.GetActiveBurns(), "breaking shields leaves armor-only selection unchanged", 2, 3);
        enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Magnitude = .5f, Duration = 1 });
        AssertSources(enemy.GetActiveBurns(), "Expose does not enter the selection score", 2, 3);
        enemy.StatusEffects.Update(1);
        AssertSources(enemy.GetActiveBurns(), "expiring Expose leaves source selection unchanged", 2, 3);
        enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.ArmorBreak, Magnitude = 5, Duration = 1 });
        AssertSources(enemy.GetActiveBurns(), "armor break immediately changes effective source ranking", 1, 2);

        var ranked = Enemy(session, armor: 7, rank: "Elite");
        ApplyCompetingBurns(ranked);
        Check.Nearly(7, ranked.EffectiveArmor, "rank armor participates in the enemy selection wrapper");
        AssertSources(ranked.GetActiveBurns(), "elite armor preserves the piercing advantage", 2, 3);
    }

    private static void ExpectedDamageEstimatesMatchResolver(GameSession session)
    {
        var cases = new (float Armor, float Shield, float Expose, float Magnitude, float Interval, float Pierce, float Break)[]
        {
            (0, 0, 0, 10, .5f, 0, 0),
            (7, 0, 0, 20, .5f, 0, 0),
            (7, 0, 0, 14, .5f, 4, 0),
            (7, 2, 0, 14, .5f, 4, 0),
            (7, 30, 0, 14, .5f, 4, 0),
            (7, 2, .5f, 20, .5f, 0, 0),
            (7, 30, .5f, 20, .5f, 0, 0),
            (7, 0, .5f, 20, .5f, 0, 3),
            (7, 2, .5f, 10, 1, 4, 3),
            (12, 10, 0, 24, .5f, 0, 0),
            (22, 0, 0, 1, .5f, 0, 0),
            (22, 2, 0, 10, .5f, 0, 0),
            (22, 0, .5f, 10, .25f, 30, 0)
        };
        foreach (var item in cases)
        {
            var enemy = Enemy(session, item.Armor, item.Shield);
            enemy.StatusEffects.Apply(Burn(1, item.Magnitude, interval: item.Interval, pierce: item.Pierce));
            enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Magnitude = item.Expose, Duration = 10 });
            enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.ArmorBreak, Magnitude = item.Break, Duration = 10 });
            var expectedTickDamage = enemy.GetExpectedBurnDamagePerSecond(Source(enemy.StatusEffects, 1)) * item.Interval;
            var healthBefore = enemy.Health;
            var shieldBefore = enemy.Shield;
            session.DamageResolver.Apply(enemy, new DamagePayload
            {
                Damage = item.Magnitude * item.Interval,
                ArmorPierce = item.Pierce,
                IsDamageOverTime = true,
                SourceTowerId = 1
            });
            var actualDamage = healthBefore - enemy.Health + shieldBefore - enemy.Shield;
            Check.Nearly(actualDamage, expectedTickDamage, $"current damage estimate matches resolved shield plus health damage: {item}");
            if (item.Armor == 22 && item.Shield == 0 && item.Pierce == 0)
                Check.Nearly(0, expectedTickDamage, "armor-blocked damage over time has no minimum damage floor");
            if (item.Armor == 12 && item.Shield == 10)
                Check.Nearly(10, expectedTickDamage, "the current damage estimate includes shield damage before armor");
        }

        var nearlyDead = Enemy(session);
        nearlyDead.StatusEffects.Apply(Burn(1, 10));
        nearlyDead.ApplyHealthDamage(nearlyDead.Health - .5f);
        Check.Nearly(10, nearlyDead.GetExpectedBurnDamagePerSecond(Source(nearlyDead.StatusEffects, 1)),
            "the current damage estimate does not clamp to remaining health");
    }

    private static void EnemySystemUsesSelectedSources(GameSession session)
    {
        var enemy = Enemy(session, armor: 7);
        ApplyCompetingBurns(enemy);
        session.Enemies.Add(enemy);
        var reports = new List<DamageReport>();
        session.DamageResolver.DamageApplied += reports.Add;
        try
        {
            var system = new EnemySystem();
            var healthBefore = enemy.Health;
            system.Update(.5f, session);
            Check.Nearly(11.5f, healthBefore - enemy.Health, "combat resolves the two piercing burns against effective armor");
            Check.True(reports.Select(report => report.SourceTowerId).Order().SequenceEqual(new[] { 2, 3 }),
                "combat attributes damage only to the selected burn sources");
            Check.Nearly(0, Source(enemy.StatusEffects, 1).TickProgress, "combat freezes the dormant burn's tick clock");
            Check.Nearly(9.5f, Source(enemy.StatusEffects, 1).RemainingSeconds, "combat still advances the dormant burn's duration");

            reports.Clear();
            enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Magnitude = .5f, Duration = 2 });
            healthBefore = enemy.Health;
            system.Update(.5f, session);
            Check.Nearly(18.25f, healthBefore - enemy.Health, "Expose increases actual damage while preserving the selected sources");
            Check.True(reports.Select(report => report.SourceTowerId).Order().SequenceEqual(new[] { 2, 3 }),
                "Expose does not switch tick attribution to a different pair");

            reports.Clear();
            enemy.GrantShield(100, 100);
            system.Update(.5f, session);
            Check.Nearly(20.25f, reports.Sum(report => report.ShieldDamage), "shields absorb both selected exposed burn ticks");
            Check.True(reports.Select(report => report.SourceTowerId).Order().SequenceEqual(new[] { 2, 3 }),
                "shields do not switch tick attribution to a different pair");
            Check.Nearly(0, Source(enemy.StatusEffects, 1).TickProgress, "shield and Expose changes never advance the dormant source");

            reports.Clear();
            enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.ArmorBreak, Magnitude = 5, Duration = 2 });
            system.Update(.5f, session);
            Check.True(reports.Select(report => report.SourceTowerId).Order().SequenceEqual(new[] { 1, 2 }),
                "armor break changes tick selection even while shield and Expose remain active");
        }
        finally
        {
            session.DamageResolver.DamageApplied -= reports.Add;
            session.Enemies.Remove(enemy);
        }
    }

    private static void ShieldAndExposeChangeDamageWithoutChangingSelection(GameSession session)
    {
        foreach (var (shield, expose, expectedDamage) in new[]
        {
            (10f, 0f, 19f), (100f, 0f, 21f), (10f, .5f, 23.5f), (100f, .5f, 31.5f)
        })
        {
            var enemy = Enemy(session, armor: 12, shield: shield);
            enemy.StatusEffects.Apply(Burn(1, 24));
            enemy.StatusEffects.Apply(Burn(2, 24));
            enemy.StatusEffects.Apply(Burn(3, 18, pierce: 10));
            enemy.StatusEffects.Apply(new StatusApplication { Type = StatusType.Exposed, Magnitude = expose, Duration = 10 });
            Check.Nearly(10, enemy.EffectiveArmor, "the shield fixture has ten effective armor");
            Check.Nearly(4, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(Source(enemy.StatusEffects, 1), enemy.EffectiveArmor),
                "the unpiercing source's selection score excludes shield and Expose");
            Check.Nearly(18, StatusEffectController.ArmorAdjustedBurnDamagePerSecond(Source(enemy.StatusEffects, 3), enemy.EffectiveArmor),
                "the piercing source retains its raw selection score");
            AssertSources(enemy.GetActiveBurns(), "every shield and Expose variant selects the piercing source and lower-ID raw tie", 1, 3);

            var ticks = enemy.StatusEffects.ConsumeBurnTicks(.5f, enemy.EffectiveArmor);
            Check.True(ticks.Select(tick => tick.SourceId).SequenceEqual(new[] { 1, 3 }),
                "selected sources retain their existing insertion order when ticking");
            var healthBefore = enemy.Health;
            var shieldBefore = enemy.Shield;
            foreach (var tick in ticks)
                session.DamageResolver.Apply(enemy, new DamagePayload
                {
                    Damage = tick.Damage,
                    ArmorPierce = tick.ArmorPierce,
                    IsDamageOverTime = true,
                    SourceTowerId = tick.SourceId
                });
            Check.Nearly(expectedDamage, healthBefore - enemy.Health + shieldBefore - enemy.Shield,
                $"the selected pair resolves sequentially against shield {shield} and Expose {expose}");
            Check.Nearly(0, Source(enemy.StatusEffects, 2).TickProgress, "the unselected raw tie earns no deferred tick progress");
        }
    }

    private static StatusApplication Burn(int source, float magnitude, float duration = 10, float interval = .5f, float pierce = 0) =>
        new() { Type = StatusType.Burn, SourceId = source, Magnitude = magnitude, Duration = duration, TickInterval = interval, ArmorPierce = pierce };

    private static ActiveStatus Source(StatusEffectController statuses, int source) =>
        statuses.Active.Single(status => status.Type == StatusType.Burn && status.SourceId == source);

    private static void AssertSources(IReadOnlyList<ActiveStatus> burns, string message, params int[] sources) =>
        Check.True(burns.Select(status => status.SourceId).Order().SequenceEqual(sources.Order()), message);

    private static void ApplyCompetingBurns(EnemyInstance enemy)
    {
        enemy.StatusEffects.Apply(Burn(1, 20));
        enemy.StatusEffects.Apply(Burn(2, 14, pierce: 4));
        enemy.StatusEffects.Apply(Burn(3, 13, pierce: 4));
    }

    private static EnemyInstance Enemy(GameSession session, float armor = 0, float shield = 0, string rank = "Standard") =>
        new(1, new EnemyDefinition
        {
            Id = "burn-selection-target",
            DisplayName = "Burn selection target",
            MaxHealth = 10000,
            Speed = 1,
            Armor = armor,
            Shield = shield,
            Visual = new EnemyVisualData { Radius = 10 }
        }, session.Map.Path, 1, 1, rank);
}
