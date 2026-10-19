using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace DayInfo.Client;

// The mod's entry point, one per half. Classes marked [Service] or [RpcHandlersFor] are found and
// created on their own, so this only needs to exist. Ask for what you need in the constructor.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, CefHost cef, ILogger logger)
{
    // Runs once when the mod loads, before any service starts.
    private void Init()
    {
        // CEF finds page message classes as ICefRpcHandler, so register WorldHudPage under it, as the
        // same instance WorldHud is handed.
        services.RegisterSingleton<WorldHudPage>();
        services.RegisterSingleton<ICefRpcHandler, WorldHudPage>(services.Resolve<WorldHudPage>());

        // The place names the HUD shows, which only exist as a file this mod ships.
        services.RegisterSingleton(Locations.Load(Path.Combine(ModDirectory, "data", "locations.json")));

        // Serve the web pages this mod ships at readym://DayInfo/.
        cef.RegisterFolder(WorldHud.PageHost, Path.Combine(ModDirectory, "ui"));

        logger.LogInformation("Example mod loaded. The time, place, date and weather show in the top-right corner.");
    }
}
