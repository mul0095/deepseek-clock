using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Recomputes display state on the UI thread once per second.</summary>
internal sealed class ClockTicker : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly TimeZoneInfo _displayZone = TimeZoneInfo.Local;

    public event Action<ClockState>? Tick;

    public ClockTicker() => _timer.Tick += (_, _) => Raise();

    public void Start()
    {
        Raise();
        _timer.Start();
    }

    private void Raise() => Tick?.Invoke(ClockState.From(DateTimeOffset.Now, _displayZone));

    public void Dispose() => _timer.Dispose();
}
