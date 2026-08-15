using System.Runtime.InteropServices;
using System.Text;

namespace MinimalBastion.Diagnostics;

public static class CrashReporter
{
    public static string DefaultLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion",
        "Logs",
        "latest-crash.log");

    public static string? TryWrite(Exception exception, string? destinationPath = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var path = Path.GetFullPath(destinationPath ?? DefaultLogPath);
        var temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var assembly = typeof(CrashReporter).Assembly;
            var report = new StringBuilder()
                .AppendLine("MINIMAL BASTION CRASH REPORT")
                .AppendLine($"UTC: {DateTime.UtcNow:O}")
                .AppendLine($"GAME: {assembly.GetName().Version}")
                .AppendLine($"BUILD: {assembly.ManifestModule.ModuleVersionId:N}")
                .AppendLine($"RUNTIME: {RuntimeInformation.FrameworkDescription}")
                .AppendLine($"OS: {RuntimeInformation.OSDescription}")
                .AppendLine($"ARCH: {RuntimeInformation.ProcessArchitecture}")
                .AppendLine()
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(temporaryPath, report, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
            return path;
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { }
        }
    }
}
