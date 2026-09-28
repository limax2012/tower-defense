using System.Runtime.InteropServices;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using MaximalBastion.Simulation;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

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
    private SpriteFont _display = null!;
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
        Window.Title = "Maximal Bastion Marketing Capture (Hidden)";
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
        _display = Content.Load<SpriteFont>(UiTypography.DisplayAsset);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var ui = new UIManager(_font, Content.Load<SpriteFont>(UiTypography.DisplayAsset));
        ConfigureUi(ui, content);

        var foundry = BuildActiveSession(content, "foundry_loop", "normal", "standard",
            AutoPlayerStrategy.Experienced, 1421, 17, minimumSeconds: 12f);
        ClearSelection(foundry);
        CaptureGameplay("01-cinderworks-crossfire.png", ui, foundry);
        ClearSelection(foundry);
        CaptureCover("cover-630x500.png", foundry);
        CaptureBattlefield("06-cinderworks-action-detail.png", foundry, new Rectangle(40, 55, 880, 495));

        var crosswind = BuildActiveSession(content, "crosswind_basin", "normal", "standard",
            AutoPlayerStrategy.Experienced, 2917, 19, minimumSeconds: 12f);
        ClearSelection(crosswind);
        CaptureGameplay("02-rainline-heights-battle.png", ui, crosswind);

        var prism = BuildActiveSession(content, "prism_circuit", "normal", "core_six",
            AutoPlayerStrategy.Experienced, 4759, 18, minimumSeconds: 12f);
        SelectTower(prism, "ember_coil");
        CaptureGameplay("03-prism-nullspace-core-six.png", ui, prism);

        var surge = BuildActiveSession(content, "relay_divide", "normal", "standard",
            AutoPlayerStrategy.Experienced, 9256, 19, minimumSeconds: 12f);
        SelectTower(surge, "prism_beam");
        CaptureGameplay("04-helix-reactor-defense.png", ui, surge);
        ClearSelection(surge);
        CaptureBattlefield("07-helix-reactor-action-detail.png", surge, new Rectangle(15, 130, 900, 506));


        var gauntlet = BuildActiveSession(content, "foundry_loop", "normal", "close_quarters",
            AutoPlayerStrategy.Experienced, 6841, 14, minimumSeconds: 12f, requireSignalCarrier: true);
        SelectTower(gauntlet, "arc_relay");
        CaptureGameplay("05-cinderworks-signal-gauntlet.png", ui, gauntlet);



        HideAndDisableActivation();
        Console.WriteLine($"Marketing capture complete: 1 cover, 5 gameplay screenshots, and 2 battlefield details.");
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
        ui.ConfigureMaps(content.Maps.Values);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        ui.ConfigureChallenges(content.Challenges.Values);
        ui.ConfigureTowerNames(content.Towers.Values);
        ui.SetSaveState(false);
    }

    private static GameSession BuildActiveSession(GameContent content, string mapId, string difficultyId,
        string challengeId, AutoPlayerStrategy strategy, int seed, int waveNumber, float minimumSeconds,
        bool requireSignalCarrier = false)
    {
        if (waveNumber < 2) throw new ArgumentOutOfRangeException(nameof(waveNumber));
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var candidateSeed = seed + attempt * 7919;
            var options = new SimulationOptions
            {
                MapId = mapId, DifficultyId = difficultyId, ChallengeId = challengeId,
                Strategy = strategy, Seed = candidateSeed, MaximumWave = waveNumber - 1
            };
            var execution = HeadlessSimulation.RunForDiagnostics(content, options);
            if (execution.Session.IsDefeat || execution.Session.CurrentWave != waveNumber - 1) continue;
            // Replay the selected combat tick from the same checkpoint to preserve live effects.
            var checkpoint = execution.Session.CaptureSaveGame();
            var session = GameSession.RestoreSaveGame(content, checkpoint);
            var player = Prepare(session);
            const float step = 1f / 60;
            var bestTick = -1;
            var bestScore = float.MinValue;
            for (var tick = 0; tick < 55 * 60 && session.Waves.IsActive && !session.IsDefeat; tick++)
            {
                Advance(session, player, tick);
                if (tick * step < minimumSeconds || session.AnnouncementRemaining > 0) continue;
                var enemies = session.Enemies.Where(enemy => !enemy.IsDead).ToArray();
                var middle = enemies.Count(enemy => enemy.PathProgress is >= .20f and <= .85f);
                var central = enemies.Count(enemy => enemy.Position.X is >= 160 and <= 800 &&
                    enemy.Position.Y is >= 150 and <= 630);
                var attacking = session.Towers.Count(tower => !tower.IsSupport && enemies.Any(enemy =>
                    Vector2.DistanceSquared(tower.Position, enemy.Position) <=
                    session.GetEffectiveRange(tower) * session.GetEffectiveRange(tower)));
                if (middle < 4 || central < 4 || attacking < Math.Min(5, session.Towers.Count) ||
                    (requireSignalCarrier && !enemies.Any(enemy => enemy.SignalRole != EnemySignalRole.None))) continue;
                var beams = session.Effects.Effects.Count(effect => effect.Kind == MaximalBastion.Effects.EffectKind.Beam);
                var firing = session.Towers.Count(tower => tower.RecoilAnimationRemaining > .025f);
                if (firing < 6 || session.Projectiles.Projectiles.Count < 10) continue;
                var score = firing * 18 + attacking * 2 + Math.Min(enemies.Length, 28) +
                    Math.Min(session.Projectiles.Projectiles.Count, 28) * 5 + Math.Min(beams, 8) * 9 +
                    Math.Min(session.Effects.Effects.Count, 24) - Math.Max(0, enemies.Length - 45) * 2;
                if (score <= bestScore) continue;
                bestScore = score;
                bestTick = tick;
            }
            if (bestTick < 0) continue;
            session = GameSession.RestoreSaveGame(content, checkpoint);
            player = Prepare(session);
            for (var tick = 0; tick <= bestTick; tick++) Advance(session, player, tick);
            Console.WriteLine($"{mapId} W{waveNumber}: seed {candidateSeed}, {session.Towers.Count} towers, " +
                $"{session.Enemies.Count} enemies, {session.Projectiles.Projectiles.Count} projectiles, " +
                $"{session.Towers.Count(tower => tower.RecoilAnimationRemaining > .025f)} firing, {session.Effects.Effects.Count} effects at {(bestTick + 1) * step:0.00}s; action score {bestScore}.");
            return session;

            AutoPlayer Prepare(GameSession run)
            {
                var auto = new AutoPlayer(run, strategy, candidateSeed + 10_003, options);
                auto.PrepareForWave(run);
                if (!run.StartNextWave(true)) throw new InvalidOperationException($"Cannot start {mapId} W{waveNumber}.");
                return auto;
            }

            static void Advance(GameSession run, AutoPlayer auto, int tick)
            {
                run.Update(step);
                if (tick % 60 == 59) auto.ReactDuringWave(run);
            }
        }
        throw new InvalidOperationException($"No active {mapId} W{waveNumber} composition found in six seeded runs.");
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

    private static void ClearSelection(GameSession session) =>
        session.HandleInspectionInput(default(InputSnapshot) with
        {
            MousePosition = Vector2.Zero,
            RightPressed = true,
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

    private void CaptureBattlefield(string fileName, GameSession session, Rectangle crop)
    {
        using var battlefield = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight);
        GraphicsDevice.SetRenderTarget(battlefield);
        GraphicsDevice.Clear(ColorPalette.Navy);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        _renderer.Draw(_batch, _primitives, session);
        _batch.End();
        using var detail = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight);
        GraphicsDevice.SetRenderTarget(detail);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(battlefield, new Rectangle(0, 0, detail.Width, detail.Height),
            new Rectangle(crop.X * GameConstants.RenderScale, crop.Y * GameConstants.RenderScale,
                crop.Width * GameConstants.RenderScale, crop.Height * GameConstants.RenderScale), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        SavePng(detail, Path.Combine(_outputDirectory, fileName), detail.Width, detail.Height);
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
        var source = new Rectangle(40 * GameConstants.RenderScale, 55 * GameConstants.RenderScale,
            880 * GameConstants.RenderScale, 525 * GameConstants.RenderScale);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(battlefield, new Rectangle(0, 124, CoverWidth, CoverHeight - 124), source, Color.White);
        _batch.Draw(_pixel, new Rectangle(0, 0, CoverWidth, 124), ColorPalette.Panel);
        _batch.Draw(_pixel, new Rectangle(22, 122, CoverWidth - 44, 2), ColorPalette.Metal);
        DrawCenteredFitted("MAXIMAL", new Rectangle(145, 25, 340, 40), ColorPalette.Cyan, .30f, _display);
        DrawCenteredFitted("BASTION", new Rectangle(135, 55, 360, 66), ColorPalette.Paper, .54f, _display);
        _batch.End();
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(1.45f));
        BastionBrandMark.Draw(_batch, _primitives, new Vector2(65, 40), 0);
        BastionBrandMark.Draw(_batch, _primitives, new Vector2(CoverWidth / 1.45f - 65, 40), 0);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        SavePng(cover, Path.Combine(_outputDirectory, fileName), CoverWidth, CoverHeight);
    }

    private void DrawCenteredFitted(string text, Rectangle bounds, Color color, float preferredScale, SpriteFont font)
    {
        var measured = font.MeasureString(text);
        var scale = MathF.Min(preferredScale, MathF.Min(bounds.Width / measured.X, bounds.Height / measured.Y));
        var position = bounds.Center.ToVector2();
        _batch.DrawString(font, text, position, color, 0, measured * 0.5f, scale, SpriteEffects.None, 0);
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
