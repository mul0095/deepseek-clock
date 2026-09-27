using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class ClockCountdownTests
{
    [Theory]
    [InlineData(8040, "2h 14m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(125, "2m 5s")]
    [InlineData(60, "1m 0s")]
    [InlineData(12, "12s")]
    [InlineData(59, "59s")]
    [InlineData(-5, "0s")]
    public void FormatsCountdown(double seconds, string expected)
        => Assert.Equal(expected, ClockCountdown.Format(TimeSpan.FromSeconds(seconds)));
}
