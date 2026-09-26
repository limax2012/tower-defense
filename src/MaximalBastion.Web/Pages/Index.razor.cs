using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using MaximalBastion.Core;

namespace MaximalBastion.Web.Pages;

public partial class Index
{
    private Game? _game;
    private bool _mobile;
    private readonly PlatformBrowserDisplayState _browserDisplayState = new();

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (!firstRender) return;

        var browser = (IJSInProcessRuntime)JsRuntime;
        _mobile = browser.Invoke<bool>("maximalBastion.mobile.enabled");
        var files = browser.Invoke<Dictionary<string, string>>("maximalBastion.storage.readAll");
        PlatformServices.InitializePersistentFiles(
            files,
            (path, contents) => browser.InvokeVoid("maximalBastion.storage.write", path, contents),
            path => browser.InvokeVoid("maximalBastion.storage.remove", path));
        PlatformServices.ClipboardReader = () => browser.Invoke<string?>("maximalBastion.clipboard.read");
        PlatformServices.ClipboardWriter = text => browser.Invoke<bool>("maximalBastion.clipboard.write", text);
        PlatformServices.FullscreenSetter = enabled => browser.InvokeVoid("maximalBastion.setFullscreen", enabled);
        PlatformServices.RuntimeStageSetter = stage => browser.InvokeVoid("maximalBastion.setRuntimeStage", stage);
        PlatformServices.LoadingTransitionSetter = (title, status) =>
            browser.InvokeVoid("maximalBastionLoading.begin", title, status);
        PlatformServices.LoadingTransitionCompleter = () => browser.InvokeVoid("maximalBastionLoading.complete");
        PlatformServices.ImmediateOneShotAudioParametersSetter = enabled =>
            browser.InvokeVoid("maximalBastion.audio.setImmediateParameters", enabled);
        PlatformServices.InputFocusReader = () => browser.Invoke<bool>("maximalBastion.hasInputFocus");
        PlatformServices.PointerStateReader = () => browser.Invoke<PlatformPointerState>("maximalBastion.pointer.read");
        PlatformServices.BrowserDisplayStateReader = () => _browserDisplayState;
        _ = JsRuntime.InvokeVoidAsync("maximalBastion.start", DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void SetBrowserDisplayState(bool active, bool pending, int backBufferWidth, int backBufferHeight)
    {
        _browserDisplayState.Active = active;
        _browserDisplayState.Pending = pending;
        _browserDisplayState.BackBufferWidth = backBufferWidth;
        _browserDisplayState.BackBufferHeight = backBufferHeight;
        _browserDisplayState.Revision++;
    }

    [JSInvokable]
    public void Tick()
    {
        if (_game is null)
        {
            _game = _mobile ? new MobileGame() : new Game1();
            _game.Run();
        }

        _game.Tick();
    }

    [JSInvokable]
    public string MobileRequest(string request) => _game is MobileGame game
        ? game.Controller.HandleRequest(request)
        : throw new InvalidOperationException("The mobile engine is not ready.");

    [JSInvokable]
    public string MobileState() => _game is MobileGame game
        ? game.Controller.GetStateJson()
        : throw new InvalidOperationException("The mobile engine is not ready.");

    [JSInvokable]
    public void SetMobileSuspended(bool suspended)
    {
        if (_game is MobileGame game) game.Controller.SetSuspended(suspended);
    }
}
