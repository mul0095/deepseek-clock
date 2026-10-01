namespace DeepSeekClock.Core;

/// <summary>Bundled fallback rates when the official pricing page is unavailable.</summary>
public static class DeepSeekPricing
{
    public static ModelPricing Peak(DeepSeekModel model) => PricingCatalog.Bundled.For(model, PricingPhase.Peak);

    /// <summary>Off-peak rates: exactly half of peak.</summary>
    public static ModelPricing OffPeak(DeepSeekModel model)
    {
        return PricingCatalog.Bundled.For(model, PricingPhase.OffPeak);
    }

    /// <summary>Rates for the selected model and phase.</summary>
    public static ModelPricing Pricing(DeepSeekModel model, PricingPhase phase) =>
        PricingCatalog.Bundled.For(model, phase);
}
