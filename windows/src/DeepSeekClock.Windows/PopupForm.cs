using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>A compact, themed tray window with the same information hierarchy as the macOS popover.</summary>
internal sealed class PopupForm : Form
{
    private static readonly Color Surface = Color.FromArgb(250, 238, 249);
    private static readonly Color CardSurface = Color.FromArgb(242, 220, 241);
    private static readonly Color Ink = Color.FromArgb(42, 31, 48);
    private static readonly Color MutedInk = Color.FromArgb(117, 99, 121);
    private static readonly Color Accent = Color.FromArgb(113, 72, 207);
    private static readonly Color OffPeakInk = Color.FromArgb(27, 119, 83);
    private static readonly Color PeakInk = Color.FromArgb(166, 99, 14);

    private readonly Font _brandFont = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font _eyebrowFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _countdownFont = new("Segoe UI", 38f, FontStyle.Regular);
    private readonly Font _smallFont = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly Font _cardTitleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _metricFont = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly Font _priceFont = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly Font _tabFont = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly PictureBox _brandIcon = new() { Size = new Size(24, 24), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly PillLabel _status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _eyebrow = new() { AutoSize = true };
    private readonly Label _countdown = new() { AutoSize = true, UseCompatibleTextRendering = true };
    private readonly Label _transition = new() { AutoSize = true, AutoEllipsis = true };
    private readonly Button _flashTab = new() { Text = "Flash", Width = 76, Height = 28, FlatStyle = FlatStyle.Flat };
    private readonly Button _proTab = new() { Text = "V4 Pro", Width = 82, Height = 28, FlatStyle = FlatStyle.Flat };
    private readonly Label _cacheHit = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _cacheMiss = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _output = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly TableLayoutPanel _rates = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = CardSurface };
    private readonly RoundedPanel _rateCard = new(CardSurface, 15);
    private readonly RoundedPanel _tabSurface = new(Color.FromArgb(232, 204, 231), 10) { Dock = DockStyle.Fill };
    private readonly Bitmap _brandBitmap;
    private PricingPhase _phase;
    private DeepSeekModel _selectedModel = DeepSeekModel.Flash;

    public event EventHandler? QuitRequested;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(350, 438);
        BackColor = Surface;
        Padding = new Padding(20, 17, 20, 16);
        DoubleBuffered = true;
        ApplyRoundedRegion();

