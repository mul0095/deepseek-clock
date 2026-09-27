namespace DeepSeekClock.Core;

/// <summary>The three billable meters, in USD per 1M tokens.</summary>
public readonly record struct ModelPricing(decimal InputCacheHit, decimal InputCacheMiss, decimal Output);
