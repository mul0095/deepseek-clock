using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class ClockStateTests
{
    [Fact]
    public void FutureYearWithoutPublishedHolidayNoticeIsMarkedEstimated()
    {
        Assert.True(ClockState.From(TestSupport.Utc(2026, 9, 28, 2, 0), TimeZoneInfo.Utc).HolidayCalendarAvailable);
        Assert.False(ClockState.From(TestSupport.Utc(2027, 1, 4, 2, 0), TimeZoneInfo.Utc).HolidayCalendarAvailable);
    }

    [Fact]
    public void PhaseMatchesTheSchedule()
    {
        Assert.Equal(PricingPhase.Peak, ClockState.From(TestSupport.Utc(2026, 9, 21, 2, 0), TimeZoneInfo.Utc).Phase);
        Assert.Equal(PricingPhase.OffPeak, ClockState.From(TestSupport.Utc(2026, 9, 21, 5, 0), TimeZoneInfo.Utc).Phase);
    }

    [Fact]
    public void CountdownMatchesTheNextTransition()
    {
        var state = ClockState.From(TestSupport.Utc(2026, 9, 21, 2, 0), TimeZoneInfo.Utc);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0), state.NextTransition);
        Assert.Equal("2h 0m", state.Countdown);
    }

    [Fact]
    public void TransitionTextUsesRequestedTimeZone()
    {
        var transition = TestSupport.Utc(2026, 9, 21, 4, 0);
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var kolkataText = ClockState.FormatTransition(transition, kolkata);
        var losAngelesText = ClockState.FormatTransition(transition, losAngeles);
        Assert.False(string.IsNullOrEmpty(kolkataText));
        Assert.NotEqual(kolkataText, losAngelesText);
    }

    [Fact]
    public void TransitionTextIsNullWhenThereIsNoTransition()
    {
        var state = new ClockState(PricingPhase.OffPeak, null, "0s", null);
        Assert.Null(state.TransitionText);
    }
}
