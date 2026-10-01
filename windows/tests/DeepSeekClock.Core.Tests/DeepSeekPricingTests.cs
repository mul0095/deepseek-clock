using System.Globalization;
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class DeepSeekPricingTests
{
    public static TheoryData<DeepSeekModel> AllModels => new() { DeepSeekModel.Flash, DeepSeekModel.Pro };

    [Theory]
    [MemberData(nameof(AllModels))]
    public void OffPeakIsExactlyHalfOfPeak(DeepSeekModel model)
    {
        var peak = DeepSeekPricing.Peak(model);
        var offPeak = DeepSeekPricing.OffPeak(model);
        Assert.Equal(peak.InputCacheHit / 2m, offPeak.InputCacheHit);
        Assert.Equal(peak.InputCacheMiss / 2m, offPeak.InputCacheMiss);
        Assert.Equal(peak.Output / 2m, offPeak.Output);
    }

    [Fact]
    public void FlashPeakRates()
    {
        var pricing = DeepSeekPricing.Peak(DeepSeekModel.Flash);
        Assert.Equal(0.006m, pricing.InputCacheHit);
        Assert.Equal(0.30m, pricing.InputCacheMiss);
        Assert.Equal(1.20m, pricing.Output);
    }

    [Fact]
    public void ProPeakRates()
    {
        var pricing = DeepSeekPricing.Peak(DeepSeekModel.Pro);
        Assert.Equal(0.044m, pricing.InputCacheHit);
        Assert.Equal(1.32m, pricing.InputCacheMiss);
        Assert.Equal(3.96m, pricing.Output);
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void PricingResolvesByPhase(DeepSeekModel model)
    {
        Assert.Equal(DeepSeekPricing.Peak(model), DeepSeekPricing.Pricing(model, PricingPhase.Peak));
        Assert.Equal(DeepSeekPricing.OffPeak(model), DeepSeekPricing.Pricing(model, PricingPhase.OffPeak));
    }

    [Theory]
    [InlineData("0.30", "$0.30")]
    [InlineData("0.006", "$0.006")]
    [InlineData("3.96", "$3.96")]
    [InlineData("0.003", "$0.003")]
    [InlineData("0.15", "$0.15")]
    public void PriceFormatting(string value, string expected)
        => Assert.Equal(expected, UsdPriceFormatter.Format(decimal.Parse(value, CultureInfo.InvariantCulture)));

    [Fact]
    public void ModelDisplayNames()
    {
        Assert.Equal(new[] { DeepSeekModel.Flash, DeepSeekModel.Pro }, Enum.GetValues<DeepSeekModel>());
        Assert.Equal("Flash", DeepSeekModel.Flash.DisplayName());
        Assert.Equal("V4 Pro", DeepSeekModel.Pro.DisplayName());
    }
}
