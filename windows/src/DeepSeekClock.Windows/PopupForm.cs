using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using Microsoft.Win32;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>A compact, system-themed tray window with pricing and countdown.</summary>
internal sealed class PopupForm : Form
{
    private static readonly string ThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Font _brandFont = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font _eyebrowFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _countdownFont = new("Segoe UI", 34f, FontStyle.Regular);
    private readonly Font _smallFont = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly Font _cardTitleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _metricFont = new("Segoe UI", 9f, FontStyle.Regular);
    private readonly Font _priceFont = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly Font _tabFont = new("Segoe UI", 9f, FontStyle.Regular);

    private readonly PictureBox _brandIcon = new() { Size = new Size(24, 24), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
    private readonly Label _brandTitle = new() { Text = "DeepSeek Clock", AutoSize = true };
    private readonly PillLabel _status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _eyebrow = new() { AutoSize = false, Height = 21 };
    private readonly SingleLineLabel _countdown = new() { Height = 58 };
    private readonly Label _transition = new() { AutoSize = false, Height = 21, AutoEllipsis = true };
    private readonly Button _flashTab = new() { Text = "Flash", Width = 76, Height = 28, FlatStyle = FlatStyle.Flat };
    private readonly Button _proTab = new() { Text = "V4 Pro", Width = 82, Height = 28, FlatStyle = FlatStyle.Flat };
    private readonly Label _cacheHit = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _cacheMiss = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _output = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly List<Label> _captions = new();
    private readonly Label _rateHeading = new() { Text = "Current rates", AutoSize = true };
    private readonly Label _units = new() { Text = "USD / 1M tokens", AutoSize = true };
    private readonly LinkLabel _consoleLink = new() { Text = "DeepSeek Console  ↗", AutoSize = true };
    private readonly LinkLabel _quitLink = new() { Text = "Quit", AutoSize = true };
    private readonly TableLayoutPanel _header = new() { ColumnCount = 2, RowCount = 1 };
    private readonly TableLayoutPanel _brandLayout = new() { ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
    private readonly TableLayoutPanel _rates = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
    private readonly RoundedPanel _rateCard = new(ThemeColors.Light.Card, 15);
    private readonly RoundedPanel _tabSurface = new(ThemeColors.Light.TabSurface, 10) { Dock = DockStyle.Fill };
    private readonly RoundedPanel _contentSurface = new(ThemeColors.Light.Surface, 22);
    private readonly FlowLayoutPanel _content = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly TableLayoutPanel _footer = new() { Height = 38, ColumnCount = 2, RowCount = 1 };
    private readonly Bitmap _brandBitmap;

    private ThemeColors _theme = ThemeColors.Light;
    private PricingPhase _phase = PricingPhase.OffPeak;
    private DeepSeekModel _selectedModel = DeepSeekModel.Flash;

    public event EventHandler? QuitRequested;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(380, 470);
        MinimumSize = MaximumSize = Size;
        DoubleBuffered = true;
        Padding = Padding.Empty;
        ApplyRoundedRegion();

        _brandBitmap = LoadBrandBitmap();
        BuildHeader();
        BuildRateCard();
        BuildLayout();
        _flashTab.Click += (_, _) => SelectModel(DeepSeekModel.Flash);
        _proTab.Click += (_, _) => SelectModel(DeepSeekModel.Pro);
        _consoleLink.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://platform.deepseek.com") { UseShellExecute = true });
        _quitLink.LinkClicked += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyTheme();
        UpdateRates();
        LayoutContent();
    }

    public void ShowState(ClockState state)
    {
        _phase = state.Phase;
        var isPeak = state.Phase == PricingPhase.Peak;
        _status.Text = isPeak ? "Peak" : "Off-peak";
        _status.ForeColor = isPeak ? _theme.PeakText : _theme.OffPeakText;
        _status.BackColor = isPeak ? _theme.PeakSurface : _theme.OffPeakSurface;
        _eyebrow.Text = isPeak ? "Peak pricing" : "50% cheaper";
        _eyebrow.ForeColor = isPeak ? _theme.PeakText : _theme.OffPeakText;
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
        LayoutContent();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPanel.RoundedRectangle(ClientRectangle, 22);
        using var pen = new Pen(_theme.Border);
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
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            foreach (var font in new[] { _brandFont, _eyebrowFont, _countdownFont, _smallFont, _cardTitleFont, _metricFont, _priceFont, _tabFont })
                font.Dispose();
            _brandBitmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildHeader()
    {
        _brandIcon.Image = _brandBitmap;
        _brandTitle.Font = _brandFont;
        _brandTitle.Anchor = AnchorStyles.Left;

        _brandLayout.Dock = DockStyle.Fill;
        _brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 31));
        _brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _brandLayout.Controls.Add(_brandIcon, 0, 0);
        _brandLayout.Controls.Add(_brandTitle, 1, 0);

        _status.Font = _smallFont;
        _status.Size = new Size(94, 29);
        _status.Anchor = AnchorStyles.Right;
        _header.Location = new Point(20, 16);
        _header.Size = new Size(340, 38);
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        _header.Controls.Add(_brandLayout, 0, 0);
        _header.Controls.Add(_status, 1, 0);
        Controls.Add(_header);

        _eyebrow.Font = _eyebrowFont;
        _countdown.Font = _countdownFont;
        _transition.Font = _smallFont;
    }

