using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeepSeekClock.Core;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfButton = System.Windows.Controls.Button;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace DeepSeekClock.Windows;

public partial class PopupView : WpfUserControl
{
    private DeepSeekModel _selectedModel = DeepSeekModel.Flash;
    private PricingPhase _phase = PricingPhase.OffPeak;
    private bool _lightTheme = true;

    public event EventHandler? QuitRequested;
    public event EventHandler? PinRequested;
    public event EventHandler? OptionsRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? HideRequested;
    public event System.Windows.Input.MouseButtonEventHandler? DragRequested;

    internal void SetPinned(bool pinned)
    {
        PinButton.Foreground = (MediaBrush)Resources[pinned ? "SelectedTabBrush" : "MutedBrush"];
        PinButton.ToolTip = pinned ? "Return to tray popup" : "Keep window open";
        System.Windows.Automation.AutomationProperties.SetName(PinButton, pinned ? "Unpin window" : "Keep window open");
    }

    internal void CopyThemeTo(ResourceDictionary destination)
    {
        foreach (var key in Resources.Keys)
            if (Resources[key] is MediaBrush brush)
                destination[key] = brush;
    }

    public PopupView()
    {
        InitializeComponent();
        using var stream = typeof(PopupView).Assembly.GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("The brand icon is missing.");
        BrandIcon.Source = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
    }

    public void ShowState(ClockState state)
    {
        _phase = state.Phase;
        UpdatePhaseColors();
        Countdown.Text = state.Countdown;
        Transition.Text = state.TransitionText is { } time
            ? $"{(_phase == PricingPhase.Peak ? "Peak" : "Off-peak")} ends at {time}"
            : string.Empty;
        UpdateRates();
    }

    private void UpdatePhaseColors()
    {
        var isPeak = _phase == PricingPhase.Peak;
        StatusText.Text = isPeak ? "Peak" : "Off-peak";
        StatusPill.Background = isPeak
            ? Brush("#FFFAEBD3", "#FF4A3821")
            : Brush("#FFE4F2E9", "#FF274331");
        var phaseBrush = Brush(isPeak ? "#FF965B09" : "#FF187048", isPeak ? "#FFFFD37D" : "#FF6FDA9D");
        Eyebrow.Text = isPeak ? "Peak pricing" : "50% cheaper";
        Eyebrow.Foreground = phaseBrush;
        StatusText.Foreground = phaseBrush;
        StatusDot.Fill = phaseBrush;
    }

    public void SetTheme(bool light)
    {
        _lightTheme = light;
        var palette = light
            ? new Dictionary<string, string>
            {
                ["SurfaceBrush"] = "#FFF9FAFB", ["BorderBrush"] = "#FFE1E4E8", ["CardBrush"] = "#FFF1F3F5",
                ["CardBorderBrush"] = "#FFE7E9EC", ["TabSurfaceBrush"] = "#FFE5E8EC", ["TextBrush"] = "#FF202124",
                ["MutedBrush"] = "#FF717780", ["SelectedTabBrush"] = "#FF3478E5", ["SelectedTabTextBrush"] = "#FFFFFFFF",
                ["TabTextBrush"] = "#FF30343A", ["HoverBrush"] = "#FFDDE2E8", ["StatusBrush"] = "#FF187048",
                ["StatusSurfaceBrush"] = "#FFE4F2E9",
            }
            : new Dictionary<string, string>
            {
                ["SurfaceBrush"] = "#FF1F1F1F", ["BorderBrush"] = "#FF343434", ["CardBrush"] = "#FF292929",
                ["CardBorderBrush"] = "#FF343434", ["TabSurfaceBrush"] = "#FF252525", ["TextBrush"] = "#FFF2F2F2",
                ["MutedBrush"] = "#FFAAAAAA", ["SelectedTabBrush"] = "#FF3478E5", ["SelectedTabTextBrush"] = "#FFFFFFFF",
                ["TabTextBrush"] = "#FFE3E3E3", ["HoverBrush"] = "#FF414141", ["StatusBrush"] = "#FF6FDA9D",
                ["StatusSurfaceBrush"] = "#FF274331",
            };

        foreach (var (key, value) in palette)
            Resources[key] = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(value)!);
        UpdatePhaseColors();
        UpdateRates();
    }

    private SolidColorBrush Brush(string light, string dark) => new(
        (MediaColor)MediaColorConverter.ConvertFromString(_lightTheme ? light : dark)!);

    private void UpdateRates()
    {
        var pricing = DeepSeekPricing.Pricing(_selectedModel, _phase);
        CacheHit.Text = UsdPriceFormatter.Format(pricing.InputCacheHit);
        CacheMiss.Text = UsdPriceFormatter.Format(pricing.InputCacheMiss);
        Output.Text = UsdPriceFormatter.Format(pricing.Output);
        ApplyTabStyle(FlashTab, _selectedModel == DeepSeekModel.Flash);
        ApplyTabStyle(ProTab, _selectedModel == DeepSeekModel.Pro);
    }

    private void ApplyTabStyle(WpfButton button, bool selected)
    {
        button.Background = selected ? (MediaBrush)Resources["SelectedTabBrush"] : MediaBrushes.Transparent;
        button.Foreground = selected ? (MediaBrush)Resources["SelectedTabTextBrush"] : (MediaBrush)Resources["TabTextBrush"];
        button.BorderThickness = new Thickness(0);
        button.FontSize = 12;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        button.Cursor = System.Windows.Input.Cursors.Hand;
        button.Padding = new Thickness(6, 0, 6, 0);
    }

    private void FlashTab_OnClick(object sender, RoutedEventArgs e) { _selectedModel = DeepSeekModel.Flash; UpdateRates(); }
    private void Pin_OnClick(object sender, RoutedEventArgs e) => PinRequested?.Invoke(this, EventArgs.Empty);
    private void Options_OnClick(object sender, RoutedEventArgs e) => OptionsRequested?.Invoke(OptionsButton, EventArgs.Empty);
    private void Minimize_OnClick(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke(this, EventArgs.Empty);
    private void Hide_OnClick(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);

    private void Header_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase)
                return;
        DragRequested?.Invoke(this, e);
    }
    private void ProTab_OnClick(object sender, RoutedEventArgs e) { _selectedModel = DeepSeekModel.Pro; UpdateRates(); }
    private void Quit_OnClick(object sender, RoutedEventArgs e) => QuitRequested?.Invoke(this, EventArgs.Empty);
    private void Console_OnClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://platform.deepseek.com") { UseShellExecute = true });
}
