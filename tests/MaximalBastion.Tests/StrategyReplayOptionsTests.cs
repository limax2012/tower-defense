using System.Globalization;
using MaximalBastion.Diagnostics;

namespace MaximalBastion.Tests;

internal static class StrategyReplayOptionsTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maximal-bastion-replay-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trace = Path.Combine(directory, "trace.json");
            var output = Path.Combine(directory, "replay.mp4");
            File.WriteAllText(trace, "source trace");
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var options = StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output,
                    "--speed", "1.5", "--fps", "30", "--from-wave", "21", "--max-frames", "60", "--ffmpeg", "encoder path"]);
                Check.True(options.Speed == 1.5 && options.FramesPerSecond == 30 && options.FromWave == 21 &&
                    options.MaximumFrames == 60 && options.FfmpegPath == "encoder path",
                    "replay arguments use invariant numbers and preserve encoder paths");
                Check.True(!File.Exists(output), "parsing replay options does not create or overwrite an output");
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }

            Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace]),
                "missing replay output is rejected");
            Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output, "--speed"]),
                "missing option value is rejected");
            Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output, "--unknown", "1"]),
                "unknown replay options are rejected");
            Check.Throws<FormatException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output, "--fps", "fast"]),
                "malformed replay numbers are rejected");
            Check.Throws<FileNotFoundException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace + ".missing", output]),
                "missing source trace is rejected");
            Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output + ".txt"]),
                "unsupported video extensions are rejected");
            foreach (var (option, value) in new[]
            {
                ("--speed", "NaN"), ("--speed", "Infinity"), ("--speed", "0.24"), ("--speed", "121"),
                ("--fps", "0"), ("--fps", "121"), ("--from-wave", "0"), ("--from-wave", "1001"),
                ("--max-frames", "-1"), ("--max-frames", "1000001"), ("--ffmpeg", " ")
            })
                Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", trace, output, option, value]),
                    "replay argument bounds reject " + option + "=" + value);

            foreach (var suffix in new[] { "", ".replay.json", ".poster.png", ".final.png" })
            {
                var source = output + suffix;
                File.WriteAllText(source, "protected source");
                var aliasedOutput = Path.Combine(directory, ".", "replay.mp4");
                Check.Throws<ArgumentException>(() => StrategyReplayOptions.Parse(["--render-strategy-replay", source, aliasedOutput]),
                    "video and companion files cannot overwrite their source trace");
                Check.Equal("protected source", File.ReadAllText(source), "source collision rejection preserves the original file");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
