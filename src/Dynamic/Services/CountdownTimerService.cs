using System.Diagnostics;

namespace Dynamic.Services;

public sealed class CountdownTimerService
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _remaining = TimeSpan.FromMinutes(5);
    private bool _completionRaised;

    public TimeSpan Remaining => _stopwatch.IsRunning
        ? TimeSpan.FromTicks(Math.Max(0, _remaining.Ticks - _stopwatch.Elapsed.Ticks))
        : _remaining;

    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(5);
    public bool IsRunning => _stopwatch.IsRunning;

    public void Start()
    {
        if (_remaining <= TimeSpan.Zero || IsRunning) return;
        _completionRaised = false;
        _stopwatch.Restart();
    }

    public void Pause()
    {
        if (!IsRunning) return;
        _remaining = Remaining;
        _stopwatch.Stop();
    }

    public void Reset(TimeSpan? duration = null)
    {
        _stopwatch.Reset();
        Duration = duration ?? TimeSpan.FromMinutes(5);
        _remaining = Duration;
        _completionRaised = false;
    }

    public bool Update()
    {
        if (!IsRunning || Remaining > TimeSpan.Zero) return false;
        _stopwatch.Stop();
        _remaining = TimeSpan.Zero;
        if (_completionRaised) return false;
        _completionRaised = true;
        return true;
    }
}