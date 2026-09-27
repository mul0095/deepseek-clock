using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>An immutable snapshot used by the tray icon and panel.</summary>
public sealed record ClockState(
    PricingPhase Phase,
    DateTimeOffset? NextTransition,
    string Countdown,
    string? TransitionText,
    bool HolidayCalendarAvailable = true)
{
    /// <summary>Builds the state for the instant <paramref name="now"/>.</summary>
    public static ClockState From(DateTimeOffset now, TimeZoneInfo displayZone)
    {
        var phase = DeepSeekSchedule.PhaseAt(now);
        var next = DeepSeekSchedule.NextTransition(now);
        var remaining = next is { } n ? n - now : TimeSpan.Zero;
        return new ClockState(
            phase,
            next,
            ClockCountdown.Format(remaining),
            next is { } transition ? FormatTransition(transition, displayZone) : null,
            ChinesePublicHolidays.HasPublishedCalendar(now.ToOffset(TimeSpan.FromHours(8)).Year));
    }

    /// <summary>Formats an instant as short wall-clock time in the requested zone.</summary>
    public static string FormatTransition(DateTimeOffset instant, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(instant, zone).ToString("t", CultureInfo.CurrentCulture);
}
