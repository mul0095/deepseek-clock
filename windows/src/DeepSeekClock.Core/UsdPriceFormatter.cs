using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>Formats USD independently of the user's locale.</summary>
public static class UsdPriceFormatter
{
    public static string Format(decimal value)
    {
        var format = value == decimal.Round(value, 2) ? "F2" : "F3";
        return "$" + value.ToString(format, CultureInfo.InvariantCulture);
    }
}
