using MaximalBastion.Combat;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertAttackImpactCoverage(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        var damaging = content.Towers.Values.Where(tower => tower.Behavior != "aura").ToArray();
        var areaBranches = new[] { "frost_spire", "ember_coil", "breaker_cannon", "siege_mortar" };
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var mode = reduced ? "reduced" : "full";
                foreach (var definition in damaging)
                {
                    var fixtures = Enumerable.Range(1, 4).Select(stage =>
                        CreateImpactBurstFixture(content, definition.Id, stage, assertions)).ToArray();
                    foreach (var fixture in fixtures) AssertLiveImpact(fixture, assertions);
                    scenes.Add(CaptureBurstPhases(fixtures, font, $"{definition.DisplayName.ToUpperInvariant()} / CONTACT TIERS",
                        $"attack-impact-{definition.Id}-{mode}.png", assertions));
                }
                foreach (var id in areaBranches)
                {
                    var fixtures = new[] { 3, 4 }.Select(stage =>
                        CreateImpactBurstFixture(content, id, stage, assertions, specializationIndex: 1)).ToArray();
                    foreach (var fixture in fixtures) AssertLiveImpact(fixture, assertions);
                    scenes.Add(CaptureBurstPhases(fixtures, font, $"{content.Towers[id].DisplayName.ToUpperInvariant()} / ALTERNATE BRANCH",
                        $"attack-impact-{id}-alternate-{mode}.png", assertions));
                }
            }

            foreach (var id in areaBranches)
            for (var stage = 1; stage <= 4; stage++)
            for (var branch = 0; branch < (stage >= 3 ? 2 : 1); branch++)
                AssertAuthoredSplashRecipients(content, id, stage, branch, assertions);
            AssertChainImpactEndpoints(content, assertions);
            AssertSplashRasterCoverage(content, assertions);
            AssertCarrierImpactVisibility(content, assertions, scenes);

            foreach (var radius in new[] { 6f, 12f })
            {
                var previous = 0f;
                for (var stage = 1; stage <= 4; stage++)
                {
                    var current = TowerAttackArt.ContactRadius(radius, Math.Min(stage, 3), stage == 4);
                    Require(current > previous && current <= 14,
                        $"Contact radius {radius:0}: stage {stage} grows visibly while remaining within a compact fourteen-pixel radius.", assertions);
                    previous = current;
                }
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private static void AssertLiveImpact(BurstFixture fixture, ICollection<string> assertions)
    {
        var session = fixture.Session;
        var tower = session.Towers[0];
        var contact = fixture.Contact!;
        var expected = AttackAppearance.FromTower(tower);
        Require(contact.Appearance == expected && contact.Purpose == EffectPurpose.Combat,
            $"{fixture.Label}: the actual hit retains its family and firing tier.", assertions);
        if (fixture.Shot is { } shot)
        {
            var area = shot.SplashRadius > 0 || shot.Kind == ProjectileKind.ImpactPoint;
            Require(contact.Kind == (area ? EffectKind.Splash : EffectKind.Impact),
                $"{fixture.Label}: live projectile contact distinguishes a damage area from a single-target hit.", assertions);
            if (area)
                Require(MathF.Abs(contact.Radius - shot.SplashRadius) < .001f && contact.Start == shot.Position &&
                        contact.TargetEnemy is not null && session.Enemies.Contains(contact.TargetEnemy),
                    $"{fixture.Label}: the splash retains its actual projectile radius and fixed impact center.", assertions);
            else
                Require(contact.TargetEnemy == shot.Target,
                    $"{fixture.Label}: the compact contact belongs to the struck enemy.", assertions);
            Require(shot.SplashTargetLimit == tower.Level.SplashTargetLimit,
                $"{fixture.Label}: projectile effects preserve the authored damage target limit.", assertions);
        }
        else
            Require(contact.Kind == EffectKind.Beam && contact.TargetEnemy is not null,
                $"{fixture.Label}: immediate weapon feedback identifies the actual beam recipient.", assertions);

        if (contact.TargetEnemy is { } enemy)
        {
            var distance = enemy.DistanceAlongPath;
            var original = PresentationFrame.Create(session, 0).EffectTargetPosition(contact);
            enemy.SetSandboxPathDistance(distance + 18, session.Map.Path);
            var checksum = SessionChecksum.Compute(session, 0);
            var frame = PresentationFrame.Create(session, .008f);
            var actual = frame.EffectTargetPosition(contact);
            Require(Vector2.DistanceSquared(actual, frame.EnemyPosition(enemy)) < .001f &&
                    Vector2.DistanceSquared(actual, original) > 16,
                $"{fixture.Label}: contact feedback follows its recipient's presented position as the enemy moves.", assertions);
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{fixture.Label}: contact attachment sampling preserves synchronized state.", assertions);
            enemy.SetSandboxPathDistance(distance, session.Map.Path);
        }
    }

    private static void AssertAuthoredSplashRecipients(GameContent content, string id, int stage, int branch,
        ICollection<string> assertions, bool carriersBeyondCap = false)
    {
        var session = CreateBurstSession(content, id, stage, assertions, branch);
        var tower = session.Towers[0];
        if (tower.Level.SplashRadius <= 0) return;
        var carrierIndex = carriersBeyondCap ? tower.Level.SplashTargetLimit : 1;
        var targetCount = Math.Max(12, carrierIndex + 2);
        Require(carrierIndex > 0 && session.SpawnSandboxTargets("t3_brute", carrierIndex, 20, "Standard", true) &&
                session.SpawnSandboxTargets("t3_brute", 1, 20, "Standard", true, EnemySignalRole.Disruptor),
            $"{id} stage {stage} branch {branch}: splash coverage includes ordinary enemies and a durable signal carrier.", assertions);
        var remainingOrdinary = targetCount - carrierIndex - 2;
        if (remainingOrdinary > 0)
            Require(session.SpawnSandboxTargets("t3_brute", remainingOrdinary, 20, "Standard", true),
                $"{id}: splash coverage fills the nearby recipient pool.", assertions);
        Require(session.SpawnSandboxTargets("t3_brute", 1, 20, "Standard", true, EnemySignalRole.Jammer),
            $"{id}: splash coverage includes an out-of-range signal carrier.", assertions);
        var opening = session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath;
        foreach (var enemy in session.Enemies) enemy.SetSandboxPathDistance(opening, session.Map.Path);
        TowerBehaviorRegistry.Create(tower.Definition.Behavior).Attack(new TowerInstanceContext
            { Session = session, Tower = tower, Target = session.Enemies[0] });
        var projectile = session.Projectiles.Projectiles.Single();
        var impactDistance = session.Map.Path.Project(projectile.AimPoint).DistanceAlongPath;
        for (var index = 0; index < session.Enemies.Count; index++)
        {
            var offset = index == session.Enemies.Count - 1 ? projectile.SplashRadius + 16 : index * 1.5f;
            session.Enemies[index].SetSandboxPathDistance(impactDistance + offset, session.Map.Path);
        }
        session.Projectiles.Update(10, session);
        var expected = session.Enemies.Where(enemy =>
                Vector2.DistanceSquared(enemy.Position, projectile.Position) <= projectile.SplashRadius * projectile.SplashRadius)
            .OrderBy(enemy => Vector2.DistanceSquared(enemy.Position, projectile.Position)).ThenBy(enemy => enemy.Id);
        var eligible = projectile.SplashTargetLimit > 0 ? expected.Take(projectile.SplashTargetLimit) : expected;
        var actual = session.Enemies.Where(enemy => enemy.DamagePauseTimer > 0).Select(enemy => enemy.Id).ToHashSet();
        Require(actual.SetEquals(eligible.Select(enemy => enemy.Id)) && actual.Count > 0 &&
                !actual.Contains(session.Enemies[^1].Id),
            $"{id} stage {stage} branch {branch}: actual damage reaches only eligible nearby recipients within the authored cap.", assertions);
        var splash = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Splash);
        var expectedTarget = eligible.FirstOrDefault(enemy => !enemy.IsDead && !enemy.HasEscaped);
        Require(splash.TargetEnemy == expectedTarget && session.Effects.Effects.Count == 1,
            $"{id} stage {stage} branch {branch}: the single splash owns only the nearest surviving damaged recipient, respecting range and target caps.", assertions);
        Require(MathF.Abs(splash.Radius - projectile.SplashRadius) < .001f &&
                splash.Start == projectile.Position &&
                MathF.Abs(projectile.SplashRadius - tower.Level.SplashRadius) < .001f &&
                projectile.SplashTargetLimit == tower.Level.SplashTargetLimit,
            $"{id} stage {stage} branch {branch}: effect geometry, projectile and effective doctrine/Apex damage radius agree.", assertions);
        if (!carriersBeyondCap && tower.Level.SplashTargetLimit > 0)
            AssertAuthoredSplashRecipients(content, id, stage, branch, assertions, carriersBeyondCap: true);
    }

    private static void AssertChainImpactEndpoints(GameContent content, ICollection<string> assertions)
    {
        foreach (var id in new[] { "arc_relay", "prism_beam" })
        for (var branch = 0; branch < 2; branch++)
        {
            var session = CreateBurstSession(content, id, 4, assertions, branch);
            var tower = session.Towers[0];
            Require(session.SpawnSandboxTargets("t3_brute", 8, 20, "Standard", true),
                $"{id}: chain contact review creates real nearby recipients.", assertions);
            var opening = session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath;
            for (var index = 0; index < session.Enemies.Count; index++)
                session.Enemies[index].SetSandboxPathDistance(opening + index * 8, session.Map.Path);
            TowerBehaviorRegistry.Create(tower.Definition.Behavior).Attack(new TowerInstanceContext
                { Session = session, Tower = tower, Target = session.Enemies[0] });
            var beams = session.Effects.Effects.Where(effect => effect.Kind == EffectKind.Beam).ToArray();
            var expectedCount = 1 + Math.Min(session.Enemies.Count - 1,
                tower.Level.ChainDamage > 0 ? tower.Level.ChainCount : 0);
            Require(beams.Length == expectedCount && beams.All(beam => beam.TargetEnemy is not null &&
                    beam.End == beam.TargetEnemy.Position && beam.Appearance == AttackAppearance.FromTower(tower)) &&
                    beams.Select(beam => beam.TargetEnemy!.Id).Distinct().Count() == beams.Length,
                $"{id} branch {branch}: every real primary and chain endpoint identifies a distinct struck enemy and its firing tier.", assertions);
            Require(session.Effects.Effects.All(effect => effect.Kind != EffectKind.Flash),
                $"{id} branch {branch}: beam contacts add no duplicate oversized generic flash.", assertions);
            Require(beams[0].SourceEnemy is null && beams.Skip(1).Select((beam, index) =>
                    beam.SourceEnemy == beams[index].TargetEnemy).All(matches => matches),
                $"{id} branch {branch}: every chained segment is attached to the preceding struck enemy.", assertions);
            if (beams.Length < 2) continue;
            var middle = beams[0].TargetEnemy!;
            var before = PresentationFrame.Create(session, 0).EffectTargetPosition(beams[0]);
            middle.SetSandboxPathDistance(middle.DistanceAlongPath + 18, session.Map.Path);
            var checksum = SessionChecksum.Compute(session, 0);
            var presentation = PresentationFrame.Create(session, .008f);
            var incomingEnd = presentation.EffectTargetPosition(beams[0]);
            var outgoingStart = presentation.EffectSourcePosition(beams[1]);
            Require(Vector2.DistanceSquared(incomingEnd, outgoingStart) < .001f &&
                    Vector2.DistanceSquared(incomingEnd, presentation.EnemyPosition(middle)) < .001f &&
                    Vector2.DistanceSquared(incomingEnd, before) > 16,
                $"{id} branch {branch}: moving a shared recipient keeps incoming and outgoing visual links joined on its body.", assertions);
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{id} branch {branch}: sampling both ends of a moving chain preserves synchronized state.", assertions);
        }
    }

    private void AssertCarrierImpactVisibility(GameContent content, ICollection<string> assertions,
        ICollection<VisualVerificationScene> scenes)
    {
        var previousRenderer = _renderer;
        const int width = 320;
        const int height = 240;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        try
        {
            foreach (var reduced in new[] { false, true })
            foreach (var id in new[] { "needle_turret", "arc_relay", "prism_beam", "ember_coil", "frost_spire", "siege_mortar", "breaker_cannon" })
            foreach (var role in new[] { EnemySignalRole.None, EnemySignalRole.Disruptor })
            {
                _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
                var session = CreateBurstSession(content, id, id == "breaker_cannon" ? 3 : 2, assertions);
                Require(session.SpawnSandboxTargets("t4_aegis", 1, 20, "Standard", true, role),
                    $"{id}: hit visibility review creates an actual {role} target.", assertions);
                var enemy = session.Enemies[0];
                enemy.AbsorbShield(enemy.Shield);
                enemy.SetSandboxPathDistance(session.Map.Path.Project(new Vector2(250, 110)).DistanceAlongPath,
                    session.Map.Path);
                TowerBehaviorRegistry.Create(session.Towers[0].Definition.Behavior).Attack(new TowerInstanceContext
                    { Session = session, Tower = session.Towers[0], Target = enemy });
                session.Projectiles.Update(10, session);
                var contacts = session.Effects.Effects.Where(effect => effect.Appearance.TowerId == id &&
                    (effect.TargetEnemy == enemy || role == EnemySignalRole.None && effect.Kind == EffectKind.Splash &&
                        Vector2.DistanceSquared(effect.Start, enemy.Position) <= effect.Radius * effect.Radius)).ToArray();
                Require(contacts.Length > 0,
                    $"{id}: a real attack identifies its {role} hit recipient.", assertions);
                foreach (var effect in session.Effects.Effects) effect.Remaining = effect.Duration * .65f;
                var lifetimes = session.Effects.Effects.Select(effect => effect.Remaining).ToArray();
                var focus = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
                var checksum = SessionChecksum.Compute(session, 0);
                SetEffects(false);
                var without = RenderNative();
                SetEffects(true);
                var mode = reduced ? "reduced" : "full";
                var with = RenderNative(!reduced ? $"attack-contact-{id}-{role.ToString().ToLowerInvariant()}.png" : null);
                Require(ChangedInBody(without, with, 10) > 3,
                    $"{id} / {role} / {mode}: actual contact remains visible on the enemy body at native gameplay size.", assertions);
                Require(_renderer.LastEnemyContactCount == 1,
                    $"{id} / {role} / {mode}: body-layer hit feedback emits one compact contact on its recipient.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{id} / {role}: contact visibility sampling preserves synchronized state.", assertions);
                if (role == EnemySignalRole.None) continue;

                Require(session.SpawnSandboxTargets("t4_aegis", 1, 20, "Standard", true, EnemySignalRole.Jammer),
                    $"{id}: overlapping contact review creates a complete foreground carrier.", assertions);
                var front = session.Enemies[^1];
                front.AbsorbShield(front.Shield);
                front.SetSandboxPathDistance(enemy.DistanceAlongPath + 6, session.Map.Path);
                checksum = SessionChecksum.Compute(session, 0);
                SetEffects(false);
                var coveredWithout = RenderNative();
                SetEffects(true);
                var coveredWith = RenderNative(!reduced ? $"attack-contact-{id}-carrier-occluded.png" : null);
                Require(ChangedInBody(coveredWithout, coveredWith, 5) == 0,
                    $"{id} / {mode}: the complete foreground carrier occludes a rear carrier's body-mounted hit without floating contact overlays.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{id} / {mode}: carrier contact occlusion preserves synchronized state.", assertions);

                void SetEffects(bool visible)
                {
                    for (var index = 0; index < lifetimes.Length; index++)
                        session.Effects.Effects[index].Remaining = visible ? lifetimes[index] : 0;
                }

                Color[] RenderNative(string? fileName = null)
                {
                    GraphicsDevice.SetRenderTarget(target);
                    GraphicsDevice.Clear(ColorPalette.Canvas);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Matrix.CreateTranslation(width / 2f - focus.X, height / 2f - focus.Y, 0));
                    _renderer.DrawCombatShowcase(_batch, _primitives, session);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    if (fileName is not null) scenes.Add(SaveTierScene(target, fileName));
                    var pixels = new Color[width * height];
                    target.GetData(pixels);
                    return pixels;
                }
            }
        }
        finally { _renderer = previousRenderer; }

        static int ChangedInBody(Color[] before, Color[] after, int halfWidth)
        {
            var changed = 0;
            for (var y = height / 2 - halfWidth; y < height / 2 + halfWidth; y++)
            for (var x = width / 2 - halfWidth; x < width / 2 + halfWidth; x++)
                if (before[y * width + x] != after[y * width + x]) changed++;
            return changed;
        }
    }

    private void AssertSplashRasterCoverage(GameContent content, ICollection<string> assertions)
    {
        var variants = new[]
        {
            ("frost_spire", 1, 0), ("frost_spire", 4, 1),
            ("ember_coil", 2, 0), ("ember_coil", 4, 0),
            ("breaker_cannon", 3, 0), ("breaker_cannon", 4, 1),
            ("siege_mortar", 1, 0), ("siege_mortar", 4, 1)
        };
        const int size = 320;
        var center = new Vector2(size / 2f);
        using var target = new RenderTarget2D(GraphicsDevice, size, size, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        var pixels = new Color[size * size];
        foreach (var (id, stage, branch) in variants)
        {
            var session = CreateBurstSession(content, id, stage, assertions, branch);
            var tower = session.Towers[0];
            var radius = tower.Level.SplashRadius;
            Require(radius > 0 && radius < size / 2 - 8,
                $"{id} stage {stage}: authored splash fits the isolated native-scale review canvas.", assertions);
            foreach (var reduced in new[] { false, true })
            {
                var checksum = SessionChecksum.Compute(session, 0);
                var coverage = Measure(radius);
                var compact = Measure(radius * .65f);
                var mode = reduced ? "Reduced" : "Full";
                Require(coverage.Exterior == 0 && coverage.Reach >= radius * .55f && coverage.Directions >= 3,
                    $"{id} stage {stage} ({mode}): splash spray spreads broadly around the impact and stays near the damage area.", assertions);
                Require(coverage.OuterMean < 50 && coverage.OuterBrightFraction < .08f,
                    $"{id} stage {stage} ({mode}): the outer plume remains low-intensity and sparse instead of covering the area with a bright plate.", assertions);
                Require(coverage.Spread > compact.Spread * 1.15f,
                    $"{id} stage {stage} ({mode}): increasing splash radius increases visible spray spread without relying on an outlined perimeter.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{id} stage {stage}: measuring splash coverage preserves synchronized state.", assertions);

                (float Reach, float Spread, int Directions, int Exterior, float OuterMean, float OuterBrightFraction)
                    Measure(float drawRadius)
                {
                    GraphicsDevice.SetRenderTarget(target);
                    GraphicsDevice.Clear(Color.Black);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                    TowerAttackArt.DrawSplash(_batch, _primitives, id, center, tower.Definition.Visual.PrimaryColor,
                        drawRadius, .5f, 0, reduced, tower.LevelIndex + 1, tower.IsApex);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    target.GetData(pixels);
                    var directions = new bool[4];
                    var exterior = 0;
                    var visible = 0;
                    var outerSamples = 0;
                    var brightOuterSamples = 0;
                    var outerEnergy = 0f;
                    var squaredSpread = 0f;
                    var reach = 0f;
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        var pixel = pixels[index];
                        var brightness = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                        var offset = new Vector2(index % size + .5f, index / size + .5f) - center;
                        var distance = offset.Length();
                        if (distance >= drawRadius * .3f && distance <= drawRadius)
                        {
                            outerSamples++;
                            outerEnergy += brightness;
                            if (brightness >= 180) brightOuterSamples++;
                        }
                        if (brightness < 4) continue;
                        visible++;
                        squaredSpread += distance * distance;
                        reach = MathF.Max(reach, distance);
                        if (distance > drawRadius + 2) exterior++;
                        if (distance < drawRadius * .45f) continue;
                        var quadrant = (offset.X >= 0 ? 1 : 0) + (offset.Y >= 0 ? 2 : 0);
                        directions[quadrant] = true;
                    }
                    return (reach, MathF.Sqrt(squaredSpread / Math.Max(1, visible)), directions.Count(value => value),
                        exterior, outerEnergy / Math.Max(1, outerSamples),
                        brightOuterSamples / (float)Math.Max(1, outerSamples));
                }
            }
        }
    }
}
