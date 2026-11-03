using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class BeaconSupportTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var needle = content.Towers["needle_turret"];
        var beacon = content.Towers["signal_beacon"];
        var recipient = new TowerInstance(1, needle, new Vector2(40, 0));
        var source = new TowerInstance(2, beacon, Vector2.Zero);
        var stronger = new TowerInstance(3, beacon, new Vector2(0, 40));
        Check.True(stronger.TryChooseDoctrine("beacon_amplifier"), "upgrade the overlapping support source");
        var towers = new List<TowerInstance> { recipient, source, stronger };
        var buffs = new BuffSystem();
        Check.True(!buffs.HasLiveSupport(recipient, towers), "unapplied support does not light a receiver");
        buffs.Update(towers);
        var cached = buffs.Get(recipient);
        Check.True(cached.IsActive && buffs.HasLiveSupport(recipient, towers), "active support lights its receiver");
        Check.Equal(stronger.Id, cached.AttackSpeedSourceTowerId, "the strongest source supplies the cached bonus");

        Check.True(stronger.ApplyDisruption(.3f, 0), "disrupt the strongest Beacon");
        Check.True(buffs.HasLiveSupport(recipient, towers), "an active overlapping Beacon keeps the receiver lit");
        Check.True(source.ApplyDisruption(.3f, 0), "disrupt the remaining Beacon");
        Check.True(!buffs.HasLiveSupport(recipient, towers), "receiver shuts off immediately before a buff refresh");
        Check.Equal(cached, buffs.Get(recipient), "presentation queries do not mutate cached combat bonuses");
        buffs.Update(towers);
        Check.True(!buffs.Get(recipient).IsActive, "the next simulation update removes disrupted support");
        source.TickVisual(.31f);
        buffs.Update(towers);
        Check.True(buffs.HasLiveSupport(recipient, towers), "a recovered source restores its receiver");

        source.ToggleSandboxDisabled();
        Check.True(!buffs.HasLiveSupport(recipient, towers), "disabling the only live source immediately clears its receiver");
        source.ToggleSandboxDisabled();
        Check.True(buffs.HasLiveSupport(recipient, towers), "re-enabled coverage can show an existing active buff");
        towers.Remove(source);
        Check.True(!buffs.HasLiveSupport(recipient, towers), "removing the source immediately clears its receiver");
        towers.Add(source);

        Check.True(recipient.ApplySuppression(.3f, 0), "suppress the supported tower");
        Check.True(buffs.HasLiveSupport(recipient, towers), "suppression preserves valid support");
        recipient.TickVisual(.31f);
        Check.True(recipient.ApplyDisruption(.3f, 0), "disrupt the supported tower");
        Check.True(!buffs.HasLiveSupport(recipient, towers), "disrupted recipients do not display support");
        recipient.TickVisual(.31f);
        recipient.ToggleSandboxDisabled();
        Check.True(!buffs.HasLiveSupport(recipient, towers), "disabled recipients do not display support");
        recipient.ToggleSandboxDisabled();

        var edgeRecipient = new TowerInstance(4, needle, new Vector2(source.EffectiveAuraRange, 0));
        var outerRecipient = new TowerInstance(5, needle, new Vector2(source.EffectiveAuraRange + 1, 0));
        towers = new List<TowerInstance> { source, edgeRecipient, outerRecipient };
        buffs.Update(towers);
        Check.True(buffs.HasLiveSupport(edgeRecipient, towers) && !buffs.HasLiveSupport(outerRecipient, towers),
            "support includes the exact boundary and excludes recipients outside it");
        source.ActivateOverdrive();
        buffs.Update(towers);
        Check.True(buffs.HasLiveSupport(outerRecipient, towers), "Protocol expansion powers an outer recipient");
        source.TickVisual(source.Protocol.DurationSeconds + .1f);
        Check.True(buffs.Get(outerRecipient).IsActive && !buffs.HasLiveSupport(outerRecipient, towers),
            "Protocol coverage expiry immediately removes outer receiver lighting before the next buff refresh");
        Check.True(buffs.HasLiveSupport(edgeRecipient, towers), "base coverage stays lit when Protocol coverage expires");

        var level = beacon.Levels[0];
        var attackSpeed = level.AuraAttackSpeedBonus;
        var range = level.AuraRangeBonus;
        try
        {
            level.AuraAttackSpeedBonus = 0;
            level.AuraRangeBonus = 0;
            Check.True(!buffs.HasLiveSupport(edgeRecipient, towers), "a source providing no bonuses does not light receivers");
        }
        finally
        {
            level.AuraAttackSpeedBonus = attackSpeed;
            level.AuraRangeBonus = range;
        }
    }
}
