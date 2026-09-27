using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

internal sealed record HolidaySnapshot(HolidayCalendar Calendar, DateTimeOffset UpdatedAt);

/// <summary>Refreshes the published 2026 State Council notice from an official government mirror.</summary>
internal sealed class OfficialHolidayService : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _cachePath;

    public OfficialHolidayService(string? cachePath = null, HttpMessageHandler? handler = null)
    {
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekClock", "holidays-cache.json");
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.Timeout = TimeSpan.FromSeconds(12);
    }

    public HolidaySnapshot? ReadCache()
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<HolidaySnapshot>(File.ReadAllText(_cachePath));
            if (snapshot?.Calendar is { Year: 2026, DaysOff.Count: 7 } &&
                snapshot.UpdatedAt > DateTimeOffset.UnixEpoch)
                return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceInformation("No verified holiday cache: {0}", ex.Message);
        }
        return null;
    }

    public async Task<HolidaySnapshot?> RefreshAsync(CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(ChinesePublicHolidays.SourceUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!OfficialHolidayNoticeParser.TryParse(html, 2026, out var calendar) || calendar is null)
            return null;
        var snapshot = new HolidaySnapshot(calendar, DateTimeOffset.UtcNow);
        SaveCache(snapshot);
        return snapshot;
    }

    private void SaveCache(HolidaySnapshot snapshot)
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
            Trace.TraceWarning("Cannot save verified holidays: {0}", ex.Message);
        }
    }

    public void Dispose() => _client.Dispose();
}
