using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DeepSeekClock.Core;
using WpfMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace DeepSeekClock.Windows;

internal sealed class TrayAppContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;
    private readonly NotifyIcon _notifyIcon;
    private readonly WpfMenu _menu;
    private readonly ClockTicker _ticker = new();
    private readonly PopupWindow _popup;
    private readonly TaskbarLabelWindow _taskbar = new();
    private readonly OfficialPricingService _pricing = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly System.Windows.Forms.Timer _pricingRefreshTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private readonly PreferencesStore _store = new();
    private WidgetPreferences _preferences;
    private readonly WpfMenuItem _pinItem;
    private readonly WpfMenuItem _labelItem;
    private readonly WpfMenuItem _topItem;
    private readonly Dictionary<ThemePreference, WpfMenuItem> _themeItems = new();
    private RenderedTrayIcon? _currentIcon;
    private PricingPhase? _paintedPhase;
    private PricingSnapshot? _currentPricing;

    public TrayAppContext(bool showPopup = false)
    {
        _preferences = _store.Load();
        _popup = new PopupWindow(preview: showPopup);
        _currentPricing = _pricing.ReadCache();
        ApplyPricing(_currentPricing, live: false);
        _menu = new WpfMenu { Placement = PlacementMode.MousePoint };
        _menu.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DeepSeekClock;component/TrayMenu.xaml", UriKind.Relative),
        });
        _menu.Style = (Style)_menu.FindResource(typeof(WpfMenu));
        _menu.Items.Add(Item("Open DeepSeek Clock", "\uE8A7", OpenPopup));
        _labelItem = Item("Show timer on taskbar", "\uE950", null, checkable: true);
        _labelItem.Click += (_, _) => Dispatch(() => Save(_preferences with { TaskbarLabel = _labelItem.IsChecked }));
        _pinItem = Item("Keep window open", "\uE718", null, checkable: true);
        _pinItem.Click += (_, _) => Dispatch(() => Save(_preferences with { KeepOpen = _pinItem.IsChecked }));
        _topItem = Item("Always on top", "\uE74A", null, checkable: true);
        _topItem.Click += (_, _) => Dispatch(() => Save(_preferences with { AlwaysOnTop = _topItem.IsChecked }));
        _menu.Items.Add(_labelItem);
        _menu.Items.Add(_pinItem);
        _menu.Items.Add(_topItem);
        _menu.Items.Add(new Separator());
        var themes = Item("Appearance", "\uE790", null);
        foreach (var theme in Enum.GetValues<ThemePreference>())
        {
            var item = Item(theme == ThemePreference.System ? "Use Windows theme" : theme.ToString(), "", () => Save(_preferences with { Theme = theme }));
            _themeItems[theme] = item;
            themes.Items.Add(item);
        }
        _menu.Items.Add(themes);
        _menu.Items.Add(Item("Reset window position", "\uE81D", _popup.ResetPosition));
        _menu.Items.Add(Item("DeepSeek Console", "\uE8A7", () => Process.Start(new ProcessStartInfo("https://platform.deepseek.com") { UseShellExecute = true })));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("Quit", "\uE8BB", ExitThread));
        _menu.Opened += (_, _) => _popup.MenuOpen = true;
        _menu.Closed += (_, _) =>
        {
            _popup.MenuOpen = false;
            Dispatch(_popup.DismissIfInactive);
        };

        _currentIcon = TrayIconRenderer.Render(PricingPhase.OffPeak);
        _notifyIcon = new NotifyIcon { Icon = _currentIcon.Icon, Text = "DeepSeek Clock", Visible = true };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TogglePopup();
            else if (e.Button == MouseButtons.Right)
                ShowMenu(null);
        };
        _popup.QuitRequested += (_, _) => ExitThread();
        _popup.PinRequested += (_, _) => Save(_preferences with { KeepOpen = !_preferences.KeepOpen });
        _popup.OptionsRequested += (sender, _) => ShowMenu(sender as UIElement);
        _popup.PositionSaved += (_, _) => Save(_preferences with { Position = _popup.Position });
        _popup.ThemeChanged += (_, _) => _popup.CopyThemeTo(_menu.Resources);
        _taskbar.OpenRequested += (_, _) => TogglePopup();
        _taskbar.MenuRequested += (sender, _) => ShowMenu(sender as UIElement);
        _ticker.Tick += UpdateUi;
        _ticker.Start();
        _pricingRefreshTimer.Tick += (_, _) => _ = RefreshPricingAsync();
        System.Windows.Forms.Application.Idle += StartPricingOnIdle;
        ApplyPreferences();
        if (showPopup || _preferences.KeepOpen)
            System.Windows.Forms.Application.Idle += ShowPopupOnIdle;
    }

    private void Dispatch(Action action) => _popup.Dispatcher.BeginInvoke(action);

    private WpfMenuItem Item(string text, string glyph, Action? action, bool checkable = false)
    {
        var item = new WpfMenuItem { Header = text, IsCheckable = checkable };
        if (glyph.Length > 0)
            item.Icon = new TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
        if (action is not null)
            item.Click += (_, _) => Dispatch(action);
        return item;
    }

    private void ShowMenu(UIElement? anchor)
    {
        _menu.IsOpen = false;
        _popup.CopyThemeTo(_menu.Resources);
        _menu.PlacementTarget = anchor;
        _menu.Placement = anchor is null ? PlacementMode.MousePoint : anchor == _taskbar.LabelSurface ? PlacementMode.Top : PlacementMode.Bottom;
        _popup.MenuOpen = true;
        _menu.IsOpen = true;
    }

    private void Save(WidgetPreferences preferences)
    {
        _preferences = preferences;
        _store.Save(_preferences);
        ApplyPreferences();
    }

    private void ApplyPreferences()
    {
        _popup.ApplyPreferences(_preferences);
        _popup.CopyThemeTo(_menu.Resources);
        _pinItem.IsChecked = _preferences.KeepOpen;
        _labelItem.IsChecked = _preferences.TaskbarLabel;
        _topItem.IsChecked = _preferences.AlwaysOnTop;
        foreach (var (theme, item) in _themeItems)
            item.IsChecked = _preferences.Theme == theme;
        _taskbar.SetEnabled(_preferences.TaskbarLabel);
    }

    private void ShowPopupOnIdle(object? sender, EventArgs e)
    {
        System.Windows.Forms.Application.Idle -= ShowPopupOnIdle;
        OpenPopup();
    }

    private void StartPricingOnIdle(object? sender, EventArgs e)
    {
        System.Windows.Forms.Application.Idle -= StartPricingOnIdle;
        _pricingRefreshTimer.Start();
        _ = RefreshPricingAsync();
    }

    private async Task RefreshPricingAsync()
    {
        try
        {
            var snapshot = await _pricing.RefreshAsync(_shutdown.Token).ConfigureAwait(false);
            if (_shutdown.IsCancellationRequested || _popup.Dispatcher.HasShutdownStarted)
                return;
            _ = _popup.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (snapshot is not null)
                    _currentPricing = snapshot;
                ApplyPricing(_currentPricing, live: snapshot is not null);
            }));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning("Official prices unavailable: {0}", ex.Message);
            if (!_shutdown.IsCancellationRequested && !_popup.Dispatcher.HasShutdownStarted)
                _ = _popup.Dispatcher.BeginInvoke((Action)(() => ApplyPricing(_currentPricing, live: false)));
        }
    }

    private void ApplyPricing(PricingSnapshot? snapshot, bool live)
    {
        var source = snapshot is null ? "Bundled rates" :
            $"{(live ? "Official" : "Saved")} · {snapshot.UpdatedAt.ToLocalTime():g}";
        _popup.SetPricing(snapshot?.Catalog ?? PricingCatalog.Bundled, source);
    }

    internal static string Tooltip(ClockState state)
    {
        var phase = !state.HolidayCalendarAvailable ? "Estimated pricing · holiday calendar unavailable" :
            state.Phase == PricingPhase.Peak ? "Peak pricing" : "Off-peak · 50% off";
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
        _taskbar.ShowState(state);
        if (_popup.IsVisible)
            _popup.ShowState(state);
    }

    private void OpenPopup()
    {
        _popup.ShowState(ClockState.From(DateTimeOffset.Now, TimeZoneInfo.Local));
        _popup.ShowNearCursor();
    }

    private void TogglePopup()
    {
        if (_popup.IsVisible && _popup.IsActive && _popup.WindowState != WindowState.Minimized)
            _popup.Hide();
        else
            OpenPopup();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            System.Windows.Forms.Application.Idle -= ShowPopupOnIdle;
            System.Windows.Forms.Application.Idle -= StartPricingOnIdle;
            _shutdown.Cancel();
            _pricingRefreshTimer.Dispose();
            _pricing.Dispose();
            _shutdown.Dispose();
            _ticker.Dispose();
            _menu.IsOpen = false;
            _taskbar.Dispose();
            _popup.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _currentIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
