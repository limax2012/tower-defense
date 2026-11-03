using MaximalBastion.Diagnostics;

namespace MaximalBastion;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--capture-enemy-lane", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (args.Length != 3 || string.IsNullOrWhiteSpace(args[1]) ||
                    args[2].ToUpperInvariant() is not ("B" or "C"))
                    throw new ArgumentException("Usage: MaximalBastion --capture-enemy-lane <output-directory> <B|C>. B uses full indicators; C uses contextual indicators. Capture earlier artwork with its archived executable.");
                using var capture = new VisualVerificationGame(Path.GetFullPath(args[1]), args[2].ToUpperInvariant());
                capture.Run();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Enemy lane capture failed: {exception}");
                return 1;
            }
        }

        if (args.Length > 0 && args[0].Equals("--capture-marketing", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                    throw new ArgumentException("Usage: MaximalBastion --capture-marketing <output-directory>");
                using var capture = new MarketingCaptureGame(Path.GetFullPath(args[1]));
                capture.Run();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Marketing capture failed: {exception}");
                return 1;
            }
        }

        if (args.Length > 0 && args[0].Equals("--verify-ui", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                    throw new ArgumentException("Usage: MaximalBastion --verify-ui <output-directory>");
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
