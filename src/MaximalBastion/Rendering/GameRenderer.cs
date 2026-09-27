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
        foreach (var node in session.Map.Definition.PowerNodes)
        {
            var position = node.Position.ToVector2();
            p.Glow(batch, position, node.Radius * 2, node.NodeColor, .24f);
            p.DashedRing(batch, position, node.Radius, ColorPalette.WithPremultipliedAlpha(node.NodeColor, 140), 32, 2);
            p.DrawPolygon(batch, position, 13, 6, false, ColorPalette.Metal);
            p.DrawPolygon(batch, position, 8, 6, false, node.NodeColor);
            p.DrawPolygon(batch, position, 4, 6, false, ColorPalette.Paper);
        }
        p.DrawRect(batch, new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight), ColorPalette.MapBoundary);
    }

    private void DrawPath(SpriteBatch batch, PrimitiveRenderer p, GameSession session) =>
        MapEnvironmentRenderer.Path(batch, p, session.Map.Definition, ReducedEffects ? 0 : session.Statistics.SimulatedSeconds);

    private void DrawRanges(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        if (session.SelectedTower is { } selected)
            p.DashedRing(batch, selected.Position, DisplayRange(session, selected), ColorPalette.WithAlpha(ColorPalette.Gold, 155), 36, 2);
        else if (session.HoveredTower is { } hovered)
            p.DashedRing(batch, hovered.Position, DisplayRange(session, hovered), ColorPalette.WithAlpha(ColorPalette.Cyan, 105), 36, 2);

        var placementOnMap = session.PlacementPosition.X >= 0 && session.PlacementPosition.X < GameConstants.MapWidth &&
                             session.PlacementPosition.Y >= GameConstants.TopBarHeight && session.PlacementPosition.Y < GameConstants.LogicalHeight;
        if (placementOnMap && session.HasPlacementPreview && session.PlacementTowerId is { } towerId &&
            session.Content.Towers.TryGetValue(towerId, out var definition))
        {
            var position = session.PlacementPreviewPosition;
            var placementRange = definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
                ? definition.Levels[0].AuraRange
                : definition.Levels[0].Range;
            // This large ring communicates the tower's eventual attack/aura range.
            // It is intentionally distinct from the compact remote-player handling marker.
            p.DashedRing(batch, position, placementRange, ColorPalette.PlacementValid, 32, 2);
            NightGridArt.Tower(batch, p, position, definition.Visual.Radius, definition.Id, opacity: .5f, lights: !ReducedEffects);
            p.Brackets(batch, new Rectangle((int)position.X - 24, (int)position.Y - 24, 48, 48),
                session.PlacementFailure == PlacementFailure.None ? ColorPalette.PlacementValid : ColorPalette.PlacementInvalid);
        }

        if (!placementOnMap || session.TacticalPlacement == TacticalPlacementKind.None) return;
        if (session.TacticalPlacement == TacticalPlacementKind.PulsePlate)
        {
            if (!session.HasPlacementPreview) return;
            var tactical = session.Content.Tactics.EmergencyDefense;
            var position = session.PlacementPreviewPosition;
            NightGridArt.Tower(batch, p, position, tactical.Visual.Radius, tactical.Id, tactical.Charges, opacity: .5f, lights: !ReducedEffects);
        }
        else if (session.TacticalPlacement == TacticalPlacementKind.ChargeForge)
        {
            if (!session.HasPlacementPreview) return;
            var generator = session.Content.Tactics.Generator;
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
        IReadOnlyList<PowerNodeData> powerNodes)
    {
        if (powerNodes.Count == 0) return;

        var visibleNodeCount = Math.Min(3, powerNodes.Count);
        for (var index = 0; index < visibleNodeCount; index++)
        {
            var offset = (index - (visibleNodeCount - 1) * 0.5f) * 7f;
            var pipCenter = position + new Vector2(offset, 0);
            var pixelCenter = new Point((int)MathF.Round(pipCenter.X), (int)MathF.Round(pipCenter.Y));
            p.FillRect(batch, new Rectangle(pixelCenter.X - 5, pixelCenter.Y - 5, 11, 11), ColorPalette.Navy);
            p.FillRect(batch, new Rectangle(pixelCenter.X - 3, pixelCenter.Y - 3, 7, 7),
                powerNodes[index].NodeColor);
            // Odd-sized rectangles have their geometric center on the half pixel.
            // Use that same center for the circular pip so all three layers align.
            p.Circle(batch, pixelCenter.ToVector2() + new Vector2(0.5f), 2f, ColorPalette.Paper);
        }
    }

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
        NightGridArt.Tower(batch, p, generator.Position, visual.Radius, generator.Definition.Id,
            generator.LevelIndex + 1, time: ReducedEffects ? 0 : presentation.TimeSeconds, lights: !ReducedEffects);
        if (session.IsCoOp)
            p.Ring(batch, generator.Position, visual.Radius + 8, generator.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
        var track = new Rectangle((int)generator.Position.X - 22, (int)generator.Position.Y + visual.Radius + 8, 44, 5);
        p.FillRect(batch, track, ColorPalette.HealthTrack);
        p.FillRect(batch, new Rectangle(track.X, track.Y, (int)(track.Width * generator.ProductionProgress), track.Height), ColorPalette.Green);
        if (session.SelectedGenerator == generator)
            p.Ring(batch, generator.Position, visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);
    }

    private void DrawTowers(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        var time = presentation.TimeSeconds;
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);

        // Aura ranges are battlefield information, not tower foreground art.
        // Keep them beneath every tower so an Auto-armed support tower cannot
        // put its large dashed range over neighboring tower silhouettes.
        foreach (var supportTower in session.Towers.Where(tower => tower.IsSupport && !tower.IsSandboxDisabled))
        {
            var accent = supportTower.Definition.Visual.AccentColor;
            p.DashedRing(batch, supportTower.Position, session.GetEffectiveAuraRange(supportTower),
                ColorPalette.WithAlpha(accent, 120), 28, 2);
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
        var barrel = tower.Definition.Visual.Radius * presentation.TowerScale(tower) * PrismBeamArt.BarrelLength;
        return tower.Position + ray / length * MathF.Min(barrel, length);
    }

    private void DrawTower(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation, TowerInstance tower, float time, bool groundEffects = true)
    {
        var supportPulse = tower.IsSupport && !ReducedEffects ? 1f + MathF.Sin(time * 3f + tower.Id) * 0.06f : 1f;
        var pulse = supportPulse * presentation.TowerScale(tower);
        var aim = TowerAim(session, tower, presentation);
        var recoil = MathF.Max(0, tower.RecoilAnimationRemaining - presentation.LeadSeconds) / .12f * .2f;
        NightGridArt.Tower(batch, p, tower.Position, tower.Definition.Visual.Radius * pulse,
            tower.Definition.Id, tower.LevelIndex + 1, aim, ReducedEffects ? 0 : time, recoil,
            tower.IsSandboxDisabled ? .4f : 1, !ReducedEffects, groundEffects, tower.IsApex);
        if (tower == session.HoveredTower || tower == session.SelectedTower)
        {
            var span = tower.Definition.Visual.Radius + 10;
            p.Brackets(batch, new Rectangle((int)tower.Position.X - span, (int)tower.Position.Y - span, span * 2, span * 2),
                tower == session.SelectedTower ? ColorPalette.Gold : ColorPalette.Cyan, 8);
        }
        if (tower.IsDisrupted)
            StatusGlyphRenderer.DrawDisrupted(batch, p, tower.Position, tower.Definition.Visual.Radius);
        else if (tower.IsSuppressed)
            StatusGlyphRenderer.DrawSuppressed(batch, p, tower.Position, tower.Definition.Visual.Radius);
        if (tower.IsSandboxDisabled)
        {
            var slash = tower.Definition.Visual.Radius * 0.58f;
            p.Line(batch, tower.Position + new Vector2(-slash, -slash), tower.Position + new Vector2(slash, slash), ColorPalette.Coral, 3);
            p.Line(batch, tower.Position + new Vector2(slash, -slash), tower.Position + new Vector2(-slash, slash), ColorPalette.Coral, 3);
            if (tower == session.SelectedTower)
                p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + 8, ColorPalette.Gold, 3);
            return;
        }
        if (session.Map.GetPowerBuff(tower.Position).IsPowered)
            p.DashedRing(batch, tower.Position, tower.Definition.Visual.Radius + 10, ColorPalette.WithAlpha(ColorPalette.Gold, 190), 12, 2);
        if (tower.IsOverdriven)
            TowerProtocolArt.Draw(batch, p, tower.Position, tower.Definition.Visual.Radius,
                tower.Definition.Id, time, ReducedEffects, glow: groundEffects);
        if (session.IsCoOp)
            p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + 8,
                tower.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
        if (session.GetSupportBuff(tower).IsActive)
            DrawSignalBeaconEffect(batch, p, tower, time);
        if (tower == session.SelectedTower)
            p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);
    }

    private void DrawForegroundTowerOverlays(SpriteBatch batch, PrimitiveRenderer p,
        MaximalBastion.GameSession session, PresentationFrame presentation, int foregroundTowerId)
    {
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);
        var remoteSelectedTower = foregroundTowerId > 0
            ? session.Towers.FirstOrDefault(tower => tower.Id == foregroundTowerId)
            : null;

        // Towers carrying temporary interaction state remain readable above dense
        // units and attack effects. Once the state moves or clears, normal authored
        // tower order resumes automatically. Ground shadows and light pools stay beneath combat.
        if (autoTower is not null && !autoTower.IsSandboxDisabled)
            DrawTower(batch, p, session, presentation, autoTower, presentation.TimeSeconds, groundEffects: false);
        if (remoteSelectedTower is not null && remoteSelectedTower != autoTower && !remoteSelectedTower.IsSandboxDisabled)
            DrawTower(batch, p, session, presentation, remoteSelectedTower, presentation.TimeSeconds, groundEffects: false);

        // Auto's brackets and literal badge are the final battlefield marks, so
        // neither enemies nor a foreground remote selection can obscure them.
        if (autoTower is not null && !autoTower.IsSandboxDisabled)
            DrawAutoProtocolEffect(batch, p, autoTower, presentation.TimeSeconds);
    }

    private static void DrawAutoProtocolEffect(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, float time)
    {
        var pulse = (MathF.Sin(time * 3.5f + tower.Id) + 1f) * 0.5f;
        var color = ColorPalette.WithAlpha(ColorPalette.AutoMarker, (byte)(190 + pulse * 55));

        var bracketExtent = tower.Definition.Visual.Radius + 9f + pulse;
        const float bracketLength = 6f;
        foreach (var horizontalDirection in new[] { -1f, 1f })
        foreach (var verticalDirection in new[] { -1f, 1f })
        {
            var corner = tower.Position + new Vector2(horizontalDirection * bracketExtent,
                verticalDirection * bracketExtent);
            p.Line(batch, corner, corner - new Vector2(horizontalDirection * bracketLength, 0), color, 2.5f);
            p.Line(batch, corner, corner - new Vector2(0, verticalDirection * bracketLength), color, 2.5f);
        }

        // A compact literal badge reinforces the corner-bracket Auto language.
        var badgeDirection = Vector2.Normalize(new Vector2(-1f, 1f));
        var marker = tower.Position + badgeDirection * (tower.Definition.Visual.Radius + 7f);
        p.Circle(batch, marker, 7f, ColorPalette.WithAlpha(ColorPalette.AutoMarker, 245));
        p.Ring(batch, marker, 7f, ColorPalette.Navy, 2);
        var top = marker + new Vector2(0, -3.7f);
        var lowerLeft = marker + new Vector2(-3.1f, 3.2f);
        var lowerRight = marker + new Vector2(3.1f, 3.2f);
        p.Line(batch, lowerLeft, top, ColorPalette.Navy, 2);
        p.Line(batch, top, lowerRight, ColorPalette.Navy, 2);
        p.Line(batch, marker + new Vector2(-1.7f, 0.7f), marker + new Vector2(1.7f, 0.7f), ColorPalette.Navy, 2);
    }

    private static void DrawSignalBeaconEffect(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, float time)
    {
        var pulse = (MathF.Sin(time * 5f + tower.Id) + 1f) * 0.5f;
        var alpha = (byte)(175 + pulse * 55f);
        var direction = new Vector2(MathF.Cos(-MathHelper.PiOver4), MathF.Sin(-MathHelper.PiOver4));
        var markerPosition = tower.Position + direction * (tower.Definition.Visual.Radius + 1f);

        // Keep the native accent ring completely visible: Beacon support is a status,
        // not a replacement for tower identity. The pip sits inside the upper-right
        // quadrant, clear of the tier lights and centered specialization glyph.
        p.Circle(batch, markerPosition, 4.25f, ColorPalette.WithAlpha(ColorPalette.Navy, 225));
        p.Circle(batch, markerPosition, 2.55f + pulse * 0.55f, ColorPalette.WithAlpha(ColorPalette.Gold, alpha));
    }

    private void DrawEnemies(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        PresentationFrame presentation)
    {
        var time = presentation.TimeSeconds;
        DrawEnemySignalFields(batch, p, session, presentation, time);
        DrawEnemyLayer(batch, p, session, presentation, signalCarriers: false);
        DrawEnemyLayer(batch, p, session, presentation, signalCarriers: true);
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None) continue;
            DrawEnemySignalMarker(batch, p, enemy, presentation.EnemyPosition(enemy));
        }
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
            var pulseRate = enemy.IsBoss ? 8f : 5f;
            var pulseAmount = enemy.IsBoss ? 0.09f : 0.05f;
            var pulse = !ReducedEffects && (enemy.IsBoss || enemy.Definition.Id.Contains("regenerator", StringComparison.OrdinalIgnoreCase))
                ? 1f + MathF.Sin(time * pulseRate) * pulseAmount : 1f;
            var ahead = session.Map.Path.GetPosition(MathF.Min(session.Map.Path.TotalLength, enemy.DistanceAlongPath + 3));
            var heading = ahead - enemy.Position;
            var headingAngle = MathF.Atan2(heading.Y, heading.X);
            NightGridArt.Enemy(batch, p, position, enemy.Radius * pulse, enemy.Definition.Id,
                headingAngle, ReducedEffects ? 0 : time, enemy.IsBoss,
                Math.Clamp((enemy.DamagePauseTimer - .88f) * 6, 0, .7f), !ReducedEffects);

            if (enemy.IsBoss)
            {
                p.DashedRing(batch, position, enemy.Radius + 10, ColorPalette.Coral, 16, 3);
                p.DashedRing(batch, position, enemy.Radius + 16, ColorPalette.Gold, 24, 2);
            }
            else if (enemy.IsElite)
                p.DashedRing(batch, position, enemy.Radius + 7, ColorPalette.Gold, 12, 2);

            if (enemy.IsSandboxImmortal)
                p.DashedRing(batch, position, enemy.Radius + (enemy.IsBoss ? 21 : 7), ColorPalette.Paper, 10, 2);

            var barWidth = enemy.Radius * (enemy.IsBoss ? 3.5f : 2.5f);
            var healthRatio = enemy.MaxHealth > 0 ? enemy.Health / enemy.MaxHealth : 0;
            p.HealthBar(batch, position - new Vector2(0, enemy.Radius + 11), barWidth,
                healthRatio, ColorPalette.Health(healthRatio), ColorPalette.HealthTrack, ColorPalette.Ink);

            if (enemy.Shield > 0)
            {
                var naturalShieldCapacity = enemy.Definition.Shield + (enemy.IsBoss ? enemy.MaxHealth * 0.12f : 0f);
                var signalShieldCapacity = session.CounterSupportEnabled
                    ? enemy.Definition.Shield + enemy.MaxHealth * session.Challenge.CounterShieldCapacityFraction
                    : 0f;
                var shieldCapacity = MathF.Max(enemy.Shield, MathF.Max(naturalShieldCapacity, signalShieldCapacity));
                p.HealthBar(batch, position - new Vector2(0, enemy.Radius + 18), barWidth,
                    enemy.Shield / shieldCapacity, ColorPalette.Shield, ColorPalette.HealthTrack, ColorPalette.Ink, 6);
                p.Ring(batch, position, enemy.Radius + 5, ColorPalette.Shield, 3);
            }
            if (enemy.StatusEffects.IsBurning)
            {
                var burnAlpha = (byte)(145 + (MathF.Sin(time * 7f + enemy.Id) + 1f) * 42f);
                p.Ring(batch, position, MathF.Max(5, enemy.Radius - 2), ColorPalette.WithAlpha(ColorPalette.Burn, burnAlpha), 2);
            }
            if (enemy.StatusEffects.SlowFactor > 0)
                p.DashedRing(batch, position, enemy.Radius + 9, ColorPalette.Slow, 16, 2);
            if (enemy.StatusEffects.DamageMultiplier > 1f)
            {
                p.Ring(batch, position, enemy.Radius + 13, ColorPalette.Violet, 2);
                p.DrawPolygon(batch, position - new Vector2(0, enemy.Radius + 13), 3.5f, 4, false, ColorPalette.Violet, MathHelper.PiOver4);
            }
            if (enemy.StatusEffects.ArmorReduction > 0)
                StatusGlyphRenderer.DrawArmorBreak(batch, p, position, enemy.Radius);
            if (enemy.Definition.RegenerationPerSecond > 0)
                p.Ring(batch, position, enemy.Radius + 11, ColorPalette.Lime, 2);
        }
    }

    private static void DrawEnemySignalFields(SpriteBatch batch, PrimitiveRenderer p,
        MaximalBastion.GameSession session, PresentationFrame presentation, float time)
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
            var pulse = (MathF.Sin(time * 3.4f + source.Id * 0.7f) + 1f) * 0.5f;
            var alpha = source.SignalRole == EnemySignalRole.Accelerator
                ? (byte)(54 + pulse * 34)
                : (byte)(30 + pulse * 20);
            p.Ring(batch, sourcePosition, supportRadius,
                ColorPalette.WithAlpha(accent, alpha), source.SignalRole == EnemySignalRole.Accelerator ? 2 : 1);
        }

        var accelerators = sources.Where(source => source.SignalRole == EnemySignalRole.Accelerator).ToArray();
        if (accelerators.Length == 0) return;
        foreach (var recipient in session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                     enemy.FormationSpeedMultiplier > 1.001f))
        {
            var source = accelerators.OrderBy(candidate =>
                    Vector2.DistanceSquared(candidate.Position, recipient.Position))
                .FirstOrDefault(candidate => candidate.Id != recipient.Id &&
                    Vector2.DistanceSquared(candidate.Position, recipient.Position) <= supportRadius * supportRadius);
            if (source is null) continue;
            var recipientPosition = presentation.EnemyPosition(recipient);
            var sourcePosition = presentation.EnemyPosition(source);
            var linkColor = ColorPalette.WithAlpha(EnemySignalGlyphRenderer.Accent(source.SignalRole), 138);
            p.Line(batch, sourcePosition, recipientPosition, linkColor, 2);
            p.Ring(batch, recipientPosition, recipient.Radius + 3, linkColor, 2);
        }
    }

    private static void DrawEnemySignalMarker(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 position)
    {
        if (enemy.SignalRole == EnemySignalRole.None) return;
        EnemySignalGlyphRenderer.DrawEmbedded(batch, p, enemy.SignalRole, position,
            MathHelper.Clamp(enemy.Radius * 0.43f, 4.5f, 7f));
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
            // Attribution retains sold sources while their shots are in flight, including after reconstruction.
            if (!session.Statistics.TowerDefinitionByInstance.TryGetValue(projectile.Payload.SourceTowerId, out var towerId))
                towerId = session.Towers.FirstOrDefault(tower => tower.Id == projectile.Payload.SourceTowerId)?.Definition.Id;
            ProjectileArt.Draw(batch, p, towerId, position, forward, projectile.Radius, projectile.Color,
                presentation.TimeSeconds, ReducedEffects);
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
            if (effect.Kind == EffectKind.Beam && effect.BeamStyle == BeamStyle.Prism)
            {
                if (effect.SourceTowerId != 0 && ActivePrismBeam(session, effect.SourceTowerId, presentation) != effect)
                    continue;
                PrismBeamArt.Draw(batch, p, PrismBeamOrigin(session, effect, presentation), effect.End, effect.Color, effect.Radius,
                    progress, 1 - progress, ReducedEffects, effect.SourceTowerId != 0);
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
                if (!ReducedEffects && effect.Color == session.Content.Towers["arc_relay"].Visual.PrimaryColor)
                {
                    var previous = effect.Start;
                    var delta = effect.End - effect.Start;
                    var normal = SafeDirection(new Vector2(-delta.Y, delta.X));
                    for (var step = 1; step <= 6; step++)
                    {
                        var next = Vector2.Lerp(effect.Start, effect.End, step / 6f) +
                            (step == 6 ? Vector2.Zero : normal * MathF.Sin(step * 7 + progress * 14) * 5);
                        p.Line(batch, previous, next, effectColor, 2);
                        previous = next;
                    }
                }
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
