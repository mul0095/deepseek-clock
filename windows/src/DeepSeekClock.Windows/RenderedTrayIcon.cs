using System.Drawing;
using System.Runtime.InteropServices;

namespace DeepSeekClock.Windows;

/// <summary>Owns the HICON created from a rendered bitmap.</summary>
internal sealed class RenderedTrayIcon : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;

    public RenderedTrayIcon(Bitmap bitmap)
    {
        _handle = bitmap.GetHicon();
        try
        {
            Icon = Icon.FromHandle(_handle);
        }
        catch
        {
            DestroyIcon(_handle);
            throw;
        }
    }

    public Icon Icon { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Icon.Dispose();
        DestroyIcon(_handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint handle);
}
