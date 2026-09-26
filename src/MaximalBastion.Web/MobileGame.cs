using MaximalBastion.Audio;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Mobile;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace MaximalBastion.Web;

/// <summary>Battlefield-only host for the Flutter interface; simulation and rendering are shared with desktop.</summary>
public sealed class MobileGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameRenderer _renderer = new();
    private SpriteBatch _batch = null!;
    private PrimitiveRenderer _primitives = null!;
    private RasterizerState _scissor = null!;
    private AudioManager? _audio;
    private int _displayRevision = -1;
    public MobileSessionController Controller { get; private set; } = null!;

    public MobileGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = GameConstants.MapWidth,
            PreferredBackBufferHeight = GameConstants.LogicalHeight,
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = false,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsFixedTimeStep = false;
        IsMouseVisible = false;
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new PrimitiveRenderer(GraphicsDevice);
        _scissor = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        Controller = new MobileSessionController(content, PlatformServices.PersistentRootDirectory);
        SoundEffect? menu = null;
        try { menu = Content.Load<SoundEffect>("Audio/MainMenuLoop"); }
        catch { }
        var songs = new List<Song>();
        foreach (var asset in Game1.GameplayMusicAssets)
        {
            try { songs.Add(Content.Load<Song>(asset)); }
            catch { }
        }
        _audio = AudioManager.TryCreate(menu, songs);
        Controller.SessionChanged += session =>
        {
            if (session is null) _audio?.Detach();
            else _audio?.Attach(session);
        };
    }

    protected override void Update(GameTime gameTime)
    {
        var display = PlatformServices.BrowserDisplayStateReader?.Invoke();
        if (display is not null && display.Revision != _displayRevision)
        {
            _displayRevision = display.Revision;
            if (display.BackBufferWidth > 0 && display.BackBufferHeight > 0)
            {
                _graphics.PreferredBackBufferWidth = display.BackBufferWidth;
                _graphics.PreferredBackBufferHeight = display.BackBufferHeight;
                _graphics.ApplyChanges();
            }
        }
        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Controller.Update(elapsed);
        _renderer.ReducedEffects = Controller.Settings.ReducedEffects;
        if (_audio is not null)
        {
            _audio.SfxVolume = Controller.Suspended ? 0 : Controller.Settings.SfxVolume;
            _audio.MusicVolume = Controller.Suspended ? 0 : Controller.Settings.MusicVolume;
            _audio.Update(Math.Min(elapsed, 0.1f));
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(null);
        var display = PlatformServices.BrowserDisplayStateReader?.Invoke();
        var width = Math.Max(1, display?.BackBufferWidth ?? _graphics.PreferredBackBufferWidth);
        var height = Math.Max(1, display?.BackBufferHeight ?? _graphics.PreferredBackBufferHeight);
        GraphicsDevice.Clear(Controller.Session?.Map.Definition.Background.BaseColor ?? ColorPalette.Navy);
        var scale = MathF.Min(width / (float)GameConstants.MapWidth, height / (float)GameConstants.LogicalHeight);
        var left = (width - GameConstants.MapWidth * scale) / 2;
        var top = (height - GameConstants.LogicalHeight * scale) / 2;
        GraphicsDevice.ScissorRectangle = new Rectangle((int)left, (int)top,
            Math.Max(1, (int)(GameConstants.MapWidth * scale)), Math.Max(1, (int)(GameConstants.LogicalHeight * scale)));
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, _scissor, null,
            Matrix.CreateScale(scale) * Matrix.CreateTranslation(left, top, 0));
        if (Controller.Session is { } session)
            _renderer.Draw(_batch, _primitives, session, showTransientCombat: !Controller.InspectingHistory);
        _batch.End();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        Controller?.SetSuspended(true);
        _audio?.Dispose();
        _primitives?.Dispose();
        _batch?.Dispose();
        _scissor?.Dispose();
        base.UnloadContent();
    }
}
