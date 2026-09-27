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
    private readonly bool _preview;
    private bool _hasPosition;
    private bool _changingMode;
    private bool _dragging;
    private WidgetPreferences _preferences = new();

    public bool MenuOpen { get; set; }
    public bool KeepOpen => _preferences.KeepOpen;
    public WindowPosition Position => new(_anchor.X, _anchor.Y);

    public event EventHandler? QuitRequested;
    public event EventHandler? PinRequested;
    public event EventHandler? OptionsRequested;
    public event EventHandler? PositionSaved;
    public event EventHandler? ThemeChanged;

    public PopupWindow(bool preview = false)
    {
        _preview = preview;
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
        Icon = _view.BrandIcon.Source;

        _view.QuitRequested += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);
        _view.PinRequested += (_, _) => PinRequested?.Invoke(this, EventArgs.Empty);
        _view.OptionsRequested += (sender, _) => OptionsRequested?.Invoke(sender, EventArgs.Empty);
        _view.HideRequested += (_, _) => Hide();
        _view.MinimizeRequested += (_, _) =>
        {
            if (ShowInTaskbar)
                WindowState = WindowState.Minimized;
            else
                Hide();
        };
        _view.DragRequested += (_, e) =>
        {
            _dragging = true;
            try { DragMove(); }
            finally { _dragging = false; }
            RememberPosition();
            PositionSaved?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)DismissIfInactive);
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

    public void ApplyPreferences(WidgetPreferences preferences)
    {
        _preferences = preferences;
        _changingMode = true;
        try
        {
            ShowInTaskbar = _preview;
            Topmost = preferences.AlwaysOnTop;
        }
        finally { _changingMode = false; }
        if (!_hasPosition && preferences.Position is { } position)
        {
            _anchor = new System.Drawing.Point(position.X, position.Y);
            _hasPosition = true;
        }
        ApplyTheme();
    }

    public void CopyThemeTo(ResourceDictionary destination) => _view.CopyThemeTo(destination);

    public void DismissIfInactive()
    {
        if (!_disposed && !_preview && !KeepOpen && !IsActive && !MenuOpen && !_changingMode && !_dragging)
            Hide();
    }

    public void ResetPosition()
    {
        _hasPosition = false;
        ShowNearCursor();
        PositionSaved?.Invoke(this, EventArgs.Empty);
    }

    public void ShowNearCursor()
    {
        if (!_hasPosition)
            _anchor = System.Windows.Forms.Cursor.Position;
        ApplyTheme();
        WindowState = WindowState.Normal;
        Show();
        UpdateLayout();
        PositionNearAnchor();
        RememberPosition();
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
        {
            _view.SetTheme(_preferences.Theme switch
            {
                ThemePreference.Light => true,
                ThemePreference.Dark => false,
                _ => ReadLightTheme(),
            });
            _view.SetPinned(KeepOpen);
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RememberPosition()
    {
        if (WindowState == WindowState.Normal && GetWindowRect(new WindowInteropHelper(this).Handle, out var bounds))
        {
            _anchor = new System.Drawing.Point(bounds.Left, bounds.Top);
            _hasPosition = true;
        }
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
        if (_disposed || !IsVisible || _dragging || WindowState != WindowState.Normal)
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
