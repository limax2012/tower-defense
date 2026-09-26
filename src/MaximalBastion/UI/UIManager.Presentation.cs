using MaximalBastion.Analytics;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    private const int MenuMargin = 24;
    private const float MenuHeadingScale = 1.5f;
    private const float ControlTextScale = .65f;
    private const float SecondarySurfaceTint = .045f;
    private const float HoverSurfaceTint = .13f;
    private const int ButtonShadowDepth = 3;
    private static readonly Rectangle MenuFrameBounds = new(MenuMargin, MenuMargin,
        GameConstants.LogicalWidth - MenuMargin * 2, GameConstants.LogicalHeight - MenuMargin * 2);
    private static Rectangle PageHeadingBand(int? lowerEdge = null) => new(300, MenuFrameBounds.Top + 2,
        680, (lowerEdge ?? CommandSurfaceArt.UpperConduitTop(MenuFrameBounds)) - MenuFrameBounds.Top - 2);
    private int _settingsCategory;
    private int _careerCategory;
    private bool _resultDetails;
    internal static readonly Rectangle ResultDetailsBounds = new(824, 101, 158, 32);
    internal static readonly Rectangle SandboxPlateBounds = new(1120, 205, 140, 19);
    internal static Rectangle SettingsCategoryBounds(int index) => new(350 + index * 196, 164, 188, 38);
    internal static Rectangle CareerCategoryBounds(int index) => new(330 + index * 210, 105, 200, 36);
    internal bool ResultDetailsVisible => _resultDetails;

    internal IReadOnlyList<(UiAction Action, string Label)> MainMenuActions()
    {
        var actions = new List<(UiAction, string)> { (UiAction.OpenSoloSetup, "PLAY") };
        if (PlatformCapabilities.OnlineCoOp) actions.Add((UiAction.CoOp, "CO-OP"));
        actions.Add((UiAction.LoadGame, "LOAD"));
        actions.Add((UiAction.RunHistory, "HISTORY"));
        actions.Add((UiAction.Settings, "SETTINGS"));
        if (PlatformCapabilities.ExitCommand) actions.Add((UiAction.Exit, "QUIT"));
        return actions;
    }

    internal Rectangle MainMenuActionBounds(UiAction action)
    {
        var items = MainMenuActions();
        const int gap = 12;
        var height = items.Sum(item => MenuActionHeight(item.Action)) + (items.Count - 1) * gap;
        var y = 470 - (height + ButtonShadowDepth) / 2;
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) y += gap;
            var rowHeight = MenuActionHeight(items[i].Action);
            if (items[i].Action == action) return new Rectangle(490, y, 300, rowHeight);
            y += rowHeight;
        }
        return Rectangle.Empty;

        static int MenuActionHeight(UiAction item) => item == UiAction.OpenSoloSetup ? 50 : 42;
    }

    private static Color MainMenuActionAccent(UiAction action) => action switch
    {
        UiAction.OpenSoloSetup => ColorPalette.Gold,
        UiAction.CoOp => ColorPalette.Violet,
        UiAction.LoadGame => ColorPalette.Cobalt,
        UiAction.RunHistory => ColorPalette.Green,
        UiAction.Exit => ColorPalette.Coral,
        _ => ColorPalette.Muted
    };

    private bool SettingVisible(int index) => index == 7 || _settingsCategory switch
    {
        0 => index is 0 or 1 or 2,
        1 => index is 3 or 4,
        _ => index is 5 or 6
    };

    internal string HoveredModeDescription
    {
        get
        {
            var modes = SetupChallenges();
            for (var index = 0; index < modes.Count; index++)
                if (SetupCardRectangle(2, index, modes.Count).Contains(_hudPointer.ToPoint()))
                    return modes[index].Id == "standard" ? "Full arsenal. Standard rules." : ModeSummary(modes[index].Id);
            return "";
        }
    }

    private static string ModeSummary(string id) => id switch
    {
        "core_six" => "Six core tower types.",
        "no_reserves" => "No Plates, Forge, manual Protocols, or selling.",
        "close_quarters" => "Signal enemies disrupt nearby defenses.",
        "sandbox_lab" => "Unlimited resources. Spawn targets and test defenses.",
        _ => ""
    };

    private void DrawSettings(SpriteBatch b, PrimitiveRenderer p)
    {
        DrawMenuFrame(b, p);
        DrawHeading(b, "Settings", PageHeadingBand());
        var categories = new[] { "DISPLAY", "AUDIO", "GAMEPLAY" };
        for (var i = 0; i < categories.Length; i++)
            DrawSetupChoice(b, p, SettingsCategoryBounds(i), categories[i], ColorPalette.Cyan, i == _settingsCategory);
        if (_settingsCategory == 0)
        {
            DrawButton(b, p, _windowModeButton, _settings.Fullscreen ? "FULLSCREEN" : "WINDOWED", true, ColorPalette.CardOutline);
            if (PlatformCapabilities.ConfigurableVSync)
                DrawButton(b, p, _vsyncButton, "V-SYNC  " + (_settings.VSync ? "ON" : "OFF"), true, ColorPalette.CardOutline);
            DrawButton(b, p, _effectsButton, "EFFECTS  " + (_settings.ReducedEffects ? "REDUCED" : "FULL"), true, ColorPalette.CardOutline);
        }
        else if (_settingsCategory == 1)
        {
            DrawVolumeSlider(b, p, _volumeButton, "SOUND", _settings.SfxVolume, ColorPalette.Cyan);
            DrawVolumeSlider(b, p, _musicVolumeButton, "MUSIC", _settings.MusicVolume, ColorPalette.Cyan);
        }
        else
        {
            DrawButton(b, p, _autoStartButton, "AUTO WAVES  " + (_settings.AutoStartWaves ? $"{_settings.AutoStartDelaySeconds}s" : "OFF"), true, ColorPalette.CardOutline);
            DrawButton(b, p, _hotkeyBadgesButton, "HOTKEY LABELS  " + (_settings.ShowHotkeyBadges ? "ON" : "OFF"), true, ColorPalette.CardOutline);
        }
        DrawButton(b, p, _settingsBackButton, "BACK", true, ColorPalette.CardOutline);
        if (!string.IsNullOrWhiteSpace(_settingsStatus)) DrawMenuStatus(b, _settingsStatus, ColorPalette.Muted);
    }

    private void DrawMenuStatus(SpriteBatch b, string status, Color color)
    {
        if (status == "One rolling autosave; manual slots are available between waves." ||
            status == "Completed campaigns and endless progress are recorded locally.") return;
        DrawFittedCenteredText(b, status, new Vector2(640, 666), color, .45f, 1120);
    }

    private void DrawCareerProgress(SpriteBatch b, PrimitiveRenderer p)
    {
        DrawMenuFrame(b, p, ColorPalette.Gold);
        DrawHeading(b, "Achievements", PageHeadingBand(CareerCategoryBounds(0).Top - 1),
            lowerBoundary: "Category tabs");
        var categories = new[] { "ACHIEVEMENTS", "MEDALS", "RECORDS" };
        for (var i = 0; i < 3; i++)
            DrawSetupChoice(b, p, CareerCategoryBounds(i), categories[i], ColorPalette.Cyan, i == _careerCategory);
        var career = CareerProgression.Analyze(_runHistory);
        if (_careerCategory == 2)
        {
            var labels = new[] { "CAMPAIGNS WON", "FARTHEST WAVE", "FASTEST WIN", "FEWEST TOWERS" };
            var values = new[] { career.CampaignsSecured.ToString(),
                CareerRunLabel(career.DeepestRun, r => r.CurrentWave.ToString()),
                CareerRunLabel(career.FastestClear, r => FormatRunDuration(r.DefenseSeconds)),
                CareerRunLabel(career.LeanestClear, r => (r.FinalLayout?.Towers.Count ?? 0).ToString()) };
            for (var i = 0; i < 4; i++)
            {
                var y = 195 + i * 80;
                var card = new Rectangle((GameConstants.LogicalWidth - 650) / 2, y, 650, 67);
                p.FillRect(b, card, ColorPalette.PanelAlt * .62f);
                p.Line(b, new Vector2(card.Left, y + 66), new Vector2(card.Right, y + 66), ColorPalette.Divider);
                DrawText(b, labels[i], new Vector2(card.Left + 30, y + 12), ColorPalette.Muted, .48f);
                DrawFittedText(b, values[i], new Vector2(card.Left + 30, y + 34), ColorPalette.Paper, .70f, 590);
            }
        }
        else
        {
            var achievements = _careerCategory == 0;
            var page = achievements ? _careerAchievementPage : _careerMedalPage;
            var size = achievements ? 8 : 7;
            var count = achievements ? career.Achievements.Count : career.Medals.Count;
            var pages = Math.Max(1, (count + size - 1) / size);
            for (var i = 0; i < Math.Min(size, count - page * size); i++)
            {
                var index = page * size + i;
                var y = 164 + i * 51;
                var name = achievements ? career.Achievements[index].DisplayName : career.Medals[index].Definition.DisplayName;
                var description = achievements ? career.Achievements[index].Description : career.Medals[index].Definition.Description;
                var complete = achievements ? career.Achievements[index].IsUnlocked : career.Medals[index].IsUnlocked;
                var progress = complete ? "COMPLETE" : achievements ? career.Achievements[index].Progress : "LOCKED";
                var accent = achievements ? ColorPalette.Cyan : ColorPalette.Gold;
                p.FillRect(b, new Rectangle(84, y - 3, 1112, 47), complete
                    ? ColorPalette.Surface(accent, .045f) : ColorPalette.Panel * .88f);
                DrawCompletionInsignia(b, p, new Vector2(106, y + 20), 15, complete, accent);
                p.Line(b, new Vector2(134, y + 47), new Vector2(1196, y + 47), ColorPalette.Divider);
                DrawText(b, name, new Vector2(140, y), ColorPalette.Paper, .66f);
                DrawFittedText(b, description, new Vector2(140, y + 24), ColorPalette.Muted, .45f, 800);
                DrawTextRight(b, progress, new Vector2(1186, y + 2), complete ? ColorPalette.GreenText : ColorPalette.Muted, .48f);
                DrawAchievementProgress(b, p, new Rectangle(1048, y + 30, 138, 3),
                    complete ? 1 : achievements ? AchievementProgressFraction(career.Achievements[index].Progress) : 0, accent);
            }
            if (pages > 1)
            {
                DrawButton(b, p, _careerAchievementPreviousButton, "<", page > 0, ColorPalette.CardOutline);
                DrawText(b, $"{page + 1}/{pages}", new Vector2(1120, 606), ColorPalette.Muted, .46f, true);
                DrawButton(b, p, _careerAchievementNextButton, ">", page + 1 < pages, ColorPalette.CardOutline);
            }
        }
        DrawButton(b, p, _runHistoryCareerBackButton, "BACK", true, ColorPalette.CardOutline);
    }
}