    private void BuildRateCard()
    {
        _rateCard.Size = new Size(340, 204);
        _rateCard.Padding = new Padding(12, 10, 12, 10);
        _rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        _rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        _rates.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        _rates.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        for (var i = 0; i < 3; i++)
            _rates.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));

        _rateHeading.Font = _cardTitleFont;
        _units.Font = _smallFont;
        _units.Anchor = AnchorStyles.Right;
        _rates.Controls.Add(_rateHeading, 0, 0);
        _rates.Controls.Add(_units, 1, 0);

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
        var label = new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = _metricFont, AutoEllipsis = true };
        value.Font = _priceFont;
        _rates.Controls.Add(label, 0, row);
        _rates.Controls.Add(value, 1, row);
        _captions.Add(label);
    }

    private void BuildLayout()
    {
        _content.Location = new Point(20, 65);
        _content.Size = new Size(340, 385);
        _content.AutoScroll = false;
        _content.Margin = Padding.Empty;
        _content.Padding = Padding.Empty;
        _contentSurface.Location = Point.Empty;
        _contentSurface.Size = _content.Size;
        _contentSurface.Padding = Padding.Empty;

        _eyebrow.Margin = new Padding(0, 0, 0, 1);
        _countdown.Margin = new Padding(0, 0, 0, 0);
        _transition.Margin = new Padding(0, 0, 0, 12);
        _rateCard.Margin = new Padding(0, 0, 0, 10);

        _content.Controls.Add(_eyebrow);
        _content.Controls.Add(_countdown);
        _content.Controls.Add(_transition);
        _content.Controls.Add(_rateCard);
        _content.Controls.Add(BuildFooter());
        Controls.Add(_contentSurface);
        Controls.Add(_content);
    }

    private Control BuildFooter()
    {
        _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        _footer.Padding = new Padding(0, 8, 0, 0);
        _footer.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
        };

        foreach (var link in new[] { _consoleLink, _quitLink })
        {
            link.Font = _smallFont;
            link.LinkBehavior = LinkBehavior.NeverUnderline;
            link.Anchor = link == _consoleLink ? AnchorStyles.Left : AnchorStyles.Right;
        }
        _footer.Controls.Add(_consoleLink, 0, 0);
        _footer.Controls.Add(_quitLink, 1, 0);
        return _footer;
    }

    private void LayoutContent()
    {
        if (_content is null || _header is null || _rateCard is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;
        var contentWidth = Math.Max(240, ClientSize.Width - 40);
        _header.Location = new Point(20, 16);
        _header.Size = new Size(contentWidth, 38);
        _content.Location = new Point(20, 65);
        _content.Size = new Size(contentWidth, Math.Max(250, ClientSize.Height - 80));
        _contentSurface.Location = _content.Location;
        _contentSurface.Size = _content.Size;
        _rateCard.Width = contentWidth;
        _countdown.Width = contentWidth;
        _transition.Width = contentWidth;
        _footer.Width = contentWidth;
        _rates.PerformLayout();
        _content.PerformLayout();
    }

    private void StyleTab(Button button)
    {
        button.Font = _tabFont;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = _theme.TabHover;
        var selected = _selectedModel == (button == _flashTab ? DeepSeekModel.Flash : DeepSeekModel.Pro);
        button.BackColor = selected ? _theme.Accent : _theme.TabSurface;
        button.ForeColor = selected ? _theme.AccentText : _theme.Text;
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

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (IsDisposed || Disposing)
            return;
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke((Action)ApplyTheme); }
            catch (InvalidOperationException) { }
        }
        else
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        _theme = ReadLightTheme() ? ThemeColors.Light : ThemeColors.Dark;
        BackColor = _theme.Surface;
        _content.BackColor = _theme.Surface;
        _contentSurface.SetSurface(_theme.Surface, _theme.Border);
        _rateCard.SetSurface(_theme.Card, _theme.Border);
        _tabSurface.SetSurface(_theme.TabSurface, _theme.Border);
        _rates.BackColor = _theme.Card;
        _header.BackColor = _theme.Surface;
        _brandLayout.BackColor = _theme.Surface;
        _brandIcon.BackColor = _theme.Surface;
        _footer.BackColor = _theme.Surface;
        _brandTitle.ForeColor = _theme.Text;
        _rateHeading.ForeColor = _theme.Text;
        _units.ForeColor = _theme.Muted;
        _countdown.ForeColor = _theme.Text;
        _transition.ForeColor = _theme.Muted;
        _consoleLink.LinkColor = _theme.Muted;
        _consoleLink.ActiveLinkColor = _theme.Accent;
        _consoleLink.VisitedLinkColor = _theme.Muted;
        _quitLink.LinkColor = _theme.Muted;
        _quitLink.ActiveLinkColor = _theme.Accent;
        _quitLink.VisitedLinkColor = _theme.Muted;
        foreach (var caption in _captions)
            caption.ForeColor = _theme.Muted;
        foreach (var price in new[] { _cacheHit, _cacheMiss, _output })
            price.ForeColor = _theme.Text;
        _status.ForeColor = _phase == PricingPhase.Peak ? _theme.PeakText : _theme.OffPeakText;
        _status.BackColor = _phase == PricingPhase.Peak ? _theme.PeakSurface : _theme.OffPeakSurface;
        _eyebrow.ForeColor = _phase == PricingPhase.Peak ? _theme.PeakText : _theme.OffPeakText;
        StyleTab(_flashTab);
        StyleTab(_proTab);
        Invalidate(true);
    }

    private static bool ReadLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeRegistryPath);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
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

    private sealed record ThemeColors(Color Surface, Color Card, Color TabSurface, Color TabHover,
        Color Text, Color Muted, Color Border, Color Accent, Color AccentText,
        Color OffPeakText, Color OffPeakSurface, Color PeakText, Color PeakSurface)
    {
        public static ThemeColors Light { get; } = new(
            SystemColors.Control, SystemColors.Window, Color.FromArgb(232, 232, 232), Color.FromArgb(220, 220, 220),
            SystemColors.ControlText, SystemColors.GrayText, SystemColors.ControlDark,
            SystemColors.Highlight, SystemColors.HighlightText,
            Color.FromArgb(24, 112, 72), Color.FromArgb(225, 241, 229),
            Color.FromArgb(150, 91, 9), Color.FromArgb(250, 235, 211));

        public static ThemeColors Dark { get; } = new(
            Color.FromArgb(32, 32, 32), Color.FromArgb(43, 43, 43), Color.FromArgb(58, 58, 58), Color.FromArgb(72, 72, 72),
            Color.FromArgb(244, 244, 244), Color.FromArgb(190, 190, 190), Color.FromArgb(77, 77, 77),
            Color.FromArgb(96, 205, 255), Color.FromArgb(0, 39, 58),
            Color.FromArgb(111, 218, 157), Color.FromArgb(39, 67, 49),
            Color.FromArgb(255, 211, 125), Color.FromArgb(74, 56, 33));
    }
}

internal sealed class RoundedPanel : Panel
{
    private Color _surface;
    private Color _border = SystemColors.ControlDark;
    private readonly int _radius;

    public RoundedPanel(Color surface, int radius)
    {
        _surface = surface;
        _radius = radius;
        BackColor = surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ApplyRegion();
    }

    public void SetSurface(Color surface, Color border)
    {
        _surface = surface;
        _border = border;
        BackColor = surface;
        Invalidate();
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
        using var pen = new Pen(_border);
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

internal sealed class SingleLineLabel : Label
{
    public SingleLineLabel()
    {
        AutoSize = false;
        AutoEllipsis = true;
        UseCompatibleTextRendering = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }
}
