using System.Drawing;
using System.Reflection;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Builds the plain base icon; the phase badge is added in the next task.</summary>
internal static class TrayIconRenderer
{
    private static readonly Lazy<Icon> BaseIcon = new(LoadBaseIcon);

    public static RenderedTrayIcon Render(PricingPhase phase)
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawIcon(BaseIcon.Value, new Rectangle(0, 0, size.Width, size.Height));
        }
        return new RenderedTrayIcon(bitmap);
    }

    private static Icon LoadBaseIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("Embedded resource 'deepseek_256.ico' was not found.");
        return new Icon(stream);
    }
}
