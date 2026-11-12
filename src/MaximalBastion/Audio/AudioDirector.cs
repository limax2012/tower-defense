using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Tactics;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Audio;

/// <summary>Maps local presentation events to sound requests without changing synchronized game state.</summary>
internal sealed class AudioDirector
{
    private GameSession? _session;
    private bool _combatEnabled = true;
    private bool _defeatPlayed;
    public event Action<SoundRequest>? Requested;

    public void Update(float deltaSeconds, bool combatEnabled = true)
    {
        _combatEnabled = combatEnabled && _session?.IsCoOpPaused != true;
    }

    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_session, session)) return;
        Detach();
        _session = session;
        _combatEnabled = true;
        session.TowerFired += OnTowerFired;
        session.WeaponImpacted += OnWeaponImpacted;
        session.TowerPlaced += OnPlaced;
        session.TowerUpgraded += OnUpgraded;
        session.TowerSold += OnSold;
        session.ProtocolActivated += OnProtocol;
        session.EnemyKilled += OnKilled;
        session.EnemyEscaped += OnEscaped;
        session.EnemySpawned += OnSpawned;
        session.BossPhaseChanged += OnBossPhase;
        session.EmergencyDefenseDeployed += OnPlateDeployed;
        session.EmergencyDefenseTriggered += OnPlateTriggered;
        session.GeneratorPlaced += OnForgePlaced;
        session.GeneratorUpgraded += OnForgeUpgraded;
        session.GeneratorSold += OnForgeSold;
        session.EmergencyChargeProduced += OnChargeReady;
        session.WaveStarted += OnWaveStarted;
        session.WaveCompleted += OnWaveCompleted;
        session.DamageResolver.DamageApplied += OnDamageApplied;
    }

    public void Detach()
    {
        if (_session is { } session)
        {
            session.TowerFired -= OnTowerFired;
            session.WeaponImpacted -= OnWeaponImpacted;
            session.TowerPlaced -= OnPlaced;
            session.TowerUpgraded -= OnUpgraded;
            session.TowerSold -= OnSold;
            session.ProtocolActivated -= OnProtocol;
            session.EnemyKilled -= OnKilled;
            session.EnemyEscaped -= OnEscaped;
            session.EnemySpawned -= OnSpawned;
            session.BossPhaseChanged -= OnBossPhase;
            session.EmergencyDefenseDeployed -= OnPlateDeployed;
            session.EmergencyDefenseTriggered -= OnPlateTriggered;
            session.GeneratorPlaced -= OnForgePlaced;
            session.GeneratorUpgraded -= OnForgeUpgraded;
            session.GeneratorSold -= OnForgeSold;
            session.EmergencyChargeProduced -= OnChargeReady;
            session.WaveStarted -= OnWaveStarted;
            session.WaveCompleted -= OnWaveCompleted;
            session.DamageResolver.DamageApplied -= OnDamageApplied;
        }
        _session = null;
        _defeatPlayed = false;
    }

    private void OnTowerFired(TowerInstance tower)
    {
        if (!_combatEnabled || _session is null) return;
        var family = tower.Definition.Id;
        var key = new SoundKey(family, WeaponSoundLayer.Shot, LevelIndex: tower.LevelIndex, Apex: tower.IsApex);
        var gain = family switch
        {
            "needle_turret" => 0.15f, "watchtower" => 0.30f,
            "breaker_cannon" => 0.32f, "siege_mortar" => 0.29f,
            "ember_coil" => 0.24f, "prism_beam" => 0.22f, _ => 0.25f
        };
        Request(new SoundRequest(key, gain, Pan: Pan(tower.Position),
            Cooldown: AudioManager.CombatCueCooldown(family)));
        var support = _session.GetSupportBuff(tower);
        var beaconId = support.AttackSpeedSourceTowerId > 0 ? support.AttackSpeedSourceTowerId : support.RangeSourceTowerId;
        var beacon = beaconId > 0 ? _session.Towers.FirstOrDefault(source => source.Id == beaconId) : null;
        if (beacon is not null)
            Request(new SoundRequest(new SoundKey("signal_beacon", WeaponSoundLayer.Shot,
                LevelIndex: beacon.LevelIndex, Apex: beacon.IsApex), 0.11f,
                SoundCategory.Detail, Pan(beacon.Position), Cooldown: 1.15f));
    }

    private void OnWeaponImpacted(WeaponImpact impact)
    {
        if (!_combatEnabled || !impact.Appearance.IsKnown) return;
        var family = impact.Appearance.TowerId!;
        // Each beam attack is one zap; its immediate contact must not double that cue.
        if (family == "prism_beam") return;
        var chain = family == "arc_relay" && impact.SecondaryHitCount > 0;
        var key = new SoundKey(family, chain ? WeaponSoundLayer.Chain : WeaponSoundLayer.Impact,
            LevelIndex: impact.Appearance.Level - 1, Apex: impact.Appearance.IsApex);
        var gain = family switch
        {
            "needle_turret" => 0.065f, "siege_mortar" => 0.34f, "breaker_cannon" => 0.27f,
            "watchtower" => 0.22f, "arc_relay" => chain ? 0.20f : 0.12f, _ => 0.17f
        };
        Request(new SoundRequest(key, gain, Pan: Pan(impact.Position),
            Cooldown: MathF.Max(0.075f, AudioManager.CombatCueCooldown(family))));
    }

    private void OnDamageApplied(DamageReport report)
    {
        if (!_combatEnabled || report.Target is null || report.ShieldDamage <= 0) return;
        if (report.ShieldBroken)
            Cue(SoundCue.ShieldBreak, 0.28f, SoundCategory.Detail, 0.22f, report.Target.Position);
        else if (!report.IsDamageOverTime)
            Cue(SoundCue.ShieldHit, 0.12f, SoundCategory.Detail, 0.16f, report.Target.Position);
    }

    private void OnKilled(EnemyInstance enemy)
    {
        if (!_combatEnabled) return;
        var cue = enemy.IsBoss || enemy.Definition.Id == "t5_regenerator" ? SoundCue.KillLarge :
            enemy.BaseArmor >= 4 || enemy.IsElite ? SoundCue.KillArmored : SoundCue.KillSmall;
        Cue(cue, enemy.IsBoss ? 0.58f : cue == SoundCue.KillLarge ? 0.25f : 0.16f,
            enemy.IsBoss ? SoundCategory.Critical : SoundCategory.Detail,
            enemy.IsBoss ? 0.5f : 0.14f, enemy.Position);
    }

    private void OnEscaped(EnemyInstance enemy)
    {
        if (_session?.IsSandbox != true && _session?.Economy.Lives <= 0)
        {
            if (_defeatPlayed) return;
            _defeatPlayed = true;
            Cue(SoundCue.Defeat, 0.95f, SoundCategory.Critical, 1f);
        }
        else Cue(SoundCue.Leak, enemy.IsBoss ? 0.66f : 0.42f, SoundCategory.Critical, 0.22f);
    }

    private void OnSpawned(EnemyInstance enemy)
    {
        if (enemy.IsBoss) Cue(SoundCue.BossArrival, 0.65f, SoundCategory.Critical, 1.2f);
    }
    private void OnBossPhase(EnemyInstance enemy) => Cue(SoundCue.BossPhase, 0.72f, SoundCategory.Critical, 0.5f);
    private void OnPlaced(TowerInstance tower) => Cue(SoundCue.Place, 0.52f, position: tower.Position);
    private void OnUpgraded(TowerInstance tower, int cost) => Cue(tower.IsApex ? SoundCue.Apex : SoundCue.Upgrade,
        tower.IsApex ? 0.72f : 0.56f, position: tower.Position);
    private void OnSold(TowerInstance tower, int refund) => Cue(SoundCue.Sell, 0.44f, position: tower.Position);
    private void OnProtocol(TowerInstance tower, ProtocolActivationOrigin origin)
    {
        var automatic = origin is ProtocolActivationOrigin.ArmedAutomatic or ProtocolActivationOrigin.ApexAutomatic;
        if (automatic && !_combatEnabled) return;
        Cue(SoundCue.Protocol, automatic ? 0.34f : 0.52f,
            automatic ? SoundCategory.Combat : SoundCategory.Notification,
            automatic ? 0.65f : 0.20f, tower.Position, ProtocolPitch(tower.Definition.Id));
    }
    private void OnPlateDeployed(PulsePlateInstance plate, bool purchased) => Cue(SoundCue.PlateDeploy, 0.40f, position: plate.Position);
    private void OnPlateTriggered(PulsePlateInstance plate, int hitCount) => Cue(SoundCue.PlateTrigger, 0.46f,
        SoundCategory.Combat, 0.14f, plate.Position);
    private void OnForgePlaced(ChargeForgeInstance forge) => Cue(SoundCue.ForgePlace, 0.52f, position: forge.Position);
    private void OnForgeUpgraded(ChargeForgeInstance forge, int cost) => Cue(SoundCue.ForgeUpgrade, 0.54f, position: forge.Position);
    private void OnForgeSold(ChargeForgeInstance forge, int refund) => Cue(SoundCue.Sell, 0.44f, position: forge.Position);
    private void OnChargeReady() => Cue(SoundCue.ChargeReady, 0.38f, cooldown: 0.30f);
    private void OnWaveStarted(WaveDefinition wave) => Cue(SoundCue.WaveStart, 0.58f, cooldown: 0.5f);
    private void OnWaveCompleted(int wave)
    {
        var victory = _session is { IsEndlessMode: false } && wave >= _session.TotalWaves;
        Cue(victory ? SoundCue.Victory : SoundCue.WaveClear, victory ? 0.72f : 0.55f,
            victory ? SoundCategory.Critical : SoundCategory.Notification, 0.5f);
    }

    private void Cue(SoundCue cue, float gain, SoundCategory category = SoundCategory.Notification,
        float cooldown = 0.08f, Vector2? position = null, float pitch = 0) =>
        Request(new SoundRequest(new SoundKey(null, Cue: cue), gain, category,
            position.HasValue ? Pan(position.Value) : 0, Pitch: pitch, Cooldown: cooldown));

    internal static float ProtocolPitch(string family) => family switch
    {
        "siege_mortar" => -0.25f, "breaker_cannon" => -0.16f, "ember_coil" => -0.08f,
        "watchtower" => -0.03f, "needle_turret" => 0.04f, "shard_fan" => 0.10f,
        "signal_beacon" => 0.14f, "frost_spire" => 0.18f, "arc_relay" => 0.22f,
        "prism_beam" => 0.28f, _ => 0
    };

    private void Request(SoundRequest request) => Requested?.Invoke(request);
    private static float Pan(Vector2 position) => Math.Clamp((position.X - 480) / 480 * 0.32f, -0.32f, 0.32f);
}
