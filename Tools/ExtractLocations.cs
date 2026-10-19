// Builds Content/data/locations.json, the place names the HUD shows, from the game's plugins.
//
//   dotnet run Tools/ExtractLocations.cs -- "<game>\OblivionRemastered\Content\Dev\ObvData\Data" Content/data/locations.json
//
// Reads every plugin Plugins.txt enables, in load order, so later plugins override earlier ones the
// way they do in game. The Remastered's own plugin swaps names for localization keys ("LOC_FN_..."),
// whose text lives in the game's packed assets; a key never replaces a name, so the English names
// from Oblivion.esm and the DLC plugins stay. It keeps:
//   - interior cells: their name, keyed by form id as the game numbers it at runtime
//   - worldspaces: their name, keyed by editor id (an exterior area is "L_<editor id>")
//   - exterior cells: per worldspace, the name of the cell or else of its map region, keyed "x,y"

#:property JsonSerializerIsReflectionEnabledByDefault=true

using System.IO.Compression;
using System.Text;
using System.Text.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run Tools/ExtractLocations.cs -- <Data folder> <output json>");
    return 1;
}

var dataDir = args[0];
var loadOrder = File.ReadAllLines(Path.Combine(dataDir, "Plugins.txt"))
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith('#'))
    .ToList();

var interiors = new Dictionary<uint, string>();
var worlds = new Dictionary<uint, World>();
var regions = new Dictionary<uint, string>();
var exteriors = new Dictionary<(uint World, int X, int Y), Cell>();

for (var index = 0; index < loadOrder.Count; index++)
{
    var path = Path.Combine(dataDir, loadOrder[index]);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"skipped {loadOrder[index]}: not found");
        continue;
    }

    new Plugin(File.ReadAllBytes(path), index, loadOrder, interiors, worlds, regions, exteriors).Read();
    Console.WriteLine($"read {loadOrder[index]}");
}

// Names are shared by many cells, so the cells point into one list.
var names = new List<string>();
var nameIndex = new Dictionary<string, int>();
int NameId(string name)
{
    if (!nameIndex.TryGetValue(name, out var id))
    {
        id = names.Count;
        names.Add(name);
        nameIndex[name] = id;
    }

    return id;
}

// A cell lists every region it is in, from all of Cyrodiil down to a single farm. The one covering
// the fewest cells is the most specific, so that is the name it gets.
var regionSize = exteriors.Values.SelectMany(c => c.Regions).Where(regions.ContainsKey)
    .GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());

string? RegionName(Cell cell) => cell.Regions.Where(regions.ContainsKey)
    .OrderBy(r => regionSize[r]).Select(r => regions[r]).FirstOrDefault();

// A worldspace without a name of its own, Tamriel among them, takes its parent's, or else the region
// that covers most of it ("Cyrodiil").
string? WorldName(uint id, int depth = 0)
{
    if (!worlds.TryGetValue(id, out var w))
        return null;
    if (w.Name is not null)
        return w.Name;
    if (w.Parent is { } parent && depth < 8 && WorldName(parent, depth + 1) is { } inherited)
        return inherited;

    return exteriors.Where(e => e.Key.World == id).SelectMany(e => e.Value.Regions).Where(regions.ContainsKey)
        .GroupBy(r => r).OrderByDescending(g => g.Count()).Select(g => regions[g.Key]).FirstOrDefault();
}

var worldsOut = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
foreach (var (id, world) in worlds)
{
    if (world.EditorId is null)
        continue;

    var cells = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var ((worldId, x, y), cell) in exteriors)
    {
        if (worldId != id)
            continue;

        var name = cell.Name ?? RegionName(cell);
        if (name is not null)
            cells[$"{x},{y}"] = NameId(name);
    }

    worldsOut[world.EditorId] = new { name = WorldName(id), cells };
}

var output = new
{
    names,
    worlds = worldsOut,
    interiors = new SortedDictionary<string, string>(interiors.ToDictionary(p => p.Key.ToString("X8"), p => p.Value)),
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(output));
Console.WriteLine($"wrote {args[1]}: {interiors.Count} interiors, {worldsOut.Count} worldspaces, {names.Count} names");
return 0;

sealed class World
{
    public string? EditorId;
    public string? Name;
    public uint? Parent;
}

sealed class Cell
{
    public string? Name;
    public uint[] Regions = [];
}

