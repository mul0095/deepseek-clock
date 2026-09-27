namespace DeepSeekClock.Core;

/// <summary>National days off published by the State Council for 2026.</summary>
public static class ChinesePublicHolidays
{
    public const string SourceUrl = "https://www.beijing.gov.cn/zhengce/zhengcefagui/202511/t20251104_4258873.html";

    private static readonly (DateOnly Start, DateOnly End)[] DaysOff2026 =
    [
        (new(2026, 1, 1), new(2026, 1, 3)),
        (new(2026, 2, 15), new(2026, 2, 23)),
        (new(2026, 4, 4), new(2026, 4, 6)),
        (new(2026, 5, 1), new(2026, 5, 5)),
        (new(2026, 6, 19), new(2026, 6, 21)),
        (new(2026, 9, 25), new(2026, 9, 27)),
        (new(2026, 10, 1), new(2026, 10, 7)),
    ];

    public static bool HasPublishedCalendar(int year) => year == 2026;

    public static bool IsHoliday(DateTimeOffset instant)
    {
        // The notice lists Chinese civil dates. UTC+08 has no daylight-saving changes.
        var date = DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(8)).DateTime);
        return date.Year == 2026 && DaysOff2026.Any(range => date >= range.Start && date <= range.End);
    }
}
