using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed class GameRenderer
{
    public bool ReducedEffects { get; set; }

    public void Draw(SpriteBatch batch, PrimitiveRenderer primitives, MaximalBastion.GameSession session,
        bool showTransientCombat = true, float presentationLeadSeconds = 0, int foregroundTowerId = 0)
    {
        var presentation = PresentationFrame.Create(session, presentationLeadSeconds);
        primitives.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), session.Map.Definition.Background.BaseColor);
        DrawTerrain(batch, primitives, session);
        DrawPath(batch, primitives, session);
        if (session.Map.Definition.PathVisual.Style == "trail")
            RainlineDistrictArt.Weather(batch, primitives, ReducedEffects ? 0 : session.Statistics.SimulatedSeconds);
        DrawMarkers(batch, primitives, session);
        DrawTacticalDefenses(batch, primitives, session, presentation);
        DrawRanges(batch, primitives, session);
        DrawTowers(batch, primitives, session, presentation);
        DrawEnemies(batch, primitives, session, presentation);
        if (showTransientCombat)
        {
            DrawProjectiles(batch, primitives, session, presentation);
            DrawEffects(batch, primitives, session, presentation);
        }
        DrawForegroundTowerOverlays(batch, primitives, session, presentation, foregroundTowerId);
        DrawPlacementIndicators(batch, primitives, session);
    }

    internal void DrawCombatShowcase(SpriteBatch batch, PrimitiveRenderer primitives,
        MaximalBastion.GameSession session, float presentationLeadSeconds = 0)
    {
        var presentation = PresentationFrame.Create(session, presentationLeadSeconds);
        DrawPath(batch, primitives, session);
        DrawTowers(batch, primitives, session, presentation);
        DrawEnemies(batch, primitives, session, presentation);
        DrawProjectiles(batch, primitives, session, presentation);
        DrawEffects(batch, primitives, session, presentation);
    }

    private void DrawTerrain(SpriteBatch batch, PrimitiveRenderer p, GameSession session)
    {
        var time = ReducedEffects ? 0 : session.Statistics.SimulatedSeconds;
        MapEnvironmentRenderer.Ground(batch, p, session.Map.Definition, time);
        foreach (var region in session.Map.BuildableRegions)
        {
            var active = session.PlacementTowerId is not null || session.TacticalPlacement == TacticalPlacementKind.ChargeForge;
            var pointer = session.HasPlacementPreview ? session.PlacementPreviewPosition : session.PlacementPosition;
            var hover = active && region.Contains(pointer.ToPoint());
            MapEnvironmentRenderer.Deck(batch, p, region, session.Map.Definition.PathVisual.Style, hover);
        }
        var inspected = session.SelectedTower ?? session.HoveredTower;
        var connectedNodes = inspected is null ? null : session.Map.GetPowerNodes(inspected.Position);
        foreach (var node in session.Map.Definition.PowerNodes)
        {
            var position = node.Position.ToVector2();
            p.Ring(batch, position, node.Radius, node.NodeColor * .46f, 1);
            PowerNodeArt.DrawDevice(batch, p, position, node.NodeColor, glow: !ReducedEffects);
            if (inspected is not null && connectedNodes!.Contains(node))
                PowerNodeArt.DrawLink(batch, p, position, inspected.Position, node.NodeColor);
        }
        p.DrawRect(batch, new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight), ColorPalette.MapBoundary);
    }

    private void DrawPath(SpriteBatch batch, PrimitiveRenderer p, GameSession session) =>
        MapEnvironmentRenderer.Path(batch, p, session.Map.Definition, ReducedEffects ? 0 : session.Statistics.SimulatedSeconds);

    private void DrawRanges(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        if (session.SelectedTower is { IsSupport: false } selected)
            p.Ring(batch, selected.Position, DisplayRange(session, selected), ColorPalette.Gold * .54f, 1);
        else if (session.SelectedTower is null && session.HoveredTower is { IsSupport: false } hovered)
            p.Ring(batch, hovered.Position, DisplayRange(session, hovered), ColorPalette.Cyan * .38f, 1);

        var placementOnMap = session.PlacementPosition.X >= 0 && session.PlacementPosition.X < GameConstants.MapWidth &&
                             session.PlacementPosition.Y >= GameConstants.TopBarHeight && session.PlacementPosition.Y < GameConstants.LogicalHeight;
        if (placementOnMap && session.HasPlacementPreview && session.PlacementTowerId is { } towerId &&
            session.Content.Towers.TryGetValue(towerId, out var definition))
        {
            var position = session.PlacementPreviewPosition;
            var placementRange = definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
                ? definition.Levels[0].AuraRange
                : definition.Levels[0].Range;
            p.Ring(batch, position, placementRange,
                (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) ? ColorPalette.Green : ColorPalette.Cyan) * .48f, 1);
            BattlefieldInteractionArt.DrawFootprint(batch, p, position, definition.Visual.Radius,
                definition.Id, 1, false,
                session.PlacementFailure == PlacementFailure.None ? ColorPalette.PlacementValid : ColorPalette.PlacementInvalid,
                1, true, !ReducedEffects);
            NightGridArt.Tower(batch, p, position, definition.Visual.Radius, definition.Id, opacity: .5f, lights: !ReducedEffects);
        }

        if (!placementOnMap || session.TacticalPlacement == TacticalPlacementKind.None) return;
        if (session.TacticalPlacement == TacticalPlacementKind.PulsePlate)
        {
            if (!session.HasPlacementPreview) return;
            var tactical = session.Content.Tactics.EmergencyDefense;
            var position = session.PlacementPreviewPosition;
            BattlefieldInteractionArt.DrawFootprint(batch, p, position, tactical.Visual.Radius,
                tactical.Id, 1, false,
                session.PlacementFailure == PlacementFailure.None ? ColorPalette.PlacementValid : ColorPalette.PlacementInvalid,
                .8f, true, !ReducedEffects);
            NightGridArt.Tower(batch, p, position, tactical.Visual.Radius, tactical.Id, tactical.Charges, opacity: .5f, lights: !ReducedEffects);
        }
        else if (session.TacticalPlacement == TacticalPlacementKind.ChargeForge)
        {
            if (!session.HasPlacementPreview) return;
            var generator = session.Content.Tactics.Generator;
            BattlefieldInteractionArt.DrawFootprint(batch, p, session.PlacementPreviewPosition, generator.Visual.Radius,
                generator.Id, 1, false,
                session.PlacementFailure == PlacementFailure.None ? ColorPalette.PlacementValid : ColorPalette.PlacementInvalid,
                .8f, true, !ReducedEffects);
            NightGridArt.Tower(batch, p, session.PlacementPreviewPosition, generator.Visual.Radius, generator.Id, opacity: .5f, lights: !ReducedEffects);
        }
    }

    private static float DisplayRange(MaximalBastion.GameSession session, TowerInstance tower) =>
        tower.IsSupport ? session.GetEffectiveAuraRange(tower) : session.GetEffectiveRange(tower);

    private static void DrawPlacementIndicators(SpriteBatch batch, PrimitiveRenderer p,
        MaximalBastion.GameSession session)
    {
        if (!session.HasPlacementPreview || session.PlacementTowerId is null) return;

        var position = session.PlacementPreviewPosition;
        DrawPowerNodePlacementIndicator(batch, p, position, session.Map.GetPowerNodes(position));
    }

    internal static void DrawPowerNodePlacementIndicator(SpriteBatch batch, PrimitiveRenderer p, Vector2 position,
        IReadOnlyList<PowerNodeData> powerNodes) => PowerNodeArt.DrawCouplers(batch, p, position, powerNodes);

    private void DrawTacticalDefenses(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var plate in session.EmergencyDefenses)
        {
            var pulse = plate.ArmRemaining > 0 ? 0.82f : 1f;
            NightGridArt.Tower(batch, p, plate.Position, plate.Definition.Visual.Radius * pulse,
                plate.Definition.Id, plate.ChargesRemaining, lights: !ReducedEffects);
        }

        if (session.Generator is not { } generator) return;
        var visual = generator.Definition.Visual;
        if (session.SelectedGenerator == generator)
            BattlefieldInteractionArt.DrawFootprint(batch, p, generator.Position, visual.Radius,
                generator.Definition.Id, generator.LevelIndex + 1, false, ColorPalette.Gold, 1, true, !ReducedEffects);
        NightGridArt.Tower(batch, p, generator.Position, visual.Radius, generator.Definition.Id,
            generator.LevelIndex + 1, time: ReducedEffects ? 0 : presentation.TimeSeconds, lights: !ReducedEffects);
        if (session.IsCoOp)
            BattlefieldInteractionArt.DrawOwnerContact(batch, p, generator.Position, visual.Radius,
                generator.Definition.Id, generator.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral);
        var track = new Rectangle((int)generator.Position.X - 22, (int)generator.Position.Y + visual.Radius + 8, 44, 5);
        p.FillRect(batch, track, ColorPalette.HealthTrack);
        p.FillRect(batch, new Rectangle(track.X, track.Y, (int)(track.Width * generator.ProductionProgress), track.Height), ColorPalette.Green);
    }

    private void DrawTowers(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        var time = presentation.TimeSeconds;
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);

        // Precise support boundaries stay beneath tower silhouettes and foreground interaction art.
        foreach (var supportTower in session.Towers.Where(tower => tower.IsSupport && !tower.IsSandboxDisabled))
        {
            var opacity = supportTower == session.SelectedTower ? .60f :
                supportTower == session.HoveredTower ? .48f : .35f;
            p.Ring(batch, supportTower.Position, session.GetEffectiveAuraRange(supportTower),
                ColorPalette.Green * opacity, 1);
        }

        foreach (var tower in session.Towers)
            if (tower != autoTower)
                DrawTower(batch, p, session, presentation, tower, time);

        // Auto is a temporary render priority, not a permanent tower property.
        // Moving Auto to another tower naturally returns this one to normal order.
        if (autoTower is not null)
            DrawTower(batch, p, session, presentation, autoTower, time);
    }

    internal static EffectInstance? ActivePrismBeam(GameSession session, int towerId, PresentationFrame presentation)
    {
        for (var index = session.Effects.Effects.Count - 1; index >= 0; index--)
        {
            var effect = session.Effects.Effects[index];
            if (effect.Kind == EffectKind.Beam && effect.BeamStyle == BeamStyle.Prism &&
                effect.SourceTowerId == towerId && presentation.EffectRemaining(effect) > 0)
                return effect;
        }
        return null;
    }

    internal static float TowerAim(GameSession session, TowerInstance tower, PresentationFrame presentation)
    {
        if (tower.Definition.Id == "prism_beam" && ActivePrismBeam(session, tower.Id, presentation) is { } beam)
        {
            var shot = beam.End - beam.Start;
            if (shot.LengthSquared() > .000001f) return MathF.Atan2(shot.Y, shot.X);
        }
        var target = tower.IsSupport ? null : session.TargetSelector.Select(tower.Position,
            session.GetEffectiveRange(tower), tower.TargetMode, session.Enemies);
        return target is null ? -MathHelper.PiOver2 : MathF.Atan2(target.Position.Y - tower.Position.Y, target.Position.X - tower.Position.X);
    }

    internal static Vector2 PrismBeamOrigin(GameSession session, EffectInstance effect, PresentationFrame presentation)
    {
        if (effect.SourceTowerId == 0) return effect.Start;
        var tower = session.Towers.FirstOrDefault(candidate => candidate.Id == effect.SourceTowerId);
        if (tower is null) return effect.Start;
        var ray = effect.End - tower.Position;
        var length = ray.Length();
        if (length < .001f) return tower.Position;
        var barrel = tower.Definition.Visual.Radius * presentation.TowerScale(tower) *
            TowerTierVisuals.PrismBarrelLength(tower.LevelIndex + 1, tower.IsApex);
        return tower.Position + ray / length * MathF.Min(barrel, length);
    }

    private void DrawTower(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, TowerInstance tower, float time, bool groundEffects = true)
    {
        var supportPulse = tower.IsSupport && !ReducedEffects ? 1f + MathF.Sin(time * 3f + tower.Id) * 0.06f : 1f;
        var pulse = supportPulse * presentation.TowerScale(tower);
        var aim = TowerAim(session, tower, presentation);
        var recoil = MathF.Max(0, tower.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f * .2f;
        if (tower == session.HoveredTower || tower == session.SelectedTower)
            BattlefieldInteractionArt.DrawFootprint(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, tower.LevelIndex + 1, tower.IsApex,
                tower == session.SelectedTower ? ColorPalette.Gold : ColorPalette.Cyan,
                tower == session.SelectedTower ? 1 : .65f, tower == session.SelectedTower, groundEffects && !ReducedEffects);
        NightGridArt.Tower(batch, p, tower.Position, tower.Definition.Visual.Radius * pulse,
            tower.Definition.Id, tower.LevelIndex + 1, aim, ReducedEffects ? 0 : time, recoil,
            tower.IsSandboxDisabled ? .4f : 1, !ReducedEffects, groundEffects, tower.IsApex);
        if (tower.IsSandboxDisabled)
        {
            BattlefieldInteractionArt.DrawOwnerContact(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, ColorPalette.Coral);
            return;
        }
        var radius = tower.Definition.Visual.Radius * pulse;
        if (session.Map.GetPowerBuff(tower.Position).IsPowered)
            TowerStatusArt.DrawPowerConnection(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects);
        if (tower.IsSupport && !tower.IsDisrupted)
            TowerAttackArt.DrawBeaconBroadcast(batch, p, tower.Position, ColorPalette.Green, radius,
                time, ReducedEffects, tower.LevelIndex + 1, tower.IsApex);
        if (tower.IsOverdriven)
            TowerProtocolArt.Draw(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects, glow: groundEffects);
        if (session.GetSupportBuff(tower).IsActive)
            TowerStatusArt.DrawSupport(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects);
        if (tower.IsDisrupted)
            TowerStatusArt.DrawDisrupted(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects);
        else if (tower.IsSuppressed)
            TowerStatusArt.DrawSuppressed(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects);
        if (session.IsCoOp)
            BattlefieldInteractionArt.DrawOwnerContact(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, tower.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral);
    }

    private void DrawForegroundTowerOverlays(SpriteBatch batch, PrimitiveRenderer p,
        MaximalBastion.GameSession session, PresentationFrame presentation, int foregroundTowerId)
    {
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);
        var remoteSelectedTower = foregroundTowerId > 0
            ? session.Towers.FirstOrDefault(tower => tower.Id == foregroundTowerId)
            : null;

        if (remoteSelectedTower is not null && !remoteSelectedTower.IsSandboxDisabled)
            BattlefieldInteractionArt.DrawFootprint(batch, p, remoteSelectedTower.Position,
                remoteSelectedTower.Definition.Visual.Radius, remoteSelectedTower.Definition.Id,
                remoteSelectedTower.LevelIndex + 1, remoteSelectedTower.IsApex, ColorPalette.Cobalt,
                .75f, true, glow: false);

        // Towers carrying temporary interaction state remain readable above dense
        // units and attack effects. Once the state moves or clears, normal authored
        // tower order resumes automatically. Ground shadows and light pools stay beneath combat.
        if (autoTower is not null && !autoTower.IsSandboxDisabled)
            DrawTower(batch, p, session, presentation, autoTower, presentation.TimeSeconds, groundEffects: false);
        if (remoteSelectedTower is not null && remoteSelectedTower != autoTower && !remoteSelectedTower.IsSandboxDisabled)
            DrawTower(batch, p, session, presentation, remoteSelectedTower, presentation.TimeSeconds, groundEffects: false);
        if (session.SelectedTower is { IsSandboxDisabled: false } selected && selected != autoTower && selected != remoteSelectedTower)
            DrawTower(batch, p, session, presentation, selected, presentation.TimeSeconds, groundEffects: false);

        // The attached Auto controller stays readable above transient combat effects.
        if (autoTower is not null && !autoTower.IsSandboxDisabled)
            BattlefieldInteractionArt.DrawAutoController(batch, p, autoTower.Position, autoTower.Definition.Visual.Radius,
                autoTower.Definition.Id, presentation.TimeSeconds, ReducedEffects);
    }

    private void DrawEnemies(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        DrawEnemySignalFields(batch, p, session, presentation);
        DrawEnemyLayer(batch, p, session, presentation, signalCarriers: false);
        DrawEnemyLayer(batch, p, session, presentation, signalCarriers: true);
    }

    private void DrawEnemyLayer(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, bool signalCarriers)
    {
        var time = presentation.TimeSeconds;
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped ||
                (enemy.SignalRole != EnemySignalRole.None) != signalCarriers) continue;
            var position = presentation.EnemyPosition(enemy);
            var radius = EnemyBodyRadius(enemy, time);
            var headingAngle = EnemyHeading(session, enemy);
            NightGridArt.Enemy(batch, p, position, radius, enemy.Definition.Id,
                headingAngle, ReducedEffects ? 0 : time, enemy.IsBoss,
                Math.Clamp((enemy.DamagePauseTimer - .88f) * 6, 0, .7f), !ReducedEffects);

            EnemySignalArt.DrawHardware(batch, p, enemy, position, radius, headingAngle, time, ReducedEffects);
            EnemyStatusArt.Draw(batch, p, enemy, position, radius, headingAngle, time, ReducedEffects);

            var barWidth = enemy.Radius * (enemy.IsBoss ? 3.5f : 2.5f);
            var healthRatio = enemy.MaxHealth > 0 ? enemy.Health / enemy.MaxHealth : 0;
            p.HealthBar(batch, position - new Vector2(0, enemy.Radius + 11), barWidth,
                healthRatio, ColorPalette.Health(healthRatio), ColorPalette.HealthTrack, ColorPalette.Ink);
            if (enemy.IsSandboxImmortal)
            {
                var at = position - new Vector2(0, enemy.Radius + 11);
                p.Line(batch, at + new Vector2(-barWidth * .5f, 3), at + new Vector2(barWidth * .5f, 3), ColorPalette.Muted, 1);
                p.Line(batch, at + new Vector2(-barWidth * .5f - 1, -1), at + new Vector2(-barWidth * .5f - 1, 2), ColorPalette.Paper, 2);
                p.Line(batch, at + new Vector2(barWidth * .5f + 1, -1), at + new Vector2(barWidth * .5f + 1, 2), ColorPalette.Paper, 2);
            }

            if (enemy.Shield > 0)
            {
                var naturalShieldCapacity = enemy.Definition.Shield + (enemy.IsBoss ? enemy.MaxHealth * 0.12f : 0f);
                var signalShieldCapacity = session.CounterSupportEnabled
                    ? enemy.Definition.Shield + enemy.MaxHealth * session.Challenge.CounterShieldCapacityFraction
                    : 0f;
                var shieldCapacity = MathF.Max(enemy.Shield, MathF.Max(naturalShieldCapacity, signalShieldCapacity));
                p.HealthBar(batch, position - new Vector2(0, enemy.Radius + 18), barWidth,
                    enemy.Shield / shieldCapacity, ColorPalette.Shield, ColorPalette.HealthTrack, ColorPalette.Ink, 6);
            }
        }
    }

    private static void DrawEnemySignalFields(SpriteBatch batch, PrimitiveRenderer p,
        MaximalBastion.GameSession session, PresentationFrame presentation)
    {
        if (!session.CounterPressureEnabled) return;
        var sources = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                enemy.SignalRole is EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark)
            .ToArray();
        if (sources.Length == 0) return;

        var supportRadius = session.Challenge.CounterSupportRadius;
        foreach (var source in sources)
        {
            var sourcePosition = presentation.EnemyPosition(source);
            var accent = EnemySignalGlyphRenderer.Accent(source.SignalRole);
            p.Ring(batch, sourcePosition, supportRadius,
                accent * .15f, 1);
        }
    }

    private float EnemyBodyRadius(EnemyInstance enemy, float time) => enemy.Radius *
        (!ReducedEffects && enemy.IsBoss ? 1f + MathF.Sin(time * 8f) * .09f : 1f);

    private static float EnemyHeading(GameSession session, EnemyInstance enemy)
    {
        var ahead = session.Map.Path.GetPosition(MathF.Min(session.Map.Path.TotalLength, enemy.DistanceAlongPath + 3));
        var direction = ahead - enemy.Position;
        return MathF.Atan2(direction.Y, direction.X);
    }

    private void DrawProjectiles(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var projectile in session.Projectiles.Projectiles)
        {
            var position = presentation.ProjectilePosition(projectile);
            var destination = projectile.Kind == Combat.ProjectileKind.Homing &&
                              projectile.Target is { IsDead: false, HasEscaped: false }
                ? presentation.EnemyPosition(projectile.Target)
                : projectile.AimPoint;
            var forward = destination - position;
            if (forward.LengthSquared() < .0001f) forward = destination - projectile.Position;
            var appearance = projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId);
            ProjectileArt.Draw(batch, p, appearance.TowerId, position, forward, projectile.Radius, projectile.Color,
                presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
        }
    }

    private void DrawEffects(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var effect in session.Effects.Effects)
        {
            var remaining = presentation.EffectRemaining(effect);
            if (remaining <= 0) continue;
            var progress = MathHelper.Clamp(remaining / MathF.Max(effect.Duration, 0.001f), 0, 1);
            var alpha = (byte)(100 + 155 * progress);
            var effectColor = effect.Color * progress;
            if (effect.Signal.IsKnown)
            {
                DrawSignalEffect(batch, p, session, presentation, effect, progress);
                continue;
            }
            var appearance = effect.Appearance.Resolve(session, effect.SourceTowerId);
            if (effect.Kind == EffectKind.Beam && effect.BeamStyle == BeamStyle.Prism)
            {
                if (effect.SourceTowerId != 0 && ActivePrismBeam(session, effect.SourceTowerId, presentation) != effect)
                    continue;
                PrismBeamArt.Draw(batch, p, PrismBeamOrigin(session, effect, presentation), effect.End, effect.Color, effect.Radius,
                    progress, ReducedEffects, effect.SourceTowerId != 0, appearance.Level, appearance.IsApex);
                TowerAttackArt.DrawImpact(batch, p, "prism_beam", effect.End, effect.End - effect.Start,
                    effect.Color, 7, progress, presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
                if (!ReducedEffects && session.Towers.FirstOrDefault(t => t.Id == effect.SourceTowerId) is { } emitter)
                {
                    var recoil = MathF.Max(0, emitter.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f * .2f;
                    NightGridArt.DrawMuzzleFlash(batch, p, PrismBeamOrigin(session, effect, presentation),
                        emitter.Definition.Visual.Radius * presentation.TowerScale(emitter), effect.Color, recoil);
                }
                continue;
            }
            if (effect.Kind == EffectKind.Beam && appearance.TowerId == "arc_relay")
            {
                TowerAttackArt.DrawArc(batch, p, effect.Start, effect.End, effect.Color, progress,
                    presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
                continue;
            }
            if (appearance.IsKnown && effect.Kind is EffectKind.Impact or EffectKind.Splash or EffectKind.Flash or EffectKind.Shatter)
            {
                TowerAttackArt.DrawImpact(batch, p, appearance.TowerId!, effect.Start, effect.Direction,
                    effect.Color, effect.Radius, progress, presentation.TimeSeconds, ReducedEffects,
                    appearance.Level, appearance.IsApex);
                continue;
            }
            if (!ReducedEffects && effect.Kind != EffectKind.Ping)
            {
                p.Glow(batch, effect.Start, MathF.Min(130, effect.Radius * 3 + 18), effect.Color, progress * .65f);
                if (effect.Kind is EffectKind.Shatter or EffectKind.Splash)
                {
                    var age = 1 - progress;
                    for (var shard = 0; shard < 8; shard++)
                    {
                        var a = shard * MathHelper.TwoPi / 8 + effect.Start.X;
                        var direction = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        var distance = age * (effect.Radius + 28);
                        var at = effect.Start + direction * distance;
                        p.Line(batch, at, at - direction * (3 + 8 * progress), effectColor, 2);
                    }
                }
            }
            if (effect.Kind == EffectKind.Beam)
            {
                if (!ReducedEffects)
                    p.Line(batch, effect.Start, effect.End, effect.Color * (.16f * progress), effect.Radius + 9);
                p.Line(batch, effect.Start, effect.End, effectColor, Math.Max(2, effect.Radius + 1));
                p.Line(batch, effect.Start, effect.End, ColorPalette.Paper * progress, 1);
            }
            else if (effect.Kind == EffectKind.Ping)
            {
                var expansion = 1f - progress;
                var radius = effect.Radius + expansion * 34f;
                p.DashedRing(batch, effect.Start, radius, effectColor, 18, 3);
                if (!ReducedEffects)
                {
                    p.Ring(batch, effect.Start, Math.Max(7, effect.Radius * progress), ColorPalette.WithAlpha(ColorPalette.Paper, alpha), 2);
                    p.DrawShape(batch, effect.Start, 8, "diamond", effectColor, ColorPalette.Paper, 1, false);
                }
            }
            else if (effect.Kind == EffectKind.Impact)
            {
                var age = 1f - progress;
                var radius = effect.Radius * (0.72f + age * 0.38f);
                p.Ring(batch, effect.Start, radius, effectColor, 2);
                if (!ReducedEffects)
                {
                    for (var index = 0; index < 4; index++)
                    {
                        var angle = MathHelper.PiOver4 + index * MathHelper.PiOver2;
                        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        p.Line(batch, effect.Start + direction * (radius + 1),
                            effect.Start + direction * (radius + 4 + age * 3), effectColor, 2);
                    }
                }
            }
            else if (effect.Kind == EffectKind.Splash)
            {
                var age = 1f - progress;
                var radius = effect.Radius * MathHelper.SmoothStep(0.18f, 1f, age);
                p.Ring(batch, effect.Start, MathF.Max(3, radius), effectColor, ReducedEffects ? 2 : 4);
                if (!ReducedEffects)
                {
                    p.Ring(batch, effect.Start, MathF.Max(2, radius * 0.64f),
                        ColorPalette.WithAlpha(ColorPalette.Paper, (byte)(alpha * 0.72f)), 2);
                    for (var index = 0; index < 6; index++)
                    {
                        var angle = index * MathHelper.TwoPi / 6f;
                        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        var inner = radius * 0.72f;
                        p.Line(batch, effect.Start + direction * inner,
                            effect.Start + direction * MathF.Min(effect.Radius, inner + 7 + age * 5),
                            effectColor, 2);
                    }
                }
            }
            else if (effect.Kind == EffectKind.Shatter)
            {
                var age = 1f - progress;
                var radius = effect.Radius * (0.72f + age * 0.36f);
                p.Ring(batch, effect.Start, MathF.Max(3, radius), effectColor, 2);
                if (!ReducedEffects)
                {
                    for (var index = 0; index < 6; index++)
                    {
                        var angle = index * MathHelper.TwoPi / 6f + age * 0.22f;
                        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        p.Line(batch,
                            effect.Start + direction * (radius * (0.34f + age * 0.18f)),
                            effect.Start + direction * (radius + 3 + age * 5),
                            effectColor, 2);
                    }
                }
            }
            else
            {
                var radius = effect.Radius * (1.2f - progress * 0.2f);
                p.Ring(batch, effect.Start, radius, effectColor, 4);
                if (!ReducedEffects && progress > 0.2f)
                {
                    p.Ring(batch, effect.Start, Math.Max(2, radius - 5), ColorPalette.Paper, 2);
                    if (effect.Radius >= 20)
                    {
                        var spokeInner = radius + 3;
                        var spokeOuter = radius + 10 + 5 * (1 - progress);
                        for (var index = 0; index < 4; index++)
                        {
                            var angle = MathHelper.PiOver4 + index * MathHelper.PiOver2;
                            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                            p.Line(batch, effect.Start + direction * spokeInner, effect.Start + direction * spokeOuter, effectColor, 2);
                        }
                    }
                }
            }
        }
    }

    private void DrawSignalEffect(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, EffectInstance effect, float progress)
    {
        var signal = effect.Signal;
        var time = presentation.TimeSeconds;
        var source = session.Enemies.FirstOrDefault(enemy => enemy.Id == signal.SourceEnemyId &&
            !enemy.IsDead && !enemy.HasEscaped);
        if (signal.TargetEnemyId != 0)
        {
            var target = session.Enemies.FirstOrDefault(enemy => enemy.Id == signal.TargetEnemyId &&
                !enemy.IsDead && !enemy.HasEscaped);
            if (target is null) return;
            var position = presentation.EnemyPosition(target);
            if (effect.Kind == EffectKind.Beam)
            {
                if (source is not null && source != target)
                    EnemySignalArt.DrawTransfer(batch, p, presentation.EnemyPosition(source), position,
                        signal.Role, progress, time, ReducedEffects);
                return;
            }
            var radius = EnemyBodyRadius(target, time);
            var heading = EnemyHeading(session, target);
            if (signal.Role == EnemySignalRole.Restorer)
                EnemyStatusArt.DrawRepairPulse(batch, p, target, position, radius, heading, progress, time, ReducedEffects);
            else if (signal.Role == EnemySignalRole.Bulwark)
                EnemyStatusArt.DrawShieldPulse(batch, p, target, position, radius, heading, progress, time, ReducedEffects);
            return;
        }
        if (signal.TargetTowerId != 0)
        {
            var target = session.Towers.FirstOrDefault(tower => tower.Id == signal.TargetTowerId && !tower.IsSandboxDisabled);
            if (target is null || (signal.Role == EnemySignalRole.Disruptor ? !target.IsDisrupted : !target.IsSuppressed)) return;
            if (effect.Kind == EffectKind.Beam && source is not null)
                EnemySignalArt.DrawTransfer(batch, p, presentation.EnemyPosition(source), target.Position,
                    signal.Role, progress, time, ReducedEffects);
            else if (effect.Kind == EffectKind.Flash && !ReducedEffects)
                p.Glow(batch, target.Position, target.Definition.Visual.Radius * .8f,
                    EnemySignalGlyphRenderer.Accent(signal.Role), .22f * progress);
            return;
        }
        if (source is null || (signal.Role == EnemySignalRole.Jammer && effect.Kind == EffectKind.Flash)) return;
        EnemySignalArt.DrawEmitterPulse(batch, p, source, presentation.EnemyPosition(source),
            EnemyBodyRadius(source, time), EnemyHeading(session, source), progress, time, ReducedEffects);
    }

    private static void DrawMarkers(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        var points = session.Map.Definition.Path.Select(point => point.ToVector2()).ToArray();
        if (points.Length < 2) return;

        var entryDirection = SafeDirection(points[1] - points[0]);
        var inset = Math.Max(30f, session.Map.Definition.PathWidth * 0.75f);
        var markerSpan = Math.Clamp(session.Map.Definition.PathWidth * 0.28f, 10f, 18f);

        var entry = ClampMarkerToField(points[0] + entryDirection * inset, markerSpan);

        // A quiet map-colored plaque sits beside the route rather than on top of
        // it. The exit marker is intentionally omitted: the path already ends at
        // the field boundary, and another symbol competes with defenses.
        DrawEntryMark(batch, p, entry, entryDirection, session.Map.Definition.PathWidth,
            session.Map.Definition.PathVisual);
    }

    private static void DrawEntryMark(SpriteBatch batch, PrimitiveRenderer p, Vector2 center,
        Vector2 direction, float pathWidth, PathVisualData visual)
    {
        var normal = new Vector2(-direction.Y, direction.X);
        // Entrance paths currently enter from the left, so the positive normal
        // consistently places this below the conduit: away from the HUD on
        // Foundry and away from Prism's nearby build zone.
        var plaqueCenter = ClampMarkerToField(center + normal * (pathWidth * 0.5f + 9f), 14f);
        var plaque = new Rectangle((int)plaqueCenter.X - 12, (int)plaqueCenter.Y - 6, 24, 12);
        p.FillRect(batch, plaque, visual.SecondaryColor);
        p.DrawRect(batch, plaque, Color.Lerp(visual.SecondaryColor, visual.AccentColor, 0.42f), 1);
        for (var offset = -4f; offset <= 4f; offset += 4f)
        {
            var barCenter = plaqueCenter + direction * offset;
            p.Line(batch, barCenter - normal * 2f, barCenter + normal * 2f,
                Color.Lerp(visual.SecondaryColor, visual.AccentColor, 0.62f), 1);
        }
    }

    private static Vector2 ClampMarkerToField(Vector2 position, float markerSpan)
    {
        var margin = markerSpan + 3f;
        return new Vector2(
            Math.Clamp(position.X, margin, GameConstants.MapWidth - margin),
            Math.Clamp(position.Y, margin, GameConstants.LogicalHeight - margin));
    }

    private static Vector2 SafeDirection(Vector2 delta)
    {
        return delta.LengthSquared() > 0.001f ? Vector2.Normalize(delta) : Vector2.UnitX;
    }
}
