using DeepSeekClock.Core;

namespace DeepSeekClock.Core.Tests;

public sealed class OfficialHolidayNoticeParserTests
{
    private const string Notice = """
        <h1>国务院办公厅关于2026年部分节假日安排的通知</h1>
        <p>一、元旦：1月1日（周四）至3日（周六）放假调休，共3天。1月4日上班。</p>
        <p>二、春节：2月15日（农历腊月二十八、周日）至23日（周一）放假调休，共9天。</p>
        <p>三、清明节：4月4日（周六）至6日（周一）放假，共3天。</p>
        <p>四、劳动节：5月1日（周五）至5日（周二）放假调休，共5天。</p>
        <p>五、端午节：6月19日（周五）至21日（周日）放假，共3天。</p>
        <p>六、中秋节：9月25日（周五）至27日（周日）放假，共3天。</p>
        <p>七、国庆节：10月1日（周四）至7日（周三）放假调休，共7天。</p>
        """;

    [Fact]
    public void ReadsTheSevenPublishedRanges()
    {
        Assert.True(OfficialHolidayNoticeParser.TryParse(Notice, 2026, out var calendar));
        Assert.Equal(7, calendar!.DaysOff.Count);
        Assert.Contains(new HolidayRange(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 27)), calendar.DaysOff);
    }

    [Fact]
    public void IncompleteOrWrongYearNoticeCannotReplaceCalendar()
    {
        Assert.False(OfficialHolidayNoticeParser.TryParse(Notice.Replace("七、国庆节", "七、其他"), 2026, out _));
        Assert.False(OfficialHolidayNoticeParser.TryParse(Notice, 2027, out _));
    }
}
