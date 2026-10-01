namespace DeepSeekClock.Core;

/// <summary>The two pricing phases DeepSeek can be in at any moment.</summary>
public enum PricingPhase
{
    /// <summary>Full price.</summary>
    Peak,

    /// <summary>50% discount.</summary>
    OffPeak,
}
