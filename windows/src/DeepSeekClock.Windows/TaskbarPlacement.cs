using System.Runtime.InteropServices;
using System.Text;

namespace DeepSeekClock.Windows;

/// <summary>Positions a non-activating overlay in physical taskbar coordinates.</summary>
internal static class TaskbarPlacement
{
    public static void Configure(IntPtr handle)
    {
        const int extendedStyle = -20;
        const long toolWindowNoActivate = 0x80L | 0x08000000L;
        SetWindowLongPtr(handle, extendedStyle, new IntPtr(GetWindowLongPtr(handle, extendedStyle).ToInt64() | toolWindowNoActivate));
    }

    public static bool TryGetPosition(IntPtr handle, double logicalWidth, double logicalHeight, out Rectangle position)
    {
        position = default;
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !IsWindowVisible(taskbar) || !GetWindowRect(taskbar, out var native))
            return false;
        var bar = native.Rectangle;
        var monitor = Screen.FromHandle(taskbar).Bounds;
        if (ForegroundCovers(monitor))
            return false;
        var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var trayNative))
            return false;
        var scale = Math.Max(96, GetDpiForWindow(taskbar)) / 96d;
        var width = (int)Math.Ceiling(logicalWidth * scale);
        var height = (int)Math.Ceiling(logicalHeight * scale);
        var horizontal = bar.Width >= bar.Height;
        var visible = Rectangle.Intersect(bar, monitor);
        // An auto-hidden taskbar only leaves a thin reveal strip on screen.
        if (horizontal ? visible.Height < height : visible.Width < Math.Min(width, bar.Width))
            return false;
        if (!horizontal)
            width = Math.Min(width, bar.Width);
        var peers = new List<Rectangle>();
        EnumWindows((window, _) =>
        {
            // Codex Usage Widget and similar notification-area overlays are owned by the taskbar.
            if (window != handle && GetWindow(window, 4) == taskbar && IsWindowVisible(window) &&
                GetWindowRect(window, out var bounds) && bar.Contains(bounds.Rectangle))
                peers.Add(bounds.Rectangle);
            return true;
        }, IntPtr.Zero);
        position = FindSlot(bar, trayNative.Rectangle, new Size(width, height), peers, (int)Math.Ceiling(4 * scale));
        return bar.Contains(position);
    }

    internal static Rectangle FindSlot(Rectangle bar, Rectangle tray, Size size, IEnumerable<Rectangle> peers, int gap)
    {
        var horizontal = bar.Width >= bar.Height;
        var slot = horizontal
            ? new Rectangle(tray.Left - size.Width - gap, bar.Top + (bar.Height - size.Height) / 2, size.Width, size.Height)
            : new Rectangle(bar.Left + (bar.Width - size.Width) / 2, tray.Top - size.Height - gap, size.Width, size.Height);
        foreach (var peer in peers.OrderByDescending(r => horizontal ? r.Right : r.Bottom))
            if (slot.IntersectsWith(peer))
                slot = horizontal ? slot with { X = peer.Left - size.Width - gap } : slot with { Y = peer.Top - size.Height - gap };
        return slot;
    }

    public static void Move(IntPtr handle, Rectangle bounds) =>
        SetWindowPos(handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0010);

    private static bool ForegroundCovers(Rectangle monitor)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || IsIconic(foreground))
            return false;
        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == Environment.ProcessId)
            return false;
        var name = new StringBuilder(128);
        GetClassName(foreground, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;
        if (DwmGetWindowAttribute(foreground, 9, out var rect, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(foreground, out rect))
            return false;
        return rect.Left <= monitor.Left + 1 && rect.Top <= monitor.Top + 1 && rect.Right >= monitor.Right - 1 && rect.Bottom >= monitor.Bottom - 1;
    }

    internal delegate void WinEventCallback(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint threadId, uint time);
    internal static IntPtr WatchForeground(WinEventCallback callback) => SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 2);
    internal static void UnwatchForeground(IntPtr hook) { if (hook != IntPtr.Zero) UnhookWinEvent(hook); }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle, uint attribute, out NativeRect value, int size);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}
