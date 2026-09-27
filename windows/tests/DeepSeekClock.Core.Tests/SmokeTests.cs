using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void PricingPhaseHasExactlyTwoValues()
    {
        Assert.Equal(2, System.Enum.GetValues<PricingPhase>().Length);
    }
}
