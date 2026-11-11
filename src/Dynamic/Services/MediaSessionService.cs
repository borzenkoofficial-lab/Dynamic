using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Dynamic.Services;

public sealed record MediaSessionSnapshot(
    string Title,
    string Artist,
    bool IsPlaying,
    bool CanTogglePlayback,
    bool CanSkipNext,
    TimeSpan Position,
    TimeSpan Duration,
    IRandomAccessStreamReference? Artwork);

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;
    private int _refreshVersion;

    public event EventHandler? Changed;
    public MediaSessionSnapshot? Snapshot { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionsChanged;
            await RefreshAsync();
        }
        catch (Exception)
        {
            Snapshot = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task TogglePlaybackAsync()
    {
        var session = _session;
        if (session is null || Snapshot?.CanTogglePlayback != true)
            return;

        try { await session.TryTogglePlayPauseAsync(); }
        catch (Exception) { /* The source app may close or reject the command. */ }
        await RefreshAsync();
    }

    public async Task SkipNextAsync()
    {
        var session = _session;
        if (session is null || Snapshot?.CanSkipNext != true)
            return;

        try { await session.TrySkipNextAsync(); }
        catch (Exception) { /* The source app may close or reject the command. */ }
        await RefreshAsync();
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => _ = RefreshAsync();

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => _ = RefreshAsync();

    private void OnSessionMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();

    private void OnSessionPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_disposed || _manager is null)
            return;

        var version = Interlocked.Increment(ref _refreshVersion);
        try
        {
            var nextSession = _manager.GetCurrentSession();
            if (!ReferenceEquals(nextSession, _session))
            {
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged -= OnSessionMediaPropertiesChanged;
                    _session.PlaybackInfoChanged -= OnSessionPlaybackInfoChanged;
                }

                _session = nextSession;
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged += OnSessionMediaPropertiesChanged;
                    _session.PlaybackInfoChanged += OnSessionPlaybackInfoChanged;
                }
            }

            if (_session is null)
            {
                Snapshot = null;
            }
            else
            {
                var properties = await _session.TryGetMediaPropertiesAsync();
                var playback = _session.GetPlaybackInfo();
                var timeline = _session.GetTimelineProperties();
                var duration = timeline.EndTime - timeline.StartTime;
                if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
                if (_disposed || version != Volatile.Read(ref _refreshVersion)) return;
                Snapshot = new MediaSessionSnapshot(
                    string.IsNullOrWhiteSpace(properties.Title) ? "Неизвестный трек" : properties.Title,
                    string.IsNullOrWhiteSpace(properties.Artist) ? "Неизвестный исполнитель" : properties.Artist,
                    playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    playback.Controls.IsPlayPauseToggleEnabled,
                    playback.Controls.IsNextEnabled,
                    timeline.Position < TimeSpan.Zero ? TimeSpan.Zero : timeline.Position,
                    duration,
                    properties.Thumbnail);
            }
        }
        catch (Exception)
        {
            if (version == Volatile.Read(ref _refreshVersion)) Snapshot = null;
        }

        if (!_disposed && version == Volatile.Read(ref _refreshVersion))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Interlocked.Increment(ref _refreshVersion);
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnSessionPlaybackInfoChanged;
        }

        _session = null;
        _manager = null;
    }
}
