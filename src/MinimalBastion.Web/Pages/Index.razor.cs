using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using MinimalBastion.Core;

namespace MinimalBastion.Web.Pages;

public partial class Index
{
    private Game? _game;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (!firstRender) return;

        var browser = (IJSInProcessRuntime)JsRuntime;
        var files = browser.Invoke<Dictionary<string, string>>("minimalBastion.storage.readAll");
        PlatformServices.InitializePersistentFiles(
            files,
            (path, contents) => browser.InvokeVoid("minimalBastion.storage.write", path, contents),
            path => browser.InvokeVoid("minimalBastion.storage.remove", path));
        PlatformServices.ClipboardReader = () => browser.Invoke<string?>("minimalBastion.clipboard.read");
        PlatformServices.ClipboardWriter = text => browser.Invoke<bool>("minimalBastion.clipboard.write", text);
        PlatformServices.FullscreenSetter = enabled => browser.InvokeVoid("minimalBastion.setFullscreen", enabled);
        PlatformServices.InputFocusReader = () => browser.Invoke<bool>("minimalBastion.hasInputFocus");
        PlatformServices.PointerStateReader = () => browser.Invoke<PlatformPointerState>("minimalBastion.pointer.read");
        _ = JsRuntime.InvokeVoidAsync("minimalBastion.start", DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void Tick()
    {
        if (_game is null)
        {
            _game = new Game1();
            _game.Run();
        }

        _game.Tick();
    }
}
