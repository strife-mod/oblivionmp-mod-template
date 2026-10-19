#!powershell.exe -ExecutionPolicy Bypass -File

# Edit these lists to match your mod. MakeModFolder.ps1 uses them to build
#
#   Output/mods/<modName>/manifest.json
#   Output/mods/<modName>/client/...
#   Output/mods/<modName>/server/...
#
# The server hands the client folder to connecting players and keeps the server folder to itself.

# Folder name of the packaged mod. Conventionally the uniqueId from manifest.json.
$modName = "DayInfo"

# The projects the two halves build from.
$clientProject = "DayInfo.Client"
$serverProject = "DayInfo.Server"

# --- Client half, from <clientProject>/bin/<Configuration>/net10.0 ---
$clientBuildFiles = @(
    "DayInfo.Client.dll",
    "DayInfo.Common.dll"   # shared assembly both halves need
)

# --- Server half, from <serverProject>/bin/<Configuration>/net10.0 ---
$serverBuildFiles = @(
    "DayInfo.Server.dll",
    "DayInfo.Common.dll"
)

# Copied from "Content" into the mod root, alongside the two halves.
$manifestFiles = @(
    "manifest.json"
)

# Copied from "Content" into client/ and server/. Add any non-code files your mod ships.
$clientContentFiles = @(
    "ui",    # the web pages, served to CEF by Mod.cs
    "data"   # place names, built by Tools/ExtractLocations.cs
)

$serverContentFiles = @(
    # "config.json"
)

# Added only in Debug builds.
$clientDebugBuildFiles = @(
    "DayInfo.Client.pdb",
    "DayInfo.Common.pdb"
)

$serverDebugBuildFiles = @(
    "DayInfo.Server.pdb",
    "DayInfo.Common.pdb"
)
