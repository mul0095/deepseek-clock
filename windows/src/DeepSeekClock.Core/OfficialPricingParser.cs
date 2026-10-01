using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace DeepSeekClock.Core;

/// <summary>Reads the public DeepSeek pricing table; rejects incomplete or changed layouts.</summary>
public static class OfficialPricingParser
{
    public const string SourceUrl = "https://api-docs.deepseek.com/quick_start/pricing/";

    public static bool TryParse(string html, out PricingCatalog? catalog)
    {
        catalog = null;
        if (html.Length > 1_000_000)
            return false;

        var rows = Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var values = new Dictionary<(string Meter, string Phase), (decimal Flash, decimal Pro)>();
        var inTable = false;
        var meter = "";
        foreach (Match row in rows)
        {
            var cells = Regex.Matches(row.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Select(match => PlainText(match.Groups[1].Value)).ToArray();
            if (!inTable)
            {
                if (cells.Length >= 3 && cells[0] == "MODEL" &&
                    cells[^2].StartsWith("deepseek-flash", StringComparison.OrdinalIgnoreCase) &&
                    cells[^1].StartsWith("deepseek-v4-pro", StringComparison.OrdinalIgnoreCase))
                    inTable = true;
                continue;
            }
            if (cells.Any(cell => cell.Contains("Concurrency Limit", StringComparison.OrdinalIgnoreCase)))
                break;
            if (cells.Any(cell => cell.Contains("CACHE HIT", StringComparison.OrdinalIgnoreCase))) meter = "hit";
            else if (cells.Any(cell => cell.Contains("CACHE MISS", StringComparison.OrdinalIgnoreCase))) meter = "miss";
            else if (cells.Any(cell => cell.Contains("OUTPUT TOKENS", StringComparison.OrdinalIgnoreCase))) meter = "output";

            var phase = cells.FirstOrDefault(cell => cell is "PEAK" or "OFF-PEAK");
            if (meter.Length == 0 || phase is null)
                continue;
            if (cells.Length < 3 || !TryUsd(cells[^2], out var flash) || !TryUsd(cells[^1], out var pro) ||
                !values.TryAdd((meter, phase), (flash, pro)))
                return false;
        }
        if (values.Count != 6 || !values.ContainsKey(("hit", "PEAK")) ||
            !values.ContainsKey(("miss", "PEAK")) || !values.ContainsKey(("output", "PEAK")) ||
            !values.ContainsKey(("hit", "OFF-PEAK")) || !values.ContainsKey(("miss", "OFF-PEAK")) ||
            !values.ContainsKey(("output", "OFF-PEAK")))
            return false;

        catalog = new PricingCatalog(
            Rates("PEAK", true), Rates("OFF-PEAK", true),
            Rates("PEAK", false), Rates("OFF-PEAK", false));
        if (!Valid(catalog.FlashPeak, catalog.FlashOffPeak) || !Valid(catalog.ProPeak, catalog.ProOffPeak))
        {
            catalog = null;
            return false;
        }
        return true;

        ModelPricing Rates(string phase, bool flash) => new(
            flash ? values[("hit", phase)].Flash : values[("hit", phase)].Pro,
            flash ? values[("miss", phase)].Flash : values[("miss", phase)].Pro,
            flash ? values[("output", phase)].Flash : values[("output", phase)].Pro);
    }

    private static string PlainText(string html) => Regex.Replace(
        WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    private static bool TryUsd(string text, out decimal value) =>
        decimal.TryParse(text.Trim().TrimStart('$'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value) && value > 0 && value < 100;

    private static bool Valid(ModelPricing peak, ModelPricing offPeak) =>
        peak.InputCacheHit >= offPeak.InputCacheHit &&
        peak.InputCacheMiss >= offPeak.InputCacheMiss &&
        peak.Output >= offPeak.Output;
}
