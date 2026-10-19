using ReadyM.Cef;
using ReadyM.Cef.Rpc;

namespace DayInfo.Client;

// The messages between the mod and the HUD page. A [SendToCef] method has no body: the generator
// writes it, and the page listens with readym.on('<Name>', fn). A [ReceiveFromCef] method runs on
// the game thread when the page calls readym.send<Name>(...). The first parameter is always the page.
//
// Not a [Service]: Mod.Init registers it as an ICefRpcHandler, which is how CEF finds it.
public sealed partial class WorldHudPage
{
    // Set by WorldHud. Called when the page has loaded and is listening.
    public Action? Ready { get; set; }

    [SendToCef]
    public partial void SendWorldState(BrowserHandle page, int hour, int minute, string location, string date,
        string weatherIcon);

    [ReceiveFromCef]
    private void OnReady(BrowserHandle page) => Ready?.Invoke();
}
