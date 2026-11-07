using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation;

namespace MaximalBastion.Tests;

internal static class AutoPlayerCommandTests
{
    public static void Run() => Run(new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load());

    public static void Run(GameContent content)
    {
        foreach (var strategy in new[] { AutoPlayerStrategy.Experienced, AutoPlayerStrategy.Randomized, AutoPlayerStrategy.LongRange })
        {
            var direct = new GameSession(content, content.Map.Id, "easy", "standard");
            var dispatched = GameSession.RestoreSaveGame(content, direct.CaptureSaveGame());
            var commands = new List<GameCommand>();
            var originalPlayer = new AutoPlayer(direct, strategy, 1337);
            var commandPlayer = new AutoPlayer(dispatched, strategy, 1337, applyCommand: command =>
            {
                commands.Add(command);
                return GameCommandProcessor.Apply(dispatched, command).Accepted;
            });
            var ticks = 0;
            while (ticks < 18_000 && !direct.IsDefeat && direct.CurrentWave < 3)
            {
                if (direct.CanStartWave)
                {
                    originalPlayer.PrepareForWave(direct);
                    commandPlayer.PrepareForWave(dispatched);
                    Check.Equal(SessionChecksum.Compute(direct, ticks), SessionChecksum.Compute(dispatched, ticks),
                        $"{strategy} command preparation preserves gameplay state");
                    Check.Equal(direct.StartNextWave(), dispatched.StartNextWave(), "both command paths start the same wave");
                }
                direct.Update(DeterministicSessionRunner.FixedStepSeconds);
                dispatched.Update(DeterministicSessionRunner.FixedStepSeconds);
                ticks++;
                if (ticks % 60 != 0) continue;
                if (direct.Waves.IsActive && !direct.IsDefeat)
                {
                    originalPlayer.ReactDuringWave(direct);
                    commandPlayer.ReactDuringWave(dispatched);
                }
                Check.Equal(SessionChecksum.Compute(direct, ticks), SessionChecksum.Compute(dispatched, ticks),
                    $"{strategy} command dispatch preserves deterministic combat");
            }
            Check.True(commands.Any(command => command.Type == GameCommandType.PlaceTower), "policy sends purchases through the sink");
            Check.True(commands.All(GameCommandValidator.IsStructurallyValid), "policy emits structurally valid commands");
            Check.Equal(SessionChecksum.Compute(direct, ticks), SessionChecksum.Compute(dispatched, ticks),
                "command dispatch agrees with the original action path");
        }

        var rejected = new GameSession(content, content.Map.Id, "easy", "standard");
        var before = SessionChecksum.Compute(rejected, 0);
        var attempts = 0;
        var blockedPlayer = new AutoPlayer(rejected, AutoPlayerStrategy.Experienced, 1337,
            applyCommand: _ => { attempts++; return false; });
        blockedPlayer.PrepareForWave(rejected);
        Check.True(attempts > 0, "rejected sink receives proposed actions");
        Check.Equal(before, SessionChecksum.Compute(rejected, 0), "a rejecting sink cannot be bypassed by direct policy actions");
    }
}
