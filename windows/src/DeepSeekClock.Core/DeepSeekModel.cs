namespace DeepSeekClock.Core;

/// <summary>The DeepSeek models this app can show prices for.</summary>
public enum DeepSeekModel
{
    Flash,
    Pro,
}

public static class DeepSeekModelExtensions
{
    /// <summary>Short, friendly name shown in the panel.</summary>
    public static string DisplayName(this DeepSeekModel model) => model switch
    {
        DeepSeekModel.Flash => "Flash",
        DeepSeekModel.Pro => "V4 Pro",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
}
