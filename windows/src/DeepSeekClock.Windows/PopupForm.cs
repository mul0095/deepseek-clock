using System.Drawing;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Borderless panel showing phase, countdown, and current rates.</summary>
internal sealed class PopupForm : Form
{
    private const int RowWidth = 236;
    private readonly Font _titleFont = new(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold);
    private readonly Label _phase = new() { AutoSize = true };
    private readonly Label _countdown = new() { AutoSize = true };
    private readonly Label _transition = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly FlowLayoutPanel _content = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };
    private readonly List<(Label Label, DeepSeekModel Model, Meter Kind)> _priceLabels = new();

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        BackColor = SystemColors.Window;
        _phase.Font = _titleFont;
        Controls.Add(_content);
        _content.Controls.Add(_phase);
        _content.Controls.Add(_countdown);
        _content.Controls.Add(_transition);
        foreach (var model in new[] { DeepSeekModel.Flash, DeepSeekModel.Pro })
            AddModel(model);
    }

    public void ShowState(ClockState state)
    {
        var peak = state.Phase == PricingPhase.Peak;
        _phase.Text = peak ? "Peak pricing" : "Off-peak · 50% off";
        _phase.ForeColor = peak ? Color.FromArgb(176, 112, 0) : Color.FromArgb(30, 140, 84);
        _countdown.Text = $"Price changes in {state.Countdown}";
        _transition.Text = state.TransitionText is { } at ? $"at {at}" : " ";
        foreach (var (label, model, kind) in _priceLabels)
        {
            var pricing = DeepSeekPricing.Pricing(model, state.Phase);
            label.Text = UsdPriceFormatter.Format(kind switch
            {
                Meter.CacheHit => pricing.InputCacheHit,
                Meter.CacheMiss => pricing.InputCacheMiss,
                _ => pricing.Output,
            });
        }
    }

    public void ShowNearCursor()
    {
        Show();
        PerformLayout();
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Max(screen.Left, Math.Min(cursor.X, screen.Right - Width));
        Top = Math.Max(screen.Top, Math.Min(cursor.Y, screen.Bottom - Height));
        BringToFront();
        Activate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _titleFont.Dispose();
        base.Dispose(disposing);
    }

    private void AddModel(DeepSeekModel model)
    {
        _content.Controls.Add(new Label
        {
            Text = model.DisplayName(),
            AutoSize = true,
            Font = _titleFont,
            Margin = new Padding(3, 10, 3, 2),
        });
        foreach (var meter in Meters)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = RowWidth,
                Margin = new Padding(3, 0, 3, 0),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            var price = new Label { Text = string.Empty, AutoSize = true, Anchor = AnchorStyles.Right };
            row.Controls.Add(new Label { Text = meter.Label, AutoSize = true }, 0, 0);
            row.Controls.Add(price, 1, 0);
            _content.Controls.Add(row);
            _priceLabels.Add((price, model, meter.Kind));
        }
    }

    private enum Meter { CacheHit, CacheMiss, Output }

    private static readonly (string Label, Meter Kind)[] Meters =
    {
        ("Input · cache hit", Meter.CacheHit),
        ("Input · cache miss", Meter.CacheMiss),
        ("Output", Meter.Output),
    };
}
