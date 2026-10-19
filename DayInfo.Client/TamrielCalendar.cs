namespace DayInfo.Client;

// The date as Oblivion writes it, e.g. "Morndas, 27th of Last Seed, 3E 433".
//
// The area replicates only GameDaysPassed, so the date is counted from the day the game starts.
// Oblivion.esm sets GameDaysPassed = 1 on the 27th of Last Seed, 3E 433, which is a Morndas. The
// calendar has fixed month lengths and no leap years.
public static class TamrielCalendar
{
    private static readonly string[] Weekdays =
        ["Sundas", "Morndas", "Tirdas", "Middas", "Turdas", "Fredas", "Loredas"];

    private static readonly string[] Months =
    [
        "Morning Star", "Sun's Dawn", "First Seed", "Rain's Hand", "Second Seed", "Midyear",
        "Sun's Height", "Last Seed", "Hearthfire", "Frostfall", "Sun's Dusk", "Evening Star",
    ];

    private static readonly int[] MonthLengths = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    private const int StartYear = 433;
    private const int StartWeekday = 1; // Morndas

    // The 27th of Last Seed, counted from the 1st of Morning Star.
    private static readonly int StartDayOfYear = MonthLengths[..7].Sum() + 26;

    public static string Format(float gameDaysPassed)
    {
        var elapsed = Math.Max(0, (int)gameDaysPassed - 1);

        var dayOfYear = StartDayOfYear + elapsed;
        var year = StartYear + dayOfYear / 365;
        dayOfYear %= 365;

        var month = 0;
        while (dayOfYear >= MonthLengths[month])
            dayOfYear -= MonthLengths[month++];

        var weekday = Weekdays[(StartWeekday + elapsed) % 7];
        return $"{weekday}, {Ordinal(dayOfYear + 1)} of {Months[month]}, 3E {year}";
    }

    private static string Ordinal(int day) => (day % 100, day % 10) switch
    {
        (11 or 12 or 13, _) => $"{day}th",
        (_, 1) => $"{day}st",
        (_, 2) => $"{day}nd",
        (_, 3) => $"{day}rd",
        _ => $"{day}th",
    };
}
