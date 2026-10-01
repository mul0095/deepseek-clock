namespace DeepSeekClock.Core;

/// <summary>Published prices in USD per million tokens for both supported models.</summary>
public sealed record PricingCatalog(
    ModelPricing FlashPeak,
    ModelPricing FlashOffPeak,
    ModelPricing ProPeak,
    ModelPricing ProOffPeak)
{
    public static PricingCatalog Bundled { get; } = new(
        new(0.006m, 0.30m, 1.20m),
        new(0.003m, 0.15m, 0.60m),
        new(0.044m, 1.32m, 3.96m),
        new(0.022m, 0.66m, 1.98m));

    public ModelPricing For(DeepSeekModel model, PricingPhase phase) => (model, phase) switch
    {
        (DeepSeekModel.Flash, PricingPhase.Peak) => FlashPeak,
        (DeepSeekModel.Flash, PricingPhase.OffPeak) => FlashOffPeak,
        (DeepSeekModel.Pro, PricingPhase.Peak) => ProPeak,
        (DeepSeekModel.Pro, PricingPhase.OffPeak) => ProOffPeak,
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
}