// One plugin file. Records are 20-byte headers followed by subrecords; groups nest them.
sealed class Plugin(
    byte[] bytes, int loadIndex, List<string> loadOrder,
    Dictionary<uint, string> interiors, Dictionary<uint, World> worlds, Dictionary<uint, string> regions,
    Dictionary<(uint, int, int), Cell> exteriors)
{
    private readonly List<int> _masterIndices = [];

    public void Read()
    {
        var headerSize = BitConverter.ToInt32(bytes, 4);
        foreach (var (type, data) in Subrecords(bytes.AsSpan(20, headerSize).ToArray()))
        {
            if (type == "MAST")
                _masterIndices.Add(loadOrder.FindIndex(p => p.Equals(ZString(data), StringComparison.OrdinalIgnoreCase)));
        }

        ReadGroups(20 + headerSize, bytes.Length, world: null);
    }

    // A form id as the game numbers it at runtime: the top byte names a master, or this plugin.
    private uint Runtime(uint formId)
    {
        var mod = (int)(formId >> 24);
        var index = mod < _masterIndices.Count ? _masterIndices[mod] : loadIndex;
        return (uint)(index << 24) | (formId & 0x00FFFFFF);
    }

    private void ReadGroups(int pos, int end, uint? world)
    {
        while (pos < end)
        {
            var type = Encoding.ASCII.GetString(bytes, pos, 4);
            var size = BitConverter.ToInt32(bytes, pos + 4);

            if (type == "GRUP")
            {
                // Group type 1 holds a worldspace's cells, labelled with its form id.
                var groupType = BitConverter.ToInt32(bytes, pos + 12);
                var inner = groupType == 1 ? Runtime(BitConverter.ToUInt32(bytes, pos + 8)) : world;
                ReadGroups(pos + 20, pos + size, inner);
                pos += size;
                continue;
            }

            var flags = BitConverter.ToUInt32(bytes, pos + 8);
            var id = Runtime(BitConverter.ToUInt32(bytes, pos + 12));
            if (type is "CELL" or "WRLD" or "REGN")
            {
                var data = bytes.AsSpan(pos + 20, size).ToArray();
                if ((flags & 0x00040000) != 0)
                    data = Inflate(data);
                Record(type, id, data, world);
            }

            pos += 20 + size;
        }
    }

    private void Record(string type, uint id, byte[] data, uint? world)
    {
        string? editorId = null, name = null, mapName = null;
        uint? parent = null;
        int? x = null, y = null;
        var interior = false;
        uint[] cellRegions = [];

        foreach (var (sub, d) in Subrecords(data))
        {
            switch (sub)
            {
                case "EDID": editorId = ZString(d); break;
                case "FULL": name = ZString(d); break;
                case "RDMP": mapName = ZString(d); break;
                case "WNAM" when type == "WRLD": parent = Runtime(BitConverter.ToUInt32(d)); break;
                case "DATA" when type == "CELL": interior = (d[0] & 1) != 0; break;
                case "XCLC": x = BitConverter.ToInt32(d, 0); y = BitConverter.ToInt32(d, 4); break;
                case "XCLR":
                    cellRegions = Enumerable.Range(0, d.Length / 4).Select(i => Runtime(BitConverter.ToUInt32(d, i * 4))).ToArray();
                    break;
            }
        }

        name = Text(name);
        mapName = Text(mapName);

        switch (type)
        {
            case "REGN":
                if (mapName is not null)
                    regions[id] = mapName;
                break;
            case "WRLD":
                worlds[id] = new World
                {
                    EditorId = editorId,
                    Name = name ?? worlds.GetValueOrDefault(id)?.Name,
                    Parent = parent,
                };
                break;
            case "CELL" when interior:
                if (name is not null)
                    interiors[id] = name;
                break;
            case "CELL" when world is { } w && x is { } cx && y is { } cy:
                exteriors[(w, cx, cy)] = new Cell
                {
                    Name = name ?? exteriors.GetValueOrDefault((w, cx, cy))?.Name,
                    Regions = cellRegions,
                };
                break;
        }
    }

    private static IEnumerable<(string Type, byte[] Data)> Subrecords(byte[] data)
    {
        var pos = 0;
        var extendedSize = -1;
        while (pos + 6 <= data.Length)
        {
            var type = Encoding.ASCII.GetString(data, pos, 4);
            int size = BitConverter.ToUInt16(data, pos + 4);
            pos += 6;

            // XXXX carries the size of the next subrecord when it does not fit in 16 bits.
            if (type == "XXXX")
            {
                extendedSize = BitConverter.ToInt32(data, pos);
                pos += size;
                continue;
            }

            if (extendedSize >= 0)
            {
                size = extendedSize;
                extendedSize = -1;
            }

            if (pos + size > data.Length)
                yield break;

            yield return (type, data.AsSpan(pos, size).ToArray());
            pos += size;
        }
    }

    private static byte[] Inflate(byte[] data)
    {
        var length = BitConverter.ToInt32(data, 0);
        var output = new byte[length];
        using var zlib = new ZLibStream(new MemoryStream(data, 4, data.Length - 4), CompressionMode.Decompress);
        zlib.ReadExactly(output);
        return output;
    }

    private static string ZString(byte[] d) => Encoding.Latin1.GetString(d).TrimEnd('\0');

    // A name worth showing: not blank, and not a localization key standing in for one.
    private static string? Text(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.StartsWith("LOC_", StringComparison.Ordinal) ? null : s.Trim();
}
