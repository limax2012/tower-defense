using MinimalBastion.Core;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Rendering;

public sealed class GameRenderer
{
    public bool ReducedEffects { get; set; }

    public void Draw(SpriteBatch batch, PrimitiveRenderer primitives, MinimalBastion.GameSession session,
        bool showTransientCombat = true)
    {
        primitives.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), session.Map.Definition.Background.BaseColor);
        DrawTerrain(batch, primitives, session);
        DrawPath(batch, primitives, session);
        DrawTacticalDefenses(batch, primitives, session);
        DrawRanges(batch, primitives, session);
        DrawTowers(batch, primitives, session);
        DrawEnemies(batch, primitives, session);
        if (showTransientCombat)
        {
            DrawProjectiles(batch, primitives, session);
            DrawEffects(batch, primitives, session);
        }
        DrawMarkers(batch, primitives, session);
    }

    private static void DrawTerrain(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var mapRect = new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight);
        var baseColor = session.Map.Definition.Background.BaseColor;
        var accentColor = session.Map.Definition.Background.AccentColor;
        p.FillRect(batch, mapRect, baseColor);
        DrawMapMotif(batch, p, session.Map.Definition.Background.Motif, accentColor);

        foreach (var region in session.Map.BuildableRegions)
        {
            var pointerInside = region.Contains(session.PlacementPosition.ToPoint());
            var placementActive = session.PlacementTowerId is not null || session.TacticalPlacement == TacticalPlacementKind.ChargeForge;
            var emphasized = placementActive && pointerInside;
            var regionFill = Color.Lerp(baseColor, accentColor, emphasized ? 0.34f : 0.18f);
            p.FillRect(batch, region, regionFill);
            DrawBuildZoneCorners(batch, p, region, emphasized ? ColorPalette.PlacementValid : ColorPalette.WithAlpha(ColorPalette.BuildableOutline, 165), emphasized ? 3 : 2);
        }

        foreach (var node in session.Map.Definition.PowerNodes)
        {
            var position = node.Position.ToVector2();
            p.DashedRing(batch, position, node.Radius, ColorPalette.WithAlpha(node.NodeColor, 95), 32, 2);
            p.DrawPolygon(batch, position, 14, 4, false, ColorPalette.WithAlpha(node.NodeColor, 210), MathHelper.PiOver4);
            p.DrawPolygon(batch, position, 7, 4, false, ColorPalette.Paper, MathHelper.PiOver4);
            p.Line(batch, position - new Vector2(22, 0), position - new Vector2(10, 0), node.NodeColor, 2);
            p.Line(batch, position + new Vector2(10, 0), position + new Vector2(22, 0), node.NodeColor, 2);
        }

        p.DrawRect(batch, mapRect, ColorPalette.MapBoundary, 1);
    }

    private static void DrawMapMotif(SpriteBatch batch, PrimitiveRenderer p, string motif, Color accent)
    {
        var color = ColorPalette.WithAlpha(accent, 46);
        switch (motif.ToLowerInvariant())
        {
            case "braces":
                for (var y = 150; y < GameConstants.LogicalHeight; y += 170)
                for (var x = 70; x < GameConstants.MapWidth; x += 190)
                {
                    var center = new Vector2(x, y);
                    p.Line(batch, center - new Vector2(18, 10), center, color, 2);
                    p.Line(batch, center, center + new Vector2(18, -10), color, 2);
                }
                break;

            case "facets":
                for (var y = 150; y < GameConstants.LogicalHeight; y += 175)
                for (var x = 80; x < GameConstants.MapWidth; x += 180)
                {
                    var center = new Vector2(x + ((y / 175) % 2) * 44, y);
                    p.DrawPolygon(batch, center, 14, 4, false, color, MathHelper.PiOver4);
                    p.Line(batch, center + new Vector2(14, 0), center + new Vector2(34, 0), color, 1);
                }
                break;

            case "traces":
                for (var y = 145; y < GameConstants.LogicalHeight; y += 165)
                for (var x = 72; x < GameConstants.MapWidth; x += 185)
                {
                    var start = new Vector2(x, y);
                    p.Line(batch, start, start + new Vector2(24, 0), color, 2);
                    p.Line(batch, start + new Vector2(24, 0), start + new Vector2(24, 14), color, 2);
                    p.DrawPolygon(batch, start + new Vector2(28, 18), 3, 4, false, color, MathHelper.PiOver4);
                }
                break;

            case "currents":
                for (var y = 140; y < GameConstants.LogicalHeight; y += 155)
                for (var x = 68; x < GameConstants.MapWidth; x += 180)
                {
                    var center = new Vector2(x + ((y / 155) % 2) * 36, y);
                    p.Line(batch, center - new Vector2(16, 7), center, color, 2);
                    p.Line(batch, center, center - new Vector2(16, -7), color, 2);
                    p.Line(batch, center + new Vector2(8, -7), center + new Vector2(24, 0), color, 1);
                    p.Line(batch, center + new Vector2(24, 0), center + new Vector2(8, 7), color, 1);
                }
                break;
        }
    }

    private static void DrawBuildZoneCorners(SpriteBatch batch, PrimitiveRenderer p, Rectangle region, Color color, int thickness)
    {
        const int length = 16;
        var right = region.Right - 1;
        var bottom = region.Bottom - 1;
        p.Line(batch, new Vector2(region.Left, region.Top), new Vector2(region.Left + length, region.Top), color, thickness);
        p.Line(batch, new Vector2(region.Left, region.Top), new Vector2(region.Left, region.Top + length), color, thickness);
        p.Line(batch, new Vector2(right, region.Top), new Vector2(right - length, region.Top), color, thickness);
        p.Line(batch, new Vector2(right, region.Top), new Vector2(right, region.Top + length), color, thickness);
        p.Line(batch, new Vector2(region.Left, bottom), new Vector2(region.Left + length, bottom), color, thickness);
        p.Line(batch, new Vector2(region.Left, bottom), new Vector2(region.Left, bottom - length), color, thickness);
        p.Line(batch, new Vector2(right, bottom), new Vector2(right - length, bottom), color, thickness);
        p.Line(batch, new Vector2(right, bottom), new Vector2(right, bottom - length), color, thickness);
    }

    private static void DrawPath(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var points = session.Map.Definition.Path.Select(x => x.ToVector2()).ToArray();
        var roadWidth = session.Map.Definition.PathWidth;
        var visual = session.Map.Definition.PathVisual;

        if (visual.Style.Equals("conduit", StringComparison.OrdinalIgnoreCase))
        {
            // A narrow colored sleeve and inset core read as one continuous tube.
            // Matching square joins remove tile seams without introducing round caps.
            DrawContinuousPath(batch, p, points, visual.AccentColor, roadWidth);
            DrawContinuousPath(batch, p, points, visual.BaseColor, Math.Max(12, roadWidth - 8));
            for (var i = 0; i < points.Length - 1; i++)
                DrawDashedLine(batch, p, points[i], points[i + 1], visual.SecondaryColor, 3, 10, 13);
            return;
        }

        if (visual.Style.Equals("channel", StringComparison.OrdinalIgnoreCase))
        {
            // A slim cyan bank around a slate current differentiates this route
            // without introducing segment seams or tile-like joints.
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, roadWidth);
            DrawContinuousPath(batch, p, points, visual.BaseColor, Math.Max(12, roadWidth - 6));
            for (var i = 0; i < points.Length - 1; i++)
                DrawDashedLine(batch, p, points[i], points[i + 1], visual.AccentColor, 3, 13, 19);
            return;
        }

        if (visual.Style.Equals("surge", StringComparison.OrdinalIgnoreCase))
        {
            // Surge Divide uses a powered rail rather than another road: one
            // seamless slate tube, a narrow cyan energy core, and restrained
            // gold packets that move through it while simulation time runs.
            DrawContinuousPath(batch, p, points, visual.BaseColor, roadWidth);
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, Math.Max(7, roadWidth / 7));
            var phase = session.Statistics.SimulatedSeconds * 24f;
            for (var i = 0; i < points.Length - 1; i++)
                DrawDashedLine(batch, p, points[i], points[i + 1], visual.AccentColor, 4, 9, 25, phase);
            return;
        }

        DrawContinuousPath(batch, p, points, visual.BaseColor, roadWidth);
        for (var i = 0; i < points.Length - 1; i++)
            DrawDashedLine(batch, p, points[i], points[i + 1], visual.AccentColor, 4, 18, 16);
    }

    private static void DrawContinuousPath(SpriteBatch batch, PrimitiveRenderer p, IReadOnlyList<Vector2> points, Color color, int width)
    {
        for (var i = 0; i < points.Count - 1; i++) p.Line(batch, points[i], points[i + 1], color, width);
        foreach (var point in points)
            p.FillRect(batch, new Rectangle((int)(point.X - width / 2f), (int)(point.Y - width / 2f), width, width), color);
    }

    private static void DrawDashedLine(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end, Color color,
        float thickness, float dashLength, float gapLength, float phase = 0)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.01f) return;
        var direction = delta / length;
        var period = dashLength + gapLength;
        var normalizedPhase = ((phase % period) + period) % period;
        for (var distance = -normalizedPhase; distance < length; distance += period)
        {
            var dashStart = MathF.Max(0, distance);
            var dashEnd = MathF.Min(distance + dashLength, length);
            if (dashEnd > dashStart)
                p.Line(batch, start + direction * dashStart, start + direction * dashEnd, color, thickness);
        }
    }

    private static void DrawRanges(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (session.SelectedTower is { } selected)
            p.DashedRing(batch, selected.Position, DisplayRange(session, selected), ColorPalette.WithAlpha(ColorPalette.Gold, 155), 36, 2);
        else if (session.HoveredTower is { } hovered)
            p.DashedRing(batch, hovered.Position, DisplayRange(session, hovered), ColorPalette.WithAlpha(ColorPalette.Cyan, 105), 36, 2);

        var placementOnMap = session.PlacementPosition.X >= 0 && session.PlacementPosition.X < GameConstants.MapWidth &&
                             session.PlacementPosition.Y >= GameConstants.TopBarHeight && session.PlacementPosition.Y < GameConstants.LogicalHeight;
        if (placementOnMap && session.PlacementTowerId is { } towerId && session.Content.Towers.TryGetValue(towerId, out var definition))
        {
            var placementColor = session.ValidatePlacement(towerId, session.PlacementPosition) == PlacementFailure.None
                ? ColorPalette.PlacementValid
                : ColorPalette.PlacementInvalid;
            var placementRange = definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
                ? definition.Levels[0].AuraRange
                : definition.Levels[0].Range;
            p.DashedRing(batch, session.PlacementPosition, placementRange, placementColor, 32, 2);
            p.DrawShape(batch, session.PlacementPosition, definition.Visual.Radius, definition.Visual.Shape,
                ColorPalette.WithAlpha(definition.Visual.PrimaryColor, 175), definition.Visual.AccentColor, 1, true, levelMarks: true);
        }

        if (!placementOnMap || session.TacticalPlacement == TacticalPlacementKind.None) return;
        var tacticalColor = session.PlacementFailure == PlacementFailure.None
            ? ColorPalette.PlacementValid
            : ColorPalette.PlacementInvalid;
        if (session.TacticalPlacement == TacticalPlacementKind.PulsePlate)
        {
            if (!session.HasTacticalPlacementPreview) return;
            var tactical = session.Content.Tactics.EmergencyDefense;
            var position = session.PlacementPreviewPosition;
            var previewPrimary = session.PlacementFailure == PlacementFailure.None
                ? ColorPalette.WithAlpha(tactical.Visual.PrimaryColor, 190)
                : ColorPalette.WithAlpha(tacticalColor, 190);
            p.DrawShape(batch, position, tactical.Visual.Radius, tactical.Visual.Shape,
                previewPrimary, tactical.Visual.AccentColor, tactical.Charges, true);
        }
        else if (session.TacticalPlacement == TacticalPlacementKind.ChargeForge)
        {
            var generator = session.Content.Tactics.Generator;
            p.DrawShape(batch, session.PlacementPosition, generator.Visual.Radius, generator.Visual.Shape,
                ColorPalette.WithAlpha(generator.Visual.PrimaryColor, 190), generator.Visual.AccentColor, 1, true, levelMarks: true);
        }
    }

    private static float DisplayRange(MinimalBastion.GameSession session, TowerInstance tower) =>
        tower.IsSupport ? session.GetEffectiveAuraRange(tower) : session.GetEffectiveRange(tower);

    private static void DrawTacticalDefenses(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        foreach (var plate in session.EmergencyDefenses)
        {
            var pulse = plate.ArmRemaining > 0 ? 0.82f : 1f;
            p.DrawShape(batch, plate.Position, plate.Definition.Visual.Radius, plate.Definition.Visual.Shape,
                plate.Definition.Visual.PrimaryColor, plate.Definition.Visual.AccentColor, plate.ChargesRemaining, true, pulse);
        }

        if (session.Generator is not { } generator) return;
        var visual = generator.Definition.Visual;
        p.DrawShape(batch, generator.Position, visual.Radius, visual.Shape,
            visual.PrimaryColor, visual.AccentColor, generator.LevelIndex + 1, true,
            1f + MathF.Sin(session.Statistics.SimulatedSeconds * 2.5f) * 0.04f, true);
        if (session.IsCoOp)
            p.Ring(batch, generator.Position, visual.Radius + 8, generator.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
        var track = new Rectangle((int)generator.Position.X - 22, (int)generator.Position.Y + visual.Radius + 8, 44, 5);
        p.FillRect(batch, track, ColorPalette.HealthTrack);
        p.FillRect(batch, new Rectangle(track.X, track.Y, (int)(track.Width * generator.ProductionProgress), track.Height), ColorPalette.Green);
        if (session.SelectedGenerator == generator)
            p.Ring(batch, generator.Position, visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);
    }

    private void DrawTowers(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var time = session.Statistics.SimulatedSeconds;
        foreach (var tower in session.Towers)
        {
            var supportPulse = tower.IsSupport ? 1f + MathF.Sin(time * 3f + tower.Id) * 0.06f : 1f;
            if (tower.IsOverdriven) supportPulse *= 1f + MathF.Sin(time * 13f + tower.Id) * 0.05f;
            var pulse = supportPulse * tower.VisualScale;
            var primary = tower.Definition.Visual.PrimaryColor;
            var accent = tower.Definition.Visual.AccentColor;
            p.DrawShape(batch, tower.Position, tower.Definition.Visual.Radius, tower.Definition.Visual.Shape,
                primary, accent, tower.LevelIndex + 1, true, pulse, true);
            if (tower.Specialization is { } specialization)
            {
                var branchIndex = tower.Definition.Specializations.IndexOf(specialization);
                // The first and second specialization choices are stacked in Tower Intel,
                // so matching up/down glyphs communicate the chosen branch more clearly
                // than the old, otherwise unexplained circle/diamond pair.
                if (branchIndex == 0)
                    p.DrawPolygon(batch, tower.Position, 5f, 3, false, ColorPalette.Paper, -MathHelper.PiOver2);
                else
                    p.DrawPolygon(batch, tower.Position, 5f, 3, false, ColorPalette.Paper, MathHelper.PiOver2);
            }
            if (session.Map.GetPowerBuff(tower.Position).IsPowered)
                p.DashedRing(batch, tower.Position, tower.Definition.Visual.Radius + 10, ColorPalette.WithAlpha(ColorPalette.Gold, 190), 12, 2);
            if (tower.IsOverdriven)
            {
                p.DashedRing(batch, tower.Position, tower.Definition.Visual.Radius + 15 + MathF.Sin(time * 8f) * 2f,
                    tower.Definition.Visual.PrimaryColor, 16, 3);
                if (!ReducedEffects) DrawProtocolSignature(batch, p, tower, time);
            }
            else if (session.AutoOverdriveTowerId == tower.Id)
                DrawAutoProtocolEffect(batch, p, tower, time);

            if (session.IsCoOp)
                p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + 8, tower.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
            if (session.GetSupportBuff(tower).IsActive)
                DrawSignalBeaconEffect(batch, p, tower, time);
            if (tower == session.SelectedTower)
                p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);

            if (tower.IsSupport)
                p.DashedRing(batch, tower.Position, session.GetEffectiveAuraRange(tower), ColorPalette.WithAlpha(accent, 120), 28, 2);
        }
    }

    private static void DrawProtocolSignature(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, float time)
    {
        var position = tower.Position;
        var radius = tower.Definition.Visual.Radius;
        var color = ColorPalette.WithAlpha(tower.Definition.Visual.AccentColor, 220);
        var rotation = time * 2.8f + tower.Id * 0.17f;
        var pulse = (MathF.Sin(time * 9f + tower.Id) + 1f) * 0.5f;

        switch (tower.Definition.Id)
        {
            case "needle_turret":
                DrawOrbitMarkers(batch, p, position, radius + 9, rotation, 3, 3.2f, "diamond", color);
                break;
            case "frost_spire":
                p.Ring(batch, position, radius + 5 + pulse * 6, color, 2);
                p.DashedRing(batch, position, radius + 15, ColorPalette.WithAlpha(ColorPalette.Slow, 180), 12, 2);
                break;
            case "shard_fan":
                DrawRadialSpokes(batch, p, position, radius + 4, radius + 17 + pulse * 3, rotation, 3, color, 3);
                break;
            case "watchtower":
                DrawCrosshair(batch, p, position, radius + 7, 8 + pulse * 3, color);
                break;
            case "ember_coil":
                p.DashedRing(batch, position, radius + 7 + pulse * 7, ColorPalette.WithAlpha(ColorPalette.Orange, 215), 10, 3);
                break;
            case "breaker_cannon":
                DrawOrbitMarkers(batch, p, position, radius + 10 + pulse * 2, rotation * 0.45f, 4, 3.4f, "diamond", color);
                break;
            case "arc_relay":
                DrawRadialSpokes(batch, p, position, radius + 3, radius + 14 + pulse * 5, rotation, 6, color, 2);
                break;
            case "siege_mortar":
                p.Ring(batch, position, radius + 7 + pulse * 4, color, 3);
                DrawRadialSpokes(batch, p, position, radius + 10, radius + 18, -MathHelper.PiOver2, 3, color, 2);
                break;
            case "prism_beam":
                DrawRadialSpokes(batch, p, position, 5, radius + 16 + pulse * 3, rotation, 3, color, 2);
                break;
            case "signal_beacon":
                DrawOrbitMarkers(batch, p, position, radius + 10, -rotation * 0.7f, 4, 3f, "square", color);
                break;
        }
    }

    private static void DrawOrbitMarkers(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        float rotation, int count, float markerRadius, string shape, Color color)
    {
        for (var index = 0; index < count; index++)
        {
            var angle = rotation + MathHelper.TwoPi * index / count;
            var marker = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            p.DrawShape(batch, marker, (int)MathF.Round(markerRadius), shape, color, ColorPalette.Paper, 0, false);
        }
    }

    private static void DrawRadialSpokes(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float innerRadius,
        float outerRadius, float rotation, int count, Color color, float thickness)
    {
        for (var index = 0; index < count; index++)
        {
            var angle = rotation + MathHelper.TwoPi * index / count;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            p.Line(batch, center + direction * innerRadius, center + direction * outerRadius, color, thickness);
        }
    }

    private static void DrawCrosshair(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius, float length, Color color)
    {
        p.Line(batch, center + new Vector2(-radius - length, 0), center + new Vector2(-radius, 0), color, 2);
        p.Line(batch, center + new Vector2(radius, 0), center + new Vector2(radius + length, 0), color, 2);
        p.Line(batch, center + new Vector2(0, -radius - length), center + new Vector2(0, -radius), color, 2);
        p.Line(batch, center + new Vector2(0, radius), center + new Vector2(0, radius + length), color, 2);
    }

    private static void DrawAutoProtocolEffect(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, float time)
    {
        var pulse = (MathF.Sin(time * 3.5f + tower.Id) + 1f) * 0.5f;
        var marker = tower.Position + new Vector2(-tower.Definition.Visual.Radius * 0.58f, tower.Definition.Visual.Radius * 0.58f);
        p.Circle(batch, marker, 4.3f, ColorPalette.WithAlpha(ColorPalette.Navy, 225));
        p.DrawPolygon(batch, marker, 2.8f + pulse * 0.45f, 4, false, ColorPalette.Cobalt, MathHelper.PiOver4);
    }

    private static void DrawSignalBeaconEffect(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, float time)
    {
        var pulse = (MathF.Sin(time * 5f + tower.Id) + 1f) * 0.5f;
        var alpha = (byte)(175 + pulse * 55f);
        var direction = new Vector2(MathF.Cos(-MathHelper.PiOver4), MathF.Sin(-MathHelper.PiOver4));
        var markerPosition = tower.Position + direction * (tower.Definition.Visual.Radius + 1f);

        // Keep the native accent ring completely visible: Beacon support is a status,
        // not a replacement for tower identity. The pip sits inside the upper-right
        // quadrant, clear of the level spokes and centered specialization glyph.
        p.Circle(batch, markerPosition, 4.25f, ColorPalette.WithAlpha(ColorPalette.Navy, 225));
        p.Circle(batch, markerPosition, 2.55f + pulse * 0.55f, ColorPalette.WithAlpha(ColorPalette.Gold, alpha));
    }

    private static void DrawEnemies(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var time = session.Statistics.SimulatedSeconds;
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var pulseRate = enemy.IsBoss ? 8f : 5f;
            var pulseAmount = enemy.IsBoss ? 0.09f : 0.05f;
            var pulse = enemy.IsBoss || enemy.Definition.Id.Contains("regenerator", StringComparison.OrdinalIgnoreCase)
                ? 1f + MathF.Sin(time * pulseRate) * pulseAmount : 1f;
            var primary = enemy.Definition.Visual.PrimaryColor;
            var accent = enemy.Definition.Visual.AccentColor;
            p.DrawShape(batch, enemy.Position, (int)MathF.Round(enemy.Radius), enemy.Definition.Visual.Shape,
                primary, accent, enemy.Definition.Visual.Marks + (enemy.IsBoss ? 2 : 0), enemy.Definition.Visual.Ring || enemy.IsElite || enemy.IsBoss, pulse);

            if (enemy.IsBoss)
            {
                p.DashedRing(batch, enemy.Position, enemy.Radius + 10, ColorPalette.Coral, 16, 3);
                p.DashedRing(batch, enemy.Position, enemy.Radius + 16, ColorPalette.Gold, 24, 2);
            }
            else if (enemy.IsElite)
                p.DashedRing(batch, enemy.Position, enemy.Radius + 7, ColorPalette.Gold, 12, 2);

            var healthRatio = enemy.MaxHealth > 0 ? enemy.Health / enemy.MaxHealth : 0;
            p.HealthBar(batch, enemy.Position - new Vector2(0, enemy.Radius + 11), enemy.Radius * (enemy.IsBoss ? 3.5f : 2.5f),
                healthRatio, ColorPalette.Health(healthRatio), ColorPalette.HealthTrack, ColorPalette.Ink);

            if (enemy.Shield > 0)
                p.Ring(batch, enemy.Position, enemy.Radius + 5, ColorPalette.Shield, 3);
            if (enemy.StatusEffects.IsBurning)
            {
                var burnAlpha = (byte)(145 + (MathF.Sin(time * 7f + enemy.Id) + 1f) * 42f);
                p.Ring(batch, enemy.Position, MathF.Max(5, enemy.Radius - 2), ColorPalette.WithAlpha(ColorPalette.Orange, burnAlpha), 2);
            }
            if (enemy.StatusEffects.SlowFactor > 0)
                p.DashedRing(batch, enemy.Position, enemy.Radius + 9, ColorPalette.Slow, 16, 2);
            if (enemy.StatusEffects.DamageMultiplier > 1f)
            {
                p.Ring(batch, enemy.Position, enemy.Radius + 13, ColorPalette.Violet, 2);
                p.DrawPolygon(batch, enemy.Position - new Vector2(0, enemy.Radius + 13), 3.5f, 4, false, ColorPalette.Violet, MathHelper.PiOver4);
            }
            if (enemy.StatusEffects.ArmorReduction > 0)
                StatusGlyphRenderer.DrawArmorBreak(batch, p, enemy.Position, enemy.Radius);
            if (enemy.StatusEffects.IsStunned)
                StatusGlyphRenderer.DrawStun(batch, p, enemy.Position, enemy.Radius,
                    (MathF.Sin(time * 11f + enemy.Id) + 1f) * 0.5f);
            if (enemy.Definition.RegenerationPerSecond > 0)
                p.Ring(batch, enemy.Position, enemy.Radius + 11, ColorPalette.Lime, 2);
        }
    }

    private static void DrawProjectiles(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        foreach (var projectile in session.Projectiles.Projectiles)
        {
            var shape = projectile.Kind switch
            {
                Combat.ProjectileKind.ImpactPoint => "square",
                Combat.ProjectileKind.Straight => "triangle",
                _ => "diamond"
            };
            p.DrawShape(batch, projectile.Position, Math.Max(4, (int)projectile.Radius + 2), shape,
                projectile.Color, ColorPalette.Ink, 0, false);
        }
    }

    private void DrawEffects(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        foreach (var effect in session.Effects.Effects)
        {
            var progress = MathHelper.Clamp(effect.Remaining / MathF.Max(effect.Duration, 0.001f), 0, 1);
            var alpha = (byte)(100 + 155 * progress);
            var effectColor = ColorPalette.WithAlpha(effect.Color, alpha);
            if (effect.Kind == EffectKind.Beam)
            {
                if (!ReducedEffects)
                    p.Line(batch, effect.Start, effect.End, ColorPalette.WithAlpha(ColorPalette.Ink, alpha), effect.Radius + 4);
                p.Line(batch, effect.Start, effect.End, effectColor, Math.Max(2, effect.Radius + 1));
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

    private static void DrawMarkers(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var spawn = session.Map.Definition.Spawn.ToVector2();
        var goal = session.Map.Definition.Goal.ToVector2();
        spawn.X = MathF.Max(spawn.X, 16);
        goal.X = MathF.Min(goal.X, GameConstants.MapWidth - 16);
        p.DrawShape(batch, spawn, 11, "square", ColorPalette.Cyan, ColorPalette.Navy, 1, false);
        p.DrawShape(batch, goal, 12, "triangle", ColorPalette.Coral, ColorPalette.Paper, 1, false);
    }
}
