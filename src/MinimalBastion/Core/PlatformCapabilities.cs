namespace MinimalBastion.Core;

public static class PlatformCapabilities
{
#if BLAZORGL
    public static bool OnlineCoOp => false;
    public static bool ExitCommand => false;
    public static bool ConfigurableVSync => false;
    public static bool StagedRuntimeTransitions => true;
#else
    public static bool OnlineCoOp => true;
    public static bool ExitCommand => true;
    public static bool ConfigurableVSync => true;
    public static bool StagedRuntimeTransitions => false;
#endif
}
