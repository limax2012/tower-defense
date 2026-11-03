using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertRadialMachineryMotion(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var fixtures = new List<GameSession>();
        foreach (var id in new[] { "frost_spire", "siege_mortar", "signal_beacon" })
        for (var stage = 1; stage <= 4; stage++)
        {
            var session = new GameSession(content, "prism_circuit", "normal", "sandbox_lab");
            Require(session.TryPlaceTower(id, new Vector2(250, 205)), $"{id}: radial review has a legal Sandbox foundation.", assertions);
            var tower = session.SelectedTower!;
            if (stage >= 2) Require(tower.TryChooseDoctrine(tower.Definition.Tier2Doctrines[0].Id), $"{id}: radial review advances its doctrine.", assertions);
            if (stage >= 3) Require(tower.TrySpecialize(tower.Definition.Specializations[0].Id), $"{id}: radial review advances its specialization.", assertions);
            if (stage == 4) Require(tower.TryApexUpgrade(), $"{id}: radial review advances to Apex.", assertions);
            tower.TickVisual(1);
            Require(session.SpawnSandboxTargets("t3_brute", 2, 20, "Standard", true), $"{id}: radial review has opposing targets.", assertions);
            session.Enemies[0].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath, session.Map.Path);
            session.Enemies[1].SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 300)).DistanceAlongPath, session.Map.Path);
            var checksum = SessionChecksum.Compute(session, 0);
            var mode = tower.TargetMode;
            var before = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            tower.TargetMode = TargetMode.Last;
            var modeChanged = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            var enemies = session.Enemies.ToArray();
            session.Enemies.Clear();
            var empty = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .01f));
            var advanced = GameRenderer.TowerAim(session, tower, PresentationFrame.Create(session, .015f));
            session.Enemies.AddRange(enemies);
            tower.TargetMode = mode;
            var rate = id switch { "siege_mortar" => .14f, "frost_spire" => .22f, _ => 0 };
            Require(before == modeChanged && before == empty && MathF.Abs(advanced - empty - rate * .005f) < .000001f,
                $"{id} stage {stage}: assembly motion is independent of target mode and presence, with {rate:0.00} radians/second presentation rate.", assertions);
            Require(checksum == SessionChecksum.Compute(session, 0), $"{id} stage {stage}: machinery motion leaves synchronized state unchanged.", assertions);
            if (id != "signal_beacon")
            {
                for (var direction = 0; direction < 4; direction++)
                    Require(NightGridArt.TowerMuzzlePosition(tower.Position, tower.Definition.Visual.Radius,
                            id, Math.Min(stage, 3), direction * MathHelper.PiOver2, apex: stage == 4) == tower.Position,
                        $"{id} stage {stage}: the radial emitter remains centered at assembly angle {direction * 90}.", assertions);
                fixtures.Add(session);
            }
        }
        var checksums = fixtures.Select(session => SessionChecksum.Compute(session, 0)).ToArray();
        scenes.Add(CaptureRadialMachinery(fixtures, font, "53-radial-motion-states.png"));
        Require(fixtures.Select((session, index) => SessionChecksum.Compute(session, 0) == checksums[index]).All(same => same),
            "Radial machinery and combined support/Protocol rendering preserve all fixture checksums.", assertions);
        foreach (var session in fixtures) session.Update(1f / 60);
        scenes.Add(CaptureRadialMachinery(fixtures, font, "53a-radial-central-discharge.png"));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RADIAL_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "radial-animation"));
        var steps = 0;
        for (var frame = 0; frame < 72; frame++)
        {
            var targetStep = (frame + 1) * 60 / 24;
            while (steps < targetStep)
            {
                foreach (var session in fixtures) session.Update(1f / 60);
                steps++;
            }
            scenes.Add(CaptureRadialMachinery(fixtures, font, $"radial-animation/frame-{frame:000}.png"));
        }
    }

    private VisualVerificationScene CaptureRadialMachinery(IReadOnlyList<GameSession> fixtures,
        SpriteFont font, string fileName)
    {
        const int width = 1840;
        const int height = 730;
        const int header = 118;
        const int rowHeight = 300;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        MechanicalReviewLabel(font, "RADIAL MACHINERY / CONTINUOUS OPERATION", new Vector2(26, 16), 1);
        MechanicalReviewLabel(font, "1.85x DETAIL ABOVE / NATIVE FIELD SIZE BELOW", new Vector2(26, 57), .60f);
        MechanicalReviewLabel(font, "NORMAL / PROTOCOL / SUPPORT / BOTH", new Vector2(1225, 57), .60f);
        for (var stage = 1; stage <= 4; stage++)
            MechanicalReviewLabel(font, stage == 4 ? "APEX" : $"T{stage}", new Vector2(Column(stage) - 17, 87), .66f);
        for (var row = 0; row < 2; row++)
        {
            var y = header + row * rowHeight;
            _primitives.FillRect(_batch, new Rectangle(16, y, width - 32, rowHeight - 10),
                row == 0 ? ColorPalette.Rainline.Deck : ColorPalette.Cinderworks.Deck);
            MechanicalReviewLabel(font, fixtures[row * 4].Towers[0].Definition.DisplayName.ToUpperInvariant(),
                new Vector2(30, y + 10), .66f);
        }
        _batch.End();
        for (var row = 0; row < 2; row++)
        for (var stage = 1; stage <= 4; stage++)
        for (var state = 0; state < 4; state++)
        {
            var session = fixtures[row * 4 + stage - 1];
            var tower = session.Towers[0];
            var frame = PresentationFrame.Create(session, 0);
            var aim = GameRenderer.TowerAim(session, tower, frame);
            var x = Column(stage) + (state - 1.5f) * 104;
            var y = header + row * rowHeight;
            DrawRadialMachine(tower, new Vector2(x, y + 120), 1.85f, aim, frame.TimeSeconds, state);
            DrawRadialMachine(tower, new Vector2(x, y + 239), 1, aim, frame.TimeSeconds, state);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        static int Column(int stage) => 230 + (stage - 1) * 460;
    }

    private void DrawRadialMachine(TowerInstance tower, Vector2 at, float scale, float aim, float time, int state)
    {
        var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
        var radius = tower.Definition.Visual.Radius * tower.VisualScale;
        var recoil = tower.RecoilAnimationRemaining / .12f * .2f;
        var level = tower.LevelIndex + 1;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, transform);
        if (state is 1 or 3)
            TowerProtocolArt.DrawEnvelope(_batch, _primitives, Vector2.Zero, radius, tower.Definition.Id,
                level, tower.IsApex, time, false);
        NightGridArt.Tower(_batch, _primitives, Vector2.Zero, radius, tower.Definition.Id,
            level, aim, time, recoil, apex: tower.IsApex);
        if (state is 1 or 3)
            TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, radius, tower.Definition.Id,
                level, tower.IsApex, aim, time, false, envelope: false);
        if (state is 2 or 3)
            TowerStatusArt.DrawSupport(_batch, _primitives, Vector2.Zero, radius, tower.Definition.Id,
                level, tower.IsApex, aim, time, false);
        _batch.End();
    }
}
