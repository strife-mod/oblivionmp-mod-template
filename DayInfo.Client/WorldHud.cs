using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Cef;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;
// A service's own Time property is the frame timing, so name the mixin apart from it.
using GameTime = OblivionMp.Sdk.Common.Archetypes.Mixins.Time;

namespace DayInfo.Client;

// A web page in the top-right corner showing the in-game time, weather, place and date. The page
// lives in Content/ui and is served at readym://DayInfo/ (see Mod.Init); its messages are in
// WorldHudPage.
//
// The SDK keeps time and weather on the Area the player is in, replicated from the server. The time
// only arrives every few in-game minutes, so between updates the clock runs on by itself at the
// area's time scale, and each update sets it right again.
//
// The HUD belongs to play, not to the menus drawn over it, so it hides while one is open.
[Service]
public sealed partial class WorldHud(
    CefHost cef, WorldHudPage page, IEntities entities, IPlayers players, IInput input, Locations locations,
    ILogger logger)
{
    public const string PageHost = "DayInfo";

    private const float RefreshSeconds = 0.5f;

    // How far the clock may run ahead of the last update before it waits for the next one, so a
    // server that stops time is not overtaken by more than this.
    private const double MaxRunAheadHours = 15 / 60.0;

    // A step back smaller than this is the clock having run a little ahead of an update, and is
    // waited out rather than shown. Anything larger is real: a wait, a sleep, or time being set.
    private const int MaxHeldBackMinutes = 10;

    private ViewportHandle _viewport;
    private BrowserHandle _browser;
    private bool _created;
    private bool _visible;
    private bool _hiddenOnce;
    private float _elapsed;

    private double _syncedHours = double.NaN;
    private double _sinceSync;

    private long _lastMinute = -1;
    private FormId _lastWeather;
    private string? _lastLocation;

    private void Start()
    {
        // Whatever was sent before the page loaded is lost, so send it again.
        page.Ready = () =>
        {
            _lastMinute = -1;
            Refresh();
        };
    }

    private void Update()
    {
        // Checked every frame rather than on the refresh below, so a menu never opens over the HUD.
        UpdateVisibility();

        _sinceSync += Time.DeltaTime;
        _elapsed += Time.DeltaTime;
        if (_elapsed < RefreshSeconds)
            return;

        _elapsed = 0;
        Refresh();
    }

    private void Refresh()
    {
        if (!TryReadArea(out var area))
            return;

        // Created on the first state rather than in Start: the game only has its web overlay once
        // gameplay begins, and a page painted before that stays invisible.
        EnsureCreated();

        var minute = CurrentMinute(area);
        var location = locations.NameOf(area.AreaId, players.LocalMainCharacter);
        if (minute == _lastMinute && area.Weather == _lastWeather && location == _lastLocation)
            return;

        _lastMinute = minute;
        _lastWeather = area.Weather;
        _lastLocation = location;

        var minuteOfDay = (int)(minute % (24 * 60));
        page.SendWorldState(_browser, minuteOfDay / 60, minuteOfDay % 60, location,
            TamrielCalendar.Format(minute / (24 * 60)), Weathers.IconOf(area.Weather));
    }

    // The in-game time as minutes since the game began, run on from the last update.
    private long CurrentMinute(AreaState area)
    {
        // Days and hours as one number, so the clock can run past midnight between updates.
        var hours = (int)area.DaysPassed * 24.0 + area.GameHour;
        if (hours != _syncedHours)
        {
            _syncedHours = hours;
            _sinceSync = 0;
        }

        var ahead = Math.Min(_sinceSync * area.TimeScale / 3600.0, MaxRunAheadHours);
        var minute = (long)((_syncedHours + ahead) * 60);

        // An update landing a moment behind where the clock had run to would tick it backwards.
        if (_lastMinute >= 0 && minute < _lastMinute && _lastMinute - minute <= MaxHeldBackMinutes)
            return _lastMinute;

        return minute;
    }

    private void EnsureCreated()
    {
        if (_created)
            return;

        _created = true;

        _viewport = cef.CreateViewport(zOrder: 10);
        cef.SetPlacement(_viewport,
            ViewportPlacement.Pinned(ViewportAlign.End, ViewportAlign.Start, width: 400, height: 124, margin: 24));
        // A display, not a form: never take the keyboard from the game.
        cef.SetFocusable(_viewport, false);

        var browser = cef.CreateBrowser();
        browser.Navigate($"readym://{PageHost}/index.html");
        cef.Assign(_viewport, browser.Handle);
        _browser = browser.Handle;

        _visible = !InGameplay();
        UpdateVisibility();

        logger.LogInformation("World HUD shown on readym://{Host}/.", PageHost);
    }

    // A menu shows the mouse cursor; the SDK's own input check is kept for anything it does catch.
    private bool InGameplay() => input.CanApplyInput() && !CursorState.IsVisible();

    private void UpdateVisibility()
    {
        if (!_created)
            return;

        var inGameplay = InGameplay();
        if (inGameplay == _visible)
            return;

        _visible = inGameplay;
        cef.SetVisible(_viewport, inGameplay);

        if (!inGameplay && !_hiddenOnce)
        {
            _hiddenOnce = true;
            logger.LogInformation("World HUD hidden while a menu is open; it hides like this every time.");
        }
    }

    private readonly record struct AreaState(
        string AreaId, float GameHour, float DaysPassed, float TimeScale, FormId Weather);

    // Not every area carries time and weather, so ask only for the ones that do. A client only
    // holds the area it is in, so the first match is the player's.
    private bool TryReadArea(out AreaState state)
    {
        foreach (var (area, time, sky) in entities.Query<Area, GameTime, Weather>())
        {
            state = new AreaState(area.AreaId.ToManaged(), time.GameHour, time.GameDaysPassed, time.TimeScale,
                sky.WeatherFormId);
            return true;
        }

        state = default;
        return false;
    }
}
