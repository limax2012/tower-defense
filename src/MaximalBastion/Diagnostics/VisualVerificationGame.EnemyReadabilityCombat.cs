using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureEnemyReadabilityCombat(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            AssertEnemySupportRanges(content, font, display, assertions, scenes);
            foreach (var map in content.Maps.Values)
            {
                var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                var candidates = new List<Vector2>();
                foreach (var region in session.Map.BuildableRegions)
                for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
                for (var x = region.Left + 24; x < region.Right - 16; x += 24)
                    candidates.Add(new Vector2(x, y));
                candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 360))).ToList();
                var towers = content.Towers.Values.ToArray();
                for (var index = 0; index < 18; index++)
                {
                    var definition = towers[index % towers.Length];
                    Require(candidates.Any(point => session.TryPlaceTower(definition.Id, point)),
                        $"{map.Id}: crowded readability combat places legal {definition.DisplayName} machinery.", assertions);
                    var tower = session.SelectedTower!;
                    var stage = definition.Id is "breaker_cannon" or "arc_relay" ? 3 : index % 3 + 1;
                    if (stage >= 2)
                        Require(session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id),
                            $"{map.Id}: readability tower {tower.Id} reaches its authored T2 assembly.", assertions);
                    if (stage >= 3)
                        Require(session.TrySpecializeTower(tower.Id, definition.Specializations[0].Id),
                            $"{map.Id}: readability tower {tower.Id} reaches its authored T3 assembly.", assertions);
                }
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });

                var families = content.Enemies.Values.ToArray();
                var ranks = new[] { "Standard", "Elite", "Boss" };
                for (var index = 0; index < 36; index++)
                {
                    session.SpawnEnemy(families[index % families.Length].Id, 160, .45f,
                        ranks[index / families.Length % ranks.Length], (EnemySignalRole)(index % 6));
                    session.Enemies[^1].SetSandboxPathDistance(
                        90 + index * (session.Map.Path.TotalLength - 800) / 36, session.Map.Path);
                }
                // Let the initial signal delays elapse before the comparison.
                for (var step = 0; step < 222; step++) session.Update(1f / 60);
                Require(session.AnnouncementRemaining <= 0 && session.Enemies.Count == 36 &&
                        session.Enemies.All(enemy => !enemy.IsDead && !enemy.HasEscaped),
                    $"{map.Id}: all thirty-six enemies are present with a clear announcement area.", assertions);

                var sources = new Dictionary<StatusType, int>
                {
                    [StatusType.Burn] = session.Towers.First(tower => tower.Definition.Id == "ember_coil").Id,
                    [StatusType.Slow] = session.Towers.First(tower => tower.Definition.Id == "frost_spire").Id,
                    [StatusType.ArmorBreak] = session.Towers.First(tower => tower.Definition.Id == "breaker_cannon").Id,
                    [StatusType.Exposed] = session.Towers.First(tower => tower.Definition.Id == "prism_beam").Id,
                    [StatusType.Stun] = session.Towers.First(tower => tower.Definition.Id == "arc_relay").Id
                };
                for (var index = 0; index < session.Enemies.Count; index++)
                {
                    var enemy = session.Enemies[index];
                    enemy.SetSandboxPathDistance(90 + index * (session.Map.Path.TotalLength - 220) / 36,
                        session.Map.Path);
                    enemy.ApplyHealthDamage(enemy.MaxHealth * .22f);
                    enemy.TickRuntimeTimers(1.1f);
                    switch (index % 6)
                    {
                        case 0: Apply(StatusType.Burn); Apply(StatusType.Slow); break;
                        case 1: Apply(StatusType.Burn); Apply(StatusType.ArmorBreak); break;
                        case 2: Apply(StatusType.Slow); Apply(StatusType.Exposed); break;
                        case 3:
                        case 4:
                            enemy.GrantShield(enemy.MaxHealth * .10f,
                                enemy.Definition.Shield + enemy.MaxHealth * .20f);
                            Apply(StatusType.Burn);
                            break;
                        default: Apply(StatusType.Stun); Apply(StatusType.ArmorBreak); break;
                    }
                    if (index % 3 == 0) Apply(StatusType.Exposed);
                    enemy.ArmSignalAbility(0);

                    void Apply(StatusType type) => enemy.ApplyStatus(new StatusApplication
                    {
                        Type = type,
                        Duration = 12,
                        Magnitude = type == StatusType.Burn ? 10 : type == StatusType.ArmorBreak ? 4 :
                            type == StatusType.Stun ? 1 : .35f,
                        SourceId = sources[type],
                        ArmorPierce = type == StatusType.Burn ? 4 : 0
                    });
                }
                for (var step = 0; step < 12; step++) session.Update(1f / 60);
                Require(families.All(family => session.Enemies.Any(enemy => enemy.Definition.Id == family.Id)) &&
                        Enum.GetValues<EnemyRank>().All(rank => session.Enemies.Any(enemy => enemy.Rank == rank)) &&
                        Enum.GetValues<EnemySignalRole>().All(role => session.Enemies.Any(enemy => enemy.SignalRole == role)),
                    $"{map.Id}: crowded combat retains every family, rank, signal role, and ordinary carrier-free construction.", assertions);
                Require(Enum.GetValues<StatusType>().All(type =>
                        session.Enemies.Any(enemy => enemy.StatusEffects.Active.Any(status => status.Type == type))),
                    $"{map.Id}: every registered temporary status is present in crowded combat.", assertions);
                Require(session.Enemies.Any(enemy => enemy.StatusEffects.IsBurning && enemy.Shield > 0) &&
                        session.Enemies.Any(enemy => enemy.StatusEffects.IsBurning && enemy.StatusEffects.SlowFactor > 0) &&
                        session.Enemies.Any(enemy => enemy.StatusEffects.IsStunned && enemy.StatusEffects.ArmorReduction > 0),
                    $"{map.Id}: shield with burn, frost with burn, and stun with armor break coexist through the actual status controller.", assertions);
                Require(session.Enemies.All(enemy =>
                {
                    Span<EnemyCondition> zones = stackalloc EnemyCondition[4];
                    return EnemyDebuffAuraArt.CopyDebuffs(enemy, zones) <= 4;
                }), $"{map.Id}: each crowded enemy uses at most four debuff fog regions.", assertions);

                ui.HandleGameplayInput(Pointer(0, 0), session);
                var checksum = SessionChecksum.Compute(session, 234);
                foreach (var reduced in new[] { false, true })
                {
                    _renderer.ReducedEffects = reduced;
                    scenes.Add(Capture($"63-enemy-readability-{map.Id}-{(reduced ? "reduced" : "full")}.png",
                        ui, GameState.Playing, session));
                    Require(checksum == SessionChecksum.Compute(session, 234),
                        $"{map.Id}: crowded {(reduced ? "Reduced" : "Full")} readability rendering preserves the simulation checksum.", assertions);
                }

                if (map.Id is not ("foundry_loop" or "prism_circuit")) continue;
                _renderer.ReducedEffects = false;
                var baseline = RenderPixels(ui, GameState.Playing, session);
                var inspected = session.Enemies.Where(enemy => enemy.Position.X is > 70 and < 870 &&
                        enemy.Position.Y is > 140 and < 620)
                    .OrderByDescending(enemy => EnemyConditionPresentation.Compose(enemy).ActiveCount)
                    .ThenBy(enemy => Vector2.DistanceSquared(enemy.Position, new Vector2(480, 360)))
                    .First();
                ui.HandleGameplayInput(Pointer(inspected.Position.X, inspected.Position.Y), session);
                var inspection = RenderPixels(ui, GameState.Playing, session);
                Require(CountChangedPixels(baseline, inspection, new Rectangle(960, 90, 320, 600)) > 200,
                    $"{map.Id}: hovering a crowded enemy exposes condition details in the existing inspection terminal.", assertions);
                scenes.Add(Capture($"63a-enemy-readability-inspection-{map.Id}.png", ui, GameState.Playing, session));
                Require(checksum == SessionChecksum.Compute(session, 234),
                    $"{map.Id}: enemy hover inspection is presentation-only.", assertions);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private void AssertEnemySupportRanges(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var reduced in new[] { false, true })
        {
            _renderer.ReducedEffects = reduced;
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            foreach (var role in Enum.GetValues<EnemySignalRole>())
            {
                var carrier = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
                carrier.SpawnEnemy("t1_crawler", 1, 1, signalRole: role);
                var source = carrier.Enemies[0];
                source.SetSandboxPathDistance(700, carrier.Map.Path);
                foreach (var distance in new[] { 640f, 825f })
                {
                    carrier.SpawnEnemy("t1_crawler", 1, 1);
                    carrier.Enemies[^1].SetSandboxPathDistance(distance, carrier.Map.Path);
                }
                carrier.ConfigureCounterPressureSimulation(supportEnabled: false, attackersEnabled: true);
                Require(EnemySignalArt.SupportRangeRadius(carrier, source) == 0,
                    $"{role}: disabled support simulation has no application outline.", assertions);
                var reference = RenderPixels(ui, GameState.Playing, carrier);
                carrier.ConfigureCounterPressureSimulation(supportEnabled: true, attackersEnabled: true);
                var checksum = SessionChecksum.Compute(carrier, 234);
                var pixels = RenderPixels(ui, GameState.Playing, carrier);
                var support = role is EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark;
                var radius = carrier.Challenge.CounterSupportRadius;
                Require(EnemySignalArt.SupportRangeRadius(carrier, source) == (support ? radius : 0),
                    $"{role}: the visible support boundary matches the authored application radius.", assertions);
                var center = source.Position;
                var scale = GameConstants.RenderScale;
                var extent = radius + 8;
                var edgePixels = 0;
                var interiorPixels = 0;
                var exteriorPixels = 0;
                var minimumInteriorRadius = float.PositiveInfinity;
                var maximumInteriorRadius = 0f;
                for (var y = Math.Max(GameConstants.TopBarHeight * scale, (int)((center.Y - extent) * scale));
                     y < Math.Min(GameConstants.RenderHeight, (center.Y + extent) * scale); y++)
                for (var x = Math.Max(0, (int)((center.X - extent) * scale));
                     x < Math.Min(GameConstants.MapWidth * scale, (center.X + extent) * scale); x++)
                {
                    if (reference[y * GameConstants.RenderWidth + x] == pixels[y * GameConstants.RenderWidth + x]) continue;
                    var distance = Vector2.Distance(new Vector2((x + .5f) / scale, (y + .5f) / scale), center);
                    if (distance < radius - 4)
                    {
                        interiorPixels++;
                        minimumInteriorRadius = MathF.Min(minimumInteriorRadius, distance);
                        maximumInteriorRadius = MathF.Max(maximumInteriorRadius, distance);
                    }
                    else if (distance > radius + 2) exteriorPixels++;
                    else edgePixels++;
                }
                var ripple = role == EnemySignalRole.Accelerator && !reduced;
                var validInterior = ripple
                    ? interiorPixels > 0 && minimumInteriorRadius > 10 && maximumInteriorRadius - minimumInteriorRadius < radius * .18f
                    : interiorPixels == 0;
                Require(validInterior && exteriorPixels == 0 && (support ? edgePixels > 100 : edgePixels == 0),
                    $"{role}: {(reduced ? "Reduced" : "Full")} support uses a clear boundary without a filled disk or effects outside its edge.", assertions);
                Require(checksum == SessionChecksum.Compute(carrier, 234),
                    $"{role}: support-range rendering preserves the simulation checksum.", assertions);
                if (support)
                    scenes.Add(Capture($"63b-support-range-{role.ToString().ToLowerInvariant()}-{(reduced ? "reduced" : "full")}.png",
                        ui, GameState.Playing, carrier));
                source.ApplyHealthDamage(source.MaxHealth);
                Require(EnemySignalArt.SupportRangeRadius(carrier, source) == 0,
                    $"{role}: dead sources have no application outline.", assertions);
                carrier.SpawnEnemy("t1_crawler", 1, 1, signalRole: role);
                var escaped = carrier.Enemies[^1];
                escaped.UpdateMovement(1000, carrier.Map.Path);
                Require(escaped.HasEscaped && EnemySignalArt.SupportRangeRadius(carrier, escaped) == 0,
                    $"{role}: escaped sources have no application outline.", assertions);
            }
        }
    }
}
