using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace DeepSeekClock.Windows;

/// <summary>Registers this executable for the current user's Windows sign-in.</summary>
internal sealed class StartupRegistration
{
    private const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string DefaultValueName = "DeepSeek Clock";

    private readonly string _runKey;
    private readonly string _approvalKey;
    private readonly string _valueName;
    private readonly string _command;

    public StartupRegistration(string executablePath, string runKey = DefaultRunKey,
        string approvalKey = DefaultApprovalKey, string valueName = DefaultValueName)
    {
        _runKey = runKey;
        _approvalKey = approvalKey;
        _valueName = valueName;
        _command = $"\"{Path.GetFullPath(executablePath)}\"";
    }

    public bool IsEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(_runKey);
            if (run?.GetValue(_valueName) is not string command ||
                !string.Equals(command, _command, StringComparison.OrdinalIgnoreCase))
                return false;

            using var approval = Registry.CurrentUser.OpenSubKey(_approvalKey);
            return approval?.GetValue(_valueName) is not byte[] state ||
                (state.Length > 0 && state[0] == 2);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            Trace.TraceWarning("Cannot read Windows startup status: {0}", ex.Message);
            return false;
        }
    }

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (enabled)
            {
                if (_command.Length > 260)
                    throw new InvalidOperationException("The executable path is too long for Windows startup.");
                using var run = Registry.CurrentUser.CreateSubKey(_runKey)
                    ?? throw new IOException("Windows startup registry key could not be opened.");
                run.SetValue(_valueName, _command, RegistryValueKind.String);

                // Windows Settings may have disabled an existing Run entry.
                using var approval = Registry.CurrentUser.OpenSubKey(_approvalKey, writable: true);
                if (approval?.GetValue(_valueName) is byte[] state &&
                    (state.Length == 0 || state[0] != 2))
                    approval.SetValue(_valueName, new byte[12] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            }
            else
            {
                using var run = Registry.CurrentUser.OpenSubKey(_runKey, writable: true);
                run?.DeleteValue(_valueName, throwOnMissingValue: false);
                using var approval = Registry.CurrentUser.OpenSubKey(_approvalKey, writable: true);
                approval?.DeleteValue(_valueName, throwOnMissingValue: false);
            }

            if (IsEnabled() == enabled)
                return true;
            error = "Windows did not apply the startup setting.";
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            error = ex.Message;
            Trace.TraceWarning("Cannot update Windows startup: {0}", ex.Message);
        }
        return false;
    }
}
