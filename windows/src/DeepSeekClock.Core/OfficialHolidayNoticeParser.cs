using System.Net;
using System.Text.RegularExpressions;

namespace DeepSeekClock.Core;

/// <summary>Reads the seven national holiday ranges from an official State Council notice.</summary>
public static class OfficialHolidayNoticeParser
{
    private static readonly string[] HolidayNames = ["元旦", "春节", "清明节", "劳动节", "端午节", "中秋节", "国庆节"];
    private static readonly Regex Range = new(
        @"(?<startMonth>\d{1,2})月(?<startDay>\d{1,2})日.*?至(?:(?<endMonth>\d{1,2})月)?(?<endDay>\d{1,2})日.*?放假",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool TryParse(string html, int year, out HolidayCalendar? calendar)
    {
        calendar = null;
        if (html.Length > 1_000_000 || !html.Contains($"关于{year}年", StringComparison.Ordinal))
            return false;

        var paragraphs = Regex.Matches(html, @"<p\b[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", ""))).ToArray();
        var holidays = new List<HolidayRange>();
        foreach (var name in HolidayNames)
        {
            var matching = paragraphs.Where(paragraph => paragraph.Contains(name, StringComparison.Ordinal))
                .Select(paragraph => Range.Match(paragraph)).Where(match => match.Success).ToArray();
            if (matching.Length != 1)
                return false;
            try
            {
                var match = matching[0];
                var startMonth = int.Parse(match.Groups["startMonth"].Value);
                var endMonth = match.Groups["endMonth"].Success ? int.Parse(match.Groups["endMonth"].Value) : startMonth;
                var start = new DateOnly(year, startMonth, int.Parse(match.Groups["startDay"].Value));
                var end = new DateOnly(year, endMonth, int.Parse(match.Groups["endDay"].Value));
                if (end < start || end.DayNumber - start.DayNumber > 14)
                    return false;
                holidays.Add(new HolidayRange(start, end));
            }
            catch (ArgumentOutOfRangeException) { return false; }
            catch (FormatException) { return false; }
        }
        calendar = new HolidayCalendar(year, holidays);
        return true;
    }
}