        _brandBitmap = LoadBrandBitmap();
        BuildHeader();
        BuildRateCard();
        BuildLayout();
        _flashTab.Click += (_, _) => SelectModel(DeepSeekModel.Flash);
        _proTab.Click += (_, _) => SelectModel(DeepSeekModel.Pro);
    }

    public void ShowState(ClockState state)
    {
        _phase = state.Phase;
        var isPeak = state.Phase == PricingPhase.Peak;
        _status.Text = isPeak ? "Peak" : "Off-peak";
        _status.ForeColor = isPeak ? PeakInk : OffPeakInk;
        _status.BackColor = isPeak ? Color.FromArgb(251, 228, 193) : Color.FromArgb(222, 239, 226);
        _eyebrow.Text = isPeak ? "Peak pricing" : "50% cheaper";
        _eyebrow.ForeColor = isPeak ? PeakInk : OffPeakInk;
        _countdown.Text = state.Countdown;
        _transition.Text = state.TransitionText is { } time
            ? $"{(isPeak ? "Peak" : "Off-peak")} ends at {time}"
            : string.Empty;
        UpdateRates();
    }

    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var area = Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Max(area.Left, Math.Min(cursor.X, area.Right - Width));
        Top = Math.Max(area.Top, Math.Min(cursor.Y, area.Bottom - Height));
        Show();
        BringToFront();
        Activate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CsDropShadow = 0x00020000;
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPanel.RoundedRectangle(ClientRectangle, 22);
        using var pen = new Pen(Color.FromArgb(226, 194, 225), 1f);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var font in new[] { _brandFont, _eyebrowFont, _countdownFont, _smallFont, _cardTitleFont, _metricFont, _priceFont, _tabFont })
                font.Dispose();
            _brandBitmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildHeader()
    {
        _brandIcon.Image = _brandBitmap;
        var title = new Label { Text = "DeepSeek Clock", AutoSize = true, Font = _brandFont, ForeColor = Ink, Anchor = AnchorStyles.Left };
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Surface };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 31));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.Controls.Add(_brandIcon, 0, 0);
        brand.Controls.Add(title, 1, 0);

        _status.Font = _smallFont;
        _status.AutoSize = false;
        _status.Size = new Size(94, 28);
        _status.Anchor = AnchorStyles.Right;
        var header = new TableLayoutPanel { Location = new Point(20, 17), Width = 310, Height = 34, ColumnCount = 2, RowCount = 1, BackColor = Surface };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        header.Controls.Add(brand, 0, 0);
        header.Controls.Add(_status, 1, 0);
        Controls.Add(header);

        _eyebrow.Font = _eyebrowFont;
        _countdown.Font = _countdownFont;
        _countdown.ForeColor = Ink;
        _transition.Font = _smallFont;
        _transition.ForeColor = MutedInk;
    }

    private void BuildRateCard()
    {
        _rateCard.Location = new Point(0, 0);
        _rateCard.Size = new Size(310, 190);
        _rateCard.Padding = new Padding(12, 10, 12, 10);
        _rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        _rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        _rates.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        _rates.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        for (var i = 0; i < 3; i++)
            _rates.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));

        var heading = new Label { Text = "Current rates", AutoSize = true, Font = _cardTitleFont, ForeColor = Ink, Anchor = AnchorStyles.Left };
        var units = new Label { Text = "USD / 1M tokens", AutoSize = true, Font = _smallFont, ForeColor = MutedInk, Anchor = AnchorStyles.Right };
        _rates.Controls.Add(heading, 0, 0);
        _rates.Controls.Add(units, 1, 0);

        _tabSurface.Padding = new Padding(3, 2, 3, 2);
        var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        StyleTab(_flashTab);
        StyleTab(_proTab);
        tabs.Controls.Add(_flashTab);
        tabs.Controls.Add(_proTab);
        _rates.Controls.Add(_tabSurface, 0, 1);
        _rates.SetColumnSpan(_tabSurface, 2);
        _tabSurface.Controls.Add(tabs);

        AddRateRow("Input · cache hit", _cacheHit, 2);
        AddRateRow("Input · cache miss", _cacheMiss, 3);
        AddRateRow("Output", _output, 4);
        _rateCard.Controls.Add(_rates);
    }

    private void AddRateRow(string caption, Label value, int row)
    {
        var label = new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = _metricFont, ForeColor = MutedInk, AutoEllipsis = true };
        value.Font = _priceFont;
        value.ForeColor = Ink;
        _rates.Controls.Add(label, 0, row);
        _rates.Controls.Add(value, 1, row);
    }

    private void BuildLayout()
    {
        var content = new FlowLayoutPanel
        {
            Location = new Point(20, 57),
            Size = new Size(310, 346),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        _eyebrow.Margin = new Padding(0, 0, 0, 1);
        _countdown.Margin = new Padding(0, -2, 0, 0);
        _transition.Margin = new Padding(0, 0, 0, 12);
        _rateCard.Margin = new Padding(0, 0, 0, 11);
        content.Controls.Add(_eyebrow);
        content.Controls.Add(_countdown);
        content.Controls.Add(_transition);
        content.Controls.Add(_rateCard);
        content.Controls.Add(BuildFooter());
        Controls.Add(content);
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Width = 310, Height = 36, ColumnCount = 2, RowCount = 1, BackColor = Surface, Padding = new Padding(0, 8, 0, 0) };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 194, 225));
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        var console = new LinkLabel
        {
            Text = "DeepSeek Console  ↗",
            AutoSize = true,
            Font = _smallFont,
            LinkColor = MutedInk,
            ActiveLinkColor = Accent,
            VisitedLinkColor = MutedInk,
            LinkBehavior = LinkBehavior.NeverUnderline,
            Anchor = AnchorStyles.Left,
        };
        console.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://platform.deepseek.com") { UseShellExecute = true });
        var quit = new LinkLabel
        {
            Text = "Quit",
            AutoSize = true,
            Font = _smallFont,
            LinkColor = MutedInk,
            ActiveLinkColor = Accent,
            VisitedLinkColor = MutedInk,
            LinkBehavior = LinkBehavior.NeverUnderline,
            Anchor = AnchorStyles.Right,
        };
        quit.LinkClicked += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);
        footer.Controls.Add(console, 0, 0);
        footer.Controls.Add(quit, 1, 0);
        return footer;
    }

    private void StyleTab(Button button)
    {
        button.Font = _tabFont;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(218, 190, 237);
        button.BackColor = _selectedModel == (button == _flashTab ? DeepSeekModel.Flash : DeepSeekModel.Pro) ? Accent : Color.Transparent;
        button.ForeColor = _selectedModel == (button == _flashTab ? DeepSeekModel.Flash : DeepSeekModel.Pro) ? Color.White : Ink;
        button.Cursor = Cursors.Hand;
    }

    private void SelectModel(DeepSeekModel model)
    {
        _selectedModel = model;
        StyleTab(_flashTab);
        StyleTab(_proTab);
        UpdateRates();
    }

    private void UpdateRates()
    {
        var pricing = DeepSeekPricing.Pricing(_selectedModel, _phase);
        _cacheHit.Text = UsdPriceFormatter.Format(pricing.InputCacheHit);
        _cacheMiss.Text = UsdPriceFormatter.Format(pricing.InputCacheMiss);
        _output.Text = UsdPriceFormatter.Format(pricing.Output);
    }

    private void ApplyRoundedRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;
        var previous = Region;
        Region = new Region(RoundedPanel.RoundedRectangle(new Rectangle(Point.Empty, ClientSize), 22));
        previous?.Dispose();
    }

    private static Bitmap LoadBrandBitmap()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("Embedded resource 'deepseek_256.ico' was not found.");
        using var icon = new Icon(stream);
        return icon.ToBitmap();
    }
}

internal sealed class RoundedPanel : Panel
{
    private readonly Color _surface;
    private readonly int _radius;

    public RoundedPanel(Color surface, int radius)
    {
        _surface = surface;
        _radius = radius;
        BackColor = surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ApplyRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRectangle(ClientRectangle, _radius);
        using var brush = new SolidBrush(_surface);
        using var pen = new Pen(Color.FromArgb(229, 202, 229));
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Region?.Dispose();
        base.Dispose(disposing);
    }

    private void ApplyRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;
        var old = Region;
        Region = new Region(RoundedRectangle(ClientRectangle, _radius));
        old?.Dispose();
    }

    internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class PillLabel : Label
{
    public PillLabel()
    {
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPanel.RoundedRectangle(bounds, Math.Max(1, Height / 2));
        using var brush = new SolidBrush(BackColor == Color.Transparent ? Parent?.BackColor ?? SystemColors.Control : BackColor);
        e.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
