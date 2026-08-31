using System.Reflection;

namespace MinimalBastion.Core;

public static class BuildInfo
{
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informationalVersion = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return typeof(BuildInfo).Assembly.GetName().Version?.ToString(3) ?? "UNKNOWN";

        var metadataIndex = informationalVersion.IndexOf('+');
        return metadataIndex >= 0 ? informationalVersion[..metadataIndex] : informationalVersion;
    }
}
