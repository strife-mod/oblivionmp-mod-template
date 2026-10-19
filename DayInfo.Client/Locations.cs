using System.Globalization;
using System.Text.Json;
using OblivionMp.Sdk.Common.Archetypes;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace DayInfo.Client;

// Place names, read from data/locations.json. Tools/ExtractLocations.cs builds that file from the
// game's plugins, since the SDK knows where a player is but not what the place is called.
//
// An area is named after what it is: "L_PersistentDungeon-<cell form id>" for an interior, and
// "L_<worldspace editor id>" for an exterior, where the player's cell picks out a region or a named
// spot and the worldspace's own name is the fallback.
public sealed class Locations
{
    private const string InteriorPrefix = "L_PersistentDungeon-";
    private const string ExteriorPrefix = "L_";

    private readonly string[] _names;
    private readonly Dictionary<string, World> _worlds;
    private readonly Dictionary<uint, string> _interiors;

    private Locations(File file)
    {
        _names = file.names;
        _worlds = new Dictionary<string, World>(file.worlds, StringComparer.OrdinalIgnoreCase);
        _interiors = file.interiors.ToDictionary(p => uint.Parse(p.Key, NumberStyles.HexNumber), p => p.Value);
    }

    public static Locations Load(string path) =>
        new(JsonSerializer.Deserialize<File>(System.IO.File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is empty."));

    // Empty when the place has no name the game data gives it.
    public string NameOf(string areaId, MainCharacter? player)
    {
        if (areaId.StartsWith(InteriorPrefix, StringComparison.Ordinal))
        {
            return uint.TryParse(areaId.AsSpan(InteriorPrefix.Length), NumberStyles.HexNumber, null, out var cell)
                   && _interiors.TryGetValue(cell, out var interior)
                ? interior
                : string.Empty;
        }

        if (!areaId.StartsWith(ExteriorPrefix, StringComparison.Ordinal)
            || !_worlds.TryGetValue(areaId[ExteriorPrefix.Length..], out var world))
            return string.Empty;

        if (player is { ParentCellKind: ParentCellKind.Exterior } p
            && world.cells.TryGetValue($"{p.ParentCellX},{p.ParentCellY}", out var index)
            && index < _names.Length)
            return _names[index];

        return world.name ?? string.Empty;
    }

    // The file's shape, named as it is written.
    private sealed record File(string[] names, Dictionary<string, World> worlds, Dictionary<string, string> interiors);

    private sealed record World(string? name, Dictionary<string, int> cells);
}
