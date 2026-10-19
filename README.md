# OblivionMP Mod Template

![version](https://img.shields.io/badge/version-1.0.0--rc.1-green)

For other versions, check the list of [tags](https://github.com/readycodeio/oblivionmp-mod-template/tags).

A template for developing an OblivionMP mod with the OblivionMP SDK.

Refer to the [OblivionMP SDK documentation](https://docs.ready.mp) for details on how to use the SDK.

## Requirements

* [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or later
* If you are using Visual Studio, you need version 2026 or later

## Repository structure

An OblivionMP mod is usually made of two halves that share a common core:

- `DayInfo.Common/`: shared by both halves. Mixins and the messages the two halves send each other
  (`[RpcContracts]`) go here, so both agree on them. Empty for now, since the example sends none.
  Its DLL ships alongside both halves.
- `DayInfo.Client/`: the client half, loaded by the game. Entry point (`Mod.cs`) and a service
  that drives a web page (`WorldHud.cs`, with `WorldHudPage.cs`, `Weathers.cs`,
  `TamrielCalendar.cs`, `Locations.cs` and `CursorState.cs`).
- `DayInfo.Server/`: the server half, loaded by the relay server. Entry point (`Mod.cs`).
- `Content/manifest.json`: mod metadata (id, name, version, dependencies).
- `Content/ui/`: the web pages the client half shows in game. `fonts/` holds Kingthings Petrock and
  Petrock Light, the font OblivionMP's own widgets use. Kevin King's license
  (`fonts/kingthingsEULA.txt`) allows any use as long as the font files are left unchanged.
- `Content/data/locations.json`: place names for the HUD, built from the game's plugins by
  `Tools/ExtractLocations.cs` (see below).

The projects compile against the OblivionMP SDK packages from NuGet, one per half:
`ReadyM.SDK.Oblivion.Common` for the shared project, `ReadyM.SDK.Oblivion.Client` for the game and
`ReadyM.SDK.Oblivion.Server` for the relay. The game and the relay provide those assemblies at
runtime, so they never end up in your mod folder.

The example is a web page in the top-right corner showing the in-game time, weather, place and
date: the SDK replicates time and weather on the player's `Area`, and `WorldHud` reads them and
sends them to the page whenever they change. The area only carries the number of days passed, so
`TamrielCalendar` counts the date from the day the game starts (Morndas, 27th of Last Seed, 3E 433).

The SDK says where a player is (the area id and the exterior cell) but not what the place is
called, so `Locations` looks it up in `Content/data/locations.json`: an interior by its cell, an
exterior by the most specific map region its cell belongs to, and otherwise by its worldspace. To
rebuild that file, for example after a game update, point the extractor at the game's data folder:

```powershell
dotnet run Tools/ExtractLocations.cs -- "<game>\OblivionRemastered\Content\Dev\ObvData\Data" Content/data/locations.json
``` Use it as a starting point and replace it with your own logic.

## Web UI

The client half can show HTML pages in game through CEF. `Mod.cs` serves `Content/ui/` at
`readym://DayInfo/`, and `WorldHud.cs` creates a viewport, places it on screen and loads
`index.html` into it.

Messages between the mod and a page are declared as methods on a partial class
(`WorldHudPage.cs`):

- `[SendToCef] partial void SendWorldState(BrowserHandle page, ...)` has no body. Call it from C#
  and the page receives it with `readym.on('WorldState', (hour, minute, weather) => ...)`.
- `[ReceiveFromCef] void OnReady(BrowserHandle page, ...)` runs on the game thread when the page
  calls `readym.sendReady(...)`.

That class is not a `[Service]`. CEF only finds it once `Mod.Init` registers it as an
`ICefRpcHandler`; without that, every send logs "was called before its handlers were bound".
A viewport also starts hidden, so `WorldHud` calls `SetVisible` after assigning the browser. And the
game only has its web overlay once gameplay starts, so `WorldHud` creates the page on the first
state it has rather than in `Start`: a page painted before that stays invisible until it changes.

A page only hears messages sent after it has registered its listeners, so the example page calls
`readym.sendReady()` on load and the mod answers with the current state.

To work on a page without the game, open it in a browser: there is no `readym` object there, and
the example shows a sample instead. Click the weather tile to step through every icon, by day and
by night, or open `index.html?icon=rain&night=1` to start on one.

## Getting started

1. Clone this repository to your local machine.
2. Open `DayInfo.sln` in your preferred C# IDE (e.g. JetBrains Rider, Visual Studio) and build it.
   The SDK packages restore from NuGet.
3. Rename the mod so it does not collide with other mods on a server:

   ```powershell
   .\RenameMod.ps1 Acme.TreasureHunt
   ```

4. Edit the code to build your own mod. Keep mixins and RPC contracts in the Common project so both
   halves agree on them.

> **Note**: both halves have to agree on every mixin and every message. Ship them together as one
> mod folder, which is what the packaging script produces.

## Packaging the mod

1. Edit `Content/manifest.json` with your mod's id, name, version and description. `uniqueId` has to
   be unique across every mod on the server, and dependency ids are matched exactly, case included.
2. Edit `ModFiles.ps1` if your mod ships extra files or you renamed the projects. The `ui` folder
   is already listed for the client half.
3. Run the packaging script with the `Release` argument:

   ```powershell
   .\MakeModFolder.ps1 Release
   ```

4. `Output/mods/` will contain one folder for your mod:

   ```
   mods/DayInfo/manifest.json
   mods/DayInfo/client/     <- handed out to connecting players, ui/ included
   mods/DayInfo/server/     <- stays on the server
   ```

5. Copy that folder into your server's `mods/` directory and restart the server. Connecting clients
   download the client half automatically; the server half never leaves the server.

Pass `-NoExplorer` to skip opening the output folder, for example when calling the script from CI.

## Debugging

Run the packaging script with the `Debug` argument to include `.pdb` symbol files:

```powershell
.\MakeModFolder.ps1 Debug
```

If you downloaded this template as a ZIP rather than cloning it, Windows marks the scripts as remote
and PowerShell refuses to load them. Clear the mark once:

```powershell
Get-ChildItem -Recurse | Unblock-File
```
