using System.Runtime.InteropServices;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Enemies;
using MinimalBastion.Rendering;
using MinimalBastion.Simulation;
using MinimalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Diagnostics;

/// <summary>
/// Produces deterministic store artwork with the shipped renderer while its
/// helper window remains hidden and ineligible for input focus.
/// </summary>
public sealed class MarketingCaptureGame : Game
{
    private const int CoverWidth = 630;
    private const int CoverHeight = 500;
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _outputDirectory;
    private SpriteBatch _batch = null!;
    private PrimitiveRenderer _primitives = null!;
    private GameRenderer _renderer = null!;
    private SpriteFont _font = null!;
    private Texture2D _pixel = null!;
    private bool _complete;

    public MarketingCaptureGame(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640,
            PreferredBackBufferHeight = 360,
            SynchronizeWithVerticalRetrace = false,
            PreferMultiSampling = false,
            HardwareModeSwitch = false,
            IsFullScreen = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = false;
        Window.Title = "Minimal Bastion Marketing Capture (Hidden)";
        HideAndDisableActivation();
    }

    protected override void Initialize()
    {
        HideAndDisableActivation();
        base.Initialize();
        HideAndDisableActivation();
    }

    protected override void LoadContent()
    {
        HideAndDisableActivation();
        if (IsCaptureWindowForeground())
            throw new InvalidOperationException("The hidden capture window unexpectedly became the foreground window.");

        Directory.CreateDirectory(_outputDirectory);
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new PrimitiveRenderer(GraphicsDevice);
        _renderer = new GameRenderer { ReducedEffects = false };
        _font = Content.Load<SpriteFont>("Fonts/Interface");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var ui = new UIManager(_font);
        ConfigureUi(ui, content);

        var foundry = BuildActiveSession(content, "foundry_loop", "hard", "standard",
            AutoPlayerStrategy.Adaptive, 1421, 17, minimumSeconds: 4.8f);
        SelectTower(foundry, "breaker_cannon");
        CaptureGameplay("01-foundry-loop-battle.png", ui, foundry);

        var crosswind = BuildActiveSession(content, "crosswind_basin", "hard", "standard",
            AutoPlayerStrategy.Conservative, 2917, 15, minimumSeconds: 5.2f);
        SelectTower(crosswind, "frost_spire");
        CaptureGameplay("02-crosswind-basin-battle.png", ui, crosswind);

        var prism = BuildActiveSession(content, "prism_circuit", "hard", "core_six",
            AutoPlayerStrategy.AntiArmor, 4759, 18, minimumSeconds: 5.0f);
        SelectTower(prism, "ember_coil");
        CaptureGameplay("03-prism-circuit-core-six.png", ui, prism);

        var surge = BuildActiveSession(content, "relay_divide", "hard", "standard",
            AutoPlayerStrategy.Synergy, 9256, 18, minimumSeconds: 5.0f);
        SelectPoweredTower(surge);
        CaptureGameplay("04-surge-divide-nodes.png", ui, surge);

        var gauntlet = BuildActiveSession(content, "foundry_loop", "normal", "close_quarters",
            AutoPlayerStrategy.Synergy, 6841, 14, minimumSeconds: 5.4f, requireSignalCarrier: true);
        gauntlet.ConfigureCoOp(1);
        ui.SetCoOpConnectionState(true);
        ui.SetCoOpWaveReadyState(0, false);
        var remoteTower = gauntlet.Towers
            .Where(tower => !tower.IsSupport)
            .OrderByDescending(tower => tower.LevelIndex)
            .ThenBy(tower => tower.Id)
            .First();
        ui.SetRemoteCoOpCursor(new Vector2(742, 250), 2, selectedTowerId: remoteTower.Id);
        SelectTower(gauntlet, "arc_relay");
        CaptureGameplay("05-online-coop-signal-gauntlet.png", ui, gauntlet);
        ui.SetRemoteCoOpCursor(null, 0);

        ClearSelection(surge);
        CaptureCover("cover-630x500.png", surge);

        HideAndDisableActivation();
        Console.WriteLine($"Marketing capture complete: 1 cover and 5 gameplay screenshots.");
        Console.WriteLine(_outputDirectory);
        _complete = true;
        Exit();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_complete) Exit();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) => base.Draw(gameTime);

    private static void ConfigureUi(UIManager ui, GameContent content)
    {
        ui.ConfigureMaps(content.Maps.Values, content.WaveSets, content.Enemies);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        ui.ConfigureChallenges(content.Challenges.Values);
        ui.ConfigureTowerLibrary(content.Towers.Values, content.Enemies.Values, content.Tactics);
        ui.SetSaveState(false);
    }

    private static GameSession BuildActiveSession(GameContent content, string mapId, string difficultyId,
        string challengeId, AutoPlayerStrategy strategy, int seed, int waveNumber, float minimumSeconds,
        bool requireSignalCarrier = false)
    {
        if (waveNumber < 2) throw new ArgumentOutOfRangeException(nameof(waveNumber));
        var options = new SimulationOptions
        {
            MapId = mapId,
            DifficultyId = difficultyId,
            ChallengeId = challengeId,
            Strategy = strategy,
            Seed = seed,
            MaximumWave = waveNumber - 1
        };
        var execution = HeadlessSimulation.RunForDiagnostics(content, options);
        var session = execution.Session;
        if (session.IsDefeat || session.CurrentWave != waveNumber - 1)
            throw new InvalidOperationException(
                $"Could not prepare {mapId} wave {waveNumber}: {execution.Result.Result} at wave {session.CurrentWave}.");

        var player = new AutoPlayer(session, strategy, seed + 10_003, options);
        player.PrepareForWave(session);
        if (!session.StartNextWave(true))
            throw new InvalidOperationException($"Could not start {mapId} wave {waveNumber}.");

        const float step = 0.05f;
        var elapsed = 0f;
        var reaction = 0f;
        while (session.Waves.IsActive && elapsed < 11f)
        {
            session.Update(step);
            elapsed += step;
            reaction += step;
            if (reaction >= 1f)
            {
                player.ReactDuringWave(session);
                reaction = 0f;
            }

            var hasCombatArt = session.Projectiles.Projectiles.Count > 0 || session.Effects.Effects.Count >= 2;
            var hasSignalCarrier = !requireSignalCarrier || session.Enemies.Any(enemy => enemy.SignalRole != EnemySignalRole.None);
            if (elapsed >= minimumSeconds && session.Enemies.Count >= 5 && hasCombatArt && hasSignalCarrier &&
                session.AnnouncementRemaining <= 0)
                break;
        }

        if (!session.Waves.IsActive || session.Enemies.Count == 0)
            throw new InvalidOperationException($"{mapId} wave {waveNumber} ended before a gameplay frame was available.");
        return session;
    }

    private static void SelectTower(GameSession session, string preferredTowerId)
    {
        var tower = session.Towers
            .Where(candidate => candidate.Definition.Id.Equals(preferredTowerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.LevelIndex)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault() ?? session.Towers.OrderByDescending(candidate => candidate.LevelIndex).First();
        session.HandleInspectionInput(Pointer(tower.Position, leftPressed: true));
    }

    private static void SelectPoweredTower(GameSession session)
    {
        var tower = session.Towers
            .Where(candidate => session.Map.GetPowerBuff(candidate.Position).IsPowered)
            .OrderByDescending(candidate => candidate.LevelIndex)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault() ?? session.Towers.OrderByDescending(candidate => candidate.LevelIndex).First();
        session.HandleInspectionInput(Pointer(tower.Position, leftPressed: true));
    }

    private static void ClearSelection(GameSession session) =>
        session.HandleInspectionInput(default(InputSnapshot) with
        {
            MousePosition = Vector2.Zero,
            EscapePressed = true,
            TextEntered = ""
        });

    private static InputSnapshot Pointer(Vector2 position, bool leftPressed = false) =>
        default(InputSnapshot) with
        {
            MousePosition = position,
            LeftPressed = leftPressed,
            IsMouseOverLogicalCanvas = true,
            TextEntered = ""
        };

    private void CaptureGameplay(string fileName, UIManager ui, GameSession session)
    {
        ui.AdvanceVisualTime(0.16f);
        var path = Path.Combine(_outputDirectory, fileName);
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        _renderer.Draw(_batch, _primitives, session, foregroundTowerId: ui.RemoteCoOpSelectedTowerId);
        ui.Draw(_batch, _primitives, GameState.Playing, session);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        SavePng(target, path, GameConstants.RenderWidth, GameConstants.RenderHeight);
    }

    private void CaptureCover(string fileName, GameSession session)
    {
        using var battlefield = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(battlefield);
        GraphicsDevice.Clear(ColorPalette.Navy);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        _renderer.Draw(_batch, _primitives, session);
        _batch.End();

        using var cover = new RenderTarget2D(GraphicsDevice, CoverWidth, CoverHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(cover);
        GraphicsDevice.Clear(ColorPalette.Navy);
        var cropWidth = (int)MathF.Round(GameConstants.LogicalHeight * (CoverWidth / (float)CoverHeight));
        var cropX = (GameConstants.MapWidth - cropWidth) / 2;
        var source = new Rectangle(cropX * GameConstants.RenderScale, 0,
            cropWidth * GameConstants.RenderScale, GameConstants.RenderHeight);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(battlefield, new Rectangle(0, 0, CoverWidth, CoverHeight), source, Color.White);
        _batch.Draw(_pixel, new Rectangle(0, 0, CoverWidth, 128), ColorPalette.WithAlpha(ColorPalette.Navy, 236));
        _batch.Draw(_pixel, new Rectangle(0, 128, CoverWidth, 4), ColorPalette.Cyan);
        _batch.Draw(_pixel, new Rectangle(0, CoverHeight - 52, CoverWidth, 52),
            ColorPalette.WithAlpha(ColorPalette.Navy, 224));
        DrawCenteredFitted("MINIMAL BASTION", new Rectangle(28, 23, CoverWidth - 56, 62), ColorPalette.Paper, 1.25f);
        DrawCenteredFitted("TACTICAL TOWER DEFENSE", new Rectangle(40, 86, CoverWidth - 80, 28),
            ColorPalette.Gold, 0.58f);
        DrawCenteredFitted("BUILD  •  ADAPT  •  HOLD", new Rectangle(36, CoverHeight - 42, CoverWidth - 72, 30),
            ColorPalette.Paper, 0.48f);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        SavePng(cover, Path.Combine(_outputDirectory, fileName), CoverWidth, CoverHeight);
    }

    private void DrawCenteredFitted(string text, Rectangle bounds, Color color, float preferredScale)
    {
        var measured = _font.MeasureString(text);
        var scale = MathF.Min(preferredScale, MathF.Min(bounds.Width / measured.X, bounds.Height / measured.Y));
        var position = bounds.Center.ToVector2();
        _batch.DrawString(_font, text, position, color, 0, measured * 0.5f, scale, SpriteEffects.None, 0);
    }

    private static void SavePng(RenderTarget2D target, string path, int width, int height)
    {
        using var stream = File.Create(path);
        target.SaveAsPng(stream, width, height);
    }

    private void HideAndDisableActivation()
    {
        if (!OperatingSystem.IsWindows() || Window.Handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(Window.Handle, GwlExStyle);
        SetWindowLongPtr(Window.Handle, GwlExStyle,
            new IntPtr(style.ToInt64() | WsExNoActivate | WsExToolWindow));
        SetWindowPos(Window.Handle, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpHideWindow);
    }

    private bool IsCaptureWindowForeground() =>
        OperatingSystem.IsWindows() && Window.Handle != IntPtr.Zero && GetForegroundWindow() == Window.Handle;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _primitives?.Dispose();
            _pixel?.Dispose();
            _batch?.Dispose();
        }
        base.Dispose(disposing);
    }

    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpHideWindow = 0x0080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : new IntPtr(SetWindowLong32(hWnd, nIndex, value.ToInt32()));

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
