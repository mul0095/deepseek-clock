using System.Drawing;
using System.IO;
using DeepSeekClock.Windows;

namespace DeepSeekClock.Windows.Tests;

public sealed class WidgetPreferencesTests
{
    [Fact]
    public void SavesAndRestoresTaskbarAndWindowChoices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deepseek-clock-{Guid.NewGuid():N}.json");
        try
        {
            var store = new PreferencesStore(path);
            var expected = new WidgetPreferences(true, false, ThemePreference.Dark, new WindowPosition(-824, 607), true);
            store.Save(expected);
            Assert.Equal(expected, store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptSettingsFallBackToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deepseek-clock-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{broken json");
            Assert.Equal(new WidgetPreferences(), new PreferencesStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TaskbarSlotAvoidsExistingIndicatorAndStaysOnTheBar()
    {
        var bar = new Rectangle(-1920, 1040, 1920, 40);
        var tray = new Rectangle(-280, 1040, 280, 40);
        var codex = new Rectangle(-460, 1044, 170, 32);
        var deepSeek = TaskbarPlacement.FindSlot(bar, tray, new Size(172, 32), [codex], 4);
        Assert.False(deepSeek.IntersectsWith(codex));
        Assert.True(bar.Contains(deepSeek));
        Assert.True(deepSeek.Right < codex.Left);
    }
}
