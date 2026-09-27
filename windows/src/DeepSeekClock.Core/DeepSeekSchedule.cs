namespace DeepSeekClock.Core;

/// <summary>Calculates peak windows using UTC only.</summary>
public static class DeepSeekSchedule
{
    /// <summary>A peak window as [StartHour, EndHour) in 24-hour UTC.</summary>
    public readonly record struct Window(int StartHour, int EndHour)
    {
        public bool Contains(int hour) => hour >= StartHour && hour < EndHour;
    }

    /// <summary>Peak windows: 01:00-04:00 and 06:00-10:00 UTC.</summary>
    public static readonly IReadOnlyList<Window> PeakWindows =
        new[] { new Window(1, 4), new Window(6, 10) };

    /// <summary>The pricing phase in effect at the specified instant.</summary>
    public static PricingPhase PhaseAt(DateTimeOffset instant)
        => IsPeak(instant) ? PricingPhase.Peak : PricingPhase.OffPeak;

    /// <summary>True when the instant falls in a weekday peak window.</summary>
    public static bool IsPeak(DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        foreach (var window in PeakWindows)
            if (window.Contains(utc.Hour))
                return true;

        return false;
    }

    /// <summary>Finds the earliest peak-window boundary strictly after the instant.</summary>
    public static DateTimeOffset? NextTransition(DateTimeOffset after)
    {
        var utc = after.UtcDateTime;
        var startOfToday = new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        DateTimeOffset? best = null;

        for (var dayOffset = 0; dayOffset < 9; dayOffset++)
        {
            var day = startOfToday.AddDays(dayOffset);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            foreach (var window in PeakWindows)
            {
                foreach (var hour in new[] { window.StartHour, window.EndHour })
                {
                    var instant = new DateTimeOffset(day.AddHours(hour), TimeSpan.Zero);
                    if (instant > after && (best is null || instant < best.Value))
                        best = instant;
                }
            }
        }

        return best;
    }
}
