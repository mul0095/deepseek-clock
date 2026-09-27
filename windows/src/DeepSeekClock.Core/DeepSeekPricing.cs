using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>The source of truth for DeepSeek rates; off-peak is half of peak.</summary>
public static class DeepSeekPricing
{
    /// <summary>Peak (full price) rates, in USD per 1M tokens.</summary>
    public static ModelPricing Peak(DeepSeekModel model) => model switch
    {
        DeepSeekModel.Flash => new ModelPricing(Usd("0.006"), Usd("0.30"), Usd("1.20")),
        DeepSeekModel.Pro => new ModelPricing(Usd("0.044"), Usd("1.32"), Usd("3.96")),
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    /// <summary>Off-peak rates: exactly half of peak.</summary>
    public static ModelPricing OffPeak(DeepSeekModel model)
    {
        var peak = Peak(model);
        return new ModelPricing(peak.InputCacheHit / 2m, peak.InputCacheMiss / 2m, peak.Output / 2m);
    }

    /// <summary>Rates for the selected model and phase.</summary>
    public static ModelPricing Pricing(DeepSeekModel model, PricingPhase phase) => phase switch
    {
        PricingPhase.Peak => Peak(model),
        PricingPhase.OffPeak => OffPeak(model),
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static decimal Usd(string value)
        => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
