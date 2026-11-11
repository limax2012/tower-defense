using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.UI;

namespace MaximalBastion.Tests;

internal static class UiAudioFeedbackTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var ui = new UIManager(null!);
        ui.ConfigureMaps(content.Maps.Values);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        ui.ConfigureChallenges(content.Challenges.Values);
        ui.PrepareGameSetup(false);
        var input = default(InputSnapshot) with
        {
            MousePosition = UIManager.SetupCardRectangle(0, 1, content.Maps.Count).Center.ToVector2(),
            IsMouseOverLogicalCanvas = true,
            TextEntered = ""
        };
        var before = ui.CaptureNavigationSnapshot();
        ui.HandleGameSetup(input);
        Check.Equal(UiNavigationFeedback.None, ui.NavigationFeedbackSince(before, input), "Hover is silent");
        input = input with { LeftPressed = true };
        ui.HandleGameSetup(input);
        Check.Equal(UiNavigationFeedback.Navigate, ui.NavigationFeedbackSince(before, input), "A changed setup selection requests navigation feedback");
        before = ui.CaptureNavigationSnapshot();
        ui.HandleGameSetup(input);
        Check.Equal(UiNavigationFeedback.None, ui.NavigationFeedbackSince(before, input), "Selecting the current map remains silent");

        before = ui.CaptureNavigationSnapshot();
        input = input with { MousePosition = UIManager.SettingsCategoryBounds(1).Center.ToVector2() };
        ui.HandleSettingsInput(input);
        Check.Equal(UiNavigationFeedback.Navigate, ui.NavigationFeedbackSince(before, input), "Settings tab changes request feedback");
        Check.Equal(UiNavigationFeedback.None, ui.NavigationFeedbackSince(before, default), "Inactive input cannot request navigation feedback");

        ui.PrepareResultScreen();
        before = ui.CaptureNavigationSnapshot();
        input = input with { MousePosition = UIManager.ResultDetailsBounds.Center.ToVector2() };
        Check.Equal(UiAction.None, ui.HandleResultInput(input, true), "Internal detail navigation retains its action contract");
        Check.Equal(UiNavigationFeedback.Navigate, ui.NavigationFeedbackSince(before, input), "Opening statistics requests feedback");
        before = ui.CaptureNavigationSnapshot();
        input = input with { LeftPressed = false, EscapePressed = true };
        ui.HandleResultInput(input, true);
        Check.Equal(UiNavigationFeedback.Back, ui.NavigationFeedbackSince(before, input), "Closing details requests back feedback");

        input = input with { EscapePressed = false, NavigateRightPressed = true };
        ui.HandleResultInput(input, true);
        before = ui.CaptureNavigationSnapshot();
        input = input with { NavigateRightPressed = false, EnterPressed = true };
        Check.Equal(UiAction.None, ui.HandleResultInput(input, true), "Restart arms before dispatching its command");
        Check.Equal(UiNavigationFeedback.Confirm, ui.NavigationFeedbackSince(before, input), "Arming a destructive action requests confirmation feedback");
        before = ui.CaptureNavigationSnapshot();
        Check.Equal(UiAction.Restart, ui.HandleResultInput(input, true), "Confirmed restart still dispatches normally");
    }
}
