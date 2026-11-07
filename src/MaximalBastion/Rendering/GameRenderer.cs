using MaximalBastion.Combat;
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
    internal bool QuietEnemyHud { get; set; } = true;
    internal bool HideEnemyHud { get; set; }
    internal bool RichEnemyMaterials { get; set; }
    internal int EnemyMaterialParticleCount => _enemyVisuals.ParticleCount;
    internal int LastEnemyAuraRegionCount { get; private set; }
    internal int LastEnemyGlyphCount { get; private set; }
    internal int LastEnemyHealthBarCount { get; private set; }
    internal int LastEnemyShieldBarCount { get; private set; }
    internal IReadOnlyList<EnemyHudStrip> LastEnemyStrips { get; private set; } = Array.Empty<EnemyHudStrip>();
    private readonly HashSet<(int Source, int Target)> _shardLaunchTargets = new();
    private readonly EnemyVisualTimeline _enemyVisuals = new();
    private EnemyLocomotionPath? _enemyLocomotion;
    private EnemyGroundContact[] _enemyGroundContacts = [];
    private readonly Dictionary<int, SupportFeedback> _supportFeedback = new();
    private readonly record struct SupportFeedback(float Repair, float Shield, float Emitter);
    private readonly Dictionary<int, EnemyContact> _enemyContacts = new();
    private readonly record struct EnemyContact(EffectInstance Effect, AttackAppearance Appearance,
        float Progress, float Radius, float Strength);
    private readonly Dictionary<int, (int First, int Last)> _enemyAttackRanges = new();
    private readonly List<EnemyAttack> _enemyAttacks = new();
    private readonly record struct EnemyAttack(ProjectileInstance? Projectile, EffectInstance? Effect, int Next);
    internal int LastEnemyContactCount => _enemyContacts.Count;
    internal int LastEnemyAttackCount => _enemyAttacks.Count;

    public void Draw(SpriteBatch batch, PrimitiveRenderer primitives, MaximalBastion.GameSession session,
        bool showTransientCombat = true, float presentationLeadSeconds = 0, int foregroundTowerId = 0,
        bool? protocolVisualOverride = null)
    {
        var presentation = PresentationFrame.Create(session, presentationLeadSeconds);
        PrepareEnemyFeedback(session, presentation, showTransientCombat);
        _enemyVisuals.Observe(session, presentation.TimeSeconds, detailedMaterials: false);
        if (_enemyLocomotion is null || !ReferenceEquals(_enemyLocomotion.Map, session.Map))
            _enemyLocomotion = EnemyLocomotionPath.For(session.Map);
        primitives.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), session.Map.Definition.Background.BaseColor);
        DrawTerrain(batch, primitives, session);
        DrawPath(batch, primitives, session);
        if (session.Map.Definition.PathVisual.Style == "trail")
            RainlineDistrictArt.Weather(batch, primitives, ReducedEffects ? 0 : session.Statistics.SimulatedSeconds);
        DrawMarkers(batch, primitives, session);
        DrawProtocolEnvelopes(batch, primitives, session, presentation, protocolVisualOverride);
        DrawTacticalDefenses(batch, primitives, session, presentation);
        DrawRanges(batch, primitives, session);
        DrawEnemySupportRanges(batch, primitives, session, presentation);
        if (showTransientCombat) DrawEnemySupportWaves(batch, primitives, session, presentation);
        DrawEnemyGroundShadows(batch, primitives, session, presentation);
        DrawTowers(batch, primitives, session, presentation, protocolVisualOverride);
        LastEnemyAuraRegionCount = 0;
        if (showTransientCombat)
        {
            DrawProjectiles(batch, primitives, session, presentation);
            DrawEffects(batch, primitives, session, presentation);
        }
        DrawEnemyLayer(batch, primitives, session, presentation, signalCarriers: false);
        if (showTransientCombat) DrawEffects(batch, primitives, session, presentation, foreground: true);
        DrawForegroundTowerOverlays(batch, primitives, session, presentation, foregroundTowerId, protocolVisualOverride);
        if (showTransientCombat) DrawShardDischarges(batch, primitives, session, presentation);
        // Each signal carrier, including its gem, is one complete foreground unit.
        DrawEnemyLayer(batch, primitives, session, presentation, signalCarriers: true);
        DrawEnemyMeters(batch, primitives, session, presentation);
        DrawPlacementIndicators(batch, primitives, session);
    }

    internal void DrawCombatShowcase(SpriteBatch batch, PrimitiveRenderer primitives,
        MaximalBastion.GameSession session, float presentationLeadSeconds = 0, bool? protocolVisualOverride = null,
        Rectangle? enemyHudBounds = null)
    {
        var presentation = PresentationFrame.Create(session, presentationLeadSeconds);
        PrepareEnemyFeedback(session, presentation, true);
        _enemyVisuals.Observe(session, presentation.TimeSeconds, detailedMaterials: false);
        if (_enemyLocomotion is null || !ReferenceEquals(_enemyLocomotion.Map, session.Map))
            _enemyLocomotion = EnemyLocomotionPath.For(session.Map);
        DrawPath(batch, primitives, session);
        DrawProtocolEnvelopes(batch, primitives, session, presentation, protocolVisualOverride);
        DrawEnemySupportRanges(batch, primitives, session, presentation);
        DrawEnemySupportWaves(batch, primitives, session, presentation);
        DrawEnemyGroundShadows(batch, primitives, session, presentation);
        DrawTowers(batch, primitives, session, presentation, protocolVisualOverride);
        LastEnemyAuraRegionCount = 0;
        DrawProjectiles(batch, primitives, session, presentation);
        DrawEffects(batch, primitives, session, presentation);
        DrawEnemyLayer(batch, primitives, session, presentation, signalCarriers: false);
        DrawEffects(batch, primitives, session, presentation, foreground: true);
        DrawShardDischarges(batch, primitives, session, presentation);
        DrawEnemyLayer(batch, primitives, session, presentation, signalCarriers: true);
        DrawEnemyMeters(batch, primitives, session, presentation, enemyHudBounds);
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
        foreach (var node in session.Map.Definition.PowerNodes)
        {
            var position = node.Position.ToVector2();
            p.Ring(batch, position, node.Radius, node.NodeColor * .46f, 1);
            PowerNodeArt.DrawDevice(batch, p, position, node.NodeColor, glow: !ReducedEffects);
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
                (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) ? ColorPalette.BeaconEnergy : ColorPalette.Cyan) * .48f, 1);
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
        PresentationFrame presentation, bool? protocolVisualOverride = null)
    {
        var time = presentation.TimeSeconds;
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);

        // Precise support boundaries stay beneath tower silhouettes and foreground interaction art.
        foreach (var supportTower in session.Towers.Where(tower => tower.IsSupport && !tower.IsSandboxDisabled))
        {
            var opacity = supportTower == session.SelectedTower ? .60f :
                supportTower == session.HoveredTower ? .48f : .35f;
            p.Ring(batch, supportTower.Position, session.GetEffectiveAuraRange(supportTower),
                ColorPalette.BeaconEnergy * opacity, 1);
        }

        foreach (var tower in session.Towers)
            if (tower != autoTower)
                DrawTower(batch, p, session, presentation, tower, time, protocolVisualOverride: protocolVisualOverride);

        // Auto is a temporary render priority, not a permanent tower property.
        // Moving Auto to another tower naturally returns this one to normal order.
        if (autoTower is not null)
            DrawTower(batch, p, session, presentation, autoTower, time, protocolVisualOverride: protocolVisualOverride);
    }

    private void DrawProtocolEnvelopes(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, bool? protocolVisualOverride)
    {
        // Field density stays beneath machinery, range boundaries, and placement feedback.
        foreach (var tower in session.Towers)
        {
            var strength = ProtocolPower(tower, presentation, protocolVisualOverride);
            if (tower.IsSandboxDisabled || strength <= 0) continue;
            TowerProtocolArt.DrawEnvelope(batch, p, tower.Position, TowerRadius(tower, presentation),
                tower.Definition.Id, tower.LevelIndex + 1, tower.IsApex,
                presentation.TimeSeconds, ReducedEffects, strength);
        }
    }

    private float TowerRadius(TowerInstance tower, PresentationFrame presentation) =>
        tower.Definition.Visual.Radius * presentation.TowerScale(tower) *
        (tower.IsSupport && !ReducedEffects ? 1 + MathF.Sin(presentation.TimeSeconds * 3 + tower.Id) * .06f : 1);

    private static float ProtocolPower(TowerInstance tower, PresentationFrame presentation, bool? visualOverride) =>
        tower.IsDisrupted ? 0 : visualOverride.HasValue ? (visualOverride.Value ? 1 : 0) :
        TowerProtocolArt.Strength(tower.OverdriveRemaining, tower.Protocol.DurationSeconds, presentation.LeadSeconds);

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
        switch (tower.Definition.Id)
        {
            case "shard_fan": return -MathHelper.PiOver2 + presentation.TimeSeconds * .24f;
            case "siege_mortar": return -MathHelper.PiOver2 + presentation.TimeSeconds * .14f;
            case "frost_spire": return -MathHelper.PiOver2 + presentation.TimeSeconds * .22f;
            case "signal_beacon": return -MathHelper.PiOver2;
        }
        if (tower.Definition.Id == "prism_beam" && ActivePrismBeam(session, tower.Id, presentation) is { } beam)
        {
            var shot = presentation.EffectTargetPosition(beam) - tower.Position;
            if (shot.LengthSquared() > .000001f) return MathF.Atan2(shot.Y, shot.X);
        }
        var target = tower.IsSupport ? null : session.TargetSelector.Select(tower.Position,
            session.GetEffectiveRange(tower), tower.TargetMode, session.Enemies);
        return target is null ? -MathHelper.PiOver2 : MathF.Atan2(target.Position.Y - tower.Position.Y, target.Position.X - tower.Position.X);
    }

    internal static Vector2 PrismBeamOrigin(GameSession session, EffectInstance effect, PresentationFrame presentation)
    {
        if (effect.SourceTowerId == 0) return presentation.EffectSourcePosition(effect);
        var tower = session.Towers.FirstOrDefault(candidate => candidate.Id == effect.SourceTowerId);
        if (tower is null) return effect.Start;
        var ray = presentation.EffectTargetPosition(effect) - tower.Position;
        var length = ray.Length();
        if (length < .001f) return tower.Position;
        var barrel = tower.Definition.Visual.Radius * presentation.TowerScale(tower) *
            TowerTierVisuals.PrismBarrelLength(tower.LevelIndex + 1, tower.IsApex);
        return tower.Position + ray / length * MathF.Min(barrel, length);
    }

    private void DrawTower(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, TowerInstance tower, float time, bool groundEffects = true,
        bool? protocolVisualOverride = null)
    {
        var radius = TowerRadius(tower, presentation);
        var aim = TowerAim(session, tower, presentation);
        var recoil = MathF.Max(0, tower.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f * .2f;
        if (tower == session.HoveredTower || tower == session.SelectedTower)
            BattlefieldInteractionArt.DrawFootprint(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, tower.LevelIndex + 1, tower.IsApex,
                tower == session.SelectedTower ? ColorPalette.Gold : ColorPalette.Cyan,
                tower == session.SelectedTower ? 1 : .65f, tower == session.SelectedTower, groundEffects && !ReducedEffects);
        NightGridArt.Tower(batch, p, tower.Position, radius,
            tower.Definition.Id, tower.LevelIndex + 1, aim, ReducedEffects ? 0 : time, recoil,
            tower.IsSandboxDisabled ? .4f : 1, !ReducedEffects, groundEffects, tower.IsApex,
            power: tower.IsDisrupted ? .18f : 1);
        if (tower.IsSandboxDisabled)
        {
            BattlefieldInteractionArt.DrawOwnerContact(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, ColorPalette.Coral);
            return;
        }
        if (tower.IsSupport && !tower.IsDisrupted)
            TowerAttackArt.DrawBeaconBroadcast(batch, p, tower.Position, ColorPalette.BeaconEnergy, radius,
                time, ReducedEffects, tower.LevelIndex + 1, tower.IsApex);
        var protocolPower = ProtocolPower(tower, presentation, protocolVisualOverride);
        if (protocolPower > 0)
            TowerProtocolArt.Draw(batch, p, tower.Position, radius, tower.Definition.Id,
                tower.LevelIndex + 1, tower.IsApex, aim, time, ReducedEffects, glow: groundEffects,
                strength: protocolPower, external: groundEffects, firing: recoil / .2f, envelope: false);
        if (session.HasLiveSupport(tower))
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
        MaximalBastion.GameSession session, PresentationFrame presentation, int foregroundTowerId,
        bool? protocolVisualOverride = null)
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
            DrawTower(batch, p, session, presentation, autoTower, presentation.TimeSeconds, groundEffects: false,
                protocolVisualOverride: protocolVisualOverride);
        if (remoteSelectedTower is not null && remoteSelectedTower != autoTower && !remoteSelectedTower.IsSandboxDisabled)
            DrawTower(batch, p, session, presentation, remoteSelectedTower, presentation.TimeSeconds, groundEffects: false,
                protocolVisualOverride: protocolVisualOverride);
        if (session.SelectedTower is { IsSandboxDisabled: false } selected && selected != autoTower && selected != remoteSelectedTower)
            DrawTower(batch, p, session, presentation, selected, presentation.TimeSeconds, groundEffects: false,
                protocolVisualOverride: protocolVisualOverride);

        // The attached Auto controller stays readable above transient combat effects.
        if (autoTower is not null && !autoTower.IsSandboxDisabled)
            BattlefieldInteractionArt.DrawAutoController(batch, p, autoTower.Position, autoTower.Definition.Visual.Radius,
                autoTower.Definition.Id, presentation.TimeSeconds, ReducedEffects, autoTower.IsOverdriven);
    }

    private void DrawEnemySupportRanges(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation)
    {
        foreach (var enemy in session.Enemies)
            EnemySignalArt.DrawSupportRange(batch, p, session, enemy, enemy.Position, ReducedEffects,
                presentation.TimeSeconds);
    }

    private void PrepareEnemyFeedback(GameSession session, PresentationFrame presentation, bool enabled)
    {
        _supportFeedback.Clear();
        _enemyContacts.Clear();
        _enemyAttackRanges.Clear();
        _enemyAttacks.Clear();
        if (!enabled) return;
        foreach (var projectile in session.Projectiles.Projectiles)
            if (!projectile.IsExpired && IsLiveTarget(projectile.Target))
                AddEnemyAttack(projectile.Target!.Id, projectile, null);
        foreach (var effect in session.Effects.Effects)
        {
            var signal = effect.Signal;
            var remaining = presentation.EffectRemaining(effect);
            var progress = MathHelper.Clamp(remaining / MathF.Max(.001f, effect.Duration), 0, 1);
            if (progress <= 0) continue;
            if (session.CounterSupportEnabled && signal.Role is EnemySignalRole.Restorer or EnemySignalRole.Bulwark)
            {
                if (effect.Kind == EffectKind.Flash && signal.TargetEnemyId != 0)
                {
                    _supportFeedback.TryGetValue(signal.TargetEnemyId, out var feedback);
                    _supportFeedback[signal.TargetEnemyId] = signal.Role == EnemySignalRole.Restorer
                        ? feedback with { Repair = MathF.Max(feedback.Repair, progress) }
                        : feedback with { Shield = MathF.Max(feedback.Shield, progress) };
                }
                else if (effect.Kind == EffectKind.Splash && signal.TargetEnemyId == 0)
                {
                    _supportFeedback.TryGetValue(signal.SourceEnemyId, out var feedback);
                    _supportFeedback[signal.SourceEnemyId] = feedback with { Emitter = MathF.Max(feedback.Emitter, progress) };
                }
            }

            if (signal.IsKnown || effect.Purpose != EffectPurpose.Combat || !IsLiveTarget(effect.TargetEnemy)) continue;
            var appearance = effect.Appearance.Resolve(session, effect.SourceTowerId);
            if (!appearance.IsKnown) continue;
            var radius = effect.Radius;
            switch (effect.Kind)
            {
                case EffectKind.Impact:
                    break;
                case EffectKind.Splash:
                    // The area outlives its compact contact but keeps the recipient's draw order.
                    AddEnemyAttack(effect.TargetEnemy!.Id, null, effect);
                    var contactDuration = MathF.Min(.12f, effect.Duration);
                    progress = MathHelper.Clamp(1 - MathF.Max(0, effect.Duration - remaining) /
                        MathF.Max(.001f, contactDuration), 0, 1);
                    if (progress <= 0) continue;
                    radius = 9;
                    break;
                case EffectKind.Beam:
                    if (effect.BeamStyle == BeamStyle.Prism && effect.SourceTowerId != 0 &&
                        ActivePrismBeam(session, effect.SourceTowerId, presentation) != effect) continue;
                    AddEnemyAttack(effect.TargetEnemy!.Id, null, effect);
                    radius = effect.BeamStyle == BeamStyle.Prism ? 7 : appearance.TowerId == "arc_relay" ? 9 : radius;
                    break;
                case EffectKind.Ricochet:
                    if (MathF.Max(0, effect.Duration - remaining) < effect.TravelDuration)
                    {
                        AddEnemyAttack(effect.TargetEnemy!.Id, null, effect);
                        continue;
                    }
                    progress = MathHelper.Clamp(remaining / MathF.Max(.001f, effect.Duration - effect.TravelDuration), 0, 1);
                    radius = effect.ImpactRadius;
                    break;
                default:
                    continue;
            }
            var strength = TowerAttackArt.ContactRadius(radius, appearance.Level, appearance.IsApex) * progress;
            var targetId = effect.TargetEnemy!.Id;
            if (!_enemyContacts.TryGetValue(targetId, out var contact) || strength >= contact.Strength)
                _enemyContacts[targetId] = new EnemyContact(effect, appearance, progress, radius, strength);
        }
    }

    private static bool IsLiveTarget(EnemyInstance? enemy) => enemy is { IsDead: false, HasEscaped: false };

    private void AddEnemyAttack(int enemyId, ProjectileInstance? projectile, EffectInstance? effect)
    {
        // Reused linked ranges preserve attack order without scanning every shot for each enemy.
        var index = _enemyAttacks.Count;
        _enemyAttacks.Add(new EnemyAttack(projectile, effect, -1));
        if (_enemyAttackRanges.TryGetValue(enemyId, out var range))
        {
            _enemyAttacks[range.Last] = _enemyAttacks[range.Last] with { Next = index };
            _enemyAttackRanges[enemyId] = (range.First, index);
        }
        else _enemyAttackRanges[enemyId] = (index, index);
    }

    private void DrawEnemyAttacks(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        EnemyInstance enemy, Vector2 position, PresentationFrame presentation)
    {
        if (_enemyAttackRanges.TryGetValue(enemy.Id, out var range))
            for (var index = range.First; index >= 0; index = _enemyAttacks[index].Next)
            {
                var attack = _enemyAttacks[index];
                if (attack.Projectile is { } projectile)
                    DrawProjectile(batch, p, session, presentation, projectile);
                else if (attack.Effect is { } effect)
                {
                    var remaining = presentation.EffectRemaining(effect);
                    if (effect.Kind == EffectKind.Splash)
                    {
                        var appearance = effect.Appearance.Resolve(session, effect.SourceTowerId);
                        TowerAttackArt.DrawSplash(batch, p, appearance.TowerId!, effect.Start,
                            effect.Color, effect.Radius,
                            MathHelper.Clamp(remaining / MathF.Max(.001f, effect.Duration), 0, 1),
                            presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
                    }
                    else if (effect.Kind == EffectKind.Ricochet)
                        RicochetArt.Draw(batch, p, effect, remaining, presentation.TimeSeconds, ReducedEffects,
                            presentation.RicochetDestination(effect));
                    else
                        DrawAttackBeam(batch, p, session, presentation, effect,
                            effect.Appearance.Resolve(session, effect.SourceTowerId),
                            MathHelper.Clamp(remaining / MathF.Max(.001f, effect.Duration), 0, 1), drawContact: false);
                }
            }

        if (!_enemyContacts.TryGetValue(enemy.Id, out var contact)) return;
        var direction = contact.Effect.Kind switch
        {
            EffectKind.Impact => contact.Effect.Direction,
            EffectKind.Splash => Vector2.Zero,
            _ => position - presentation.EffectSourcePosition(contact.Effect)
        };
        TowerAttackArt.DrawImpact(batch, p, contact.Appearance.TowerId!, position, direction,
            contact.Effect.Color, contact.Radius, contact.Progress, presentation.TimeSeconds, ReducedEffects,
            contact.Appearance.Level, contact.Appearance.IsApex);
    }

    private void DrawEnemySupportWaves(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation)
    {
        if (!session.CounterSupportEnabled) return;
        foreach (var effect in session.Effects.Effects)
        {
            if (effect.Kind != EffectKind.Splash || effect.Signal.TargetEnemyId != 0 ||
                effect.Signal.Role is not (EnemySignalRole.Restorer or EnemySignalRole.Bulwark)) continue;
            var remaining = presentation.EffectRemaining(effect);
            if (remaining <= 0) continue;
            var source = session.Enemies.FirstOrDefault(enemy => enemy.Id == effect.Signal.SourceEnemyId);
            if (source is null) continue;
            EnemySignalArt.DrawSupportWave(batch, p, source, source.Position,
                EnemySignalArt.SupportRangeRadius(session, source), remaining / MathF.Max(.001f, effect.Duration), ReducedEffects);
        }
    }

    private void DrawEnemySupportFeedback(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float time)
    {
        if (!_supportFeedback.TryGetValue(enemy.Id, out var feedback)) return;
        if (feedback.Repair > 0)
            EnemyStatusArt.DrawRepairPulse(batch, p, enemy, position, radius, heading, feedback.Repair, time, ReducedEffects);
        if (feedback.Shield > 0)
            EnemyStatusArt.DrawShieldPulse(batch, p, enemy, position, radius, heading, feedback.Shield, time, ReducedEffects);
        if (feedback.Emitter > 0)
            EnemySignalArt.DrawEmitterPulse(batch, p, enemy, position, radius, heading, feedback.Emitter, time, ReducedEffects);
    }

    private void DrawEnemyMeters(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, Rectangle? hudBounds = null)
    {
        LastEnemyGlyphCount = LastEnemyHealthBarCount = LastEnemyShieldBarCount = 0;
        LastEnemyStrips = Array.Empty<EnemyHudStrip>();
        // Durability stays screen-facing above complete enemy bodies.
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var position = presentation.EnemyPosition(enemy);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var heading = EnemyHeading(session, enemy, presentation.LeadSeconds);
            if (HideEnemyHud) continue;
            DrawEnemyVitals(batch, p, session, enemy, position, radius, heading, hudBounds);
        }
    }
    private void DrawEnemyGroundShadows(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation)
    {
        var required = session.Enemies.Count * 8;
        if (_enemyGroundContacts.Length < required)
            _enemyGroundContacts = new EnemyGroundContact[Math.Max(required, _enemyGroundContacts.Length * 2)];
        for (var index = 0; index < session.Enemies.Count; index++)
        {
            var enemy = session.Enemies[index];
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var position = presentation.EnemyPosition(enemy);
            var heading = EnemyHeading(session, enemy, presentation.LeadSeconds);
            var contacts = _enemyGroundContacts.AsSpan(index * 8, EnemyLocomotion.ContactSlots(enemy).Length);
            EnemyModelArt.SampleGroundContacts(enemy, position, heading,
                enemy.DistanceAlongPath + enemy.CurrentSpeed * presentation.LeadSeconds,
                _enemyLocomotion, false, contacts);
            EnemyModelArt.DrawGroundShadow(batch, p, enemy, position, heading,
                presentation.TimeSeconds, ReducedEffects, contacts);
        }
    }

    private void DrawEnemyLayer(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, bool signalCarriers)
    {
        var time = presentation.TimeSeconds;
        for (var index = 0; index < session.Enemies.Count; index++)
        {
            var enemy = session.Enemies[index];
            if (enemy.IsDead || enemy.HasEscaped ||
                (enemy.SignalRole != EnemySignalRole.None) != signalCarriers) continue;
            var position = presentation.EnemyPosition(enemy);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var headingAngle = EnemyHeading(session, enemy, presentation.LeadSeconds);
            var feedback = _enemyVisuals.Feedback(enemy, time);
            EnemyModelArt.Draw(batch, p, enemy, position, headingAngle, time, ReducedEffects,
                enemy.DistanceAlongPath + enemy.CurrentSpeed * presentation.LeadSeconds,
                Math.Clamp((enemy.DamagePauseTimer - .88f) * 6, 0, .7f), feedback.OverdriveOpening, _enemyLocomotion,
                castShadows: false,
                groundContacts: _enemyGroundContacts.AsSpan(index * 8, EnemyLocomotion.ContactSlots(enemy).Length),
                overdriveElapsed: feedback.OverdriveElapsed);

            LastEnemyAuraRegionCount += EnemyDebuffAuraArt.Draw(batch, p, session, enemy, position, radius, time,
                ReducedEffects, _enemyVisuals.FogFeedback(enemy, time));
            EnemyStatusArt.DrawDurability(batch, p, enemy, position, radius, headingAngle, time, ReducedEffects);
            DrawEnemySupportFeedback(batch, p, enemy, position, radius, headingAngle, time);
            EnemyStunArt.Draw(batch, p, enemy, position, radius, headingAngle, time, ReducedEffects);
            EnemyTransitionArt.Draw(batch, p, enemy, position, radius, headingAngle, feedback, ReducedEffects);
            DrawEnemyAttacks(batch, p, session, enemy, position, presentation);
        }
    }

    private void DrawEnemyVitals(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        EnemyInstance enemy, Vector2 position, float radius, float heading, Rectangle? hudBounds = null)
    {
        var visible = EnemyHudPresentation.VisibleBounds(enemy, position, heading);
        var bounds = hudBounds ?? new Rectangle(0, GameConstants.TopBarHeight,
            GameConstants.SidebarX, GameConstants.LogicalHeight - GameConstants.TopBarHeight);
        if (visible.Z < bounds.Left || visible.X >= bounds.Right ||
            visible.W < bounds.Top || visible.Y >= bounds.Bottom) return;
        var frames = EnemyHudPresentation.MeterFrames(enemy, position, radius, bounds);
        var healthRatio = enemy.MaxHealth > 0 ? enemy.Health / enemy.MaxHealth : 0;
        Meter(frames.Health, healthRatio, ColorPalette.Health(healthRatio));
        LastEnemyHealthBarCount++;
        if (enemy.IsSandboxImmortal)
        {
            var frame = frames.Health;
            p.FillRect(batch, new Rectangle(frame.Left, frame.Top, 1, frame.Height), ColorPalette.Paper);
            p.FillRect(batch, new Rectangle(frame.Right - 1, frame.Top, 1, frame.Height), ColorPalette.Paper);
        }
        if (enemy.Shield > 0)
        {
            Meter(frames.Shield, enemy.Shield / EnemyShieldCapacity(session, enemy), ColorPalette.Shield);
            LastEnemyShieldBarCount++;
        }

        void Meter(Rectangle frame, float ratio, Color color)
        {
            var fill = new Rectangle(frame.X + 1, frame.Y + 1, frame.Width - 2, frame.Height - 2);
            p.FillRect(batch, frame, ColorPalette.Ink);
            p.FillRect(batch, fill, ColorPalette.HealthTrack);
            var filledWidth = (int)MathF.Round(fill.Width * Math.Clamp(ratio, 0, 1));
            if (ratio > 0) filledWidth = Math.Max(1, filledWidth);
            if (filledWidth > 0)
                p.FillRect(batch, new Rectangle(fill.X, fill.Y, filledWidth, fill.Height), color);
        }
    }

    private static float EnemyShieldCapacity(GameSession session, EnemyInstance enemy)
    {
        var naturalCapacity = enemy.Definition.Shield + (enemy.IsBoss ? enemy.MaxHealth * .12f : 0);
        var signalCapacity = session.CounterSupportEnabled
            ? enemy.Definition.Shield + enemy.MaxHealth * session.Challenge.CounterShieldCapacityFraction : 0;
        return MathF.Max(enemy.Shield, MathF.Max(naturalCapacity, signalCapacity));
    }

    private static float EnemyHeading(GameSession session, EnemyInstance enemy, float leadSeconds = 0)
    {
        var distance = enemy.DistanceAlongPath + enemy.CurrentSpeed * leadSeconds;
        var forward = EnemyLocomotionPath.For(session.Map).Forward(distance, EnemyLocomotion.MotionRadius(enemy), EnemyLocomotion.UsesTracks(enemy));
        return MathF.Atan2(forward.Y, forward.X);
    }

    private void DrawProjectiles(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var projectile in session.Projectiles.Projectiles)
        {
            if (projectile.IsExpired || IsLiveTarget(projectile.Target)) continue;
            DrawProjectile(batch, p, session, presentation, projectile);
        }
    }

    private void DrawProjectile(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, ProjectileInstance projectile)
    {
        var position = presentation.ProjectilePosition(projectile);
        var destination = projectile.Kind == ProjectileKind.Homing && IsLiveTarget(projectile.Target)
            ? presentation.EnemyPosition(projectile.Target!)
            : projectile.AimPoint;
        var forward = destination - position;
        if (forward.LengthSquared() < .0001f) forward = destination - projectile.Position;
        var appearance = projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId);
        ProjectileArt.Draw(batch, p, appearance.TowerId, position, forward, projectile.Radius, projectile.Color,
            presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
    }

    private void DrawShardDischarges(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation)
    {
        _shardLaunchTargets.Clear();
        foreach (var projectile in session.Projectiles.Projectiles)
        {
            if (projectile.IsExpired) continue;
            var appearance = projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId);
            if (appearance.TowerId != "shard_fan") continue;
            var source = session.Towers.FirstOrDefault(tower => tower.Id == projectile.Payload.SourceTowerId);
            if (source is null || source.IsSandboxDisabled) continue;
            var flash = MathF.Max(0, source.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f;
            if (flash <= .02f) continue;
            var position = presentation.ProjectilePosition(projectile);
            var radius = source.Definition.Visual.Radius * presentation.TowerScale(source);
            if (Vector2.DistanceSquared(position, source.Position) >= radius * radius * 2.6f * 2.6f) continue;
            if (!_shardLaunchTargets.Add((source.Id, projectile.Target?.Id ?? 0))) continue;
            TowerAttackArt.DrawShardLaunch(batch, p, source.Position, position, radius,
                NightGridArt.TowerAccent("shard_fan"), flash, ReducedEffects, appearance.Level, appearance.IsApex);
        }
    }

    private void DrawEffects(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, bool foreground = false)
    {
        foreach (var effect in session.Effects.Effects)
        {
            var overlay = effect.Signal.IsKnown || effect.Kind == EffectKind.Ping ||
                effect.Purpose == EffectPurpose.ForgeCharge ||
                effect.Purpose == EffectPurpose.ProtocolActivation && effect.Kind != EffectKind.Splash;
            if (overlay != foreground) continue;
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
            var bodyBound = appearance.IsKnown && effect.Purpose == EffectPurpose.Combat && IsLiveTarget(effect.TargetEnemy);
            if (effect.Kind == EffectKind.Ricochet)
            {
                if (bodyBound) continue;
                RicochetArt.Draw(batch, p, effect, remaining, presentation.TimeSeconds, ReducedEffects,
                    presentation.RicochetDestination(effect));
                continue;
            }
            if (effect.Purpose == EffectPurpose.ProtocolActivation)
            {
                TransientBurstArt.DrawProtocol(batch, p, effect, appearance, progress, ReducedEffects);
                continue;
            }

            if (effect.Kind == EffectKind.Beam)
            {
                if (bodyBound) continue;
                if (effect.BeamStyle == BeamStyle.Prism && effect.SourceTowerId != 0 &&
                    ActivePrismBeam(session, effect.SourceTowerId, presentation) != effect) continue;
                DrawAttackBeam(batch, p, session, presentation, effect, appearance, progress, drawContact: true);
                continue;
            }
            if (appearance.IsKnown && effect.Kind == EffectKind.Splash)
            {
                if (bodyBound) continue;
                TowerAttackArt.DrawSplash(batch, p, appearance.TowerId!, effect.Start,
                    effect.Color, effect.Radius, progress, presentation.TimeSeconds, ReducedEffects,
                    appearance.Level, appearance.IsApex);
                continue;
            }
            if (appearance.IsKnown && effect.Kind is EffectKind.Impact or EffectKind.Flash or EffectKind.Shatter)
            {
                if (bodyBound && effect.Kind == EffectKind.Impact) continue;
                TowerAttackArt.DrawImpact(batch, p, appearance.TowerId!, presentation.EffectTargetPosition(effect), effect.Direction,
                    effect.Color, effect.Radius, progress, presentation.TimeSeconds, ReducedEffects,
                    appearance.Level, appearance.IsApex);
                continue;
            }
            if (effect.Kind == EffectKind.Ping)
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
            else
                TransientBurstArt.DrawGeneric(batch, p, effect, progress, ReducedEffects);
        }
    }

    private void DrawAttackBeam(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, EffectInstance effect, AttackAppearance appearance, float progress,
        bool drawContact)
    {
        var contact = presentation.EffectTargetPosition(effect);
        var source = presentation.EffectSourcePosition(effect);
        if (effect.BeamStyle == BeamStyle.Prism)
        {
            var origin = PrismBeamOrigin(session, effect, presentation);
            PrismBeamArt.Draw(batch, p, origin, contact, effect.Color, effect.Radius,
                progress, ReducedEffects, effect.SourceTowerId != 0, appearance.Level, appearance.IsApex);
            if (drawContact)
                TowerAttackArt.DrawImpact(batch, p, "prism_beam", contact, contact - source,
                    effect.Color, 7, progress, presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex);
            if (!ReducedEffects && session.Towers.FirstOrDefault(t => t.Id == effect.SourceTowerId) is { } emitter)
            {
                var recoil = MathF.Max(0, emitter.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f * .2f;
                NightGridArt.DrawMuzzleFlash(batch, p, origin,
                    emitter.Definition.Visual.Radius * presentation.TowerScale(emitter), effect.Color, recoil);
            }
        }
        else if (appearance.TowerId == "arc_relay")
            TowerAttackArt.DrawArc(batch, p, source, contact, effect.Color, progress,
                presentation.TimeSeconds, ReducedEffects, appearance.Level, appearance.IsApex, drawContact);
        else
        {
            if (!ReducedEffects)
                p.Line(batch, source, contact, effect.Color * (.16f * progress), effect.Radius + 9);
            p.Line(batch, source, contact, effect.Color * progress, Math.Max(2, effect.Radius + 1));
            p.Line(batch, source, contact, ColorPalette.Paper * progress, 1);
        }
    }

    private void DrawSignalEffect(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        PresentationFrame presentation, EffectInstance effect, float progress)
    {
        var signal = effect.Signal;
        if (signal.Role is EnemySignalRole.Restorer or EnemySignalRole.Bulwark) return;
        var time = presentation.TimeSeconds;
        var source = session.Enemies.FirstOrDefault(enemy => enemy.Id == signal.SourceEnemyId &&
            !enemy.IsDead && !enemy.HasEscaped);
        if (signal.TargetTowerId != 0)
        {
            var target = session.Towers.FirstOrDefault(tower => tower.Id == signal.TargetTowerId && !tower.IsSandboxDisabled);
            if (target is null || (signal.Role == EnemySignalRole.Disruptor ? !target.IsDisrupted : !target.IsSuppressed)) return;
            if (effect.Kind == EffectKind.Beam && source is not null)
                EnemySignalArt.DrawTransfer(batch, p, SignalEmitterPosition(session, source, presentation),
                    TowerStatusArt.SignalContact(target.Position, TowerRadius(target, presentation), target.Definition.Id,
                        target.LevelIndex + 1, target.IsApex, TowerAim(session, target, presentation)),
                    signal.Role, progress, time, ReducedEffects);
            else if (effect.Kind == EffectKind.Flash && !ReducedEffects)
                p.Glow(batch, target.Position, target.Definition.Visual.Radius * .8f,
                    EnemySignalGlyphRenderer.Accent(signal.Role), .22f * progress);
            return;
        }
        if (source is null || (signal.Role == EnemySignalRole.Jammer && effect.Kind == EffectKind.Flash)) return;
        EnemySignalArt.DrawEmitterPulse(batch, p, source, presentation.EnemyPosition(source),
            EnemyModelArt.BodyRadius(source), EnemyHeading(session, source, presentation.LeadSeconds), progress, time, ReducedEffects);
    }

    private static Vector2 SignalEmitterPosition(GameSession session, EnemyInstance enemy, PresentationFrame presentation) =>
        EnemySignalArt.MountedPosition(enemy, presentation.EnemyPosition(enemy), EnemyModelArt.BodyRadius(enemy),
            EnemyHeading(session, enemy, presentation.LeadSeconds));

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
