using ReadyM.Relay.Common.Oblivion;

namespace DayInfo.Client;

// The icon the HUD draws for each weather in Oblivion.esm, keyed by form id. Weathers from other
// plugins, and the scripted ones used by quests, get a plain cloud.
public static class Weathers
{
    private const string Clear = "clear";
    private const string Cloudy = "cloudy";
    private const string Fog = "fog";
    private const string Overcast = "overcast";
    private const string Rain = "rain";
    private const string Snow = "snow";
    private const string Thunderstorm = "thunder";
    private const string Oblivion = "oblivion";

    private static readonly Dictionary<uint, string> Icons = new()
    {
        [0x00038EEE] = Clear,
        [0x00038EF0] = Cloudy,
        [0x00038EEF] = Fog,
        [0x00038EEC] = Overcast,
        [0x00038EF2] = Rain,
        [0x00038EED] = Snow,
        [0x00038EF1] = Thunderstorm,
        [0x00008BBC] = Thunderstorm, // ThunderstormKvatch, the burning city

        // Shivering Isles
        [0x00044F58] = Clear,
        [0x00078801] = Clear,
        [0x0006D221] = Clear,
        [0x0006D222] = Clear,
        [0x00044F59] = Cloudy,
        [0x00044F5A] = Fog,
        [0x00077B28] = Overcast,
        [0x00044F5C] = Rain,
        [0x00044F5D] = Thunderstorm,

        // The planes of Oblivion, the storms its gates bring to Tamriel, and the sigil stones
        [0x00032E15] = Oblivion, // Obliviondefault
        [0x00067199] = Oblivion, // OblivionStormOblivion
        [0x000836D5] = Oblivion, // OblivionStormTamriel
        [0x0006BC8B] = Oblivion, // OblivionStormTamrielMQ16
        [0x00067198] = Oblivion, // OblivionElectrical
        [0x000671A1] = Oblivion, // OblivionMountainFog
        [0x000C0999] = Oblivion, // OblivionSigil
        [0x000C42DE] = Oblivion, // SigilWhiteOut
    };

    public static string IconOf(FormId weather) => Icons.GetValueOrDefault(weather.Value, Cloudy);
}
