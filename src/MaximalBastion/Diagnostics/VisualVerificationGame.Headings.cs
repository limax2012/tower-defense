using System.Globalization;
using System.Text;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private readonly List<MenuHeadingMeasurement> _menuHeadingMeasurements = [];

    private void MeasureMenuHeading(string fileName, UIManager ui, GameState state, GameSession? session,
        Color[] pixels, int width, int height, int renderScale)
    {
        if (ui.LastMenuHeading is not { } heading)
        {
            if (state is GameState.MainMenu or GameState.ReleaseNotes or GameState.GameSetup or GameState.LoadingTransition or
                GameState.Settings or GameState.SaveSlots or GameState.RunHistory or GameState.CoOpMenu or
                GameState.CoOpLobby or GameState.CoOpReconnect or GameState.Paused or GameState.Victory or GameState.Defeat ||
                state == GameState.Playing && session?.IsCoOpPaused == true)
                _menuHeadingMeasurements.Add(new MenuHeadingMeasurement(fileName, $"Missing {state} heading", renderScale,
                    "unreported", "unreported", 0, 0, null, 0, null, null, null, false));
            return;
        }
        var search = heading.SearchBounds;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = Math.Max(0, search.Top * renderScale); y < Math.Min(height, search.Bottom * renderScale); y++)
        for (var x = Math.Max(0, search.Left * renderScale); x < Math.Min(width, search.Right * renderScale); x++)
        {
            var pixel = pixels[y * width + x];
            var visible = heading.IsArtwork
                ? Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) > 60
                : pixel.R >= ColorPalette.Paper.R * .8f && pixel.G >= ColorPalette.Paper.G * .84f &&
                  pixel.B >= ColorPalette.Paper.B * .84f;
            if (!visible) continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        if (maxX < 0)
        {
            _menuHeadingMeasurements.Add(new MenuHeadingMeasurement(fileName, heading.Text, renderScale,
                heading.UpperBoundary, heading.LowerBoundary, heading.Band.Top, heading.Band.Bottom,
                null, heading.ShadowDepth, null, null, null, false, heading.IsArtwork));
            return;
        }

        var ink = new MeasuredTitleInk(minX / (float)renderScale, minY / (float)renderScale,
            (maxX + 1) / (float)renderScale, (maxY + 1) / (float)renderScale);
        var topGap = ink.Top - heading.Band.Top;
        var bottomGap = heading.Band.Bottom - ink.Bottom - heading.ShadowDepth;
        var difference = MathF.Abs(topGap - bottomGap);
        _menuHeadingMeasurements.Add(new MenuHeadingMeasurement(fileName, heading.Text, renderScale,
            heading.UpperBoundary, heading.LowerBoundary, heading.Band.Top, heading.Band.Bottom,
            ink, heading.ShadowDepth, topGap, bottomGap, difference,
            topGap >= 0 && bottomGap >= 0 && difference <= 1, heading.IsArtwork));
        WriteMenuHeadingGuide(fileName, pixels, width, height, renderScale, heading.Band, ink, heading.ShadowDepth);
    }

    private void VerifyMenuHeadingMeasurements(ICollection<string> assertions)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(_outputDirectory, "heading-measurements.json"), JsonSerializer.Serialize(new
        {
            units = "logical pixels",
            measurement = "Visible title ink plus its shadow, or the solid wordmark silhouette excluding its glow, measured from the rendered image.",
            tolerance = 1,
            headings = _menuHeadingMeasurements
        }, options));

        var report = new StringBuilder("# Menu heading measurements\n\n");
        report.AppendLine("Distances are logical pixels. Title bounds come from the rendered foreground pixels, with the title shadow included in the lower gap. The main wordmark uses its complete solid silhouette without its glow. A passing title has no overlap and a top/bottom gap difference of at most one logical pixel. Guides mark the upper boundary in blue, the lower boundary in gold, and the title silhouette in green.");
        report.AppendLine();
        report.AppendLine("| Scene | Title | Upper boundary | Lower boundary | Top gap | Bottom gap | Difference | Result |");
        report.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | --- |");
        foreach (var item in _menuHeadingMeasurements)
        {
            report.AppendLine($"| [{item.File}](./{item.File}) | {item.Text} | {item.UpperBoundary} ({Format(item.UpperEdge)}) | {item.LowerBoundary} ({Format(item.LowerEdge)}) | {Format(item.TopGap)} | {Format(item.BottomGap)} | {Format(item.GapDifference)} | {(item.Passed ? "PASS" : "FAIL")} |");
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "heading-measurements.md"), report.ToString());

        var failures = _menuHeadingMeasurements.Where(item => !item.Passed).ToArray();
        if (failures.Length > 0)
            throw new InvalidOperationException("Menu heading symmetry failed:\n" + string.Join("\n", failures.Select(item =>
                $"{item.File}: {item.Text}; top gap {Format(item.TopGap)}, bottom gap {Format(item.BottomGap)}, difference {Format(item.GapDifference)}.")));
        foreach (var item in _menuHeadingMeasurements)
            assertions.Add($"{item.File}: {item.Text} is centered between {item.UpperBoundary} and {item.LowerBoundary} within one logical pixel.");

        static string Format(float? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "missing ink";
    }

    private void WriteMenuHeadingGuide(string fileName, Color[] source, int width, int height,
        int renderScale, Rectangle band, MeasuredTitleInk ink, float shadowDepth)
    {
        var directory = Path.Combine(_outputDirectory, "heading-guides");
        Directory.CreateDirectory(directory);
        var pixels = (Color[])source.Clone();
        var upperColor = new Color(98, 181, 255);
        var lowerColor = new Color(250, 196, 86);
        var titleColor = new Color(121, 236, 166);
        var left = Math.Max(band.Left, (int)MathF.Floor(ink.Left) - 28);
        var right = Math.Min(band.Right, (int)MathF.Ceiling(ink.Right) + 28);
        Line(left, band.Top, right, band.Top, upperColor);
        Line(left, band.Bottom, right, band.Bottom, lowerColor);
        Line(ink.Left - 3, ink.Top, ink.Right + 3, ink.Top, titleColor);
        Line(ink.Left - 3, ink.Bottom + shadowDepth, ink.Right + 3, ink.Bottom + shadowDepth, titleColor);
        Line(ink.Left - 3, ink.Top, ink.Left - 3, ink.Bottom + shadowDepth, titleColor);
        Line(ink.Right + 3, ink.Top, ink.Right + 3, ink.Bottom + shadowDepth, titleColor);
        Line(left + 6, band.Top, left + 6, ink.Top, upperColor);
        Line(left + 6, ink.Bottom + shadowDepth, left + 6, band.Bottom, lowerColor);
        using var texture = new Texture2D(GraphicsDevice, width, height);
        texture.SetData(pixels);
        using var stream = File.Create(Path.Combine(directory, fileName));
        texture.SaveAsPng(stream, width, height);

        void Line(float startX, float startY, float endX, float endY, Color color)
        {
            var x0 = Math.Max(0, (int)MathF.Round(MathF.Min(startX, endX) * renderScale));
            var y0 = Math.Max(0, (int)MathF.Round(MathF.Min(startY, endY) * renderScale));
            var x1 = Math.Min(width, (int)MathF.Round(MathF.Max(startX, endX) * renderScale) + 1);
            var y1 = Math.Min(height, (int)MathF.Round(MathF.Max(startY, endY) * renderScale) + 1);
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++) pixels[y * width + x] = color;
        }
    }

    private sealed record MeasuredTitleInk(float Left, float Top, float Right, float Bottom);
    private sealed record MenuHeadingMeasurement(string File, string Text, int RenderScale,
        string UpperBoundary, string LowerBoundary, float UpperEdge, float LowerEdge,
        MeasuredTitleInk? Ink, float ShadowDepth, float? TopGap, float? BottomGap,
        float? GapDifference, bool Passed, bool Artwork = false);
}
