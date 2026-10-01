using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Builds the tray icon with a colored phase badge.</summary>
internal static class TrayIconRenderer
{
    private static readonly Lazy<Icon> BaseIcon = new(LoadBaseIcon);

    public static RenderedTrayIcon Render(PricingPhase phase)
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        DrawBase(graphics, size);
        try
        {
            DrawBadge(graphics, phase, size);
        }
        catch
        {
            graphics.Clear(Color.Transparent);
            DrawBase(graphics, size);
        }
        return new RenderedTrayIcon(bitmap);
    }

    private static void DrawBase(Graphics graphics, Size size)
        => graphics.DrawIcon(BaseIcon.Value, new Rectangle(0, 0, size.Width, size.Height));

    private static void DrawBadge(Graphics graphics, PricingPhase phase, Size size)
    {
        var color = phase == PricingPhase.Peak
            ? Color.FromArgb(255, 176, 32)
            : Color.FromArgb(42, 178, 110);
        var diameter = Math.Max(6, size.Width / 2);
        var rect = new Rectangle(size.Width - diameter, size.Height - diameter, diameter - 1, diameter - 1);
        using var fill = new SolidBrush(color);
        using var outline = new Pen(Color.FromArgb(200, 20, 20, 20), 1f);
        graphics.FillEllipse(fill, rect);
        graphics.DrawEllipse(outline, rect);
    }

    private static Icon LoadBaseIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("Embedded resource 'deepseek_256.ico' was not found.");
        return new Icon(stream);
    }
}
