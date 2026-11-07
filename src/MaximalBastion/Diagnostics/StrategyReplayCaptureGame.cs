using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Simulation.Planning;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed record StrategyReplayOptions(string TracePath, string OutputPath, string FfmpegPath,
    double Speed = 12, int FramesPerSecond = 24, int FromWave = 1, int MaximumFrames = 0)
{
    public static StrategyReplayOptions Parse(string[] args)
    {
        if (args.Length < 3 || (args.Length - 3) % 2 != 0)
            throw new ArgumentException("Usage: --render-strategy-replay <trace.json> <output.mp4> [--speed 12] [--fps 24] [--ffmpeg ffmpeg] [--from-wave 1] [--max-frames N]");
        var options = new StrategyReplayOptions(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), "ffmpeg");
        for (var index = 3; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            options = args[index] switch
            {
                "--speed" => options with { Speed = double.Parse(value, CultureInfo.InvariantCulture) },
                "--fps" => options with { FramesPerSecond = int.Parse(value, CultureInfo.InvariantCulture) },
                "--ffmpeg" => options with { FfmpegPath = value },
                "--from-wave" => options with { FromWave = int.Parse(value, CultureInfo.InvariantCulture) },
                "--max-frames" => options with { MaximumFrames = int.Parse(value, CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException("Unknown replay option: " + args[index])
            };
        }
        options.Validate();
        return options;
    }

    internal void Validate()
    {
        if (!double.IsFinite(Speed) || Speed is < .25 or > 120 || FramesPerSecond is < 1 or > 120 ||
            FromWave is < 1 or > 1000 || MaximumFrames is < 0 or > 1_000_000)
            throw new ArgumentException("Replay speed must be 0.25–120, FPS 1–120, wave 1–1000, and frame limit 0–1000000.");
        if (!File.Exists(TracePath)) throw new FileNotFoundException("Replay trace does not exist.", TracePath);
        if (!Path.GetExtension(OutputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Replay output must end in .mp4.");
        ArgumentException.ThrowIfNullOrWhiteSpace(FfmpegPath);
        foreach (var suffix in new[] { "", ".replay.json", ".poster.png", ".final.png" })
            if (Path.GetFullPath(TracePath).Equals(Path.GetFullPath(OutputPath + suffix), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Replay output must not replace its source trace.");
    }
}

/// <summary>Exports authenticated strategy traces through the shipped renderer in a hidden window.</summary>
public sealed class StrategyReplayCaptureGame : Game
{
    private readonly StrategyReplayOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly Color[] _pixels = new Color[GameConstants.LogicalWidth * GameConstants.LogicalHeight];
    private readonly Queue<(long Tick, string Text)> _construction = new();
    private PlanningTracePlayer _player = null!;
    private SpriteBatch _batch = null!;
    private PrimitiveRenderer _primitives = null!;
    private GameRenderer _renderer = null!;
    private SpriteFont _font = null!;
    private RenderTarget2D _scene = null!;
    private RenderTarget2D _output = null!;
    private ReplayVideoEncoder _encoder = null!;
    private long _startTick;
    private long _sampleIndex;
    private int _observedActions;
    private bool _posterSaved;
    private bool _complete;
    private string _sourceHash = "";

    public StrategyReplayCaptureGame(StrategyReplayOptions options)
    {
        options.Validate();
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 360,
            SynchronizeWithVerticalRetrace = false, PreferMultiSampling = false,
            HardwareModeSwitch = false, IsFullScreen = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Window.AllowUserResizing = false;
        Window.Title = "Maximal Bastion Replay Export (Hidden)";
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
        if (OperatingSystem.IsWindows() && GetForegroundWindow() == Window.Handle)
            throw new InvalidOperationException("The hidden replay window unexpectedly became the foreground window.");
        Console.WriteLine("Authenticating the complete command trace...");
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        PlanningTrace trace;
        using (var source = new FileStream(_options.TracePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            if (source.Length > 512L * 1024 * 1024) throw new InvalidDataException("Replay trace exceeds 512 MiB.");
            trace = JsonSerializer.Deserialize<PlanningTrace>(source, PlannerStore.Json) ??
                throw new InvalidDataException("Empty replay trace.");
            source.Position = 0;
            _sourceHash = Convert.ToHexString(SHA256.HashData(source));
        }
        _player = PlanningTracePlayer.Create(content, trace, requireCurrentBuild: false);
        if (_options.FromWave > 1)
        {
            var start = trace.Actions.FirstOrDefault(action => action.Accepted && action.Wave == _options.FromWave &&
                action.Command.Type == GameCommandType.StartWave);
            if (start is null) throw new ArgumentException($"The trace has no start command for wave {_options.FromWave}.");
            _startTick = start.Tick;
        }
        var estimatedFrames = Math.Ceiling((trace.FinalTick - _startTick) * _options.FramesPerSecond / (60 * _options.Speed)) + 1;
        if (_options.MaximumFrames == 0 && estimatedFrames > 1_000_000)
            throw new ArgumentException("The replay exceeds one million frames. Increase speed or select an excerpt.");
        _player.AdvanceTo(_startTick);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_options.OutputPath))!);
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new PrimitiveRenderer(GraphicsDevice);
        _renderer = new GameRenderer { ReducedEffects = false };
        _font = Content.Load<SpriteFont>("Fonts/Interface");
        _scene = Target(GameConstants.RenderWidth, GameConstants.RenderHeight);
        _output = Target(GameConstants.LogicalWidth, GameConstants.LogicalHeight);
        _encoder = new ReplayVideoEncoder(_options.FfmpegPath, _options.OutputPath,
            GameConstants.LogicalWidth, GameConstants.LogicalHeight, _options.FramesPerSecond);
        Console.WriteLine($"Verified. Rendering {_player.Session.Map.Definition.DisplayName} at {_options.Speed:0.##}x, {_options.FramesPerSecond} fps.");
    }

    private RenderTarget2D Target(int width, int height) => new(GraphicsDevice, width, height,
        false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

    protected override void Update(GameTime gameTime) { }

    protected override void Draw(GameTime gameTime)
    {
        if (_complete) return;
        var target = Math.Min(_player.FinalTick,
            _startTick + (long)Math.Floor(_sampleIndex * _options.Speed * 60 / _options.FramesPerSecond));
        _player.AdvanceTo(target);
        ObserveConstruction();
        RenderFrame();
        _encoder.WriteFrame(MemoryMarshal.AsBytes(_pixels.AsSpan()));
        _sampleIndex++;
        if (!_posterSaved && _player.Session.CurrentWave >= 21 && _player.Session.Enemies.Count >= 12)
        {
            SavePng(".poster.png");
            _posterSaved = true;
        }
        if (_sampleIndex % (_options.FramesPerSecond * 10) == 0)
            Console.WriteLine($"{_player.Progress:P0} | wave {_player.Session.CurrentWave} | {_sampleIndex} frames | {_elapsed.Elapsed.TotalSeconds:0}s elapsed");
        if (target == _player.FinalTick || (_options.MaximumFrames > 0 && _sampleIndex >= _options.MaximumFrames))
            Finish(target);
    }

    private void RenderFrame()
    {
        GraphicsDevice.SetRenderTarget(_scene);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        _renderer.Draw(_batch, _primitives, _player.Session);
        DrawReplayHud();
        _batch.End();
        GraphicsDevice.SetRenderTarget(_output);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(_scene, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        _output.GetData(_pixels);
    }

    private void DrawReplayHud()
    {
        var session = _player.Session;
        _primitives.FillRect(_batch, new Rectangle(0, 0, 960, 52), ColorPalette.Panel);
        _primitives.FillRect(_batch, new Rectangle(960, 0, 320, 720), ColorPalette.Panel);
        _primitives.FillRect(_batch, new Rectangle(960, 0, 2, 720), ColorPalette.Divider);
        Text("MAXIMAL BASTION", 22, 15, ColorPalette.Paper, .85f, 300);
        Text($"STRATEGY REPLAY   /   {_options.Speed:0.##}x", 628, 15, ColorPalette.Cyan, .8f, 310);
        Text(session.Map.Definition.DisplayName, 984, 20, ColorPalette.Paper, 1f, 270);
        Text($"{session.Difficulty.DisplayName} / {session.Challenge.DisplayName}", 984, 68, ColorPalette.Muted, .62f, 270);
        var finished = _player.Tick == _player.FinalTick;
        var phase = finished ? (_player.Trace.Defeat ? "DEFEAT" :
            _player.Trace.CampaignVictory ? "CAMPAIGN CLEARED" :
            _player.Trace.Scenario.Endless && _player.Trace.HorizonSurvived ? "ENDLESS CLEARED" : "REPLAY COMPLETE") :
            session.Waves.IsActive ? "WAVE IN PROGRESS" : "PREPARING DEFENSE";
        Text(phase, 984, 116, finished && !_player.Trace.Defeat ? ColorPalette.GreenText : ColorPalette.Cyan, .75f, 270);
        Text($"WAVE {session.CurrentWave:00}", 984, 150, ColorPalette.Paper, 1.2f, 270);
        Text(session.CurrentWave > 30 ? "ENDLESS" : "CAMPAIGN / 30 WAVES", 984, 200, ColorPalette.Muted, .65f, 270);
        Rule(235);
        Stat("CREDITS", session.Economy.Credits.ToString("N0", CultureInfo.InvariantCulture), 246, ColorPalette.Gold);
        Stat("LIVES", session.Economy.Lives.ToString(), 278, ColorPalette.GreenText);
        Stat("TOWERS", session.Towers.Count.ToString(), 310, ColorPalette.Paper);
        Stat("APEX", session.Towers.Count(tower => tower.IsApex).ToString(), 342, ColorPalette.Violet);
        Stat("PULSE PLATES", session.Statistics.EmergencyDeployments.ToString(), 374, ColorPalette.Cyan);
        Stat("PROTOCOLS", session.Statistics.ProtocolActivations.ToString(), 406, ColorPalette.Cyan);
        Rule(446);
        Text("LATEST CONSTRUCTION", 984, 467, ColorPalette.Muted, .65f, 270);
        var row = 498;
        foreach (var entry in _construction.Reverse())
        {
            Text($"{Clock(entry.Tick)}  {entry.Text}", 984, row, ColorPalette.Paper, .66f, 270);
            row += 27;
        }
        Text($"RUN TIME  {Clock(_player.Tick)} / {Clock(_player.FinalTick)}", 984, 658, ColorPalette.Muted, .62f, 270);
        _primitives.FillRect(_batch, new Rectangle(984, 693, 270, 4), ColorPalette.Divider);
        _primitives.FillRect(_batch, new Rectangle(984, 693, (int)(270 * _player.Progress), 4), ColorPalette.Cyan);
    }

    private void Rule(int y) => _primitives.FillRect(_batch, new Rectangle(984, y, 270, 1), ColorPalette.Divider);
    private void Stat(string label, string value, int y, Color color)
    {
        Text(label, 984, y, ColorPalette.Muted, .65f, 166);
        Text(value, 1160, y - 2, color, .85f, 94);
    }
    private void Text(string text, int x, int y, Color color, float scale, int width)
    {
        scale = Math.Min(scale, width / Math.Max(1, _font.MeasureString(text).X));
        _batch.DrawString(_font, text, new Vector2(x, y), color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }
    private static string Clock(long tick) => $"{tick / 3600:00}:{tick / 60 % 60:00}";

    private void ObserveConstruction()
    {
        while (_observedActions < _player.AppliedActions)
        {
            var action = _player.Trace.Actions[_observedActions++];
            var command = action.Command;
            var tower = _player.Session.Towers.FirstOrDefault(item => item.Id == command.EntityId);
            var name = tower?.Definition.DisplayName ?? "Tower";
            var description = command.Type switch
            {
                GameCommandType.PlaceTower => "Built " + (_player.Session.Content.Towers.GetValueOrDefault(command.TowerDefinitionId)?.DisplayName ?? command.TowerDefinitionId),
                GameCommandType.UpgradeTower => "Upgraded " + name,
                GameCommandType.ChooseDoctrine => "Doctrine: " + name,
                GameCommandType.SpecializeTower => "Specialized " + name,
                GameCommandType.SellTower => "Sold " + name,
                GameCommandType.PlaceGenerator => "Built Charge Forge",
                GameCommandType.UpgradeGenerator => "Upgraded Charge Forge",
                GameCommandType.SellGenerator => "Sold Charge Forge",
                _ => null
            };
            if (description is null) continue;
            _construction.Enqueue((action.Tick, description));
            while (_construction.Count > 5) _construction.Dequeue();
        }
    }

    private void Finish(long renderedEndTick)
    {
        SavePng(".final.png");
        if (!_posterSaved) SavePng(".poster.png");
        // A short hold keeps the final board visible without changing simulation time.
        var holdFrames = _options.MaximumFrames == 0 ? _options.FramesPerSecond * 2 :
            Math.Min(_options.FramesPerSecond * 2, Math.Max(0, _options.MaximumFrames - (int)_encoder.FramesWritten));
        for (var index = 0; index < holdFrames; index++) _encoder.WriteFrame(MemoryMarshal.AsBytes(_pixels.AsSpan()));
        _player.AdvanceTo(_player.FinalTick);
        var verification = _player.VerifyFinalState();
        _encoder.Complete();
        PlannerStore.Write(_options.OutputPath + ".replay.json", new
        {
            SchemaVersion = 1, TracePath = Path.GetFullPath(_options.TracePath), TraceSha256 = _sourceHash,
            SourceBuildHash = _player.Trace.BuildHash, RenderBuildHash = PlanningFingerprint.Build(),
            _player.Trace.ContentHash, _player.Trace.Scenario, _options.Speed, _options.FramesPerSecond,
            Width = GameConstants.LogicalWidth, Height = GameConstants.LogicalHeight,
            StartTick = _startTick, EndTick = renderedEndTick, _player.FinalTick,
            CompleteTraceRendered = _startTick == 0 && renderedEndTick == _player.FinalTick,
            _encoder.FramesWritten, VideoSeconds = (double)_encoder.FramesWritten / _options.FramesPerSecond,
            ExportSeconds = _elapsed.Elapsed.TotalSeconds, Verification = verification
        });
        _complete = true;
        Console.WriteLine($"Complete: {_encoder.OutputPath} ({_encoder.FramesWritten} frames, {_elapsed.Elapsed.TotalSeconds:0.0}s export). Final replay verified.");
        Exit();
    }

    private void SavePng(string suffix)
    {
        using var stream = File.Create(_options.OutputPath + suffix);
        _output.SaveAsPng(stream, GameConstants.LogicalWidth, GameConstants.LogicalHeight);
    }

    private void HideAndDisableActivation()
    {
        if (!OperatingSystem.IsWindows() || Window.Handle == IntPtr.Zero) return;
        var style = IntPtr.Size == 8 ? GetWindowLongPtr64(Window.Handle, -20).ToInt64() : GetWindowLong32(Window.Handle, -20);
        var hiddenStyle = style | 0x08000000L | 0x80L;
        if (IntPtr.Size == 8) SetWindowLongPtr64(Window.Handle, -20, new IntPtr(hiddenStyle));
        else SetWindowLong32(Window.Handle, -20, unchecked((int)hiddenStyle));
        SetWindowPos(Window.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x4 | 0x10 | 0x80);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _encoder?.Dispose();
            _scene?.Dispose();
            _output?.Dispose();
            _primitives?.Dispose();
            _batch?.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
