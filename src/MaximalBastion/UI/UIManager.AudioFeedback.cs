using MaximalBastion.Core;

namespace MaximalBastion.UI;

internal enum UiNavigationFeedback { None, Navigate, Confirm, Back }

internal readonly record struct UiNavigationSnapshot(
    (int Map, int Difficulty, int Challenge) Setup,
    (int Category, int Selection) Settings,
    (int Slot, int Page) Saves,
    (string? Run, int Page, int Category, int Medals, int Achievements) History,
    (bool History, bool Career, bool Results, bool TargetPicker) Details,
    (bool Save, bool History, bool Restart) Confirmation,
    (int Menu, bool Code) CoOp,
    (int Enemy, int Group, int Rank, int Health, int Signal, int Wave) Sandbox,
    int ResultSelection);

public sealed partial class UIManager
{
    // Hover and continuous slider movement are excluded from navigation feedback.
    internal UiNavigationSnapshot CaptureNavigationSnapshot() => new(
        (_selectedMapIndex, _selectedDifficultyIndex, _selectedChallengeIndex),
        (_settingsCategory, _settingsSelection),
        (_selectedSaveSlot, _saveSlotPage),
        (_selectedRunHistoryId, _runHistoryPage, _careerCategory, _careerMedalPage, _careerAchievementPage),
        (_runHistoryDetailOpen, _runHistoryCareerOpen, _resultDetails, _targetPickerOpen),
        (_saveSlotDeleteArmed, _runHistoryDeleteArmed, _restartArmed),
        (_coOpMenuSelection, _editingJoinCode),
        (_sandboxEnemyIndex, _sandboxGroupIndex, _sandboxRankIndex, _sandboxHealthIndex, _sandboxSignalIndex, _sandboxWaveNumber),
        _resultMenuSelection);

    internal UiNavigationFeedback NavigationFeedbackSince(UiNavigationSnapshot before, InputSnapshot input)
    {
        var intentional = input.LeftPressed || input.RightPressed || input.EscapePressed || input.PausePressed ||
            input.EnterPressed || input.TabPressed || input.NavigateUpPressed || input.NavigateDownPressed ||
            input.NavigateLeftPressed || input.NavigateRightPressed || input.TargetPressed ||
            input.SandboxEnemyPreviousPressed || input.SandboxEnemyNextPressed || input.SandboxRankPressed ||
            input.SandboxHealthPressed || input.SandboxSignalPressed || input.SandboxWavePreviousPressed ||
            input.SandboxWaveNextPressed;
        if (!intentional) return UiNavigationFeedback.None;
        var after = CaptureNavigationSnapshot();
        if (before == after) return UiNavigationFeedback.None;
        if (input.EscapePressed || input.RightPressed ||
            before.Details.History && !after.Details.History || before.Details.Career && !after.Details.Career ||
            before.Details.Results && !after.Details.Results || before.Details.TargetPicker && !after.Details.TargetPicker)
            return UiNavigationFeedback.Back;
        if (before.Confirmation != after.Confirmation &&
            (after.Confirmation.Save || after.Confirmation.History || after.Confirmation.Restart))
            return UiNavigationFeedback.Confirm;
        return UiNavigationFeedback.Navigate;
    }
}
