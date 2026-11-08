using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Simulation;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class EnemyReadabilityTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        ConditionComposition(content);
        VisualTransitions(content);
        HoverInspection(content);
        SelectedInspection(content);
        DebuffAuraComposition(content);
        VitalViewportBounds(content);
        SourceColoredExpose(content);
        ActiveBurnInspection(content);
    }

    private static void ActiveBurnInspection(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var enemy = new EnemyInstance(1, new EnemyDefinition
        {
            Id = "burn_intel", MaxHealth = 1000, Armor = 12
        }, session.Map.Path, 1, 1);
        session.Enemies.Add(enemy);
        ApplyBurn(1, 30, 2, 0);
        ApplyBurn(2, 14, 3, 10);
        ApplyBurn(3, 20, 8, 0);
        var checksum = SessionChecksum.Compute(session, 0);
        Check.Equal($"{24f:0.#} DAMAGE/S / {8f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Burn),
            "burn Intel sums only the two contributing sources after armor and retains the full condition lifetime");
        Check.Equal(checksum, SessionChecksum.Compute(session, 0),
            "burn inspection cannot advance active or dormant tick phases");

        enemy.GrantShield(100, 100);
        Check.Equal($"{44f:0.#} DAMAGE/S / {8f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Burn),
            "burn Intel includes shield damage from the sources selected by armor alone");
        enemy.ApplyStatus(new StatusApplication
        {
            Type = StatusType.Exposed, Magnitude = .5f, Duration = 1
        });
        Check.True(enemy.GetActiveBurns().Select(status => status.SourceId).Order().SequenceEqual(new[] { 1, 2 }),
            "shields and Expose cannot change the two sources shown in burn Intel");
        Check.Equal($"{66f:0.#} DAMAGE/S / {8f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Burn),
            "burn Intel includes Expose in the selected sources' shield damage estimate");
        enemy.AbsorbShield(enemy.Shield);
        Check.Equal($"{46f:0.#} DAMAGE/S / {8f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Burn),
            "burn Intel includes Expose before armor when estimating the selected sources' health damage");
        enemy.StatusEffects.Update(2.1f);
        Check.Equal($"{14f:0.#} DAMAGE/S / {5.9f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Burn),
            "burn Intel follows surviving sources after an active source expires");

        void ApplyBurn(int sourceId, float magnitude, float duration, float pierce) =>
            enemy.ApplyStatus(new StatusApplication
            {
                Type = StatusType.Burn, SourceId = sourceId, Magnitude = magnitude,
                Duration = duration, ArmorPierce = pierce, TickInterval = .5f
            });
    }

    private static void DebuffAuraComposition(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t5_regenerator", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.ApplyHealthDamage(40);
        enemy.TickRuntimeTimers(1.1f);
        enemy.GrantShield(20, 50);
        enemy.SetFormationSpeedMultiplier(1.2f);
        Span<EnemyCondition> zones = stackalloc EnemyCondition[EnemyDebuffAuraArt.MaximumRegions];
        Check.Equal(0, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "shield, repair and speed buffs never create aura regions");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Magnitude = 1, Duration = .55f });
        Check.Equal(0, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "Stun stops the machine without adding a fog region");
        Check.True(enemy.StatusEffects.IsStunned && enemy.CurrentSpeed == 0 &&
            EnemyConditionPresentation.IsPresent(enemy, EnemyCondition.Stun),
            "Stun retains movement and Intel state independently of fog");
        var expected = 0;
        foreach (var status in Enum.GetValues<StatusType>())
        {
            enemy.ApplyStatus(new StatusApplication { Type = status, Magnitude = .3f, Duration = 5 });
            if (status != StatusType.Stun) expected++;
            Check.Equal(expected, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "only sustained debuffs add fog regions");
        }
        var before = SessionChecksum.Compute(session, 0);
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = 2, Duration = 5, SourceId = 99 });
        Check.Equal(4, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "multiple source stacks share one debuff color zone");
        foreach (var condition in zones)
            Check.True(condition != EnemyCondition.Stun, "Stun cannot consume a region alongside all four sustained debuffs");
        before = SessionChecksum.Compute(session, 0);
        for (var sample = 0; sample < 1000; sample++) EnemyDebuffAuraArt.CopyDebuffs(enemy, zones);
        Check.Equal(before, SessionChecksum.Compute(session, 0), "aura reads cannot alter combat state");
        Check.Equal(0, EnemyDebuffAuraArt.CopyDebuffs(enemy, Span<EnemyCondition>.Empty), "empty destinations are safe");
        enemy.StatusEffects.Update(6);
        Check.Equal(0, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "expired timed debuffs remove every aura region despite active buffs");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .3f, Duration = 5 });
        enemy.ApplyHealthDamage(enemy.MaxHealth);
        Check.Equal(0, EnemyDebuffAuraArt.CopyDebuffs(enemy, zones), "dead enemies cannot retain an aura");
        for (var layer = 0; layer < 2; layer++)
        {
            var reference = new byte[PrimitiveRenderer.DebuffFogSize * PrimitiveRenderer.DebuffFogSize];
            for (var y = 0; y < PrimitiveRenderer.DebuffFogSize; y++)
            for (var x = 0; x < PrimitiveRenderer.DebuffFogSize; x++)
                reference[y * PrimitiveRenderer.DebuffFogSize + x] = PrimitiveRenderer.SampleDebuffFog(x, y, 1, layer).A;
            foreach (var precision in new[] { false, true })
            for (var mask = 1; mask <= 15; mask++)
            {
                var alphaLevels = new HashSet<byte>();
                for (var y = 0; y < PrimitiveRenderer.DebuffFogSize; y++)
                for (var x = 0; x < PrimitiveRenderer.DebuffFogSize; x++)
                {
                    var pixel = PrimitiveRenderer.SampleDebuffFog(x, y, mask, layer, precision);
                    alphaLevels.Add(pixel.A);
                    Check.Equal(reference[y * PrimitiveRenderer.DebuffFogSize + x], pixel.A,
                        "Every color combination and Expose source uses the identical density shape");
                    Check.True(pixel.A < 160, "fog remains translucent beneath the opacity multiplier");
                    if (x == 0 || y == 0 || x == PrimitiveRenderer.DebuffFogSize - 1 || y == PrimitiveRenderer.DebuffFogSize - 1)
                        Check.Equal((byte)0, pixel.A, "fog atlas edges stay transparent without rectangular seams");
                }
                Check.True(alphaLevels.Count > 60, "fog retains continuous soft density for every color assignment");
            }
        }
        AssertColor(new(.75f, 0), 3, ColorPalette.Burn, "Two colors split vertically, with Burn on the right");
        AssertColor(new(-.75f, 0), 3, ColorPalette.Slow, "Two colors split vertically, with Slow on the left");
        AssertColor(new(.65f, -.375f), 7, ColorPalette.Burn, "The first three-color sector is upper-right");
        AssertColor(new(0, .75f), 7, ColorPalette.Slow, "The second three-color sector is centered downward");
        AssertColor(new(-.65f, -.375f), 7, ColorPalette.ArmorBreak, "The third three-color sector is upper-left");
        AssertColor(new(.6f, -.6f), 15, ColorPalette.Burn, "Four colors start in the upper-right quadrant");
        AssertColor(new(.6f, .6f), 15, ColorPalette.Slow, "Four colors use the lower-right quadrant");
        AssertColor(new(-.6f, .6f), 15, ColorPalette.ArmorBreak, "Four colors use the lower-left quadrant");
        AssertColor(new(-.6f, -.6f), 15, ColorPalette.ExposeFog, "Four colors use the upper-left quadrant");

        static void AssertColor(Vector2 point, int mask, Color color, string description) =>
            Check.True(Vector3.Distance(PrimitiveRenderer.DebuffFogColor(point, mask), color.ToVector3()) < .001f, description);
    }

    private static void VitalViewportBounds(GameContent content)
    {
        foreach (var viewport in new[] { new Rectangle(0, 56, 960, 664), new Rectangle(21, 60, 278, 608), new Rectangle(981, 60, 278, 608) })
        foreach (var origin in new[] { new Vector2(-100), new Vector2(2000), viewport.Center.ToVector2() })
        {
            var center = EnemyHudPresentation.ClampMeterOrigin(origin, 90, 3, 7, viewport);
            Check.True(center.X - 45 >= viewport.Left + 3 && center.X + 45 <= viewport.Right - 3 &&
                center.Y - 3 >= viewport.Top + 2 && center.Y + 7 <= viewport.Bottom - 2,
                "meters respect gameplay and either independent menu feed viewport");
        }
    }

    private static void SourceColoredExpose(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var prism = Place("prism_beam");
        var watchtower = Place("watchtower");
        session.SpawnEnemy("t3_brute", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Duration = 3, Magnitude = .2f, SourceId = prism });
        Check.Equal(ColorPalette.Violet, EnemyConditionPresentation.ExposeAccent(session, enemy),
            "Prism's sustained weak-point incision retains its violet source language");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Duration = 3, Magnitude = .3f, SourceId = watchtower });
        Check.Equal(ColorPalette.PrecisionShot, EnemyConditionPresentation.ExposeAccent(session, enemy),
            "the strongest active Watchtower mark uses the actual precision source color");
        Check.True(session.TrySellTower(watchtower), "source fixture can sell its Watchtower");
        var checksum = SessionChecksum.Compute(session, 0);
        Check.Equal(ColorPalette.PrecisionShot, EnemyConditionPresentation.ExposeAccent(session, enemy),
            "existing attribution retains the color after its applying tower is sold");
        Check.Equal(checksum, SessionChecksum.Compute(session, 0), "source color lookup is read-only");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Duration = 3, Magnitude = .3f, SourceId = prism });
        Check.Equal(ColorPalette.Violet, EnemyConditionPresentation.ExposeAccent(session, enemy),
            "a refreshed mark follows the active status controller's current source");

        int Place(string definition)
        {
            foreach (var region in session.Map.BuildableRegions)
            for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
            for (var x = region.Left + 24; x < region.Right - 16; x += 24)
                if (session.TryPlaceTower(definition, new Vector2(x, y))) return session.SelectedTower!.Id;
            throw new InvalidOperationException($"Cannot place {definition} for the source-color fixture.");
        }
    }

    private static void ConditionComposition(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t5_regenerator", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.ApplyHealthDamage(100);
        enemy.TickRuntimeTimers(1.1f);
        enemy.GrantShield(80, 160);
        enemy.SetFormationSpeedMultiplier(1.2f);
        foreach (var type in Enum.GetValues<StatusType>())
            enemy.ApplyStatus(new StatusApplication { Type = type, Duration = 4, Magnitude = type == StatusType.Burn ? 12 : .3f });
        var before = SessionChecksum.Compute(session, 0);
        var composition = EnemyConditionPresentation.Compose(enemy);
        Check.Equal(8, composition.ActiveCount, "all debuffs and innate or formation conditions remain inspectable");
        Span<EnemyCondition> details = stackalloc EnemyCondition[8];
        Check.Equal(8, EnemyConditionPresentation.CopyActive(enemy, details), "inspection exposes the complete active set");
        foreach (var condition in details)
        {
            Check.True(composition.Has(condition), "inspection composition shares the complete active set");
            Check.True(!string.IsNullOrWhiteSpace(EnemyConditionPresentation.Detail(enemy, condition)),
                "each existing condition has useful inspection detail");
        }
        Span<EnemyCondition> limited = stackalloc EnemyCondition[1];
        Check.Equal(1, EnemyConditionPresentation.CopyActive(enemy, limited), "bounded inspection destinations are respected");
        Check.Equal(EnemyCondition.Stun, limited[0], "bounded inspection preserves tactical priority");
        Check.Equal(0, EnemyConditionPresentation.CopyActive(enemy, Span<EnemyCondition>.Empty), "empty inspection buffers are safe");
        Check.Equal(ColorPalette.EmberShot, EnemyConditionPresentation.Accent(EnemyCondition.Burn), "burn retains Ember red");
        Check.Equal(ColorPalette.BreakerShot, EnemyConditionPresentation.Accent(EnemyCondition.ArmorBreak), "fractures retain Breaker gold");
        Check.Equal(ColorPalette.ArcShot, EnemyConditionPresentation.Accent(EnemyCondition.Stun), "stun retains Arc electric blue");
        Check.Equal(ColorPalette.Violet, EnemyConditionPresentation.Accent(EnemyCondition.Exposed), "unattributed Expose uses the Prism source family");
        Check.Equal(before, SessionChecksum.Compute(session, 0), "all visual composition and detail queries leave gameplay unchanged");

        enemy.StatusEffects.Update(4.1f);
        enemy.AbsorbShield(enemy.Shield);
        enemy.SetFormationSpeedMultiplier(1);
        enemy.Regenerate(1000);
        Check.Equal(0, EnemyConditionPresentation.Compose(enemy).ActiveCount,
            "expired debuffs, shield depletion, full repair, and formation exit remove every inspected condition");
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 2, Magnitude = .3f });
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Duration = 2, Magnitude = 12 });
        composition = EnemyConditionPresentation.Compose(enemy);
        Check.Equal(2, composition.ActiveCount, "burn does not consume or conceal frost state");
    }

    private static void VisualTransitions(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        session.SpawnEnemy("t4_aegis", 1, 1, "Boss");
        var enemy = session.Enemies.Single();
        var timeline = new EnemyVisualTimeline();
        timeline.Observe(session, 0);
        Check.Nearly(0, timeline.Feedback(enemy, 0).OverdriveTransition, "spawn does not invent a boss transition");
        enemy.AbsorbShield(20);
        timeline.Observe(session, .1f);
        Check.Nearly(1, timeline.Feedback(enemy, .1f).ShieldImpact, "shield damage starts a local contour response");
        enemy.AbsorbShield(enemy.Shield);
        timeline.Observe(session, .2f);
        Check.Nearly(1, timeline.Feedback(enemy, .2f).ShieldBreak, "shield depletion starts a short breakup response");
        session.DamageResolver.Apply(enemy, new DamagePayload
        {
            Damage = enemy.MaxHealth * .6f, ArmorPierce = 1000, IgnoreShield = true
        });
        Check.True(enemy.BossPhaseActive, "the existing health threshold activates boss overdrive");
        var before = SessionChecksum.Compute(session, 0);
        timeline.Observe(session, .25f);
        var opening = timeline.Feedback(enemy, .25f);
        Check.Nearly(0, opening.OverdriveOpening, "newly observed overdrive begins with closed mechanical panels");
        Check.Nearly(1, opening.OverdriveTransition, "overdrive has one transition release");
        timeline.Observe(session, .425f);
        Check.Nearly(.5f, timeline.Feedback(enemy, .425f).OverdriveOpening, "panels open continuously during the transition");
        timeline.Observe(session, 1);
        var settled = timeline.Feedback(enemy, 1);
        Check.Nearly(1, settled.OverdriveOpening, "overdrive panels settle fully open");
        Check.Nearly(0, settled.OverdriveTransition, "overdrive release expires without a persistent halo");
        Check.Nearly(0, settled.ShieldBreak, "depletion fragments expire");
        Check.Equal(before, SessionChecksum.Compute(session, 0), "presentation timing never enters synchronized state");
        enemy.ApplyHealthDamage(enemy.MaxHealth);
        timeline.Observe(session, 1.1f);
        Check.Equal(0, timeline.Count, "resolved enemies leave no presentation-cache entries");
        var next = new GameSession(content, "foundry_loop", "normal", "standard");
        next.SpawnEnemy("t4_aegis", 1, 1, "Boss");
        timeline.Observe(next, 0);
        Check.Equal(1, timeline.Count, "a new session starts an independent bounded visual timeline");
        Check.Nearly(0, timeline.Feedback(next.Enemies.Single(), 0).ShieldImpact, "reused IDs inherit no shield-hit feedback");
    }

    private static void HoverInspection(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t2_runner", 1, 1);
        var enemy = session.Enemies.Single();
        enemy.SetSandboxPathDistance(150, session.Map.Path);
        var before = SessionChecksum.Compute(session, 0);
        Check.Equal(enemy, UIManager.FindEnemyIntelTarget(session, enemy.Position)!, "hover exposes an enemy in the existing intel panel");
        Check.True(UIManager.FindEnemyIntelTarget(session, new Vector2(1100, 300)) is null, "sidebar input never selects a battlefield enemy");
        Check.True(UIManager.FindEnemyIntelTarget(session, new Vector2(-1)) is null, "off-canvas input has no enemy target");
        Check.Equal(before, SessionChecksum.Compute(session, 0), "enemy inspection does not select or command synchronized entities");
        session.BeginPlacement("needle_turret");
        Check.True(UIManager.FindEnemyIntelTarget(session, enemy.Position) is null, "placement retains its existing intel priority");
        session.CancelPlacement();
        enemy.ApplyHealthDamage(enemy.MaxHealth);
        Check.True(UIManager.FindEnemyIntelTarget(session, enemy.Position) is null, "dead enemies cannot retain hover details");
    }

    private static void SelectedInspection(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var ui = new UIManager(null!);
        session.SpawnEnemy("t2_runner", 1, 1);
        session.SpawnEnemy("t3_brute", 1, 1);
        var first = session.Enemies[0];
        var second = session.Enemies[1];
        first.SetSandboxPathDistance(150, session.Map.Path);
        second.SetSandboxPathDistance(280, session.Map.Path);
        var checksum = SessionChecksum.Compute(session, 0);
        Input(Presented(first), click: true);
        Check.Equal(first, ui.SelectedEnemyIntelTarget(session)!, "click pins a live enemy for inspection");
        Check.Equal(checksum, SessionChecksum.Compute(session, 0), "enemy selection is local UI state and cannot change simulation checksums");
        first.SetSandboxPathDistance(220, session.Map.Path);
        first.ApplyHealthDamage(1);
        Input(new Vector2(1100, 600));
        Check.Equal(first, ui.EnemyIntelTarget(session, new Vector2(1100, 600))!, "moving into Intel keeps the moving, damaged enemy selected");
        Input(Presented(second));
        Check.Equal(first, ui.EnemyIntelTarget(session, Presented(second))!, "hovering another enemy does not replace a pinned target");
        Input(Presented(second), click: true);
        Check.Equal(second, ui.SelectedEnemyIntelTarget(session)!, "clicking another enemy changes the pinned target");
        Input(new Vector2(1100, 600), click: true);
        Check.Equal(second, ui.SelectedEnemyIntelTarget(session)!, "clicking the Intel panel leaves selection intact");
        Input(new Vector2(1100, 600), right: true);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "right click clears selection from anywhere");
        Input(Presented(first), click: true);
        Check.Equal(UiAction.None, Input(Vector2.Zero, escape: true), "Escape clears enemy selection before pausing");
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "Escape releases the pinned target");
        Check.Equal(UiAction.Pause, Input(Vector2.Zero, escape: true), "a subsequent Escape preserves the existing pause behavior");
        Input(Presented(first), click: true);
        Input(new Vector2(450, 350), click: true);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "clicking empty battlefield clears selection");
        Input(Presented(first), click: true);
        session.BeginPlacement("needle_turret");
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "tower placement releases enemy inspection");
        Input(Presented(second), click: true);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "placement clicks cannot select enemies");
        session.CancelPlacement();
        Input(Presented(first), click: true);
        session.BeginEmergencyPlacement();
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "tactical placement releases enemy inspection");
        session.CancelPlacement();
        Check.True(session.TryPlaceTower("needle_turret", new Vector2(270, 350)), "inspection fixture uses a legal tower foundation");
        var tower = session.SelectedTower!;
        session.CancelPlacement();
        Input(Presented(first), click: true);
        Check.Equal(first, ui.SelectedEnemyIntelTarget(session)!, "an enemy click replaces a selected tower");
        Check.True(session.SelectedTower is null, "enemy selection leaves no tower receiving hidden hotkey actions");
        Input(tower.Position, click: true);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null && session.SelectedTower == tower,
            "clicking a tower clears enemy selection and preserves its existing controls");
        Input(Presented(first), click: true);
        first.ApplyHealthDamage(first.MaxHealth);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "dead enemies release pinned inspection");
        Input(Presented(second), click: true);
        second.SetSandboxPathDistance(session.Map.Path.TotalLength, session.Map.Path);
        second.UpdateMovement(0, session.Map.Path);
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "escaped enemies release pinned inspection");
        session.SpawnEnemy("t2_runner", 1, 1);
        var third = session.Enemies.Last();
        third.SetSandboxPathDistance(150, session.Map.Path);
        Input(Presented(third), click: true);
        session.ResetSandboxExperiment();
        Check.True(ui.SelectedEnemyIntelTarget(session) is null, "removed sandbox targets cannot leave stale inspection");
        session.SpawnEnemy("t2_runner", 1, 1);
        var replacement = session.Enemies.Single();
        replacement.SetSandboxPathDistance(150, session.Map.Path);
        Input(Presented(replacement), click: true);
        var next = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        next.SpawnEnemy("t2_runner", 1, 1);
        Check.True(ui.SelectedEnemyIntelTarget(next) is null, "a new session cannot inherit enemy selection through reused IDs");

        Vector2 Presented(EnemyInstance enemy) => PresentationFrame.Create(session, 0).EnemyPosition(enemy);
        UiAction Input(Vector2 position, bool click = false, bool right = false, bool escape = false)
        {
            var input = default(InputSnapshot) with
            {
                MousePosition = position, LeftPressed = click, RightPressed = right,
                EscapePressed = escape, IsMouseOverLogicalCanvas = true, TextEntered = ""
            };
            var action = ui.HandleGameplayInput(input, session);
            if (action == UiAction.None) session.HandleWorldInput(input);
            return action;
        }
    }
}
