using System.Runtime.InteropServices;
using MaximalBastion.Core;

namespace MaximalBastion.Tests;

internal static class WindowInputFocusTests
{
    private const uint VideoSubsystem = 0x00000020;
    private const uint HiddenWindow = 0x00000008;

    public static void Run()
    {
        if (!OperatingSystem.IsWindows()) return;

        Check.Equal(IntPtr.Zero, WindowInputFocus.TryGetWindowsHandle(IntPtr.Zero),
            "An unavailable SDL window has no native input target");
        Check.True(!WindowInputFocus.IsActive(IntPtr.Zero, true),
            "A missing window cannot accept input even when the framework reports it active");

        Check.Equal(0, SDL_InitSubSystem(VideoSubsystem), "SDL video initializes for hidden native focus checks");
        var firstWindow = IntPtr.Zero;
        var secondWindow = IntPtr.Zero;
        try
        {
            firstWindow = CreateHiddenWindow();
            secondWindow = CreateHiddenWindow();
            var firstHandle = AssertHiddenWindowRejectsInput(firstWindow);
            var secondHandle = AssertHiddenWindowRejectsInput(secondWindow);
            Check.True(firstHandle != secondHandle,
                "Focus resolution identifies each native window independently within the same process");
            Check.Equal(firstHandle, WindowInputFocus.TryGetWindowsHandle(firstWindow),
                "Resolving another SDL window does not replace the first window's input target");
        }
        finally
        {
            if (secondWindow != IntPtr.Zero) SDL_DestroyWindow(secondWindow);
            if (firstWindow != IntPtr.Zero) SDL_DestroyWindow(firstWindow);
            SDL_QuitSubSystem(VideoSubsystem);
        }
    }

    private static IntPtr CreateHiddenWindow()
    {
        var window = SDL_CreateWindow("Maximal Bastion input verification", 0, 0, 64, 64, HiddenWindow);
        Check.True(window != IntPtr.Zero, "SDL creates a hidden window for native focus checks");
        return window;
    }

    private static IntPtr AssertHiddenWindowRejectsInput(IntPtr sdlWindow)
    {
        var nativeWindow = WindowInputFocus.TryGetWindowsHandle(sdlWindow);
        Check.True(nativeWindow != IntPtr.Zero && IsWindow(nativeWindow),
            "The SDL window resolves to a valid Windows HWND");
        Check.True(nativeWindow != sdlWindow,
            "The native input target is the HWND rather than the SDL window pointer");
        Check.True(!IsWindowVisible(nativeWindow), "Native focus verification leaves its window hidden");
        Check.True(!WindowInputFocus.IsActive(sdlWindow, true),
            "A background native window rejects input when the framework active flag is stale");
        Check.True(!WindowInputFocus.IsActive(sdlWindow, false),
            "An inactive framework window rejects input");
        return nativeWindow;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_InitSubSystem(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_QuitSubSystem(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        int x, int y, int width, int height, uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
