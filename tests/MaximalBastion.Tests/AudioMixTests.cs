using MaximalBastion.Audio;
using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Tactics;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class AudioMixTests
{
    public static void Run()
    {
        NonfiniteControls();
        CriticalAdmissionUnderNotifications();
        MixedTierBeamContinuity();
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        DirectorSubscriptions(content);
        DamageTicksAndPause(content);
        ConstructionAndTacticalCues(content);
        WaveAndThreatCues(content);
        PrismPulseCadence(content);
    }

    private static void NonfiniteControls()
    {
        var mixer = new SoundMixer();
        var request = new SoundRequest(new SoundKey(null, Cue: SoundCue.UiConfirm), .3f,
            SoundCategory.Interface, Pan: float.NaN, Pitch: float.PositiveInfinity, Cooldown: float.NaN);
        foreach (var gain in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
            Check.True(!mixer.TryPlay(request with { Gain = gain }), "invalid gain cannot allocate an audio voice");
        Check.True(mixer.TryPlay(request), "finite gain survives invalid optional controls");
        var voice = mixer.Voices.Single();
        Check.Nearly(0, voice.Pan, "nonfinite pan defaults to center");
        Check.Nearly(0, voice.Pitch, "nonfinite pitch defaults to original speed");
        foreach (var elapsed in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
            mixer.Update(elapsed);
        Check.Nearly(0, voice.Age, "invalid clock values cannot poison voice lifetimes");
        Check.True(float.IsFinite(voice.OutputGain), "playback gain stays finite");
        mixer.Update(1);
        Check.Equal(0, mixer.Voices.Count, "finite elapsed time still retires the voice");
    }

    private static void CriticalAdmissionUnderNotifications()
    {
        var mixer = new SoundMixer();
        foreach (var cue in new[] { SoundCue.Apex, SoundCue.Protocol, SoundCue.WaveStart, SoundCue.ForgeUpgrade })
            mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: cue), .8f, SoundCategory.Notification));
        Check.True(mixer.Voices.Sum(voice => voice.Gain * voice.Sample.Peak) > .5f,
            "overlapping construction and transition cues exercise the peak ceiling");
        Check.True(mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: SoundCue.Defeat), .72f,
            SoundCategory.Critical)), "terminal warnings can reclaim lower-priority notification headroom");
        Check.True(mixer.Voices.Any(voice => voice.Key.Cue == SoundCue.Defeat), "the warning owns an audible voice");
        Check.True(mixer.Voices.Sum(voice => voice.Gain * voice.Sample.Peak) <= SoundMixer.TotalPeakBudget + .0001f,
            "priority reclamation preserves the worst-case peak ceiling");

        mixer.Clear();
        foreach (var cue in Enum.GetValues<SoundCue>())
            mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: cue), .02f, SoundCategory.Notification));
        Check.Equal(SoundMixer.MaximumVoices, mixer.Voices.Count, "quiet notifications exercise the voice-count ceiling");
        Check.True(mixer.TryPlay(new SoundRequest(new SoundKey("watchtower"), .02f, SoundCategory.Critical)),
            "critical admission can reclaim a lower-priority slot independently of peak headroom");
        Check.Equal(SoundMixer.MaximumVoices, mixer.Voices.Count, "priority replacement cannot exceed the voice-count ceiling");

        mixer.Clear();
        var destruction = new SoundRequest(new SoundKey(null, Cue: SoundCue.KillLarge), .25f,
            SoundCategory.Detail, Cooldown: .5f);
        Check.True(mixer.TryPlay(destruction), "ordinary large destruction starts a restrained detail cue");
        var bossDestruction = destruction with { Gain = .58f, Category = SoundCategory.Critical };
        Check.True(mixer.TryPlay(bossDestruction), "boss destruction overrides the ordinary destruction cooldown");
        Check.True(!mixer.TryPlay(bossDestruction), "same-priority critical repeats still respect their cooldown");
    }

    private static void MixedTierBeamContinuity()
    {
        var mixer = new SoundMixer();
        var low = new SoundRequest(new SoundKey("prism_beam", WeaponSoundLayer.Sustain), .23f,
            Cooldown: 0, HoldSeconds: .15f);
        Check.True(mixer.TryPlay(low), "the first optical carrier starts");
        var id = mixer.Voices.Single().Id;
        mixer.Update(.06f);
        var high = low with { Key = low.Key with { LevelIndex = 2, Apex = true } };
        for (var frame = 0; frame < 20; frame++)
        {
            mixer.TryPlay(high);
            mixer.TryPlay(low);
            mixer.Update(.016f);
            Check.Equal(1, mixer.Voices.Count, "mixed-tier optical emitters share bounded sustained audio");
            Check.Equal(id, mixer.Voices[0].Id, "mixed-tier renewals preserve carrier phase during an engagement");
        }
        mixer.StopCombat();
        mixer.Update(.04f);
        Check.True(mixer.Voices.Single().Envelope is > 0 and < 1, "pause releases the shared carrier without a hard cut");
        mixer.Update(.06f);
        Check.Equal(0, mixer.Voices.Count, "pause reaches silence after the release tail");
        mixer.TryPlay(high);
        Check.True(mixer.Voices.Single().Key.Apex, "a new engagement can adopt the newly active Apex emitter");
        mixer.Update(.3f);
        Check.Equal(0, mixer.Voices.Count, "target loss releases a carrier without another gameplay event");
    }

    private static void DirectorSubscriptions(GameContent content)
    {
        var first = Session(content);
        var second = Session(content);
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        director.Attach(first);
        director.Attach(first);
        Fire(first);
        Check.Equal(1, requests.Count, "repeated attach cannot duplicate launch subscriptions");
        director.Detach();
        Fire(first);
        first.DamageResolver.Apply(first.Enemies[0], new DamagePayload { Damage = 1 });
        first.OnBossPhaseChanged(first.Enemies[0]);
        Check.Equal(1, requests.Count, "detached sessions cannot emit combat or notification cues");
        director.Attach(first);
        Fire(first);
        Check.Equal(2, requests.Count, "reattaching restores exactly one subscription");
        director.Attach(second);
        Fire(first);
        Check.Equal(2, requests.Count, "replaced co-op sessions cannot emit through stale subscriptions");
        Fire(second);
        Check.Equal(3, requests.Count, "the replacement session owns the director");
        director.Detach();
    }

    private static void DamageTicksAndPause(GameContent content)
    {
        var session = Session(content);
        var target = session.Enemies[0];
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        director.Attach(session);
        for (var tick = 0; tick < 20; tick++)
            session.DamageResolver.Apply(target, new DamagePayload { Damage = 1, IsDamageOverTime = true });
        Check.Equal(0, requests.Count, "routine burn ticks do not generate weapon or damage chatter");
        target.GrantShield(20, 20);
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 1 });
        Check.Equal(SoundCue.ShieldHit, requests.Single().Key.Cue, "a direct shield contact has restrained feedback");
        requests.Clear();
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 1, IsDamageOverTime = true });
        Check.Equal(0, requests.Count, "ongoing shield burn ticks remain quiet");
        session.DamageResolver.Apply(target, new DamagePayload { Damage = 30, IsDamageOverTime = true });
        Check.Equal(SoundCue.ShieldBreak, requests.Single().Key.Cue, "a shield transition remains audible when caused by burn");
        requests.Clear();
        director.Update(.016f, false);
        Fire(session);
        Check.Equal(0, requests.Count, "paused presentation suppresses combat requests");
        director.Update(.016f, true);
        Fire(session);
        Check.Equal(1, requests.Count, "resumed presentation restores weapon feedback");
        director.Detach();
    }

    private static void ConstructionAndTacticalCues(GameContent content)
    {
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        var sandbox = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        director.Attach(sandbox);
        Expect(SoundCue.Place, () => sandbox.TryPlaceTower("needle_turret", new Vector2(45, 200)));
        var tower = sandbox.Towers.Single();
        Expect(SoundCue.Upgrade, () => sandbox.TryChooseTowerDoctrine(tower.Id, tower.Definition.Tier2Doctrines[0].Id));
        Expect(SoundCue.Upgrade, () => sandbox.TrySpecializeTower(tower.Id, tower.Definition.Specializations[0].Id));
        Expect(SoundCue.Apex, () => sandbox.TryUpgradeTower(tower.Id));
        Expect(SoundCue.Protocol, () => sandbox.TryOverdriveTower(tower.Id));
        Check.Equal(.9f, requests.Single().Gain, "activation retains the full short-hum playback gain");
        Check.Equal(.04f, requests.Single().Pitch, "activation retains the Needle family transposition");
        Expect(SoundCue.Sell, () => sandbox.TrySellTower(tower.Id));
        Check.True(sandbox.SpawnSandboxTargets("t1_crawler", 1, 1, "Standard", true), "plate cue target spawns");
        sandbox.Enemies[0].SetSandboxPathDistance(240, sandbox.Map.Path);
        Expect(SoundCue.PlateDeploy, () => sandbox.TryDeployEmergencyDefense(sandbox.Map.Path.GetPosition(240)));
        requests.Clear();
        new TacticalDefenseSystem().Update(.21f, sandbox);
        Check.Equal(SoundCue.PlateTrigger, requests.Single().Key.Cue, "a real armed plate pulse emits one trigger cue");

        var campaign = new GameSession(content, "foundry_loop", "normal", "standard");
        campaign.Economy.AddCredits(10000);
        director.Attach(campaign);
        Expect(SoundCue.ForgePlace, () => campaign.TryPlaceGenerator(new Vector2(45, 200)));
        Expect(SoundCue.ForgeUpgrade, () => campaign.TryUpgradeGenerator());
        Expect(SoundCue.WaveStart, () => campaign.StartNextWave());
        requests.Clear();
        var tactical = new TacticalDefenseSystem();
        tactical.Update(campaign.Generator!.ProductionRemaining + .1f, campaign);
        Check.Equal(SoundCue.ChargeReady, requests.Single().Key.Cue, "a completed production cycle emits one charge cue");
        while (campaign.EmergencyInventory < campaign.Generator.Level.Capacity)
            tactical.Update(campaign.Generator.ProductionRemaining + .1f, campaign);
        requests.Clear();
        tactical.Update(100, campaign);
        Check.Equal(0, requests.Count, "a full Forge does not repeat charge completion cues");
        Expect(SoundCue.Sell, () => campaign.TrySellGenerator());
        director.Detach();

        void Expect(SoundCue cue, Func<bool> operation)
        {
            requests.Clear();
            Check.True(operation(), cue + " operation succeeds");
            Check.Equal(cue, requests.Single().Key.Cue, cue + " operation emits exactly one matching cue");
        }
    }

    private static void WaveAndThreatCues(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        director.Attach(session);
        Expect(SoundCue.WaveClear, () => session.OnWaveCompleted(1));
        Expect(SoundCue.Victory, () => session.OnWaveCompleted(session.TotalWaves));
        Expect(SoundCue.BossArrival, () => session.SpawnEnemy("t5_regenerator", 1, 0, "Boss"));
        var boss = session.Enemies.Single();
        Expect(SoundCue.BossPhase, () => session.DamageResolver.Apply(boss,
            new DamagePayload { Damage = boss.MaxHealth * .51f + boss.BaseArmor, IgnoreShield = true }));
        Expect(SoundCue.KillLarge, () => session.DamageResolver.Apply(boss,
            new DamagePayload { Damage = boss.MaxHealth * 2, IgnoreShield = true }));
        session.SpawnEnemy("t1_crawler", 1, 1);
        Expect(SoundCue.Leak, () => session.OnEnemyEscaped(session.Enemies[^1]));
        session.Economy.LoseLives(session.Economy.Lives - 1);
        session.SpawnEnemy("t1_crawler", 1, 1);
        Expect(SoundCue.Defeat, () => session.OnEnemyEscaped(session.Enemies[^1]));
        requests.Clear();
        session.SpawnEnemy("t1_crawler", 1, 1);
        session.OnEnemyEscaped(session.Enemies[^1]);
        Check.Equal(0, requests.Count, "additional terminal breaches cannot restart defeat feedback");
        director.Detach();

        void Expect(SoundCue cue, Action operation)
        {
            requests.Clear();
            operation();
            Check.Equal(cue, requests.Single().Key.Cue, cue + " transition emits exactly one matching cue");
        }
    }

    private static void PrismPulseCadence(GameContent content)
    {
        foreach (var speed in new[] { 1, 2 })
        {
            var session = Session(content, "prism_beam");
            session.SetSpeed(speed);
            var director = new AudioDirector();
            var mixer = new SoundMixer();
            var attacks = 0;
            var requests = new List<SoundRequest>();
            var starts = 0;
            session.TowerFired += _ => attacks++;
            director.Requested += request =>
            {
                if (request.Key.WeaponId != "prism_beam") return;
                requests.Add(request);
                mixer.TryPlay(request);
            };
            mixer.Started += _ => starts++;
            director.Attach(session);
            for (var frame = 0; frame < 120; frame++)
            {
                director.Update(1f / 60);
                mixer.Update(1f / 60);
                session.Update(1f / 60);
            }
            Check.True(attacks >= 6 && starts >= 6, "Prism emits discrete repeated zaps at both gameplay speeds");
            Check.Equal(attacks, requests.Count, "each Prism attack requests one zap with no doubled contact");
            Check.True(requests.All(request => request.Key.Layer == WeaponSoundLayer.Shot && request.HoldSeconds == 0),
                "Prism attacks never request a continuous harmonic carrier");
            session.Towers.Clear();
            director.Update(.5f);
            mixer.Update(.5f);
            Check.Equal(0, mixer.Voices.Count, "Prism reaches silence between engagements");
            director.Detach();
        }
    }

    private static GameSession Session(GameContent content, string family = "needle_turret")
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var target = new EnemyInstance(1, content.Enemies["t1_crawler"], session.Map.Path, 1000, 0);
        session.Enemies.Add(target);
        session.Towers.Add(new TowerInstance(1, content.Towers[family], target.Position + new Vector2(0, 50)));
        return session;
    }

    private static void Fire(GameSession session) =>
        Check.True(new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session),
            "the audio fixture has a valid target");
}
