using System.Runtime.InteropServices;

namespace MinimalBastion.Core;

public static class ClipboardService
{
    public static string? TryGetText()
    {
#if BLAZORGL
        return PlatformServices.ClipboardReader?.Invoke();
#else
        try
        {
            var pointer = SDL_GetClipboardText();
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUTF8(pointer); }
            finally { SDL_free(pointer); }
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
#endif
    }

    public static bool TrySetText(string text)
    {
#if BLAZORGL
        return PlatformServices.ClipboardWriter?.Invoke(text ?? "") == true;
#else
        try { return SDL_SetClipboardText(text ?? "") == 0; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
#endif
    }

#if !BLAZORGL
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetClipboardText();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_free(IntPtr memory);
#endif
}
