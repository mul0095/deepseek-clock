using System;

namespace DeepSeekClock.Core.Tests;

/// <summary>Date-building helpers so each test reads as the behavior it checks.</summary>
internal static class TestSupport
{
    /// <summary>Builds an instant from components interpreted in UTC.</summary>
    public static DateTimeOffset Utc(int year, int month, int day,
                                     int hour, int minute = 0, int second = 0)
        => new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));

    /// <summary>Builds an instant from wall-clock components in a given IANA zone.</summary>
    public static DateTimeOffset InZone(string timeZoneId, int year, int month, int day,
                                        int hour, int minute = 0, int second = 0)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var wall = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }
}
