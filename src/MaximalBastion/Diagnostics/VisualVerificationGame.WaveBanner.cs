using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertWaveStartPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        AssertGameEntryPresentation(content, font, display, assertions, scenes);
        AssertBossArrivalPresentation(content, font, display, assertions, scenes);
        var reference = new GameSession(content, "foundry_loop", "normal", "standard");
        var referenceUi = new UIManager(font, display);
        ConfigureUi(referenceUi, content);
        referenceUi.HandleGameplayInput(Pointer(0, 0), reference);
        Require(reference.AnnouncementRemaining == 0, "The terrain reference has no active announcement.", assertions);
        var terrainPixels = RenderPixels(referenceUi, GameState.Playing, reference);
        var announcementRegion = new Rectangle(240, 64, 480, 88);
        Color[]? regularPixels = null;
        Color[]? earlyPixels = null;
        foreach (var sample in new[]
                 {
                     (Number: 1, Bonus: 0, Sandbox: false, Name: "first"),
                     (Number: 21, Bonus: 0, Sandbox: false, Name: "regular"),
                     (Number: 21, Bonus: 20, Sandbox: false, Name: "early-call"),
                     (Number: 1234, Bonus: 0, Sandbox: false, Name: "endless"),
                     (Number: 21, Bonus: 0, Sandbox: true, Name: "sandbox")
                 })
        {
            var session = new GameSession(content, "foundry_loop", "normal", sample.Sandbox ? "sandbox_lab" : "close_quarters");
            session.OnWaveCompleted(1);
            Require(session.AnnouncementRemaining > 0,
                $"{sample.Name}: the wave starts while a completion announcement is active.", assertions);
            if (sample.Sandbox)
                Require(session.StartSandboxWave(sample.Number), "Sandbox starts an authored test wave.", assertions);
            else
                session.OnWaveStarted(new WaveDefinition
                {
                    Number = sample.Number,
                    Archetype = "Residual Current",
                    Briefing = "A mixed formation advances along the route."
                }, sample.Bonus);

            Require(session.AnnouncementTitle is null && session.AnnouncementSubtitle is null && session.AnnouncementRemaining == 0,
                $"{sample.Name}: starting the wave leaves no active announcement.", assertions);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            ui.HandleGameplayInput(Pointer(0, 0), session);
            var checksum = SessionChecksum.Compute(session, 694);
            using var scene = RenderScene(ui, GameState.Playing, session);
            var pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
            scene.GetData(pixels);
            using var native = DownsampleEnemyLaneScene(scene);
            scenes.Add(SaveTierScene(native, $"wave-start-{sample.Name}.png"));
            Require(checksum == SessionChecksum.Compute(session, 694),
                $"{sample.Name}: drawing the wave-start state preserves synchronized state.", assertions);
            Require(CountChangedPixels(terrainPixels, pixels, announcementRegion) == 0,
                $"{sample.Name}: the terrain beneath the top bar remains unobstructed at wave start.", assertions);
            if (sample.Name == "regular") regularPixels = pixels;
            if (sample.Name == "early-call") earlyPixels = pixels;
        }

        var all = new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight);
        Require(regularPixels is not null && earlyPixels is not null &&
                CountChangedPixels(regularPixels, earlyPixels, all) == 0,
            "An early-call bonus adds no wave-start overlay to the battlefield.", assertions);
    }

    private void AssertBossArrivalPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        var wave = session.Waves.GetAuthoredWave(30) ?? throw new InvalidOperationException("Missing campaign wave 30.");
        var group = wave.Groups[^1];
        Require(group.Rank == "Boss" && group.Count == 1, "Cinderworks wave 30 ends with a Bastion Core.", assertions);
        session.Waves.RestoreCoOpState(new WaveRuntimeState
        {
            CurrentWaveNumber = wave.Number, ActiveWaveNumber = wave.Number,
            GroupIndex = wave.Groups.Count - 1, QueuedEnemies = group.Count, DelayRemaining = group.DelayBefore
        });
        for (var tick = 0; tick < 120 && session.Enemies.Count == 0; tick++) session.Update(1f / 60);
        Require(session.Enemies.Count == 1 && session.Enemies[0].IsBoss && !session.HasVisibleAnnouncement,
            "The authored Bastion Core enters wave 30 with the announcement area clear on its first tick.", assertions);

        for (var tick = 0; tick < 132; tick++) session.Update(1f / 60);
        var boss = session.Enemies[0];
        Require(boss.Position.X > 40 && !session.HasVisibleAnnouncement,
            "The Bastion Core is on the field with the announcement area clear.", assertions);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        ui.HandleGameplayInput(Pointer(0, 0), session);
        scenes.Add(Capture("wave-30-boss-arrival.png", ui, GameState.Playing, session));

        session.DamageResolver.Apply(boss, new DamagePayload
        {
            Damage = boss.Shield + boss.MaxHealth * .49f, ArmorPierce = boss.BaseArmor
        });
        Require(!boss.BossPhaseActive && boss.Shield == 0 && boss.Health > boss.MaxHealth * .5f,
            "The boss has spent its initial shield and is above the half-health threshold.", assertions);
        var normalSpeed = boss.CurrentSpeed;
        var beforePhase = RenderPixels(ui, GameState.Playing, session);
        session.DamageResolver.Apply(boss, new DamagePayload { Damage = boss.MaxHealth * .02f, ArmorPierce = boss.BaseArmor });
        Require(boss.Health <= boss.MaxHealth * .5f && boss.BossPhaseActive && boss.Shield > 0 &&
                boss.CurrentSpeed > normalSpeed && !session.HasVisibleAnnouncement,
            "Crossing half health restores the boss's shield and increases its speed while the announcement area stays clear.", assertions);
        var afterPhase = RenderPixels(ui, GameState.Playing, session);
        Require(CountChangedPixels(beforePhase, afterPhase, new Rectangle(240, 64, 480, 48)) == 0,
            "The battlefield beneath the HUD stays visible as the Bastion Core enters Overdrive.", assertions);
        for (var tick = 0; tick < 12; tick++) session.Update(1f / 60);
        scenes.Add(Capture("wave-30-boss-overdrive.png", ui, GameState.Playing, session));
    }

    private void AssertGameEntryPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var sample in new[]
                 {
                     (Map: "foundry_loop", Challenge: "sandbox_lab", Restore: false, Name: "sandbox"),
                     (Map: "foundry_loop", Challenge: "close_quarters", Restore: false, Name: "signal-gauntlet"),
                     (Map: "relay_divide", Challenge: "standard", Restore: false, Name: "helix"),
                     (Map: "foundry_loop", Challenge: "standard", Restore: true, Name: "restored")
                 })
        {
            var session = new GameSession(content, sample.Map, "normal", sample.Challenge);
            if (sample.Restore) session = GameSession.RestoreSaveGame(content, session.CaptureSaveGame());
            Require(session.AnnouncementTitle is null && session.AnnouncementSubtitle is null && session.AnnouncementRemaining == 0,
                $"{sample.Name}: entering the game leaves no active announcement.", assertions);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            ui.HandleGameplayInput(Pointer(0, 0), session);
            var checksum = SessionChecksum.Compute(session, 695);
            using var scene = RenderScene(ui, GameState.Playing, session);
            using var native = DownsampleEnemyLaneScene(scene);
            scenes.Add(SaveTierScene(native, $"game-entry-{sample.Name}.png"));
            Require(checksum == SessionChecksum.Compute(session, 695),
                $"{sample.Name}: drawing the game-entry state preserves synchronized state.", assertions);
        }
    }
}
