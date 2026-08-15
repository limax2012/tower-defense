# Minimal Bastion Balance Pass

> Historical isolated-combat pass. The current four-map doctrine/Protocol baseline is documented in `AUTONOMOUS_BALANCE.md`; canonical reports are listed there and supersede this early two-map snapshot.

## Scope

This pass measures the current runtime rather than relying on displayed DPS. The headless benchmark includes projectile travel, target movement, range uptime, shields, flat armor, DOT duration and tick timing, chain/splash behavior, overkill, kills, leaks, and support contribution.

The benchmark command is:

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
dotnet run --project tests\MinimalBastion.Tests -c Release -- --balance
```

## Balance summary

Raw DPS is the full direct attack payload per second against one target. For Shard Fan it assumes all pellets hit one target; for Arc Relay it excludes chain links; for Ember Coil it excludes burn damage. Effective DPS is the deterministic stationary-target result at level 1 unless noted.

| Tower | Cost | Raw DPS | Effective DPS | Main role | Major strength | Major weakness | Pass result |
| --- | ---: | ---: | ---: | --- | --- | --- | --- |
| Needle Turret | 90 | 16.0 | 16.0 | Cheap generalist | Best early reliability and cost efficiency | Short range; armor-sensitive | Attack speed 1.8 -> 2.0 APS, projectile speed 450 -> 500 |
| Frost Spire | 140 | 3.2 | 3.2 | Control/support | 35% slow for 2 seconds at level 1 | Low direct damage and poor solo killing | Slow 30% -> 35%; later slow scaling improved |
| Shard Fan | 150 | 21.6 | 21.6 | Anti-swarm | Three projectiles can be distributed across nearby enemies | Short range and flat armor is very punishing | Damage/attack speed increased; target distribution fixed |
| Watchtower | 190 | 23.0 | 23.0 | Long-range/boss damage | 250 range, Strongest targeting, fast projectile | Less cost-efficient than Needle at close range | Damage 38 -> 46; APS 0.45 -> 0.50 |
| Ember Coil | 220 | 9.0 direct | 14.9 | Persistent damage | Burn continues after the projectile and outside range | DOT is weak against armor and needs time to pay off | Burn 8 -> 6 DPS; duration 3.0 -> 2.5 seconds; explicit 0.5-second ticks |
| Breaker Cannon | 250 | 16.8 | 16.8 unarmored | Anti-armor | 6 armor pierce makes it excellent against Brutes/Aegis armor | Ordinary single-target output is only average | Retained; its niche is already meaningful |
| Signal Beacon | 300 | 0 direct | 4.0 assisted DPS | Support aura | 15% attack-speed and 10% range bonus at level 1 | No direct damage; weak with only one recipient | Aura 10% -> 15%; later aura scaling improved |
| Arc Relay | 320 | 11.1 primary | 26.0 dense aggregate | Chain/group damage | Two unique chain hops at level 1 | Poor boss damage and needs enemy density | Chain values reduced modestly; chain traversal and visuals fixed |
| Siege Mortar | 360 | 13.5 | 120.0 at 8 dense targets | Area damage | Large splash converts density into high aggregate damage | Slow projectile, poor fast-target uptime, armor-sensitive | Damage 55 -> 45; APS 0.32 -> 0.30; splash 52 -> 48 |
| Prism Beam | 450 | 21.0 | 22.1 | Sustained focused damage | Instant hits, low overkill, Exposed scaling, shield bypass at level 3 | Expensive and rapidly loses damage to flat armor | Damage/APS reduced to lower machine-gun dominance |

Upgrade costs remain data-driven and are, in order, the level 1 -> 2 and level 2 -> 3 costs:

| Tower | Upgrade costs | Level 1 -> 2 raw/effective gain per currency |
| --- | ---: | ---: |
| Needle Turret | 55 / 85 | about 6.0 / 55 = 0.109 DPS per credit |
| Shard Fan | 90 / 135 | about 12.8 / 90 = 0.142 full-target DPS per credit |
| Watchtower | 120 / 185 | about 12.4 / 120 = 0.103 DPS per credit |
| Frost Spire | 90 / 145 | about 1.9 / 90 = 0.021 direct DPS per credit, plus slow |
| Ember Coil | 140 / 210 | about 7.2 / 140 = 0.051 effective DPS per credit |
| Breaker Cannon | 160 / 240 | about 9.0 / 160 = 0.056 DPS per credit before armor advantage |
| Arc Relay | 220 / 330 | about 6.4 / 220 = 0.029 primary DPS per credit, plus chain damage |
| Siege Mortar | 240 / 360 | about 8.6 / 240 = 0.036 single-target DPS per credit, much higher in groups |
| Prism Beam | 300 / 450 | about 15.0 / 300 = 0.050 direct DPS per credit, plus Exposed |
| Signal Beacon | 210 / 330 | evaluated by assisted damage rather than direct DPS |

## Major findings

1. The apparent Ember Coil strength was primarily a runtime bug. Burn damage was consumed every frame, then the normal one-damage minimum was applied to each fractional frame slice. An 8-DPS burn therefore dealt approximately one damage every 0.02 seconds instead of 0.16 damage per frame. The level-1 benchmark fell from 58.4 to 14.9 DPS after fixing this.
2. Siege Mortar remains the strongest group specialist by design. In the eight-target dense test its level-1 aggregate DPS fell from 146.7 to 120.0, while its single-target DPS is 13.5. It is excellent when enemies cluster, but not a general-purpose boss tower.
3. Prism Beam’s fast direct hits were reliable and exposed targets, but its old 28 raw DPS and 4 attacks per second made it too close to a universal answer. Its level-1 values are now 6 damage at 3.5 APS.
4. Needle Turret was inexpensive but felt slow. Its 2.0 APS gives it a visible, reliable early-game rhythm without making it the best late-game carry.
5. Shard Fan was not actually multi-targeting. Every pellet retained the original target, so the fan did not create a real swarm niche. Pellets now select distinct nearby enemies when available and reuse the primary target only when no alternatives exist.
6. Arc Relay had damage code, but chain selection was not hop-by-hop and the only visual was a flash. It now walks from the primary target to the nearest eligible target for every hop, excludes already-hit enemies, respects the chain cap/range, and draws each damaging link.

## Changes made

- Needle Turret APS: `1.8 / 2.0 / 2.3 -> 2.0 / 2.2 / 2.5`; projectile speed: `450 / 480 / 520 -> 500 / 530 / 560`.
- Shard Fan damage: `7 / 8 / 10 -> 9 / 10 / 12`; APS: `0.65 / 0.72 / 0.80 -> 0.80 / 0.86 / 0.92`; pellets now distribute to distinct eligible targets.
- Watchtower damage: `38 / 58 / 90 -> 46 / 68 / 105`; APS: `0.45 / 0.48 / 0.52 -> 0.50 / 0.52 / 0.55`; level-3 armor pierce `6 -> 8`.
- Frost Spire slow: `30% / 38% / 45% -> 35% / 42% / 50%`.
- Ember Coil burn: `8 / 12 / 18 -> 6 / 9 / 14 DPS`; duration: `3.0 / 3.5 / 4.0 -> 2.5 / 3.0 / 3.5 seconds`; burn now ticks every `0.5 seconds` and does not receive the normal-hit minimum damage floor; level-3 direct damage `21 -> 20`.
- Arc Relay damage: `18 / 26 / 38 -> 17 / 25 / 36`; chain damage: `12 / 17 / 25 -> 11 / 15 / 22`; chain range: `90 / 100 / 110 -> 95 / 105 / 115`.
- Siege Mortar damage: `55 / 78 / 110 -> 45 / 65 / 92`; APS: `0.32 / 0.36 / 0.40 -> 0.30 / 0.34 / 0.38`; splash radius: `52 / 60 / 70 -> 48 / 56 / 64`.
- Prism Beam damage: `7 / 10 / 14 -> 6 / 9 / 12`; APS: `4.0 / 4.5 / 5.0 -> 3.5 / 4.0 / 4.5`.
- Signal Beacon aura attack speed: `10% / 17% / 25% -> 15% / 25% / 35%`; aura range: `140 / 155 / 175 -> 145 / 165 / 185`; range bonus: `8% / 12% / 18% -> 10% / 16% / 22%`.
- Breaker Cannon values were retained after the armor sweep confirmed its anti-armor advantage is already substantial and its unarmored output is not dominant.

## Armor explanation

Armor is flat subtraction, not percentage reduction:

```text
incoming = payload damage after Exposed
shield absorbs first, unless the payload ignores shields
remaining armor = max(0, base armor - armor break - armor pierce)
normal hit damage = max(1, incoming - remaining armor)
DOT tick damage = max(0, incoming - remaining armor)
```

For a normal 25-damage attack against 10 armor, 15 damage gets through. Ten damage is prevented, or 40% of the pre-floor hit. A 10-damage hit against 4 armor deals 6. A 10-damage hit against 20 armor deals the one-damage minimum. This naturally favors slow, heavy attacks over rapid low-damage attacks against armor.

DOT uses an explicit 0.5-second tick and does not use the normal-hit minimum floor. This prevents fractional frame damage from becoming excessive, while also making low-damage burn meaningfully weak against armor. A 4-damage burn tick against 4 armor deals zero.

Breaker Cannon level 1 has 24 damage, 0.70 APS, and 6 armor pierce. Against Brute armor 4 it effectively ignores the armor; against Aegis armor 8 it still has only 2 effective armor left. Needle Turret level 1, by comparison, deals 8 per hit, so it falls from 16 DPS unarmored to 8 DPS against armor 4 and 2 DPS against armor 8. Breaker remains ordinary against unarmored enemies but is 2.25 times Needle's level-1 DPS against armor 4 and 8.25 times its DPS against armor 8 in the isolated sweep. The current enemy roster therefore gives Breaker a real, understandable niche.

## Arc Relay fix

Arc Relay's primary damage path was present, so it was not wholly dead; the failure was that its chain traversal selected candidates using the original origin rather than the previous link. That made long or bent chains fail in unintuitive ways, and the flash-only effect made successful links hard to see.

The fix performs a nearest-target search for each hop, adds every selected enemy to an exclusion set, stops at the configured chain count or range, and emits a beam for the primary hit and every chain link. The regression test verifies 20 primary damage, 10 damage on each of two unique hops, no damage to an out-of-range fourth enemy, and three corresponding beam effects.

## Automated testing

The test project now contains 29 deterministic regression tests, including Arc Relay traversal, the final-wave-group crash, DOT/armor floor, two map openings, Surge Zones, branches, Overdrive, online commands/transport, reliable Pulse Plates, and wave-only forge production. The `--balance` benchmark runs headless scenarios for:

- stationary single target and high-health boss;
- dense group for splash and chain behavior;
- armored targets at 0, 4, and 8 armor;
- fast enemy with short range uptime;
- weak swarm with kills, leaks, and overkill percentage;
- two Needle Turrets with and without Signal Beacon support.

The current benchmark reports 1,090 enemies in the 20-wave set, 22,270 theoretical kill credits, 2,900 wave credits, up to 400 early-start credits, and 400 starting credits. It records damage, shield damage, armor absorbed, overkill, hits, kills, leaks, and support-assisted damage.

The enemy roster is intentionally legible: Crawler 70 HP / 70 speed / 8 reward, Runner 55 / 125 / 10, Brute 260 / 48 / 4 armor / 25, Aegis 520 / 62 / 8 armor / 100 shield / 3 lives / 45, and Regenerator 800 / 42 / 2 armor / 18 HP per second / 5 lives / 75. Wave health scales from 1.000x to 1.665x and speed from 1.000x to 1.095x. This makes the late roster favor Breaker Cannon and Prism Beam for armor/shield problems, Mortar and Arc Relay for density, and Watchtower for durable priority targets without invalidating Needle Turret as a cheap foundation.

## Remaining uncertainty

The benchmark deliberately uses repeatable geometry. It cannot decide how satisfying control, burn, or branch choices feel to a person. Full-game agents now cover both maps and many policies, but human playtests should still verify internet latency, branch legibility, actual placement coverage, and waves 14-20 where elites, Aegis, Regenerator, and the boss overlap.
