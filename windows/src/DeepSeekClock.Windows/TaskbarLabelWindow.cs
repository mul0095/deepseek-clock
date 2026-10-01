using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeepSeekClock.Core;
using Microsoft.Win32;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace DeepSeekClock.Windows;

internal sealed class TaskbarLabelWindow : Window, IDisposable
{
    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly System.Windows.Shapes.Ellipse _dot = new() { Width = 5, Height = 5, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TaskbarPlacement.WinEventCallback _foregroundCallback;
    private IntPtr _hook;
    private IntPtr _handle;
    private bool _enabled;
    private bool _disposed;
    private Rectangle? _lastPosition;
    private SolidColorBrush _hover = new(MediaColor.FromArgb(24, 255, 255, 255));
    public Border LabelSurface { get; } = new() { CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 0, 9, 0), Margin = new Thickness(1) };

    public event EventHandler? OpenRequested;
    public event EventHandler? MenuRequested;

    public TaskbarLabelWindow()
    {
        Title = "DeepSeek Clock taskbar";
        Width = 172;
        Height = 32;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        UseLayoutRounding = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        row.Children.Add(_dot);
        row.Children.Add(_text);
        LabelSurface.Child = row;
        LabelSurface.Cursor = System.Windows.Input.Cursors.Hand;
        LabelSurface.Background = new SolidColorBrush(MediaColor.FromArgb(1, 0, 0, 0));
        LabelSurface.MouseEnter += (_, _) => LabelSurface.Background = _hover;
        LabelSurface.MouseLeave += (_, _) => LabelSurface.Background = new SolidColorBrush(MediaColor.FromArgb(1, 0, 0, 0));
        LabelSurface.MouseLeftButtonUp += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        LabelSurface.MouseRightButtonUp += (_, _) => MenuRequested?.Invoke(LabelSurface, EventArgs.Empty);
        Content = LabelSurface;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            TaskbarPlacement.Configure(_handle);
        };
        _timer.Tick += (_, _) => Reposition();
        _foregroundCallback = (_, _, _, _, _, _, _) =>
        {
            if (!_disposed)
                Dispatcher.BeginInvoke((Action)Reposition);
        };
        SystemEvents.UserPreferenceChanged += SystemThemeChanged;
        ApplySystemTheme();
    }

    public void ShowState(ClockState state)
    {
        _text.Text = $"DeepSeek {(state.HolidayCalendarAvailable ? "" : "~")}{state.Countdown}";
        _dot.Fill = new SolidColorBrush(state.Phase == PricingPhase.Peak ? MediaColor.FromRgb(232, 182, 109) : MediaColor.FromRgb(103, 201, 147));
        LabelSurface.ToolTip = TrayAppContext.Tooltip(state) + "\nClick to open · Right-click for options";
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;
        _enabled = enabled;
        if (enabled)
        {
            new WindowInteropHelper(this).EnsureHandle();
            _hook = TaskbarPlacement.WatchForeground(_foregroundCallback);
            _timer.Start();
            Reposition();
        }
        else
        {
            _timer.Stop();
            TaskbarPlacement.UnwatchForeground(_hook);
            _hook = IntPtr.Zero;
            _lastPosition = null;
            Hide();
        }
    }

    private void Reposition()
    {
        if (!_enabled || _disposed)
            return;
        if (!TaskbarPlacement.TryGetPosition(_handle, Width, Height, out var bounds))
        {
            _lastPosition = null;
            Hide();
            return;
        }
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        if (_lastPosition != bounds)
        {
            TaskbarPlacement.Move(_handle, bounds);
            _lastPosition = bounds;
        }
        Opacity = 1;
    }

    private void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!_disposed)
            Dispatcher.BeginInvoke((Action)ApplySystemTheme);
    }

    private void ApplySystemTheme()
    {
        if (_disposed)
            return;
        var light = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (UnauthorizedAccessException) { }
        _text.Foreground = new SolidColorBrush(light ? MediaColor.FromRgb(32, 33, 36) : MediaColor.FromRgb(242, 242, 242));
        _hover = new SolidColorBrush(light ? MediaColor.FromArgb(18, 0, 0, 0) : MediaColor.FromArgb(24, 255, 255, 255));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        TaskbarPlacement.UnwatchForeground(_hook);
        SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
        Close();
    }
}
