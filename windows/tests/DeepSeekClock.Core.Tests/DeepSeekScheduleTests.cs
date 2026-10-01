using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

// Reference dates (2026): Sep 21 = Mon, 25 = Fri, 26 = Sat, 27 = Sun, 28 = Mon.
public class DeepSeekScheduleTests
{
    [Fact]
    public void PeakWindowBoundariesOnMonday()
    {
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 0, 59)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 1, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 3, 59)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 4, 0)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 5, 59)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 6, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 9, 59)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 10, 0)));
    }

    [Theory]
    [InlineData(21)] [InlineData(22)] [InlineData(23)] [InlineData(24)] [InlineData(28)]
    public void EveryWeekdayIsPeakInsideBothWindows(int day)
    {
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, 2, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, 7, 0)));
    }

    [Theory]
    [InlineData(26)] [InlineData(27)]
    public void WeekendsAreAlwaysOffPeak(int day)
    {
        foreach (var hour in new[] { 0, 2, 7, 12, 23 })
            Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, hour, 0)));
    }

    [Fact]
    public void ChinesePublicHolidaySuppressesPeakAndSkipsItsTransitions()
    {
        var holidayMorning = TestSupport.Utc(2026, 9, 25, 2, 0);
        Assert.True(ChinesePublicHolidays.IsHoliday(holidayMorning));
        Assert.False(DeepSeekSchedule.IsPeak(holidayMorning));
        Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0), DeepSeekSchedule.NextTransition(holidayMorning));
    }

    [Fact]
    public void NationalDayHolidaySkipsTheFullPublishedBreak()
    {
        Assert.Equal(TestSupport.Utc(2026, 10, 8, 1, 0),
            DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 30, 11, 0)));
    }

    [Fact]
    public void HolidayDatesUseChinaCivilTime()
    {
        Assert.False(ChinesePublicHolidays.IsHoliday(TestSupport.Utc(2026, 9, 24, 15, 59)));
        Assert.True(ChinesePublicHolidays.IsHoliday(TestSupport.Utc(2026, 9, 24, 16, 0)));
        Assert.True(ChinesePublicHolidays.HasPublishedCalendar(2026));
        Assert.False(ChinesePublicHolidays.HasPublishedCalendar(2027));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(5)] [InlineData(10)]
    [InlineData(11)] [InlineData(12)] [InlineData(18)] [InlineData(23)]
    public void NonPeakWeekdayHoursAreOffPeak(int hour)
        => Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, hour, 0)));

    [Fact]
    public void TransitionWithinFirstWindow()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 2, 0)));

    [Fact]
    public void TransitionBetweenWindows()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 6, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 4, 30)));

    [Fact]
    public void TransitionAfterLastWindowJumpsToNextDay()
        => Assert.Equal(TestSupport.Utc(2026, 9, 22, 1, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 10, 30)));

    [Fact]
    public void TransitionFromFridayEveningJumpsToMonday()
        => Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 25, 10, 30)));

    [Fact]
    public void TransitionAcrossWeekend()
    {
        Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 26, 12, 0)));
        Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 27, 23, 0)));
    }

    [Fact]
    public void TransitionIsStrictlyAfterTheGivenDate()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0), DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 1, 0)));

    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(6)] [InlineData(9)]
    [InlineData(12)] [InlineData(15)] [InlineData(18)] [InlineData(21)]
    public void TransitionAlwaysFound(int hour)
        => Assert.NotNull(DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, hour, 0)));

    [Fact]
    public void CalculationIsIndependentOfDescribingTimeZone()
    {
        var peakInstant = TestSupport.InZone("Asia/Tokyo", 2026, 9, 21, 11, 0);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 2, 0), peakInstant);
        Assert.True(DeepSeekSchedule.IsPeak(peakInstant));

        var offPeakInstant = TestSupport.InZone("Asia/Tokyo", 2026, 9, 21, 14, 0);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 5, 0), offPeakInstant);
        Assert.False(DeepSeekSchedule.IsPeak(offPeakInstant));
    }

    [Fact]
    public void PhaseAtMapsIsPeakToTheEnum()
    {
        Assert.Equal(PricingPhase.Peak, DeepSeekSchedule.PhaseAt(TestSupport.Utc(2026, 9, 21, 2, 0)));
        Assert.Equal(PricingPhase.OffPeak, DeepSeekSchedule.PhaseAt(TestSupport.Utc(2026, 9, 21, 5, 0)));
    }
}
