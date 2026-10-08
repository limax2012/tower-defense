using System.Security.Cryptography;

namespace MaximalBastion.Core;

/// <summary>Runtime services shared by the desktop and portable simulation builds.</summary>
public static class RuntimeCompat
{
    public static void ThrowIfNull(object? value, string parameterName)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
    }

    public static byte[] Sha256(byte[] data)
    {
        using var hash = SHA256.Create();
        return hash.ComputeHash(data);
    }

    public static string ToHexString(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

    public static void MoveFile(string source, string destination, bool overwrite)
    {
#if PORTABLE_GAMEPLAY
        if (overwrite && File.Exists(destination)) File.Replace(source, destination, null);
        else File.Move(source, destination);
#else
        File.Move(source, destination, overwrite);
#endif
    }
}
