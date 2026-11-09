using System.Runtime.InteropServices;

namespace MaximalBastion.Core;

internal static class WindowInputFocus
{
    internal static bool IsActive(IntPtr sdlWindow, bool frameworkActive)
    {
        if (!frameworkActive) return false;
#if BLAZORGL
        return true;
#else
        if (!OperatingSystem.IsWindows()) return true;
        var nativeWindow = TryGetWindowsHandle(sdlWindow);
        return nativeWindow != IntPtr.Zero && GetForegroundWindow() == nativeWindow;
#endif
    }

#if !BLAZORGL
    internal static IntPtr TryGetWindowsHandle(IntPtr sdlWindow)
    {
        if (!OperatingSystem.IsWindows() || sdlWindow == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            SDL_GetVersion(out var version);
            var info = new SdlWindowManagerInfo { Version = version };
            return SDL_GetWindowWMInfo(sdlWindow, ref info) != 0 && info.Subsystem == 1
                ? info.Window
                : IntPtr.Zero;
        }
        catch (DllNotFoundException) { return IntPtr.Zero; }
        catch (EntryPointNotFoundException) { return IntPtr.Zero; }
        catch (BadImageFormatException) { return IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SdlVersion
    {
        public byte Major;
        public byte Minor;
        public byte Patch;
    }

    // DesktopGL exposes an SDL_Window pointer. SDL_SysWMinfo supplies the HWND;
    // its header occupies eight bytes and its platform union reserves 64 bytes.
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    private struct SdlWindowManagerInfo
    {
        [FieldOffset(0)] public SdlVersion Version;
        [FieldOffset(4)] public int Subsystem;
        [FieldOffset(8)] public IntPtr Window;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_GetVersion(out SdlVersion version);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_GetWindowWMInfo(IntPtr window, ref SdlWindowManagerInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
#endif
}
