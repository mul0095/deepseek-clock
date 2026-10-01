namespace DeepSeekClock.Core;

/// <summary>Turns a duration into a short string that fits the panel.</summary>
public static class ClockCountdown
{
    /// <summary>8040s -> "2h 14m", 125s -> "2m 5s", 12s -> "12s".</summary>
    public static string Format(TimeSpan interval)
    {
        var total = Math.Max(0L, (long)Math.Round(interval.TotalSeconds, MidpointRounding.AwayFromZero));
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;
        if (hours > 0) return $"{hours}h {minutes}m";
        if (minutes > 0) return $"{minutes}m {seconds}s";
        return $"{seconds}s";
    }
}
