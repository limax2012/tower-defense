using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class CombatAudioEventTests
{
    public static void Run()
    {
        LaunchCadence();
        DelayedAreaContact();
        CancelledContact();
        InstantContactAndChains();
        DamageReactionMetadata();
        ObserversPreserveSimulation();
    }

    private static void LaunchCadence()
    {
        var session = Session("shard_fan", "pellet_burst", new TowerLevelDefinition
        {
            Range = 200, Damage = 5, AttacksPerSecond = 2, ProjectileSpeed = 100, PelletCount = 5
        });
        AddTarget(session, 1);
        var shots = 0;
        var impacts = 0;
        session.TowerFired += _ => shots++;
        session.WeaponImpacted += _ => impacts++;
        var system = new TowerSystem(session.TargetSelector);
        system.Update(0, session);
        Check.Equal(1, shots, "one firing event represents the entire plasma fan");
        Check.Equal(5, session.Projectiles.Projectiles.Count, "fan launch still produces every pellet");
        Check.Equal(0, impacts, "launch cannot announce a travelling projectile's impact");
        system.Update(.2f, session);
        Check.Equal(1, shots, "weapon cooldown prevents spurious launch events");
        system.Update(.31f, session);
        Check.Equal(2, shots, "the next attack produces one new launch");
        Check.True(system.TryFireImmediate(session.Towers[0], session), "immediate Protocol attacks can fire");
        Check.Equal(3, shots, "Protocol immediate fire uses the same single launch event");
        session.Enemies.Clear();
        Check.True(!system.TryFireImmediate(session.Towers[0], session), "no target means no discharge");
        Check.Equal(3, shots, "failed target acquisition is silent");
    }

    private static void DelayedAreaContact()
    {
        var session = Session("siege_mortar", "splash_projectile", new TowerLevelDefinition
        {
            Range = 300, Damage = 5, AttacksPerSecond = 1, ProjectileSpeed = 100, SplashRadius = 60
        });
        AddTarget(session, 1, 100);
        AddTarget(session, 2, 110);
        AddTarget(session, 3, 120);
        var contacts = new List<WeaponImpact>();
        var damageCount = 0;
        session.WeaponImpacted += contacts.Add;
        session.DamageResolver.DamageApplied += _ => damageCount++;
        new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session);
        var firingAppearance = AttackAppearance.FromTower(session.Towers[0]);
        session.Towers.Clear();
        session.Projectiles.Update(.2f, session);
        Check.Equal(0, contacts.Count, "mortar flight remains silent before arrival");
        session.Projectiles.Update(1, session);
        Check.Equal(3, damageCount, "mortar blast still resolves every recipient");
        Check.Equal(1, contacts.Count, "one area detonation emits one impact regardless of victims");
        Check.Equal(firingAppearance, contacts[0].Appearance, "impact retains its weapon identity after sale");
        Check.Nearly(30, contacts[0].Position.Y, "impact location is the destination, not the launcher");

        session.Enemies.Clear();
        session.Projectiles.Add(new ProjectileInstance(new Vector2(100, 100), new Vector2(100, 30), null,
            100, ProjectileKind.ImpactPoint, 60, new DamagePayload { Damage = 5 }, Color.White, 4)
        {
            Appearance = firingAppearance
        });
        session.Projectiles.Update(1, session);
        Check.Equal(2, contacts.Count, "a shell detonating on empty ground still has a contact cue");
    }

    private static void CancelledContact()
    {
        var session = Session("needle_turret", "single_projectile");
        var target = AddTarget(session, 1);
        var contacts = 0;
        session.WeaponImpacted += _ => contacts++;
        new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session);
        target.ApplyHealthDamage(target.MaxHealth);
        session.Projectiles.Update(1, session);
        Check.Equal(0, contacts, "an expired homing shot cannot produce a phantom impact");
        Check.Equal(0, session.Projectiles.Projectiles.Count, "expired shots still leave the projectile system");
    }

    private static void InstantContactAndChains()
    {
        foreach (var behavior in new[] { "chain", "beam" })
        {
            var session = Session(behavior == "chain" ? "arc_relay" : "prism_beam", behavior,
                new TowerLevelDefinition
                {
                    Range = 200, Damage = 5, AttacksPerSecond = 2,
                    ChainCount = 4, ChainRange = 40, ChainDamage = 3
                });
            AddTarget(session, 1, 100);
            AddTarget(session, 2, 110);
            AddTarget(session, 3, 120);
            var order = new List<string>();
            var impacts = new List<WeaponImpact>();
            session.TowerFired += _ => order.Add("fire");
            session.DamageResolver.DamageApplied += _ => order.Add("damage");
            session.WeaponImpacted += impact => { order.Add("impact"); impacts.Add(impact); };
            new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session);
            Check.Equal("fire", order[0], "instant weapons announce firing before resolving damage");
            Check.Equal("impact", order[^1], "chain contact is announced after all hops resolve");
            Check.Equal(1, impacts.Count, "one chain attack emits one aggregate impact");
            Check.Equal(2, impacts[0].SecondaryHitCount, "chain texture describes actual hops, not the maximum");
            Check.Equal(3, order.Count(value => value == "damage"), "chain event aggregation preserves damage");
        }
    }

    private static void DamageReactionMetadata()
    {
        var session = Session("ember_coil", "burn_projectile");
        var target = AddTarget(session, 1);
        target.GrantShield(20, 20);
        var reports = new List<DamageReport>();
        var contacts = 0;
        session.DamageResolver.DamageApplied += reports.Add;
        session.WeaponImpacted += _ => contacts++;
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 5, SourceTowerId = 1 });
        Check.True(!reports[^1].IsDamageOverTime && !reports[^1].ShieldBroken,
            "a shield strike is distinct from a shield break");
        session.DamageResolver.Apply(target, new DamagePayload
        {
            Damage = 20, SourceTowerId = 1, IsDamageOverTime = true
        });
        Check.True(reports[^1].IsDamageOverTime && reports[^1].ShieldBroken,
            "burn damage retains tick identity even when it breaks a shield");
        Check.True(ReferenceEquals(target, reports[^1].Target),
            "damage reactions can inspect recipient material and scale");
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 5, SourceTowerId = 1 });
        Check.True(!reports[^1].ShieldBroken, "depleted shields do not repeatedly announce breaking");
        Check.Equal(0, contacts, "raw damage and DoT cannot synthesize weapon impacts");
    }

    private static void ObserversPreserveSimulation()
    {
        var observed = Session("needle_turret", "single_projectile");
        var silent = Session("needle_turret", "single_projectile");
        AddTarget(observed, 1);
        AddTarget(silent, 1);
        var shotCount = 0;
        var impactCount = 0;
        var damageCount = 0;
        observed.TowerFired += tower => { _ = tower.IsApex; shotCount++; };
        observed.WeaponImpacted += impact => { _ = impact.Appearance; impactCount++; };
        observed.DamageResolver.DamageApplied += report => { _ = report.Target?.Radius; damageCount++; };
        for (var tick = 0; tick < 180; tick++)
        {
            observed.Update(1f / 60f);
            silent.Update(1f / 60f);
            Check.Equal(SessionChecksum.Compute(silent, tick), SessionChecksum.Compute(observed, tick),
                "presentation observers preserve deterministic simulation on every tick");
        }
        Check.True(shotCount > 1 && impactCount > 1 && damageCount > 1,
            "checksum comparison exercises actual combat presentation events");
        Check.Equal(JsonSerializer.Serialize(silent.Projectiles.CaptureCoOpState()),
            JsonSerializer.Serialize(observed.Projectiles.CaptureCoOpState()),
            "presentation subscribers leave active projectile snapshots identical");
    }

    private static GameSession Session(string towerId, string behavior, TowerLevelDefinition? level = null)
    {
        var tower = new TowerDefinition
        {
            Id = towerId, Behavior = behavior,
            Levels = [level ?? new TowerLevelDefinition
            {
                Range = 200, Damage = 5, AttacksPerSecond = 2, ProjectileSpeed = 100
            }]
        };
        var content = new GameContent
        {
            Towers = new() { [towerId] = tower },
            Enemies = new() { ["target"] = new EnemyDefinition { Id = "target", MaxHealth = 10000, Speed = 0 } },
            Map = new MapDefinition
            {
                Id = "audio_test", Path = [new() { X = 0, Y = 30 }, new() { X = 500, Y = 30 }],
                StartingCredits = 300, StartingLives = 20
            },
            Waves = new WaveSetDefinition { Waves = [new() { Number = 1 }] }
        };
        var session = new GameSession(content);
        session.Towers.Add(new TowerInstance(1, tower, new Vector2(100, 100)));
        return session;
    }

    private static EnemyInstance AddTarget(GameSession session, int id, float distance = 100)
    {
        var enemy = new EnemyInstance(id, session.Content.Enemies["target"], session.Map.Path, 1, 1);
        enemy.SetSandboxPathDistance(distance, session.Map.Path);
        session.Enemies.Add(enemy);
        return enemy;
    }
}
