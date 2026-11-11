using Windows.Media.Control;

namespace Dynamic.Services;

public sealed record MediaSessionSnapshot(
    string Title,
    string Artist,
    bool IsPlaying,
    bool CanTogglePlayback,
    bool CanSkipNext);

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;

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

    private async Task RefreshAsync()
    {
        if (_disposed || _manager is null)
            return;

        try
        {
            var nextSession = _manager.GetCurrentSession();
            if (!ReferenceEquals(nextSession, _session))
                _session = nextSession;

            if (_session is null)
            {
                Snapshot = null;
            }
            else
            {
                var properties = await _session.TryGetMediaPropertiesAsync();
                var playback = _session.GetPlaybackInfo();
                Snapshot = new MediaSessionSnapshot(
                    string.IsNullOrWhiteSpace(properties.Title) ? "Неизвестный трек" : properties.Title,
                    string.IsNullOrWhiteSpace(properties.Artist) ? "Неизвестный исполнитель" : properties.Artist,
                    playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    playback.Controls.IsPlayPauseToggleEnabled,
                    playback.Controls.IsNextEnabled);
            }
        }
        catch (Exception)
        {
            Snapshot = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        _session = null;
        _manager = null;
    }
}
