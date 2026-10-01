using System.IO;
using System.Text.Json;

namespace DeepSeekClock.Windows;

internal enum ThemePreference { System, Light, Dark }
internal sealed record WindowPosition(int X, int Y);
internal sealed record WidgetPreferences(
    bool KeepOpen = false,
    bool AlwaysOnTop = true,
    ThemePreference Theme = ThemePreference.System,
    WindowPosition? Position = null,
    bool TaskbarLabel = false);

internal sealed class PreferencesStore
{
    private readonly string _path;

    public PreferencesStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekClock", "settings.json");

    public WidgetPreferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<WidgetPreferences>(File.ReadAllText(_path));
            return value is not null && Enum.IsDefined(value.Theme) ? value : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(WidgetPreferences value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Cannot save window preferences: {0}", ex.Message);
        }
    }
}
