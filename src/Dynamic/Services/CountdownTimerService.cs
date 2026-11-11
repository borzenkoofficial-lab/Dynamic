namespace Dynamic.Services;

public sealed class CountdownTimerService
{
    private TimeSpan _remaining = TimeSpan.FromMinutes(5);
    private DateTimeOffset? _deadlineUtc;
    private bool _completionRaised;

    public TimeSpan Remaining
    {
        get
        {
            if (_deadlineUtc is not { } deadline) return _remaining;
            var remaining = deadline - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(5);
    public bool IsRunning => _deadlineUtc.HasValue;

    public void Start()
    {
        if (_remaining <= TimeSpan.Zero || IsRunning) return;
        _completionRaised = false;
        _deadlineUtc = DateTimeOffset.UtcNow + _remaining;
    }

    public void Pause()
    {
        if (!IsRunning) return;
        _remaining = Remaining;
        _deadlineUtc = null;
    }

    public void Reset(TimeSpan? duration = null)
    {
        _deadlineUtc = null;
        Duration = duration ?? TimeSpan.FromMinutes(5);
        _remaining = Duration;
        _completionRaised = false;
    }

    public CountdownTimerSnapshot ExportState() =>
        new(Duration.Ticks, Remaining.Ticks, _deadlineUtc);

    public void Restore(CountdownTimerSnapshot? snapshot)
    {
        if (snapshot is null) return;
        var duration = TimeSpan.FromTicks(Math.Max(0, snapshot.DurationTicks));
        var remaining = TimeSpan.FromTicks(Math.Max(0, snapshot.RemainingTicks));
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(7)) return;
        Duration = duration;
        _remaining = remaining > duration ? duration : remaining;
        _deadlineUtc = snapshot.DeadlineUtc is { } deadline && deadline > DateTimeOffset.UtcNow
            ? deadline
            : null;
        if (snapshot.DeadlineUtc.HasValue && !_deadlineUtc.HasValue)
            _remaining = TimeSpan.Zero;
        _completionRaised = !snapshot.DeadlineUtc.HasValue && _remaining <= TimeSpan.Zero;
    }

    public bool Update()
    {
        if (Remaining > TimeSpan.Zero) return false;
        _deadlineUtc = null;
        _remaining = TimeSpan.Zero;
        if (_completionRaised) return false;
        _completionRaised = true;
        return true;
    }
}
