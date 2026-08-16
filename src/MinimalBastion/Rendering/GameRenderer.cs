using MinimalBastion.Core;
using MinimalBastion.Data;
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
        bool showTransientCombat = true, float presentationLeadSeconds = 0)
    {
        var presentation = PresentationFrame.Create(session, presentationLeadSeconds);
        primitives.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), session.Map.Definition.Background.BaseColor);
        DrawTerrain(batch, primitives, session);
        DrawPath(batch, primitives, session);
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
        DrawAutoProtocolOverlay(batch, primitives, session, presentation.TimeSeconds);
    }

    private static void DrawTerrain(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var mapRect = new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight);
        var baseColor = session.Map.Definition.Background.BaseColor;
        var accentColor = session.Map.Definition.Background.AccentColor;
        p.FillRect(batch, mapRect, baseColor);
        DrawMapMotif(batch, p, session.Map.Definition.Background.Motif, baseColor, accentColor);

        foreach (var region in session.Map.BuildableRegions)
        {
            var pointerInside = region.Contains(session.PlacementPosition.ToPoint());
            var placementActive = session.PlacementTowerId is not null || session.TacticalPlacement == TacticalPlacementKind.ChargeForge;
            var emphasized = placementActive && pointerInside;
            DrawBuildZone(batch, p, region, session.Map.Definition.Background.Motif, baseColor, accentColor,
                session.Map.Definition.PathVisual, emphasized);
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

    private static void DrawMapMotif(SpriteBatch batch, PrimitiveRenderer p, string motif, Color baseColor, Color accent)
    {
        switch (motif.ToLowerInvariant())
        {
            case "foundry_floor":
                DrawFoundryFloor(batch, p, baseColor, accent);
                break;

            case "meadow":
                // Sparse shrubs and a low basin pool replace the old arrow-like
                // wind marks. Nothing here implies movement or a gameplay force.
                p.Circle(batch, new Vector2(486, 358), 126, Color.Lerp(baseColor, ColorPalette.Cyan, 0.035f));
                p.Circle(batch, new Vector2(486, 358), 83, Color.Lerp(baseColor, ColorPalette.Cyan, 0.025f));
                foreach (var center in new[]
                {
                    new Vector2(68, 102), new Vector2(312, 92), new Vector2(530, 126), new Vector2(760, 104),
                    new Vector2(92, 556), new Vector2(332, 600), new Vector2(704, 560), new Vector2(900, 580)
                })
                {
                    p.Circle(batch, center, 8, Color.Lerp(baseColor, accent, 0.25f));
                    p.Circle(batch, center + new Vector2(10, 4), 6, Color.Lerp(baseColor, accent, 0.20f));
                    p.Circle(batch, center + new Vector2(-8, 6), 5, Color.Lerp(baseColor, accent, 0.17f));
                }
                break;

            case "crystal_field":
                // A few large translucent facets read as landscape, unlike the
                // former repeated square-and-dash icon pattern.
                foreach (var facet in new[]
                {
                    (new Vector2(92, 156), 68f, -0.28f, ColorPalette.Violet),
                    (new Vector2(468, 182), 88f, 0.42f, ColorPalette.Cyan),
                    (new Vector2(742, 420), 104f, -0.52f, ColorPalette.Violet),
                    (new Vector2(360, 594), 76f, 0.18f, ColorPalette.Cyan)
                })
                {
                    p.DrawPolygon(batch, facet.Item1, facet.Item2, 3, false,
                        Color.Lerp(baseColor, facet.Item4, 0.07f), facet.Item3);
                    p.Line(batch, facet.Item1, facet.Item1 + new Vector2(MathF.Cos(facet.Item3), MathF.Sin(facet.Item3)) * facet.Item2,
                        Color.Lerp(baseColor, facet.Item4, 0.18f), 1);
                }
                break;

            case "surge_field":
                // Large overlapping energy basins establish the divide without
                // the old circuit-trace symbols or directional arrows.
                p.FillRect(batch, new Rectangle(350, 58, 240, 662), Color.Lerp(baseColor, accent, 0.055f));
                foreach (var field in new[]
                {
                    (new Vector2(180, 330), 128f, ColorPalette.Cyan),
                    (new Vector2(486, 356), 156f, ColorPalette.Violet),
                    (new Vector2(780, 342), 124f, ColorPalette.Cyan)
                })
                {
                    p.Circle(batch, field.Item1, field.Item2, Color.Lerp(baseColor, field.Item3, 0.025f));
                    p.Ring(batch, field.Item1, field.Item2, Color.Lerp(baseColor, field.Item3, 0.13f), 1);
                }
                break;
        }
    }

    private static void DrawFoundryFloor(SpriteBatch batch, PrimitiveRenderer p, Color baseColor, Color accent)
    {
        var railShadow = Color.Lerp(baseColor, ColorPalette.Ink, 0.20f);
        var railFace = Color.Lerp(baseColor, accent, 0.24f);
        var seamColor = Color.Lerp(baseColor, accent, 0.19f);
        var rivetColor = Color.Lerp(baseColor, accent, 0.40f);
        var conduitColor = Color.Lerp(baseColor, accent, 0.33f);
        var emberColor = Color.Lerp(baseColor, ColorPalette.Orange, 0.33f);

        // Long structural rails touch the map boundary, so they read as part of
        // the floor construction rather than enclosed tower-placement panels.
        p.FillRect(batch, new Rectangle(0, 38, GameConstants.MapWidth, 14), railShadow);
        p.FillRect(batch, new Rectangle(0, 41, GameConstants.MapWidth, 4), railFace);
        p.FillRect(batch, new Rectangle(924, 0, 12, GameConstants.LogicalHeight), railShadow);
        p.FillRect(batch, new Rectangle(927, 0, 3, GameConstants.LogicalHeight), railFace);
        p.FillRect(batch, new Rectangle(0, 688, 650, 9), railShadow);
        p.FillRect(batch, new Rectangle(0, 690, 650, 2), railFace);

        // Open-ended floor seams and their tiny fastening points add scale
        // without producing another closed rectangle language.
        foreach (var seam in new[]
        {
            (new Vector2(24, 78), new Vector2(236, 78)),
            (new Vector2(698, 72), new Vector2(906, 72)),
            (new Vector2(82, 286), new Vector2(146, 286)),
            (new Vector2(332, 420), new Vector2(390, 382)),
            (new Vector2(664, 302), new Vector2(712, 334)),
            (new Vector2(796, 356), new Vector2(916, 356)),
            (new Vector2(612, 666), new Vector2(904, 666))
        })
        {
            p.Line(batch, seam.Item1, seam.Item2, seamColor, 1);
            p.Circle(batch, seam.Item1, 2f, rivetColor);
            p.Circle(batch, seam.Item2, 2f, rivetColor);
        }

        // Narrow paired conduits remain far thinner and quieter than the molten
        // route. Every run is deliberately open at both ends.
        DrawFoundryConduit(batch, p,
            new[] { new Vector2(-8, 404), new Vector2(92, 404), new Vector2(92, 448), new Vector2(146, 448) },
            conduitColor);
        DrawFoundryConduit(batch, p,
            new[] { new Vector2(734, -8), new Vector2(734, 82), new Vector2(858, 82) },
            conduitColor);
        DrawFoundryConduit(batch, p,
            new[] { new Vector2(836, 536), new Vector2(910, 536), new Vector2(910, 604), new Vector2(968, 604) },
            conduitColor);

        // Unboxed heat-vent ticks introduce a restrained ember accent. Their
        // tiny repeated scale cannot be confused with build-zone boundaries.
        DrawFoundryVentTicks(batch, p, new Vector2(112, 676), 8, emberColor);
        DrawFoundryVentTicks(batch, p, new Vector2(474, 72), 6, emberColor);
        DrawFoundryVentTicks(batch, p, new Vector2(850, 390), 5, emberColor);
    }

    private static void DrawFoundryConduit(SpriteBatch batch, PrimitiveRenderer p, IReadOnlyList<Vector2> points, Color color)
    {
        for (var index = 0; index < points.Count - 1; index++)
        {
            var delta = points[index + 1] - points[index];
            var offset = delta.LengthSquared() > 0.01f
                ? Vector2.Normalize(new Vector2(-delta.Y, delta.X)) * 4
                : Vector2.Zero;
            p.Line(batch, points[index], points[index + 1], color, 2);
            p.Line(batch, points[index] + offset, points[index + 1] + offset, Color.Lerp(color, ColorPalette.Ink, 0.22f), 1);
        }
        p.Circle(batch, points[0], 2.5f, color);
        p.Circle(batch, points[^1], 2.5f, color);
    }

    private static void DrawFoundryVentTicks(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, int count, Color color)
    {
        for (var index = 0; index < count; index++)
        {
            var x = start.X + index * 13;
            p.Line(batch, new Vector2(x, start.Y), new Vector2(x + 5, start.Y - 9), color, 2);
        }
    }

    private static void DrawBuildZone(SpriteBatch batch, PrimitiveRenderer p, Rectangle region, string motif,
        Color baseColor, Color accentColor, PathVisualData pathVisual, bool emphasized)
    {
        var isFoundry = motif.Equals("foundry_floor", StringComparison.OrdinalIgnoreCase);
        var outline = emphasized
            ? ColorPalette.PlacementValid
            : motif.Equals("meadow", StringComparison.OrdinalIgnoreCase)
                ? Color.Lerp(baseColor, accentColor, 0.58f)
                : isFoundry
                    ? Color.Lerp(baseColor, ColorPalette.Cyan, 0.58f)
                    : Color.Lerp(baseColor, pathVisual.SecondaryColor, 0.52f);
        var regionFill = Color.Lerp(baseColor, accentColor, emphasized ? 0.43f : isFoundry ? 0.11f : 0.16f);
        p.FillRect(batch, region, regionFill);

        switch (motif.ToLowerInvariant())
        {
            case "meadow":
                // A clearing is a quiet translucent patch with a restrained
                // boundary, not another tactical bracket floating over grass.
                p.DrawRect(batch, region, outline, emphasized ? 3 : 1);
                p.Circle(batch, new Vector2(region.Left, region.Top), 2.5f, outline);
                p.Circle(batch, new Vector2(region.Right - 1, region.Bottom - 1), 2.5f, outline);
                break;
            case "crystal_field":
                DrawChamferedZone(batch, p, region, outline, emphasized ? 3 : 2);
                break;
            case "surge_field":
                p.DrawRect(batch, region, outline, emphasized ? 3 : 1);
                break;
            case "foundry_floor":
                DrawBuildZoneCorners(batch, p, region, outline, emphasized ? 3 : 2);
                break;
            default:
                DrawBuildZoneCorners(batch, p, region, outline, emphasized ? 3 : 2);
                break;
        }
    }

    private static void DrawChamferedZone(SpriteBatch batch, PrimitiveRenderer p, Rectangle region, Color color, int thickness)
    {
        const int cut = 10;
        var left = region.Left;
        var right = region.Right - 1;
        var top = region.Top;
        var bottom = region.Bottom - 1;
        p.Line(batch, new Vector2(left + cut, top), new Vector2(right - cut, top), color, thickness);
        p.Line(batch, new Vector2(right - cut, top), new Vector2(right, top + cut), color, thickness);
        p.Line(batch, new Vector2(right, top + cut), new Vector2(right, bottom - cut), color, thickness);
        p.Line(batch, new Vector2(right, bottom - cut), new Vector2(right - cut, bottom), color, thickness);
        p.Line(batch, new Vector2(right - cut, bottom), new Vector2(left + cut, bottom), color, thickness);
        p.Line(batch, new Vector2(left + cut, bottom), new Vector2(left, bottom - cut), color, thickness);
        p.Line(batch, new Vector2(left, bottom - cut), new Vector2(left, top + cut), color, thickness);
        p.Line(batch, new Vector2(left, top + cut), new Vector2(left + cut, top), color, thickness);
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
        var pathWidth = session.Map.Definition.PathWidth;
        var visual = session.Map.Definition.PathVisual;

        if (visual.Style.Equals("foundry", StringComparison.OrdinalIgnoreCase))
        {
            // A solid molten transfer channel: steel banks, dark refractory
            // lining, and a warm core. It reads as foundry infrastructure rather
            // than a road, while keeping its exact gameplay footprint obvious.
            DrawContinuousPath(batch, p, points, visual.BaseColor, pathWidth);
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, Math.Max(12, pathWidth - 10));
            DrawContinuousPath(batch, p, points, visual.AccentColor, Math.Max(12, pathWidth / 3));
            DrawContinuousPath(batch, p, points, ColorPalette.Tint(visual.AccentColor, 0.30f), 4);
            return;
        }

        if (visual.Style.Equals("trail", StringComparison.OrdinalIgnoreCase))
        {
            // Layered earth and sparse static stones form a footpath. There are
            // deliberately no center lines or directional animation.
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, pathWidth);
            DrawContinuousPath(batch, p, points, visual.AccentColor, Math.Max(12, pathWidth - 4));
            DrawContinuousPath(batch, p, points, visual.BaseColor, Math.Max(12, pathWidth - 10));
            DrawTrailStones(batch, p, points, ColorPalette.Tint(visual.AccentColor, 0.18f));
            return;
        }

        if (visual.Style.Equals("prism", StringComparison.OrdinalIgnoreCase))
        {
            // A continuous violet light ribbon with a narrow cyan refraction
            // core. Solid layers avoid the road/tile reading entirely.
            DrawContinuousPath(batch, p, points, visual.AccentColor, pathWidth);
            DrawContinuousPath(batch, p, points, visual.BaseColor, Math.Max(12, pathWidth - 8));
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, 6);
            return;
        }

        if (visual.Style.Equals("surge", StringComparison.OrdinalIgnoreCase))
        {
            // Surge Divide is a static energy trench. The former moving packet
            // dashes were purely decorative, but looked like reverse motion and
            // falsely suggested a slow effect.
            DrawContinuousPath(batch, p, points, visual.BaseColor, pathWidth);
            DrawContinuousPath(batch, p, points, visual.SecondaryColor, Math.Max(14, pathWidth / 2));
            DrawContinuousPath(batch, p, points, ColorPalette.Tint(visual.SecondaryColor, 0.30f), 6);
            return;
        }

        DrawContinuousPath(batch, p, points, visual.BaseColor, pathWidth);
        DrawDashedPath(batch, p, points, visual.AccentColor, 4, 18, 16);
    }

    private static void DrawTrailStones(SpriteBatch batch, PrimitiveRenderer p, IReadOnlyList<Vector2> points, Color color)
    {
        var stoneIndex = 0;
        for (var segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
        {
            var start = points[segmentIndex];
            var delta = points[segmentIndex + 1] - start;
            var length = delta.Length();
            if (length <= 0.01f) continue;
            var direction = delta / length;
            var normal = new Vector2(-direction.Y, direction.X);
            for (var distance = 30f; distance < length - 16f; distance += 62f)
            {
                var offset = stoneIndex++ % 2 == 0 ? -13f : 13f;
                var position = start + direction * distance + normal * offset;
                p.Circle(batch, position, stoneIndex % 3 == 0 ? 2.5f : 2f, color);
                if (stoneIndex % 4 == 0)
                    p.Circle(batch, position + direction * 6 + normal * 2, 1.4f, Color.Lerp(color, ColorPalette.Ink, 0.18f));
            }
        }
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

    private static void DrawDashedPath(SpriteBatch batch, PrimitiveRenderer p, IReadOnlyList<Vector2> points, Color color,
        float thickness, float dashLength, float gapLength, float phase = 0)
    {
        // Carry the pattern distance through every right-angle turn. Restarting
        // it per segment made route corners read like separate road tiles and
        // caused animated Surge packets to jump instead of flowing around bends.
        var cumulativeDistance = 0f;
        for (var index = 0; index < points.Count - 1; index++)
        {
            DrawDashedLine(batch, p, points[index], points[index + 1], color,
                thickness, dashLength, gapLength, phase + cumulativeDistance);
            cumulativeDistance += Vector2.Distance(points[index], points[index + 1]);
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
            var placementValid = session.ValidatePlacement(towerId, session.PlacementPosition) == PlacementFailure.None;
            var placementColor = placementValid
                ? ColorPalette.PlacementValid
                : ColorPalette.PlacementInvalid;
            var placementRange = definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
                ? definition.Levels[0].AuraRange
                : definition.Levels[0].Range;
            p.DashedRing(batch, session.PlacementPosition, placementRange, placementColor, 32, 2);
            p.DrawShape(batch, session.PlacementPosition, definition.Visual.Radius, definition.Visual.Shape,
                ColorPalette.WithAlpha(definition.Visual.PrimaryColor, 175), definition.Visual.AccentColor, 1, true, levelMarks: true);
            DrawPlacementValidityMarker(batch, p, session.PlacementPosition, placementValid);
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
            DrawPlacementValidityMarker(batch, p, session.PlacementPosition,
                session.PlacementFailure == PlacementFailure.None);
        }
    }

    private static void DrawPlacementValidityMarker(SpriteBatch batch, PrimitiveRenderer p, Vector2 position, bool valid)
    {
        var source = valid ? ColorPalette.PlacementValid : ColorPalette.PlacementInvalid;
        var color = new Color(source.R, source.G, source.B);
        p.Circle(batch, position, 6, color);
        p.Ring(batch, position, 7, ColorPalette.Paper, 1);
        if (valid)
        {
            p.Line(batch, position + new Vector2(-3, 0), position + new Vector2(-1, 2.5f), ColorPalette.Paper, 1.5f);
            p.Line(batch, position + new Vector2(-1, 2.5f), position + new Vector2(3.5f, -3), ColorPalette.Paper, 1.5f);
        }
        else
        {
            p.Line(batch, position + new Vector2(-2.5f, -2.5f), position + new Vector2(2.5f, 2.5f), ColorPalette.Paper, 1.5f);
            p.Line(batch, position + new Vector2(2.5f, -2.5f), position + new Vector2(-2.5f, 2.5f), ColorPalette.Paper, 1.5f);
        }
    }

    private static float DisplayRange(MinimalBastion.GameSession session, TowerInstance tower) =>
        tower.IsSupport ? session.GetEffectiveAuraRange(tower) : session.GetEffectiveRange(tower);

    private static void DrawTacticalDefenses(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
        PresentationFrame presentation)
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
            1f + MathF.Sin(presentation.TimeSeconds * 2.5f) * 0.04f, true);
        if (session.IsCoOp)
            p.Ring(batch, generator.Position, visual.Radius + 8, generator.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
        var track = new Rectangle((int)generator.Position.X - 22, (int)generator.Position.Y + visual.Radius + 8, 44, 5);
        p.FillRect(batch, track, ColorPalette.HealthTrack);
        p.FillRect(batch, new Rectangle(track.X, track.Y, (int)(track.Width * generator.ProductionProgress), track.Height), ColorPalette.Green);
        if (session.SelectedGenerator == generator)
            p.Ring(batch, generator.Position, visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);
    }

    private void DrawTowers(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
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

    private void DrawTower(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
        PresentationFrame presentation, TowerInstance tower, float time)
    {
        var supportPulse = tower.IsSupport ? 1f + MathF.Sin(time * 3f + tower.Id) * 0.06f : 1f;
        if (tower.IsOverdriven) supportPulse *= 1f + MathF.Sin(time * 13f + tower.Id) * 0.05f;
        var pulse = supportPulse * presentation.TowerScale(tower);
        var primary = tower.IsSandboxDisabled ? ColorPalette.Muted : tower.Definition.Visual.PrimaryColor;
        var accent = tower.IsSandboxDisabled ? ColorPalette.MapBoundary : tower.Definition.Visual.AccentColor;
        p.DrawShape(batch, tower.Position, tower.Definition.Visual.Radius, tower.Definition.Visual.Shape,
            primary, accent, tower.LevelIndex + 1, true, pulse, true);
        if (tower.IsSandboxDisabled)
        {
            var slash = tower.Definition.Visual.Radius * 0.58f;
            p.Line(batch, tower.Position + new Vector2(-slash, -slash), tower.Position + new Vector2(slash, slash), ColorPalette.Coral, 3);
            p.Line(batch, tower.Position + new Vector2(slash, -slash), tower.Position + new Vector2(-slash, slash), ColorPalette.Coral, 3);
            if (tower == session.SelectedTower)
                p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + 8, ColorPalette.Gold, 3);
            return;
        }
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
        if (session.IsCoOp)
            p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + 8,
                tower.OwnerPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral, 2);
        if (session.GetSupportBuff(tower).IsActive)
            DrawSignalBeaconEffect(batch, p, tower, time);
        if (tower == session.SelectedTower)
            p.Ring(batch, tower.Position, tower.Definition.Visual.Radius + (session.IsCoOp ? 12 : 8), ColorPalette.Gold, 3);
    }

    private static void DrawAutoProtocolOverlay(SpriteBatch batch, PrimitiveRenderer p,
        MinimalBastion.GameSession session, float time)
    {
        var autoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);
        if (autoTower is null || autoTower.IsSandboxDisabled) return;
        // This final overlay pass keeps the brackets and A badge above towers,
        // enemies, projectiles, and geometric attack effects.
        DrawAutoProtocolEffect(batch, p, autoTower, time);
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
        var color = ColorPalette.WithAlpha(ColorPalette.Cobalt, (byte)(190 + pulse * 55));
        var bracketRadius = tower.Definition.Visual.Radius + 11.5f + pulse;
        const float bracketLength = 6f;

        // Four open brackets read as an armed targeting frame without replacing
        // the tower's authored identity ring, co-op ownership ring, or Beacon pip.
        for (var xIndex = 0; xIndex < 2; xIndex++)
        for (var yIndex = 0; yIndex < 2; yIndex++)
        {
            var xSign = xIndex == 0 ? -1f : 1f;
            var ySign = yIndex == 0 ? -1f : 1f;
            var corner = tower.Position + new Vector2(xSign * bracketRadius, ySign * bracketRadius);
            p.Line(batch, corner, corner - new Vector2(xSign * bracketLength, 0), color, 3);
            p.Line(batch, corner, corner - new Vector2(0, ySign * bracketLength), color, 3);
        }

        // A literal geometric A remains legible during the active Protocol, when
        // the tower's separate animated Protocol signature is also visible.
        var badgeDirection = Vector2.Normalize(new Vector2(-1f, 1f));
        var marker = tower.Position + badgeDirection * (tower.Definition.Visual.Radius + 9f);
        p.Circle(batch, marker, 8f, ColorPalette.WithAlpha(ColorPalette.Cobalt, 238));
        p.Ring(batch, marker, 8f, ColorPalette.Navy, 2);
        var top = marker + new Vector2(0, -4f);
        var lowerLeft = marker + new Vector2(-3.4f, 3.5f);
        var lowerRight = marker + new Vector2(3.4f, 3.5f);
        p.Line(batch, lowerLeft, top, ColorPalette.Paper, 2);
        p.Line(batch, top, lowerRight, ColorPalette.Paper, 2);
        p.Line(batch, marker + new Vector2(-1.8f, 0.8f), marker + new Vector2(1.8f, 0.8f), ColorPalette.Paper, 2);
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

    private static void DrawEnemies(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
        PresentationFrame presentation)
    {
        var time = presentation.TimeSeconds;
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var position = presentation.EnemyPosition(enemy);
            var pulseRate = enemy.IsBoss ? 8f : 5f;
            var pulseAmount = enemy.IsBoss ? 0.09f : 0.05f;
            var pulse = enemy.IsBoss || enemy.Definition.Id.Contains("regenerator", StringComparison.OrdinalIgnoreCase)
                ? 1f + MathF.Sin(time * pulseRate) * pulseAmount : 1f;
            var primary = enemy.Definition.Visual.PrimaryColor;
            var accent = enemy.Definition.Visual.AccentColor;
            p.DrawShape(batch, position, (int)MathF.Round(enemy.Radius), enemy.Definition.Visual.Shape,
                primary, accent, enemy.Definition.Visual.Marks + (enemy.IsBoss ? 2 : 0), enemy.Definition.Visual.Ring || enemy.IsElite || enemy.IsBoss, pulse);

            if (enemy.IsBoss)
            {
                p.DashedRing(batch, position, enemy.Radius + 10, ColorPalette.Coral, 16, 3);
                p.DashedRing(batch, position, enemy.Radius + 16, ColorPalette.Gold, 24, 2);
            }
            else if (enemy.IsElite)
                p.DashedRing(batch, position, enemy.Radius + 7, ColorPalette.Gold, 12, 2);

            if (enemy.IsSandboxImmortal)
                p.DashedRing(batch, position, enemy.Radius + (enemy.IsBoss ? 21 : 7), ColorPalette.Paper, 10, 2);

            var healthRatio = enemy.MaxHealth > 0 ? enemy.Health / enemy.MaxHealth : 0;
            p.HealthBar(batch, position - new Vector2(0, enemy.Radius + 11), enemy.Radius * (enemy.IsBoss ? 3.5f : 2.5f),
                healthRatio, ColorPalette.Health(healthRatio), ColorPalette.HealthTrack, ColorPalette.Ink);

            if (enemy.Shield > 0)
                p.Ring(batch, position, enemy.Radius + 5, ColorPalette.Shield, 3);
            if (enemy.StatusEffects.IsBurning)
            {
                var burnAlpha = (byte)(145 + (MathF.Sin(time * 7f + enemy.Id) + 1f) * 42f);
                p.Ring(batch, position, MathF.Max(5, enemy.Radius - 2), ColorPalette.WithAlpha(ColorPalette.Orange, burnAlpha), 2);
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
            if (enemy.StatusEffects.IsStunned)
                StatusGlyphRenderer.DrawStun(batch, p, position, enemy.Radius,
                    (MathF.Sin(time * 11f + enemy.Id) + 1f) * 0.5f);
            if (enemy.Definition.RegenerationPerSecond > 0)
                p.Ring(batch, position, enemy.Radius + 11, ColorPalette.Lime, 2);
        }
    }

    private void DrawProjectiles(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var projectile in session.Projectiles.Projectiles)
        {
            var position = presentation.ProjectilePosition(projectile);
            if (!ReducedEffects)
            {
                var destination = projectile.Kind == Combat.ProjectileKind.Homing &&
                                  projectile.Target is { IsDead: false, HasEscaped: false }
                    ? presentation.EnemyPosition(projectile.Target)
                    : projectile.AimPoint;
                var forward = destination - position;
                if (forward.LengthSquared() > 1f)
                {
                    forward.Normalize();
                    var trailLength = MathF.Max(7, projectile.Radius * 2.2f);
                    p.Line(batch,
                        position - forward * trailLength,
                        position - forward * 2f,
                        ColorPalette.WithAlpha(projectile.Color, 145),
                        2);
                }
            }

            var shape = projectile.Kind switch
            {
                Combat.ProjectileKind.ImpactPoint => "square",
                Combat.ProjectileKind.Straight => "triangle",
                _ => "diamond"
            };
            p.DrawShape(batch, position, Math.Max(4, (int)projectile.Radius + 2), shape,
                projectile.Color, ColorPalette.Ink, 0, false);
        }
    }

    private void DrawEffects(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session,
        PresentationFrame presentation)
    {
        foreach (var effect in session.Effects.Effects)
        {
            var remaining = presentation.EffectRemaining(effect);
            if (remaining <= 0) continue;
            var progress = MathHelper.Clamp(remaining / MathF.Max(effect.Duration, 0.001f), 0, 1);
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

    private static void DrawMarkers(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
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
