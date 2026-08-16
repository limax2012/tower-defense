using MinimalBastion.Diagnostics;

namespace MinimalBastion;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--verify-ui", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                    throw new ArgumentException("Usage: MinimalBastion --verify-ui <output-directory>");
                using var verifier = new VisualVerificationGame(Path.GetFullPath(args[1]));
                verifier.Run();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"UI verification failed: {exception}");
                return 1;
            }
        }

        try
        {
            using var game = new Game1();
            game.Run();
            return 0;
        }
        catch (Exception exception)
        {
            CrashReporter.TryWrite(exception);
            throw;
        }
    }
}
