using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

internal sealed record PricingSnapshot(PricingCatalog Catalog, DateTimeOffset UpdatedAt);

/// <summary>Loads the public pricing page without credentials and keeps the last verified table.</summary>
internal sealed class OfficialPricingService : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _cachePath;

    public OfficialPricingService(string? cachePath = null, HttpMessageHandler? handler = null)
    {
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekClock", "pricing-cache.json");
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.Timeout = TimeSpan.FromSeconds(12);
    }

    public PricingSnapshot? ReadCache()
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<PricingSnapshot>(File.ReadAllText(_cachePath));
            if (snapshot?.Catalog is { } catalog && snapshot.UpdatedAt > DateTimeOffset.UnixEpoch &&
                Valid(catalog))
                return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceInformation("No verified pricing cache: {0}", ex.Message);
        }
        return null;
    }

    public async Task<PricingSnapshot?> RefreshAsync(CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(OfficialPricingParser.SourceUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!OfficialPricingParser.TryParse(html, out var catalog) || catalog is null)
            return null;
        var snapshot = new PricingSnapshot(catalog, DateTimeOffset.UtcNow);
        SaveCache(snapshot);
        return snapshot;
    }

    private void SaveCache(PricingSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var temporary = _cachePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
            File.Move(temporary, _cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Cannot save verified pricing: {0}", ex.Message);
        }
    }

    private static bool Valid(PricingCatalog catalog) =>
        new[] { catalog.FlashPeak, catalog.FlashOffPeak, catalog.ProPeak, catalog.ProOffPeak }
            .All(rates => rates.InputCacheHit > 0 && rates.InputCacheMiss > 0 && rates.Output > 0);

    public void Dispose() => _client.Dispose();
}
