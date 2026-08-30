using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MinimalBastion.Core;

public static class GameConstants
{
    public const int LogicalWidth = 1280;
    public const int LogicalHeight = 720;
    public const int RenderScale = 2;
    public const int MaximumRenderScale = 3;
    public const int RenderWidth = LogicalWidth * RenderScale;
    public const int RenderHeight = LogicalHeight * RenderScale;
    public const float FontDrawScale = 1f / RenderScale;
    public const int MapWidth = 960;
    public const int SidebarX = 960;
    public const int TopBarHeight = 56;
    public const float TowerRadius = 18f;
    public const float PlacementPathClearance = 50f;
    public const float TowerMinimumGap = 40f;
    public const int StartingLives = 20;
    public const int StartingCredits = 400;
    public const float IntermissionSeconds = 10f;
    public const int EarlyStartBonus = 20;
    public const int FullKillRewardThroughWave = 10;
    public const float KillRewardTaperPerWave = 0.025f;
    public const float MinimumKillRewardMultiplier = 0.40f;
    public const int HalfIncomeStartWave = 15;
    public const int QuarterIncomeStartWave = 25;
    public const float OverdriveDurationSeconds = 5f;
    public const float OverdriveCooldownSeconds = 18f;
    public const float OverdriveAttackSpeedBonus = 0.75f;
    public const float SellRatio = 0.60f;
    public const int CampaignWaveCount = 30;
    public const int ApexUnlockWave = 21;
    public const int GeneratedEndlessStartWave = 31;
    public const int PulsePlateDamageSourceOffset = 100_000;
    public const int MaximumPulsePlateId = int.MaxValue - PulsePlateDamageSourceOffset + 1;
    public const int ExhaustedPulsePlateNextId = MaximumPulsePlateId + 1;

    public static int RenderScaleForOutput(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var visibleScale = MathF.Min(width / (float)LogicalWidth, height / (float)LogicalHeight);
        return Math.Clamp((int)MathF.Ceiling(visibleScale), RenderScale, MaximumRenderScale);
    }
}

public enum GameState
{
    MainMenu,
    GameSetup,
    LoadingTransition,
    TowerLibrary,
    Settings,
    SaveSlots,
    RunHistory,
    RunHistoryField,
    CoOpMenu,
    CoOpLobby,
    CoOpReconnect,
    Playing,
    Paused,
    Victory,
    Defeat,
    DefeatField
}

public enum TargetMode
{
    First,
    Last,
    Strongest,
    Weakest,
    Nearest,
    Fastest,
    Armored,
    Support
}

public enum EnemyRank
{
    Standard,
    Elite,
    Boss
}

public enum PlacementFailure
{
    None,
    OutsideBuildableRegion,
    BlocksPath,
    OverlapsTower,
    TooCloseToEdge,
    InsufficientCredits,
    UnknownTower,
    MustBeOnPath,
    TooCloseToPathEndpoint,
    OverlapsDefense,
    GeneratorAlreadyBuilt,
    NoDefenseAvailable,
    DefenseCapacityReached,
    TowerUnavailable,
    TacticalSystemsDisabled,
    IdentityCapacityReached
}

public enum TacticalPlacementKind
{
    None,
    PulsePlate,
    ChargeForge
}

public readonly record struct InputSnapshot(
    Vector2 MousePosition,
    bool LeftPressed,
    bool LeftReleased,
    bool RightPressed,
    bool PingPressed,
    bool EscapePressed,
    bool PausePressed,
    bool DebugKeyPressed,
    bool IsMouseOverLogicalCanvas,
    int TowerHotkey,
    bool UpgradePressed,
    bool SellPressed,
    bool TargetPressed,
    bool StartWavePressed,
    bool SpeedPressed,
    bool EmergencyPressed,
    bool GeneratorPressed,
    bool OverdrivePressed,
    string TextEntered,
    bool BackspacePressed,
    bool EnterPressed,
    bool CopyPressed = false,
    bool TabPressed = false,
    bool NavigateUpPressed = false,
    bool NavigateDownPressed = false,
    bool NavigateLeftPressed = false,
    bool NavigateRightPressed = false,
    bool AutoProtocolPressed = false,
    bool SandboxSpawnPressed = false,
    bool SandboxResetPressed = false,
    bool SandboxClearTowersPressed = false,
    bool SandboxWavePreviousPressed = false,
    bool SandboxWaveNextPressed = false,
    bool FullscreenPressed = false,
    bool ApexPressed = false,
    bool AlternateUpgradePressed = false,
    bool SandboxToggleTowerPressed = false,
    bool SandboxEnemyPreviousPressed = false,
    bool SandboxEnemyNextPressed = false,
    bool SandboxRankPressed = false,
    bool SandboxHealthPressed = false,
    bool SandboxSignalPressed = false,
    bool LeftDown = false);

