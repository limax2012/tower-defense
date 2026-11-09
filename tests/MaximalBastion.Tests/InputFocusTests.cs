using MaximalBastion.Core;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MaximalBastion.Tests;

internal static class InputFocusTests
{
    public static void Run()
    {
        BackgroundInputCannotSelectMenus();
        ActivationClicksRemainSuppressedUntilRelease();
        FocusEventsDiscardQueuedText();
        PlatformPointerRespectsFocus();
    }

    private static void BackgroundInputCannotSelectMenus()
    {
        var router = CreateRouter();
        var ui = new UIManager(null!);
        var point = ui.MainMenuActionBounds(UiAction.OpenSoloSetup).Center;
        router.Update(true, default, Mouse(point));
        var foregroundClick = router.Update(true, default, Mouse(point, pressed: true));
        Check.Equal(UiAction.OpenSoloSetup, ui.HandleMainMenu(foregroundClick),
            "an ordinary foreground click opens setup");

        // The native focus check can detect deactivation before a framework event arrives.
        var keys = new KeyboardState(Keys.Escape, Keys.Space, Keys.P, Keys.F11, Keys.Back, Keys.Enter, Keys.D1);
        router.QueueTextInput('A');
        var backgroundClick = router.Update(false, keys, Mouse(point, pressed: true, otherButtons: true));
        AssertInactive(backgroundClick, "background devices cannot emit input");
        Check.Equal(UiAction.None, ui.HandleMainMenu(backgroundClick),
            "clicking another window over the setup button cannot open setup");
        AssertInactive(router.Update(false, default, Mouse(point)), "background releases cannot emit input");

        AssertInactive(router.Update(true, keys, Mouse(point)), "first activation frame cannot emit input");
        var heldKeys = router.Update(true, keys, Mouse(point));
        Check.True(!heldKeys.EscapePressed && !heldKeys.StartWavePressed && !heldKeys.PausePressed &&
            !heldKeys.FullscreenPressed && !heldKeys.BackspacePressed && !heldKeys.EnterPressed &&
            heldKeys.TowerHotkey == 0 && heldKeys.TextEntered == "",
            "keys held through activation and queued background text do not become new commands");
        router.Update(true, default, Mouse(point));
        var freshInput = router.Update(true, keys, Mouse(point, pressed: true, otherButtons: true));
        Check.True(freshInput.LeftPressed && freshInput.RightPressed && freshInput.PingPressed &&
            freshInput.EscapePressed && freshInput.StartWavePressed && freshInput.FullscreenPressed,
            "fresh focused button and key presses work after release");
    }

    private static void ActivationClicksRemainSuppressedUntilRelease()
    {
        var router = CreateRouter();
        var ui = new UIManager(null!);
        var point = ui.MainMenuActionBounds(UiAction.OpenSoloSetup).Center;
        router.Update(false, default, Mouse(point));
        var activation = router.Update(true, default, Mouse(point, pressed: true));
        AssertInactive(activation, "activation click is suppressed");
        var held = router.Update(true, default, Mouse(point, pressed: true));
        Check.True(!held.LeftPressed && !held.LeftDown && !held.LeftReleased,
            "holding the activation click cannot start a drag");
        Check.Equal(UiAction.None, ui.HandleMainMenu(held), "activation click does not select a menu");
        var released = router.Update(true, default, Mouse(point));
        Check.True(!released.LeftPressed && !released.LeftDown && !released.LeftReleased,
            "releasing the activation click cannot finish a game interaction");
        var freshClick = router.Update(true, default, Mouse(point, pressed: true));
        Check.Equal(UiAction.OpenSoloSetup, ui.HandleMainMenu(freshClick),
            "the next deliberate click selects the menu normally");
        Check.True(router.Update(true, default, Mouse(point)).LeftReleased,
            "a click started while focused emits its normal release");
    }

    private static void FocusEventsDiscardQueuedText()
    {
        var router = CreateRouter();
        router.Update(true, default, default);
        router.QueueTextInput('a');
        Check.Equal("A", router.Update(true, default, default).TextEntered, "focused text is delivered");
        router.QueueTextInput('b');
        router.LoseWindowFocus();
        router.QueueTextInput('c');
        AssertInactive(router.Update(true, default, default),
            "a focus loss between updates still suppresses reactivation input");
        Check.Equal("", router.Update(true, default, default).TextEntered,
            "text from before and during activation is discarded");
        router.QueueTextInput('d');
        Check.Equal("D", router.Update(true, default, default).TextEntered, "fresh focused text resumes");
    }

    private static void PlatformPointerRespectsFocus()
    {
        var router = CreateRouter();
        var pointer = new PlatformPointerState { X = 100, Y = 100, Active = false,
            LeftPressed = true, LeftDown = true, RightPressed = true, MiddlePressed = true };
        AssertInactive(router.Update(true, default, default, pointer),
            "an inactive platform pointer suppresses all input");
        pointer.Active = true;
        AssertInactive(router.Update(true, default, default, pointer), "platform activation is suppressed");
        Check.True(!router.Update(true, default, default, pointer).LeftDown,
            "platform activation cannot become a held drag");
        pointer.LeftPressed = false;
        pointer.LeftDown = false;
        pointer.LeftReleased = true;
        Check.True(!router.Update(true, default, default, pointer).LeftReleased,
            "platform activation release is suppressed");
        pointer.LeftReleased = false;
        pointer.LeftPressed = true;
        pointer.LeftDown = true;
        Check.True(router.Update(true, default, default, pointer).LeftPressed,
            "fresh platform clicks resume after release");
    }

    private static InputRouter CreateRouter()
    {
        var transform = new ViewportTransform();
        transform.Update(GameConstants.LogicalWidth, GameConstants.LogicalHeight);
        return new InputRouter(transform);
    }

    private static MouseState Mouse(Point point, bool pressed = false, bool otherButtons = false) => new(
        point.X, point.Y, 0, pressed ? ButtonState.Pressed : ButtonState.Released,
        otherButtons ? ButtonState.Pressed : ButtonState.Released,
        otherButtons ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static void AssertInactive(InputSnapshot input, string message) =>
        Check.Equal(new InputSnapshot() with { TextEntered = "" }, input with { MousePosition = Vector2.Zero }, message);
}
