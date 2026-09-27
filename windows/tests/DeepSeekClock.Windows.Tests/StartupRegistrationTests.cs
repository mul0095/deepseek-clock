using System.IO;
using Microsoft.Win32;
using DeepSeekClock.Windows;

namespace DeepSeekClock.Windows.Tests;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void EnablesAndDisablesCurrentUserStartupWithoutPreviewMode()
    {
        var root = $@"Software\DeepSeekClock.Tests\{Guid.NewGuid():N}";
        var runPath = $@"{root}\Run";
        var approvalPath = $@"{root}\StartupApproved";
        var executable = Path.Combine(Path.GetTempPath(), "DeepSeek Clock.exe");
        var registration = new StartupRegistration(executable, runPath, approvalPath, "Test DeepSeek Clock");
        try
        {
            Assert.False(registration.IsEnabled());
            Assert.True(registration.TrySetEnabled(true, out var error), error);
            using (var run = Registry.CurrentUser.OpenSubKey(runPath))
                Assert.Equal($"\"{executable}\"", run?.GetValue("Test DeepSeek Clock"));
            Assert.True(registration.IsEnabled());

            using (var approval = Registry.CurrentUser.CreateSubKey(approvalPath))
                approval?.SetValue("Test DeepSeek Clock", new byte[12] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            Assert.False(registration.IsEnabled());
            Assert.True(registration.TrySetEnabled(true, out error), error);
            Assert.True(registration.IsEnabled());

            Assert.True(registration.TrySetEnabled(false, out error), error);
            Assert.False(registration.IsEnabled());
            using (var after = Registry.CurrentUser.OpenSubKey(runPath))
                Assert.Null(after?.GetValue("Test DeepSeek Clock"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
    }
}