public sealed class ViewportTransform
{
    public float Scale { get; private set; } = 1f;
    public Vector2 Offset { get; private set; }
    public Matrix DrawMatrix => Matrix.CreateScale(Scale) * Matrix.CreateTranslation(Offset.X, Offset.Y, 0f);
    public Rectangle DestinationRectangle => new(
        (int)MathF.Round(Offset.X),
        (int)MathF.Round(Offset.Y),
        Math.Max(1, (int)MathF.Round(GameConstants.LogicalWidth * Scale)),
        Math.Max(1, (int)MathF.Round(GameConstants.LogicalHeight * Scale)));

    public void Update(int width, int height)
    {
        Scale = MathF.Min(width / (float)GameConstants.LogicalWidth, height / (float)GameConstants.LogicalHeight);
        Offset = new Vector2((width - GameConstants.LogicalWidth * Scale) * 0.5f, (height - GameConstants.LogicalHeight * Scale) * 0.5f);
    }

    public Vector2 ScreenToLogical(Point point)
    {
        return (new Vector2(point.X, point.Y) - Offset) / MathF.Max(Scale, 0.0001f);
    }

    public bool ContainsScreenPoint(Point point, int width, int height)
    {
        var logical = ScreenToLogical(point);
        return logical.X >= 0 && logical.Y >= 0 &&
               logical.X <= GameConstants.LogicalWidth && logical.Y <= GameConstants.LogicalHeight &&
               point.X >= Offset.X && point.Y >= Offset.Y &&
               point.X <= Offset.X + GameConstants.LogicalWidth * Scale &&
               point.Y <= Offset.Y + GameConstants.LogicalHeight * Scale;
    }
}

public static class WindowLayout
{
    private const int DesktopHorizontalReserve = 32;
    private const int DesktopVerticalReserve = 96;

    public static (int Width, int Height) FitClientInsideDesktop(int requestedWidth, int requestedHeight, int desktopWidth, int desktopHeight)
    {
        requestedWidth = Math.Max(1, requestedWidth);
        requestedHeight = Math.Max(1, requestedHeight);
        var availableWidth = Math.Max(1, desktopWidth - DesktopHorizontalReserve);
        var availableHeight = Math.Max(1, desktopHeight - DesktopVerticalReserve);
        var scale = MathF.Min(1f, MathF.Min(availableWidth / (float)requestedWidth, availableHeight / (float)requestedHeight));
        return (
            Math.Max(1, (int)MathF.Floor(requestedWidth * scale)),
            Math.Max(1, (int)MathF.Floor(requestedHeight * scale)));
    }

    public static Point Recenter(Rectangle previousBounds, int newWidth, int newHeight, int desktopWidth, int desktopHeight)
    {
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);
        desktopWidth = Math.Max(1, desktopWidth);
        desktopHeight = Math.Max(1, desktopHeight);

        var previousCenterX = previousBounds.X + previousBounds.Width / 2;
        var previousCenterY = previousBounds.Y + previousBounds.Height / 2;
        var maximumX = Math.Max(0, desktopWidth - newWidth);
        var maximumY = Math.Max(0, desktopHeight - newHeight);
        return new Point(
            Math.Clamp(previousCenterX - newWidth / 2, 0, maximumX),
            Math.Clamp(previousCenterY - newHeight / 2, 0, maximumY));
    }
}

