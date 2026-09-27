using DeepSeekClock.Core;

namespace DeepSeekClock.Core.Tests;

public sealed class OfficialPricingParserTests
{
    private const string OfficialTable = """
        <table><tr><td colspan="3">MODEL</td><td>deepseek-flash<sup>(1)</sup></td><td>deepseek-v4-pro</td></tr>
        <tr><td rowspan="6">PRICING</td><td rowspan="2">1M INPUT TOKENS<br>(CACHE HIT)</td><td>OFF-PEAK</td><td>$0.003</td><td>$0.022</td></tr>
        <tr><td>PEAK</td><td>$0.006</td><td>$0.044</td></tr>
        <tr><td rowspan="2">1M INPUT TOKENS<br>(CACHE MISS)</td><td>OFF-PEAK</td><td>$0.15</td><td>$0.66</td></tr>
        <tr><td>PEAK</td><td>$0.3</td><td>$1.32</td></tr>
        <tr><td rowspan="2">1M OUTPUT TOKENS</td><td>OFF-PEAK</td><td>$0.6</td><td>$1.98</td></tr>
        <tr><td>PEAK</td><td>$1.2</td><td>$3.96</td></tr></table>
        """;

    [Fact]
    public void ReadsEveryPublishedMeterForBothModelsAndPhases()
    {
        Assert.True(OfficialPricingParser.TryParse(OfficialTable, out var catalog));
        Assert.Equal(new ModelPricing(0.006m, 0.30m, 1.20m), catalog!.FlashPeak);
        Assert.Equal(new ModelPricing(0.003m, 0.15m, 0.60m), catalog.FlashOffPeak);
        Assert.Equal(new ModelPricing(0.044m, 1.32m, 3.96m), catalog.ProPeak);
        Assert.Equal(new ModelPricing(0.022m, 0.66m, 1.98m), catalog.ProOffPeak);
    }

    [Fact]
    public void IncompleteOrUnexpectedPricesCannotReplaceTheCatalog()
    {
        Assert.False(OfficialPricingParser.TryParse(OfficialTable.Replace("$3.96", "unknown"), out _));
        Assert.False(OfficialPricingParser.TryParse(OfficialTable.Replace("deepseek-v4-pro", "another-model"), out _));
        Assert.False(OfficialPricingParser.TryParse(OfficialTable.Replace("$0.003", "$2.003"), out _));
    }
}
