using System.Text.Json;
using MaximalBastion.Audio;
using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class ProtocolAudioTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        foreach (var origin in Enum.GetValues<ProtocolActivationOrigin>())
            ActivationOrigin(content, origin, origin == ProtocolActivationOrigin.ApexAutomatic);
        ActivationOrigin(content, ProtocolActivationOrigin.Manual, true);
        ActivationOrigin(content, ProtocolActivationOrigin.Sandbox, true);
        ManualPriorityAndPause(content);
        DensityDoesNotChangeCueGain(content);
        ObserversPreserveSimulation(content);
    }

    private static void ActivationOrigin(GameContent content, ProtocolActivationOrigin origin, bool apex)
    {
        var session = Prepare(content, apex);
        var origins = new List<ProtocolActivationOrigin>();
        var legacyCount = 0;
        var requests = new List<SoundRequest>();
        var director = new AudioDirector();
        session.ProtocolActivated += (_, source) => origins.Add(source);
        session.TowerOverdriven += _ => legacyCount++;
        director.Requested += requests.Add;
        director.Attach(session);
        Activate(session, origin);
        Check.Equal(origin, origins.Single(), "Protocol feedback reports the explicit activation origin");
        Check.Equal(1, legacyCount, "Protocol activation preserves the existing statistics event");
        var cue = requests.Single(request => request.Key.WeaponId is null && request.Key.Cue == SoundCue.Protocol);
        var automatic = origin is ProtocolActivationOrigin.ArmedAutomatic or ProtocolActivationOrigin.ApexAutomatic;
        Check.Nearly(automatic ? .34f : .52f, cue.Gain, "Protocol gain distinguishes autonomous and player actions");
        Check.Equal(automatic ? SoundCategory.Combat : SoundCategory.Notification, cue.Category,
            "automatic Protocols share routine combat priority");
        Check.Nearly(automatic ? .65f : .20f, cue.Cooldown, "automatic Protocol repetition uses a longer real-time cooldown");
        Check.Nearly(AudioDirector.ProtocolPitch(session.Towers[0].Definition.Id), cue.Pitch,
            "Protocol requests retain the weapon family's original pitch");
        Check.True(session.Towers[0].IsOverdriven, "sound origin classification cannot prevent activation");
        director.Detach();
    }

    private static void ManualPriorityAndPause(GameContent content)
    {
        var automaticSession = Prepare(content);
        var manualSession = Prepare(content);
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        director.Attach(automaticSession);
        Activate(automaticSession, ProtocolActivationOrigin.ArmedAutomatic);
        var automatic = requests.Single(request => request.Key.WeaponId is null && request.Key.Cue == SoundCue.Protocol);
        director.Attach(manualSession);
        requests.Clear();
        Activate(manualSession, ProtocolActivationOrigin.Manual);
        var manual = requests.Single();
        var mixer = new SoundMixer();
        Check.True(mixer.TryPlay(automatic), "an automatic Protocol can occupy the shared cue family");
        Check.True(mixer.TryPlay(manual), "a manual Protocol overrides an automatic family cooldown");
        Check.True(!mixer.TryPlay(automatic), "automatic feedback cannot bypass a manual Protocol cooldown");

        var pausedSession = Prepare(content, true);
        director.Attach(pausedSession);
        director.Update(.016f, false);
        requests.Clear();
        Activate(pausedSession, ProtocolActivationOrigin.ApexAutomatic);
        Check.True(pausedSession.Towers[0].IsOverdriven, "presentation pause does not alter autonomous gameplay");
        Check.Equal(0, requests.Count, "paused presentation suppresses autonomous Protocol and weapon cues");
        director.Detach();
        requests.Clear();
        Check.True(pausedSession.TestSandboxProtocol(pausedSession.Towers[0].Id), "detached session can still activate a Protocol");
        Check.Equal(0, requests.Count, "Protocol-origin subscriptions detach completely");
    }

    private static void DensityDoesNotChangeCueGain(GameContent content)
    {
        var session = Prepare(content);
        var director = new AudioDirector();
        var requests = new List<SoundRequest>();
        director.Requested += requests.Add;
        director.Attach(session);
        Fire();
        var openingGain = requests.Single(request => request.Key.WeaponId == "needle_turret").Gain;
        for (var group = 0; group < 5; group++)
            Check.True(session.SpawnSandboxTargets("t1_crawler", 20, 1, "Standard", true), "dense audio fixture spawns");
        director.Update(10);
        requests.Clear();
        Fire();
        Check.Nearly(openingGain, requests.Single(request => request.Key.WeaponId == "needle_turret").Gain,
            "enemy count cannot attenuate regular weapon requests");
        requests.Clear();
        session.SpawnEnemy("t5_regenerator", 1, 0, "Boss");
        var boss = session.Enemies[^1];
        session.DamageResolver.Apply(boss, new DamagePayload { Damage = boss.MaxHealth * 2, IgnoreShield = true });
        var destruction = requests.Single(request => request.Key.WeaponId is null && request.Key.Cue == SoundCue.KillLarge);
        Check.Nearly(.58f, destruction.Gain, "boss destruction retains its gain in a dense field");
        Check.Equal(SoundCategory.Critical, destruction.Category, "boss destruction retains critical priority");
        director.Detach();

        void Fire() => Check.True(new TowerSystem(session.TargetSelector).TryFireImmediate(session.Towers[0], session),
            "density comparison has a valid firing target");
    }

    private static void ObserversPreserveSimulation(GameContent content)
    {
        foreach (var origin in Enum.GetValues<ProtocolActivationOrigin>())
        {
            var apex = origin == ProtocolActivationOrigin.ApexAutomatic;
            var observed = Prepare(content, apex);
            var silent = Prepare(content, apex);
            var director = new AudioDirector();
            var mixer = new SoundMixer();
            director.Requested += request => mixer.TryPlay(request);
            director.Attach(observed);
            var count = 0;
            observed.ProtocolActivated += (_, _) => count++;
            Activate(observed, origin);
            Activate(silent, origin);
            for (var tick = 0; tick < 60; tick++)
            {
                director.Update(1f / 60);
                mixer.Update(1f / 60);
                observed.Update(1f / 60);
                silent.Update(1f / 60);
                Check.Equal(SessionChecksum.Compute(silent, tick), SessionChecksum.Compute(observed, tick),
                    "Protocol presentation leaves every deterministic tick unchanged");
            }
            Check.Equal(1, count, "checksum comparison exercises an actual Protocol-origin event");
            var observedState = observed.CaptureCoOpState(60, 0, false);
            var silentState = silent.CaptureCoOpState(60, 0, false);
            silentState.RunId = observedState.RunId;
            Check.Equal(JsonSerializer.Serialize(silentState), JsonSerializer.Serialize(observedState),
                "Protocol origin and playback do not add synchronized snapshot state");
            director.Detach();
        }
    }

    private static GameSession Prepare(GameContent content, bool apex = false)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        Check.True(session.TryPlaceTower("needle_turret", new Vector2(45, 200)), "Protocol fixture places a tower");
        var tower = session.Towers.Single();
        if (apex)
        {
            Check.True(session.TryChooseTowerDoctrine(tower.Id, tower.Definition.Tier2Doctrines[0].Id), "Protocol fixture chooses doctrine");
            Check.True(session.TrySpecializeTower(tower.Id, tower.Definition.Specializations[0].Id), "Protocol fixture specializes");
            Check.True(session.TryUpgradeTower(tower.Id), "Protocol fixture promotes to Apex");
        }
        Check.True(session.SpawnSandboxTargets("t1_crawler", 1, 1000, "Elite", true), "Protocol fixture spawns a priority target");
        session.Enemies[0].SetSandboxPathDistance(session.Map.Path.Project(tower.Position).DistanceAlongPath, session.Map.Path);
        return session;
    }

    private static void Activate(GameSession session, ProtocolActivationOrigin origin)
    {
        var tower = session.Towers.Single();
        switch (origin)
        {
            case ProtocolActivationOrigin.Manual:
                Check.True(session.TryOverdriveTower(tower.Id), "manual Protocol activates");
                break;
            case ProtocolActivationOrigin.Sandbox:
                Check.True(session.TestSandboxProtocol(tower.Id), "Sandbox Protocol test activates");
                break;
            case ProtocolActivationOrigin.ArmedAutomatic:
                Check.True(session.TryToggleAutoProtocol(tower.Id), "automatic Protocol is armed");
                session.Update(1f / 60);
                break;
            case ProtocolActivationOrigin.ApexAutomatic:
                session.Update(1f / 60);
                break;
        }
    }
}
