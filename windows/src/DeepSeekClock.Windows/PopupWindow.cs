using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DeepSeekClock.Core;
using Microsoft.Win32;

namespace DeepSeekClock.Windows;

/// <summary>A content-sized WPF popup; all layout uses device-independent units.</summary>
internal sealed class PopupWindow : Window, IDisposable
{
    private readonly PopupView _view = new();
    private System.Drawing.Point _anchor;
    private bool _disposed;

    public event EventHandler? QuitRequested;

    public PopupWindow(bool preview = false)
    {
        Title = "DeepSeek Clock";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = preview;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        Content = _view;

        _view.QuitRequested += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);
        if (!preview)
            Deactivated += (_, _) => Hide();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
        // The tray still owns a WinForms message loop. Forward modeless keyboard input to WPF.
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)PositionNearAnchor);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyTheme();
    }

    public void ShowState(ClockState state) => _view.ShowState(state);

    public void ShowNearCursor()
    {
        _anchor = System.Windows.Forms.Cursor.Position;
        ApplyTheme();
        Show();
        UpdateLayout();
        PositionNearAnchor();
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 dismisses the popup without leaving the tray with a closed Window.
        if (!_disposed)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Close();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!_disposed && !Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke((Action)ApplyTheme);
    }

    private void ApplyTheme()
    {
        if (!_disposed)
            _view.SetTheme(ReadLightTheme());
    }

    private static bool ReadLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private void PositionNearAnchor()
    {
        if (_disposed || !IsVisible)
            return;
        // The cursor, work area and HWND rectangle are all physical pixels. WPF owns DPI scaling.
        var handle = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(handle, out var bounds))
            return;
        var area = Screen.FromPoint(_anchor).WorkingArea;
        var x = Math.Clamp(_anchor.X, area.Left, Math.Max(area.Left, area.Right - (bounds.Right - bounds.Left)));
        var y = Math.Clamp(_anchor.Y, area.Top, Math.Max(area.Top, area.Bottom - (bounds.Bottom - bounds.Top)));
        const uint NoSizeNoZOrderNoActivate = 0x0001 | 0x0004 | 0x0010;
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, NoSizeNoZOrderNoActivate);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowBounds
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out WindowBounds bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
