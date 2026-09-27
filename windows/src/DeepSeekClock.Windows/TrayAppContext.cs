using System.Windows.Forms;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Owns the tray icon and keeps the application alive without a main window.</summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ClockTicker _ticker = new();
    private readonly PopupForm _popup = new();
    private RenderedTrayIcon? _currentIcon;
    private PricingPhase? _paintedPhase;

    public TrayAppContext()
    {
        _currentIcon = TrayIconRenderer.Render(PricingPhase.OffPeak);
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open", null, (_, _) => TogglePopup());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon.Icon,
            Text = "DeepSeek Clock",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TogglePopup();
        };
        _ticker.Tick += state => UpdateUi(state);
        _ticker.Start();
    }

    /// <summary>Formats the tooltip, clamped to the Shell tooltip limit.</summary>
    internal static string Tooltip(ClockState state)
    {
        var phase = state.Phase == PricingPhase.Peak ? "Peak pricing" : "Off-peak · 50% off";
        var text = $"{phase}\nChanges in {state.Countdown}";
        if (state.TransitionText is { } at)
            text += $" at {at}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    private void UpdateUi(ClockState state)
    {
        if (_paintedPhase != state.Phase)
        {
            var rendered = TrayIconRenderer.Render(state.Phase);
            var previous = _currentIcon;
            _notifyIcon.Icon = rendered.Icon;
            _currentIcon = rendered;
            _paintedPhase = state.Phase;
            previous?.Dispose();
        }
        _notifyIcon.Text = Tooltip(state);
        if (_popup.Visible)
            _popup.ShowState(state);
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else
        {
            _popup.ShowState(ClockState.From(DateTimeOffset.Now, TimeZoneInfo.Local));
            _popup.ShowNearCursor();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ticker.Dispose();
            _popup.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _currentIcon?.Dispose();
            _currentIcon = null;
        }
        base.Dispose(disposing);
    }
}
