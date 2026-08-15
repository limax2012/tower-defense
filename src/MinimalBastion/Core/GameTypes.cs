using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MinimalBastion.Core;

public static class GameConstants
{
    public const int LogicalWidth = 1280;
    public const int LogicalHeight = 720;
    public const int RenderScale = 2;
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
    public const float OverdriveDurationSeconds = 5f;
    public const float OverdriveCooldownSeconds = 18f;
    public const float OverdriveAttackSpeedBonus = 0.75f;
    public const float SellRatio = 0.60f;
}

public enum GameState
{
    MainMenu,
    TowerLibrary,
    Settings,
    SaveSlots,
    RunHistory,
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
    Armored
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
    TacticalSystemsDisabled
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
    bool CopyPressed = false);

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

public sealed class InputRouter
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private readonly Queue<char> _textInput = new();
    private readonly ViewportTransform _transform;
    private long _nextBackspaceRepeatTimestamp;

    public InputRouter(ViewportTransform transform) => _transform = transform;

    public void QueueTextInput(char character)
    {
        if (char.IsLetterOrDigit(character) || character is '.' or ':' or '-' or '[' or ']')
            _textInput.Enqueue(char.ToUpperInvariant(character));
    }

    public InputSnapshot Update()
    {
        var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        var logical = _transform.ScreenToLogical(new Point(mouse.X, mouse.Y));
        var controlDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var textEntered = DrainTextInput();
        if (controlDown) textEntered = "";
        if (controlDown && IsPressed(keyboard, _previousKeyboard, Keys.V))
            textEntered = ClipboardService.TryGetText() ?? "";
        var snapshot = new InputSnapshot(
            logical,
            mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            mouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.RightButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
            mouse.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouse.MiddleButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed,
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
            controlDown && IsPressed(keyboard, _previousKeyboard, Keys.C));
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        return snapshot;
    }

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
