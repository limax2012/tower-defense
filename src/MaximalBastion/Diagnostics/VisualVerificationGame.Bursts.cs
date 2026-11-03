using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private sealed record BurstFixture(GameSession Session, Vector2 Focus, string Label,
        ProjectileInstance? Shot = null, EffectInstance? Contact = null);

    private void AssertTransientBursts(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var protocols = content.Towers.Values.Select(definition =>
                    CreateProtocolBurstFixture(content, definition.Id, 3, assertions)).ToArray();
                var mortars = Enumerable.Range(1, 4).Select(stage =>
                    CreateImpactBurstFixture(content, "siege_mortar", stage, assertions)).ToArray();
                var weapons = content.Towers.Values.Where(definition =>
                        definition.Id is not "signal_beacon" and not "siege_mortar")
                    .Select(definition => CreateImpactBurstFixture(content, definition.Id, 3, assertions)).ToArray();
                var mode = reduced ? "reduced" : "full";
                scenes.Add(CaptureBurstPhases(protocols, font, "PROTOCOL / IGNITION", $"61-protocol-ignition-{mode}.png", assertions));
                scenes.Add(CaptureBurstPhases(mortars, font, "SIEGE MORTAR / DETONATION", $"61a-mortar-detonation-{mode}.png", assertions));
                scenes.Add(CaptureBurstPhases(weapons, font, "WEAPON / CONTACT", $"61b-weapon-contact-{mode}.png", assertions));
            }

            _renderer.ReducedEffects = false;
            var apex = CreateProtocolBurstFixture(content, "siege_mortar", 4, assertions, automatic: true);
            scenes.Add(CaptureBurstPhases([apex], font, "APEX / AUTOMATIC IGNITION", "61c-apex-ignition.png", assertions));
            CaptureBurstBattlefield(content, font, display, assertions, scenes);
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_BURST_ANIMATION") != "1") return;

            var liveProtocols = content.Towers.Values.Select(definition =>
                CreateProtocolBurstFixture(content, definition.Id, 3, assertions)).ToArray();
            CaptureBurstMotion(liveProtocols, font, "PROTOCOL / LIVE IGNITION", "burst-protocol-animation", 48,
                assertions, scenes, focusOnTower: true);
            var liveMortars = Enumerable.Range(1, 4).Select(stage =>
                CreateImpactBurstFixture(content, "siege_mortar", stage, assertions, awaitContact: false)).ToArray();
            CaptureBurstMotion(liveMortars, font, "SIEGE MORTAR / LIVE COMBAT", "burst-mortar-animation", 180,
                assertions, scenes, focusOnTower: false);
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private void CaptureBurstBattlefield(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 26)
        for (var x = region.Left + 24; x < region.Right - 16; x += 26)
            candidates.Add(new Vector2(x, y));
        candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 350))).ToList();
        var roster = new[] { "signal_beacon", "frost_spire", "siege_mortar", "needle_turret", "siege_mortar",
            "shard_fan", "siege_mortar", "arc_relay", "siege_mortar", "prism_beam", "breaker_cannon",
            "ember_coil", "watchtower", "signal_beacon", "needle_turret", "siege_mortar" };
        foreach (var towerId in roster)
        {
            Require(candidates.Any(point => session.TryPlaceTower(towerId, point)),
                $"Burst battlefield places legal {towerId} machinery.", assertions);
            AdvanceProtocolReviewTier(session, session.SelectedTower!, 3, assertions);
            session.SelectedTower!.TickVisual(1);
        }
        session.CancelPlacement();
        Require(session.SpawnSandboxTargets("t3_brute", 24, 20, "Standard", true) && session.StartSandboxWave(20),
            "Burst battlefield combines durable fixed targets with a real Sandbox wave.", assertions);
        for (var index = 0; index < session.Enemies.Count; index++)
        {
            var distance = index < session.Towers.Count
                ? session.Map.Path.Project(session.Towers[index].Position).DistanceAlongPath
                : 100 + (index - session.Towers.Count) * (session.Map.Path.TotalLength - 200) / 8;
            session.Enemies[index].SetSandboxPathDistance(distance, session.Map.Path);
        }
        var mortarLaunches = new HashSet<int>();
        var firstImpact = false;
        for (var step = 0; step < 480 && !firstImpact; step++)
        {
            session.Update(1f / 60);
            foreach (var projectile in session.Projectiles.Projectiles.Where(projectile => projectile.Appearance.TowerId == "siege_mortar"))
                mortarLaunches.Add(projectile.Payload.SourceTowerId);
            firstImpact = session.Effects.Effects.Any(effect => effect.Kind == EffectKind.Splash &&
                effect.Purpose == EffectPurpose.Combat && effect.Appearance.TowerId == "siege_mortar");
        }
        Require(firstImpact && mortarLaunches.Count >= 3,
            $"Burst battlefield reaches an actual mortar detonation after {mortarLaunches.Count} independent Mortars have launched.", assertions);
        var receiver = session.Towers.First(tower => tower.Definition.Id == "frost_spire");
        Require(session.GetSupportBuff(receiver).IsActive && session.TrySelectTower(receiver.Id) &&
                session.TryOverdriveTower(receiver.Id) && session.Towers.Count(tower => tower.IsOverdriven) == 1,
            "Burst battlefield selects one Beacon-supported Frost Spire and activates exactly one legal manual Protocol.", assertions);
        session.HandleInspectionInput(Pointer(receiver.Position.X, receiver.Position.Y));
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            for (var sample = 0; sample < 3; sample++)
            {
                if (sample > 0)
                    for (var step = 0; step < 3; step++) session.Update(1f / 60);
                Require(session.Effects.Effects.Any(effect => effect.Kind == EffectKind.Splash &&
                        effect.Purpose == EffectPurpose.Combat && effect.Appearance.TowerId == "siege_mortar"),
                    $"Burst battlefield sample {sample + 1} contains a live projectile detonation.", assertions);
                var checksum = SessionChecksum.Compute(session, sample);
                foreach (var reduced in new[] { false, true })
                {
                    _renderer.ReducedEffects = reduced;
                    scenes.Add(Capture($"61d-burst-battlefield-{sample + 1}-{(reduced ? "reduced" : "full")}.png",
                        ui, GameState.Playing, session));
                }
                Require(checksum == SessionChecksum.Compute(session, sample),
                    $"Burst battlefield sample {sample + 1}: complete HUD, support, selection and overlapping transient effects preserve synchronized state.", assertions);
            }
            if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_BURST_ANIMATION") != "1" &&
                Environment.GetEnvironmentVariable("MAXIMAL_BASTION_IMPACT_ANIMATION") != "1") return;
            _renderer.ReducedEffects = false;
            const string directory = "burst-battlefield-animation";
            Directory.CreateDirectory(Path.Combine(_outputDirectory, directory));
            for (var frame = 0; frame < 120; frame++)
            {
                if (frame > 0) session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 60);
                var checksum = SessionChecksum.Compute(session, frame);
                scenes.Add(Capture($"{directory}/frame-{frame:000}.png", ui, GameState.Playing, session));
                Require(checksum == SessionChecksum.Compute(session, frame),
                    $"Burst battlefield frame {frame}: sixty-Hz combat rendering leaves simulation state unchanged.", assertions);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private BurstFixture CreateProtocolBurstFixture(GameContent content, string towerId, int stage,
        ICollection<string> assertions, bool automatic = false)
    {
        var session = CreateBurstSession(content, towerId, stage, assertions);
        var tower = session.Towers[0];
        if (automatic)
        {
            Require(session.SpawnSandboxTargets("t3_brute", 1, 20, "Elite", true),
                "Automatic ignition review has a real immortal priority target.", assertions);
            session.Enemies[0].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath,
                session.Map.Path);
            session.Update(1f / 60);
        }
        else
            Require(session.TryOverdriveTower(tower.Id), $"{towerId}: ignition enters through the actual Protocol command.", assertions);
        var activations = session.Effects.Effects.Where(effect => effect.Purpose == EffectPurpose.ProtocolActivation).ToArray();
        Require(tower.IsOverdriven && activations.Length == 1 && activations[0].Appearance.TowerId == towerId,
            $"{towerId}: one family-tagged transient accompanies the real {(automatic ? "automatic Apex" : "manual")} activation.", assertions);
        if (automatic)
            Require(session.OverdriveCooldownRemaining == 0 && tower.ApexProtocolCooldownRemaining > 0,
                "Automatic Apex ignition preserves independent cooldown ownership.", assertions);
        return new BurstFixture(session, tower.Position, $"{tower.Definition.DisplayName} / {(stage == 4 ? "APEX" : $"T{stage}")}");
    }

    private BurstFixture CreateImpactBurstFixture(GameContent content, string towerId, int stage,
        ICollection<string> assertions, bool awaitContact = true, int specializationIndex = 0)
    {
        var session = CreateBurstSession(content, towerId, stage, assertions, specializationIndex);
        Require(session.SpawnSandboxTargets("t3_brute", 1, 20, "Standard", true),
            $"{towerId} T{stage}: contact review uses an immortal Sandbox target.", assertions);
        session.Enemies[0].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath,
            session.Map.Path);
        var label = $"{session.Towers[0].Definition.DisplayName} / {(stage == 4 ? "APEX" : $"T{stage}")}";
        if (specializationIndex != 0) label += $" / {session.Towers[0].Specialization!.DisplayName}";
        if (!awaitContact) return new BurstFixture(session, session.Enemies[0].Position, label);
        EffectInstance? contact = null;
        ProjectileInstance? shot = null;
        for (var frame = 0; frame < 600 && contact is null; frame++)
        {
            session.Update(1f / 60);
            shot ??= session.Projectiles.Projectiles.FirstOrDefault(projectile => projectile.Appearance.TowerId == towerId);
            contact = session.Effects.Effects.FirstOrDefault(effect => effect.Appearance.TowerId == towerId &&
                effect.Purpose == EffectPurpose.Combat && effect.Kind is EffectKind.Impact or EffectKind.Splash or EffectKind.Beam);
        }
        Require(contact is not null,
            $"{towerId} T{stage}: the live weapon reaches its target and produces its authored contact effect.", assertions);
        var focus = contact!.Kind == EffectKind.Beam ? contact.End : contact.Start;
        return new BurstFixture(session, focus, label, shot, contact);
    }

    private static GameSession CreateBurstSession(GameContent content, string towerId, int stage,
        ICollection<string> assertions, int specializationIndex = 0)
    {
        var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
        Require(session.TryPlaceTower(towerId, new Vector2(250, 205)),
            $"{towerId}: transient review has a legal native-scale foundation.", assertions);
        var tower = session.SelectedTower!;
        if (specializationIndex == 0)
            AdvanceProtocolReviewTier(session, tower, stage, assertions);
        else
        {
            Require(stage >= 3 && session.TryChooseTowerDoctrine(tower.Id, tower.Definition.Tier2Doctrines[0].Id) &&
                    session.TrySpecializeTower(tower.Id, tower.Definition.Specializations[specializationIndex].Id),
                $"{towerId}: contact review applies its alternate authored specialization.", assertions);
            if (stage == 4)
                Require(session.TryUpgradeTower(tower.Id) && tower.IsApex,
                    $"{towerId}: alternate contact review applies its authored Apex upgrade.", assertions);
        }
        tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        return session;
    }

    private VisualVerificationScene CaptureBurstPhases(IReadOnlyList<BurstFixture> fixtures, SpriteFont font,
        string title, string fileName, ICollection<string> assertions)
    {
        const int width = 1856;
        const int header = 112;
        const int rowHeight = 190;
        const int left = 230;
        const int cellWidth = 268;
        var phases = new[] { 0, 2, 5, 9, 15, 28 };
        using var target = new RenderTarget2D(GraphicsDevice, width, header + fixtures.Count * rowHeight + 8,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, title, new Vector2(24, 14), .92f);
        MechanicalReviewLabel(font, $"ACTUAL GAME EFFECTS / NATIVE FIELD SIZE / {(_renderer.ReducedEffects ? "REDUCED" : "FULL")} EFFECTS",
            new Vector2(24, 53), .58f);
        for (var column = 0; column < phases.Length; column++)
            MechanicalReviewLabel(font, $"+{phases[column] / 60f:0.000}s", new Vector2(left + column * cellWidth + 98, 83), .58f);
        for (var row = 0; row < fixtures.Count; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 8), ColorPalette.Panel);
            MechanicalReviewLabel(font, fixtures[row].Label.Replace(" / ", "\n"), new Vector2(25, y + 69), .54f);
        }
        _batch.End();
        for (var row = 0; row < fixtures.Count; row++)
        {
            var fixture = fixtures[row];
            var checksum = SessionChecksum.Compute(fixture.Session, 0);
            for (var column = 0; column < phases.Length; column++)
            {
                if (column > 0) fixture.Session.Effects.Update((phases[column] - phases[column - 1]) / 60f);
                var cell = new Rectangle(left + column * cellWidth, header + row * rowHeight + 2, cellWidth - 6, rowHeight - 12);
                DrawBurstCell(fixture, cell, clipping, 1);
            }
            Require(fixture.Session.Effects.Effects.Count == 0,
                $"{fixture.Label}: captured transients expire and are removed by the normal effect clock.", assertions);
            Require(checksum == SessionChecksum.Compute(fixture.Session, 0),
                $"{fixture.Label}: transient aging and six native-scale renders preserve the synchronized combat checksum.", assertions);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }

    private void DrawBurstCell(BurstFixture fixture, Rectangle cell, RasterizerState clipping, float scale)
    {
        GraphicsDevice.ScissorRectangle = cell;
        var center = new Vector2(cell.Center.X, cell.Center.Y);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, clipping,
            null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X - fixture.Focus.X * scale,
                center.Y - fixture.Focus.Y * scale, 0));
        _renderer.DrawCombatShowcase(_batch, _primitives, fixture.Session);
        _batch.End();
    }

    private void CaptureBurstMotion(IReadOnlyList<BurstFixture> fixtures, SpriteFont font, string title,
        string directory, int frames, ICollection<string> assertions, ICollection<VisualVerificationScene> scenes,
        bool focusOnTower)
    {
        Directory.CreateDirectory(Path.Combine(_outputDirectory, directory));
        for (var frame = 0; frame < frames; frame++)
        {
            if (frame > 0)
                foreach (var fixture in fixtures) fixture.Session.Update(1f / 60);
            var checksums = fixtures.Select(fixture => SessionChecksum.Compute(fixture.Session, frame)).ToArray();
            scenes.Add(CaptureBurstMotionFrame(fixtures, font, title, $"{directory}/frame-{frame:000}.png", focusOnTower));
            Require(fixtures.Select((fixture, index) => SessionChecksum.Compute(fixture.Session, frame) == checksums[index]).All(same => same),
                $"{directory} frame {frame}: actual sixty-Hz gameplay presentation preserves every combat checksum.", assertions);
        }
        if (focusOnTower)
            Require(fixtures.All(fixture => fixture.Session.Towers[0].IsOverdriven &&
                    fixture.Session.Effects.Effects.All(effect => effect.Purpose != EffectPurpose.ProtocolActivation)),
                "All ten Protocol ignitions cleanly yield to their sustained active field during the live sequence.", assertions);
    }

    private VisualVerificationScene CaptureBurstMotionFrame(IReadOnlyList<BurstFixture> fixtures, SpriteFont font,
        string title, string fileName, bool focusOnTower)
    {
        const int width = 1440;
        const int header = 94;
        const int rowHeight = 220;
        var rows = (fixtures.Count + 1) / 2;
        using var target = new RenderTarget2D(GraphicsDevice, width, header + rows * rowHeight + 8,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clipping = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, title, new Vector2(24, 14), .92f);
        MechanicalReviewLabel(font, "60 FPS / NATIVE SIZE LEFT / 1.7x DETAIL RIGHT", new Vector2(24, 53), .58f);
        for (var index = 0; index < fixtures.Count; index++)
        {
            var x = 12 + index % 2 * 714;
            var y = header + index / 2 * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(x, y, 702, rowHeight - 8), ColorPalette.Panel);
            MechanicalReviewLabel(font, fixtures[index].Label, new Vector2(x + 14, y + 12), .56f);
        }
        _batch.End();
        for (var index = 0; index < fixtures.Count; index++)
        {
            var x = 12 + index % 2 * 714;
            var y = header + index / 2 * rowHeight;
            var source = fixtures[index];
            var focus = focusOnTower ? source.Session.Towers[0].Position :
                Vector2.Lerp(source.Session.Towers[0].Position, source.Focus, .55f);
            var fixture = source with { Focus = focus };
            DrawBurstCell(fixture, new Rectangle(x + 4, y + 39, 330, rowHeight - 51), clipping, 1);
            DrawBurstCell(fixture, new Rectangle(x + 345, y + 39, 350, rowHeight - 51), clipping, 1.7f);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }
}