public sealed class InputRouter
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private readonly Queue<char> _textInput = new();
    private readonly ViewportTransform _transform;
    private long _nextBackspaceRepeatTimestamp;
    private bool _wasWindowActive;

    public InputRouter(ViewportTransform transform) => _transform = transform;

    public void QueueTextInput(char character)
    {
        if (char.IsLetterOrDigit(character) || character is '.' or ':' or '-' or '[' or ']')
            _textInput.Enqueue(char.ToUpperInvariant(character));
    }

    public void LoseWindowFocus()
    {
        _wasWindowActive = false;
        _textInput.Clear();
        _nextBackspaceRepeatTimestamp = 0;
    }

    public InputSnapshot Update(bool windowActive)
    {
        var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        var platformPointer = PlatformServices.PointerStateReader?.Invoke();
        if (platformPointer is not null) windowActive &= platformPointer.Active;
        var screenPoint = platformPointer is null
            ? new Point(mouse.X, mouse.Y)
            : new Point(platformPointer.X, platformPointer.Y);
        var logical = _transform.ScreenToLogical(screenPoint);

        // Keyboard.GetState and Mouse.GetState can report global device state even
        // while the game is covered or minimized. Synchronize the physical state
        // without emitting actions whenever the window is inactive. The first
        // frame after focus returns is suppressed as well, preventing the click or
        // key used to reactivate the window from triggering a game command.
        if (!windowActive || !_wasWindowActive)
        {
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            _textInput.Clear();
            _nextBackspaceRepeatTimestamp = 0;
            _wasWindowActive = windowActive;
            return InactiveSnapshot(logical);
        }

        var controlDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var textEntered = DrainTextInput();
        if (controlDown) textEntered = "";
        if (controlDown && IsPressed(keyboard, _previousKeyboard, Keys.V))
            textEntered = ClipboardService.TryGetText() ?? "";
        var snapshot = new InputSnapshot(
            logical,
            platformPointer?.LeftPressed ?? mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            platformPointer?.LeftReleased ?? mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            platformPointer?.RightPressed ?? mouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.RightButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            platformPointer?.MiddlePressed ?? mouse.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.MiddleButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.Escape),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.P),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.F4),
            logical.X >= 0 && logical.Y >= 0 && logical.X <= GameConstants.LogicalWidth && logical.Y <= GameConstants.LogicalHeight,
            GetTowerHotkey(keyboard, _previousKeyboard),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.U),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.Delete),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.T),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.Space),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.S),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.Q),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.G),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.E),
            textEntered,
            ShouldBackspace(keyboard),
            IsPressed(keyboard, _previousKeyboard, Microsoft.Xna.Framework.Input.Keys.Enter),
            controlDown && IsPressed(keyboard, _previousKeyboard, Keys.C),
            IsPressed(keyboard, _previousKeyboard, Keys.Tab),
            IsPressed(keyboard, _previousKeyboard, Keys.Up),
            IsPressed(keyboard, _previousKeyboard, Keys.Down),
            IsPressed(keyboard, _previousKeyboard, Keys.Left),
            IsPressed(keyboard, _previousKeyboard, Keys.Right),
            IsPressed(keyboard, _previousKeyboard, Keys.A),
            IsPressed(keyboard, _previousKeyboard, Keys.F),
            IsPressed(keyboard, _previousKeyboard, Keys.R),
            !controlDown && IsPressed(keyboard, _previousKeyboard, Keys.C),
            IsPressed(keyboard, _previousKeyboard, Keys.OemMinus) || IsPressed(keyboard, _previousKeyboard, Keys.Subtract),
            IsPressed(keyboard, _previousKeyboard, Keys.OemPlus) || IsPressed(keyboard, _previousKeyboard, Keys.Add),
            IsPressed(keyboard, _previousKeyboard, Keys.F11),
            IsPressed(keyboard, _previousKeyboard, Keys.X),
            IsPressed(keyboard, _previousKeyboard, Keys.I),
            IsPressed(keyboard, _previousKeyboard, Keys.D),
            IsPressed(keyboard, _previousKeyboard, Keys.OemOpenBrackets),
            IsPressed(keyboard, _previousKeyboard, Keys.OemCloseBrackets),
            IsPressed(keyboard, _previousKeyboard, Keys.K),
            IsPressed(keyboard, _previousKeyboard, Keys.H),
            IsPressed(keyboard, _previousKeyboard, Keys.J),
            platformPointer?.LeftDown ?? mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _wasWindowActive = true;
        return snapshot;
    }

    private static InputSnapshot InactiveSnapshot(Vector2 mousePosition) => new(
        MousePosition: mousePosition,
        LeftPressed: false,
        LeftReleased: false,
        RightPressed: false,
        PingPressed: false,
        EscapePressed: false,
        PausePressed: false,
        DebugKeyPressed: false,
        IsMouseOverLogicalCanvas: false,
        TowerHotkey: 0,
        UpgradePressed: false,
        SellPressed: false,
        TargetPressed: false,
        StartWavePressed: false,
        SpeedPressed: false,
        EmergencyPressed: false,
        GeneratorPressed: false,
        OverdrivePressed: false,
        TextEntered: "",
        BackspacePressed: false,
        EnterPressed: false);

    private static bool IsPressed(KeyboardState current, KeyboardState previous, Microsoft.Xna.Framework.Input.Keys key)
        => current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private bool ShouldBackspace(KeyboardState keyboard)
    {
        if (!keyboard.IsKeyDown(Keys.Back))
        {
            _nextBackspaceRepeatTimestamp = 0;
            return false;
        }

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_previousKeyboard.IsKeyDown(Keys.Back))
        {
            _nextBackspaceRepeatTimestamp = now + (long)(System.Diagnostics.Stopwatch.Frequency * 0.34);
            return true;
        }
        if (_nextBackspaceRepeatTimestamp <= 0 || now < _nextBackspaceRepeatTimestamp) return false;
        _nextBackspaceRepeatTimestamp = now + (long)(System.Diagnostics.Stopwatch.Frequency * 0.045);
        return true;
    }

    private static int GetTowerHotkey(KeyboardState current, KeyboardState previous)
    {
        var keys = new[]
        {
            Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5,
            Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0
        };
        for (var index = 0; index < keys.Length; index++)
            if (IsPressed(current, previous, keys[index])) return index + 1;
        return 0;
    }

    private string DrainTextInput()
    {
        var characters = new List<char>();
        while (_textInput.TryDequeue(out var character)) characters.Add(character);
        return new string(characters.ToArray());
    }
}
