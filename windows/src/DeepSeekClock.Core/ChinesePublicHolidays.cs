namespace DeepSeekClock.Core;

/// <summary>National days off published by the State Council for 2026.</summary>
public static class ChinesePublicHolidays
{
    public const string SourceUrl = "https://www.beijing.gov.cn/zhengce/zhengcefagui/202511/t20251104_4258873.html";

    private static readonly HolidayRange[] BundledDaysOff2026 =
    [
        new(new(2026, 1, 1), new(2026, 1, 3)),
        new(new(2026, 2, 15), new(2026, 2, 23)),
        new(new(2026, 4, 4), new(2026, 4, 6)),
        new(new(2026, 5, 1), new(2026, 5, 5)),
        new(new(2026, 6, 19), new(2026, 6, 21)),
        new(new(2026, 9, 25), new(2026, 9, 27)),
        new(new(2026, 10, 1), new(2026, 10, 7)),
    ];

    private static HolidayCalendar _calendar = new(2026, BundledDaysOff2026);

    public static bool HasPublishedCalendar(int year) => Volatile.Read(ref _calendar).Year == year;

    public static void UseVerifiedCalendar(HolidayCalendar calendar) => Volatile.Write(ref _calendar, calendar);

    public static bool IsHoliday(DateTimeOffset instant)
    {
        // The notice lists Chinese civil dates. UTC+08 has no daylight-saving changes.
        var date = DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(8)).DateTime);
        var calendar = Volatile.Read(ref _calendar);
        return date.Year == calendar.Year && calendar.DaysOff.Any(range => date >= range.Start && date <= range.End);
    }
}

public sealed record HolidayRange(DateOnly Start, DateOnly End);
public sealed record HolidayCalendar(int Year, IReadOnlyList<HolidayRange> DaysOff);
