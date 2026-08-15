using MinimalBastion.Diagnostics;

namespace MinimalBastion;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            using var game = new Game1();
            game.Run();
        }
        catch (Exception exception)
        {
            CrashReporter.TryWrite(exception);
            throw;
        }
    }
}
