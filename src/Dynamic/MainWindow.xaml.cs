using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Dynamic.Core;
using Dynamic.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;

namespace Dynamic;

public sealed partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const double MagneticRadius = 260.0;
    private const float MagneticStrength = 5.5f;

    private readonly CapsuleStateMachine _stateMachine = new();
    private readonly CapsuleMotionController _motion = new();
    private readonly MediaSessionService _media = new();
    private readonly CountdownTimerService _countdown = new();
    private readonly ProgressActivityService _progress = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _proximityTimer;
    private DispatcherQueueTimer? _geometryTimer;
    private DispatcherQueueTimer? _uiTimer;
    private DispatcherQueueTimer? _toastTimer;
    private DispatcherQueueTimer? _toastHideTimer;
    private readonly Stopwatch _geometryClock = new();
    private readonly Stopwatch _mediaClock = new();
    private NativeRect _geometryFrom;
    private WindowGeometry _geometryTarget;
    private int _currentRadius = 38;
    private int _geometryFromRadius = 38;
    private MediaSessionSnapshot? _mediaSnapshot;
    private string? _loadedArtworkKey;
    private string? _lastCompletedActivity;
    private IntPtr _hwnd;
    private double _scale = 1.0;
    private Vector3 _lastMagneticTranslation = new(float.NaN, float.NaN, float.NaN);
    private float _lastMagneticScale = float.NaN;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        Capsule.PointerPressed += OnCapsulePointerPressed;
        Capsule.PointerEntered += OnCapsulePointerEntered;
        Capsule.PointerMoved += OnCapsulePointerMoved;
        Capsule.PointerExited += OnCapsulePointerExited;
        _media.Changed += OnMediaChanged;
        _progress.Changed += OnProgressChanged;
        Closed += OnWindowClosed;
        _ = _media.InitializeAsync();

        if (!ConfigureOverlayWindow())
            throw new InvalidOperationException("Dynamic could not initialize its overlay HWND.");

        StartProximityTracking();
        StartUiClock();
        UpdateTimerUi();
    }

    private void StartProximityTracking()
    {
        _proximityTimer = _dispatcherQueue.CreateTimer();
        _proximityTimer.Interval = TimeSpan.FromMilliseconds(16);
        _proximityTimer.IsRepeating = true;
        _proximityTimer.Tick += OnProximityTick;
        _proximityTimer.Start();
    }

    private void OnProximityTick(DispatcherQueueTimer sender, object args)
    {
        if (_hwnd == IntPtr.Zero || !GetCursorPos(out var cursor) || !GetWindowRect(_hwnd, out var bounds))
            return;

        var centerX = (bounds.Left + bounds.Right) * 0.5;
        var centerY = (bounds.Top + bounds.Bottom) * 0.5;
        var dx = cursor.X - centerX;
        var dy = cursor.Y - centerY;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance >= MagneticRadius || distance < 0.5)
        {
            var idleScale = _stateMachine.Target == CapsuleState.Expanded ? 1.045f : 1.0f;
            ApplyMagneticPose(Vector3.Zero, idleScale);
            return;
        }

        var proximity = 1.0 - distance / MagneticRadius;
        var eased = (float)(proximity * proximity * (3.0 - 2.0 * proximity));
        var pull = MagneticStrength * eased;
        var translation = new Vector3(
            (float)(dx / distance * pull / _scale),
            (float)(dy / distance * pull * 0.72 / _scale),
            0.0f);
        var baseScale = _stateMachine.Target == CapsuleState.Expanded ? 1.045f : 1.0f;
        var scale = baseScale + 0.018f * eased;
        ApplyMagneticPose(translation, scale);
    }

    private void ApplyMagneticPose(Vector3 translation, float scale)
    {
        var delta = Vector3.Distance(_lastMagneticTranslation, translation);
        if (delta < 0.35f && Math.Abs(_lastMagneticScale - scale) < 0.001f)
            return;

        _lastMagneticTranslation = translation;
        _lastMagneticScale = scale;
        _motion.SetMagneticPose(Capsule, translation, scale);
    }

    private void OnCapsulePointerEntered(object sender, PointerRoutedEventArgs args)
    {
        UpdateSurfaceHighlight(args);
        _motion.FadeOpacity(EdgeGlow, 0.92f, 180);
    }

    private void OnCapsulePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        UpdateSurfaceHighlight(args);
    }

    private void OnCapsulePointerExited(object sender, PointerRoutedEventArgs args)
    {
        PointerHighlight.Center = new Windows.Foundation.Point(0.5, 0.5);
        _motion.FadeOpacity(HighlightLayer, 0.0f, 260);
        _motion.FadeOpacity(EdgeGlow, 0.42f, 320);
    }

    private void UpdateSurfaceHighlight(PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(Capsule).Position;
        var width = Math.Max(1.0, Capsule.ActualWidth);
        var height = Math.Max(1.0, Capsule.ActualHeight);
        PointerHighlight.Center = new Windows.Foundation.Point(
            Math.Clamp(point.X / width, 0.0, 1.0),
            Math.Clamp(point.Y / height, 0.0, 1.0));
        HighlightLayer.Opacity = 0.92;
        EdgeGlow.Opacity = 0.92;
    }

    private void OnMediaChanged(object? sender, EventArgs args) => _dispatcherQueue.TryEnqueue(UpdateMediaUi);

    private async void UpdateMediaUi()
    {
        var media = _media.Snapshot;
        _mediaSnapshot = media;
        _mediaClock.Restart();
        if (media is null)
        {
            CapsuleSubtitle.Text = "Рабочая капсула";
            MediaTitle.Text = "Медиа не обнаружено";
            MediaArtist.Text = "Запустите музыку в поддерживаемом приложении";
            PlayPauseButton.Content = "▶";
            PlayPauseButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            MediaProgress.Value = 0;
            MediaElapsedText.Text = "0:00";
            MediaDurationText.Text = "--:--";
            MediaArtworkImage.Source = null;
            MediaArtworkImage.Visibility = Visibility.Collapsed;
            MediaArtworkGlyph.Visibility = Visibility.Visible;
            _loadedArtworkKey = null;
            return;
        }

        CapsuleSubtitle.Text = media.Title;
        MediaTitle.Text = media.Title;
        MediaArtist.Text = media.Artist;
        _motion.SetEmphasis(MediaArtwork, media.IsPlaying, 0.035);
        _motion.FadeOpacity(EdgeGlow, media.IsPlaying ? 0.72f : 0.42f, 260);
        PlayPauseButton.Content = media.IsPlaying ? "Ⅱ" : "▶";
        PlayPauseButton.IsEnabled = media.CanTogglePlayback;
        NextButton.IsEnabled = media.CanSkipNext;
        RenderMediaProgress();

        var artworkKey = media.Title + "\u001f" + media.Artist;
        if (artworkKey == _loadedArtworkKey) return;
        _loadedArtworkKey = artworkKey;
        if (media.Artwork is null)
        {
            MediaArtworkImage.Source = null;
            MediaArtworkImage.Visibility = Visibility.Collapsed;
            MediaArtworkGlyph.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            using var stream = await media.Artwork.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (_mediaSnapshot?.Title != media.Title || _mediaSnapshot?.Artist != media.Artist) return;
            MediaArtworkImage.Source = bitmap;
            MediaArtworkImage.Visibility = Visibility.Visible;
            MediaArtworkGlyph.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            MediaArtworkImage.Visibility = Visibility.Collapsed;
            MediaArtworkGlyph.Visibility = Visibility.Visible;
        }
    }

    private void RenderMediaProgress()
    {
        var media = _mediaSnapshot;
        if (media is null || media.Duration <= TimeSpan.Zero)
        {
            MediaProgress.Value = 0;
            MediaElapsedText.Text = "0:00";
            MediaDurationText.Text = "--:--";
            return;
        }
        var position = media.Position + (media.IsPlaying ? _mediaClock.Elapsed : TimeSpan.Zero);
        if (position > media.Duration) position = media.Duration;
        MediaProgress.Value = Math.Clamp(position.TotalMilliseconds / media.Duration.TotalMilliseconds, 0, 1);
        MediaElapsedText.Text = FormatTime(position);
        MediaDurationText.Text = FormatTime(media.Duration);
    }

    private static string FormatTime(TimeSpan value)
    {
        var minutes = (int)Math.Floor(Math.Max(0, value.TotalSeconds) / 60);
        return $"{minutes}:{Math.Max(0, value.Seconds):00}";
    }

    private void StartUiClock()
    {
        _uiTimer = _dispatcherQueue.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        _uiTimer.IsRepeating = true;
        _uiTimer.Tick += OnUiClockTick;
        _uiTimer.Start();
    }

    private void OnUiClockTick(DispatcherQueueTimer sender, object args)
    {
        RenderMediaProgress();
        UpdateTimerUi();
    }

    private void UpdateTimerUi()
    {
        var completed = _countdown.Update();
        var remaining = _countdown.Remaining;
        var duration = _countdown.Duration;
        var fraction = duration <= TimeSpan.Zero ? 0 : Math.Clamp(remaining.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        TimerRingLabel.Text = FormatTime(remaining);
        TimerStartButton.Content = _countdown.IsRunning ? "Пауза" : "Старт";
        TimerStatusText.Text = _countdown.IsRunning ? "Идёт отсчёт" : remaining == TimeSpan.Zero ? "Время вышло" : "Готов к запуску";
        UpdateTimerRing(fraction);
        if (completed)
        {
            _motion.SetEmphasis(TimerCard, true, 0.035);
            ShowNotification("Таймер завершён", "Время вышло. Отсчёт остановлен.");
        }
    }

    private void UpdateTimerRing(double fraction)
    {
        const double radius = 25;
        const double center = 29;
        if (fraction <= 0.001) { TimerRingProgress.Data = null; return; }
        fraction = Math.Min(fraction, 0.999);
        var angle = (-90 + 360 * fraction) * Math.PI / 180;
        var figure = new PathFigure
        {
            StartPoint = new Windows.Foundation.Point(center, center - radius),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Windows.Foundation.Point(center + radius * Math.Cos(angle), center + radius * Math.Sin(angle)),
            Size = new Windows.Foundation.Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.Clockwise
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        TimerRingProgress.Data = geometry;
    }

    private void OnTimerStartClick(object sender, RoutedEventArgs args)
    {
        if (_countdown.IsRunning) _countdown.Pause();
        else _countdown.Start();
        UpdateTimerUi();
    }

    private void OnTimerResetClick(object sender, RoutedEventArgs args)
    {
        _countdown.Reset();
        UpdateTimerUi();
    }

    private void OnTimerPreset5Click(object sender, RoutedEventArgs args) => SetTimerPreset(5);
    private void OnTimerPreset15Click(object sender, RoutedEventArgs args) => SetTimerPreset(15);
    private void OnTimerPreset25Click(object sender, RoutedEventArgs args) => SetTimerPreset(25);

    private void SetTimerPreset(int minutes)
    {
        _countdown.Reset(TimeSpan.FromMinutes(minutes));
        UpdateTimerUi();
    }

    private void OnProgressChanged(object? sender, EventArgs args) => _dispatcherQueue.TryEnqueue(UpdateProgressUi);

    private void UpdateProgressUi()
    {
        var activity = _progress.Current;
        if (activity is null)
        {
            _lastCompletedActivity = null;
            ProgressCard.Visibility = Visibility.Collapsed;
            return;
        }
        ProgressCard.Visibility = Visibility.Visible;
        ActivityTitle.Text = activity.Title;
        ActivityPercent.Text = $"{activity.Percent:0}%";
        ActivityProgress.Value = activity.Percent;
        ActivityDetail.Text = activity.Detail;
        ActivitySpeed.Text = activity.Speed;
        if (activity.IsCompleted && _lastCompletedActivity != activity.Title)
        {
            _lastCompletedActivity = activity.Title;
            _motion.Reveal(ProgressCard, 1.0f, 280);
            ShowNotification("Операция завершена", activity.Title + " — " + activity.Detail);
        }
    }

    private void ShowNotification(string title, string message)
    {
        if (_stateMachine.Target != CapsuleState.Expanded) BeginExpandedTransition(true);
        NotificationTitle.Text = title;
        NotificationMessage.Text = message;
        NotificationToast.Visibility = Visibility.Visible;
        _motion.FadeOpacity(NotificationToast, 1.0f, 180);
        _toastTimer ??= _dispatcherQueue.CreateTimer();
        _toastTimer.IsRepeating = false;
        _toastTimer.Tick -= OnToastTimerTick;
        _toastTimer.Tick += OnToastTimerTick;
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(3.8);
        _toastTimer.Start();
    }

    private void OnToastTimerTick(DispatcherQueueTimer sender, object args)
    {
        _toastTimer?.Stop();
        _motion.FadeOpacity(NotificationToast, 0.0f, 220);
        _toastHideTimer ??= _dispatcherQueue.CreateTimer();
        _toastHideTimer.IsRepeating = false;
        _toastHideTimer.Stop();
        _toastHideTimer.Interval = TimeSpan.FromMilliseconds(240);
        _toastHideTimer.Tick -= OnToastHideTimerTick;
        _toastHideTimer.Tick += OnToastHideTimerTick;
        _toastHideTimer.Start();
    }

    private void OnToastHideTimerTick(DispatcherQueueTimer sender, object args)
    {
        _toastHideTimer?.Stop();
        NotificationToast.Visibility = Visibility.Collapsed;
    }

    private async void OnPlayPauseClick(object sender, RoutedEventArgs args)
    {
        PlayPauseButton.IsEnabled = false;
        await _media.TogglePlaybackAsync();
    }

    private async void OnNextClick(object sender, RoutedEventArgs args)
    {
        NextButton.IsEnabled = false;
        await _media.SkipNextAsync();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        StopTimer(_proximityTimer, OnProximityTick);
        StopTimer(_geometryTimer, OnGeometryTick);
        StopTimer(_uiTimer, OnUiClockTick);
        if (_toastTimer is not null) _toastTimer.Stop();
        if (_toastHideTimer is not null) _toastHideTimer.Stop();
        _media.Changed -= OnMediaChanged;
        _progress.Changed -= OnProgressChanged;
        _media.Dispose();
    }

    private void OnCapsulePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        ToggleExpanded();
        args.Handled = true;
    }

    private void OnCollapseClick(object sender, RoutedEventArgs args) => BeginExpandedTransition(false);

    private void ToggleExpanded(bool forceCompact = false)
    {
        var expand = !forceCompact && _stateMachine.Target != CapsuleState.Expanded;
        BeginExpandedTransition(expand);
    }

    private void BeginExpandedTransition(bool expand)
    {
        var target = expand ? CapsuleState.Expanded : CapsuleState.Compact;
        if (!_stateMachine.Request(target)) return;
        if (expand)
        {
            ExpandedPanel.Visibility = Visibility.Visible;
            ExpandedPanel.Opacity = 0;
            _motion.FadeOpacity(ExpandedPanel, 1.0f, 300);
        }
        else
        {
            _motion.FadeOpacity(ExpandedPanel, 0.0f, 180);
        }
        if (!StartGeometryTransition(expand))
        {
            _stateMachine.Request(expand ? CapsuleState.Compact : CapsuleState.Expanded);
            _stateMachine.CompleteTransition();
            ExpandedPanel.Visibility = expand ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private bool StartGeometryTransition(bool expanded)
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var current)) return false;
        _geometryFrom = current;
        _geometryFromRadius = _currentRadius;
        _geometryTarget = CalculateTargetGeometry(expanded);
        _geometryClock.Restart();
        _geometryTimer ??= _dispatcherQueue.CreateTimer();
        _geometryTimer.Interval = TimeSpan.FromMilliseconds(16);
        _geometryTimer.IsRepeating = true;
        _geometryTimer.Tick -= OnGeometryTick;
        _geometryTimer.Tick += OnGeometryTick;
        _geometryTimer.Start();
        return true;
    }

    private WindowGeometry CalculateTargetGeometry(bool expanded)
    {
        var display = DisplayArea.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        var width = (int)Math.Round((expanded ? 520 : 390) * _scale);
        var height = (int)Math.Round((expanded ? 620 : 76) * _scale);
        var work = display.WorkArea;
        return new WindowGeometry(work.X + (work.Width - width) / 2,
            work.Y + (int)Math.Round(8 * _scale), width, height,
            (int)Math.Round((expanded ? 30 : 38) * _scale));
    }

    private void OnGeometryTick(DispatcherQueueTimer sender, object args)
    {
        const double duration = 0.38;
        var t = Math.Clamp(_geometryClock.Elapsed.TotalSeconds / duration, 0, 1);
        var eased = t >= 1 ? 1 : 1 - Math.Exp(-7.5 * t) * (Math.Cos(8 * t) + 0.9 * Math.Sin(8 * t));
        var geometry = new WindowGeometry(
            Lerp(_geometryFrom.Left, _geometryTarget.X, eased),
            Lerp(_geometryFrom.Top, _geometryTarget.Y, eased),
            Lerp(_geometryFrom.Right - _geometryFrom.Left, _geometryTarget.Width, eased),
            Lerp(_geometryFrom.Bottom - _geometryFrom.Top, _geometryTarget.Height, eased),
            Lerp(_geometryFromRadius, _geometryTarget.Radius, eased));
        if (!ApplyWindowGeometry(geometry))
        {
            _geometryTimer?.Stop();
            ApplyWindowGeometry(_geometryTarget);
            _stateMachine.CompleteTransition();
            if (_stateMachine.Target == CapsuleState.Compact)
                ExpandedPanel.Visibility = Visibility.Collapsed;
            return;
        }
        if (t < 1) return;

        _geometryTimer?.Stop();
        ApplyWindowGeometry(_geometryTarget);
        _stateMachine.CompleteTransition();
        if (_stateMachine.Target == CapsuleState.Compact)
            ExpandedPanel.Visibility = Visibility.Collapsed;
        else
        {
            _motion.Reveal(MediaCard, 1.0f, 280);
            _motion.Reveal(TimerCard, 1.0f, 320);
            _motion.Reveal(PlatformCard, 1.0f, 360);
            _motion.Reveal(StatusCard, 1.0f, 400);
            if (ProgressCard.Visibility == Visibility.Visible) _motion.Reveal(ProgressCard, 1.0f, 440);
        }
    }

    private static int Lerp(int from, int to, double amount) =>
        (int)Math.Round(from + (to - from) * amount);

    private static void StopTimer(DispatcherQueueTimer? timer, TypedEventHandler<DispatcherQueueTimer, object> handler)
    {
        if (timer is null) return;
        timer.Stop();
        timer.Tick -= handler;
    }

    private bool ConfigureOverlayWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
        AppWindow.IsShownInSwitchers = false;

        _hwnd = WindowNative.GetWindowHandle(this);
        if (_hwnd == IntPtr.Zero)
            return false;

        var style = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(style));

        var dpi = GetDpiForWindow(_hwnd);
        _scale = dpi == 0 ? 1.0 : dpi / 96.0;
        return ResizeOverlay(expanded: false);
    }

    private bool ResizeOverlay(bool expanded)
    {
        if (_hwnd == IntPtr.Zero)
            return false;

        var display = DisplayArea.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd),
            DisplayAreaFallback.Primary);

        var width = (int)Math.Round((expanded ? 520 : 390) * _scale);
        var height = (int)Math.Round((expanded ? 620 : 76) * _scale);
        var work = display.WorkArea;
        var x = work.X + (work.Width - width) / 2;
        var y = work.Y + (int)Math.Round(8 * _scale);
        var radius = (int)Math.Round((expanded ? 30 : 38) * _scale);
        return ApplyWindowGeometry(new WindowGeometry(x, y, width, height, radius));
    }

    private bool ApplyWindowGeometry(WindowGeometry geometry)
    {
        if (!SetWindowPos(_hwnd, HwndTopmost, geometry.X, geometry.Y, geometry.Width, geometry.Height,
                SwpNoActivate | SwpShowWindow)) return false;
        var region = CreateRoundRectRgn(0, 0, geometry.Width + 1, geometry.Height + 1,
            geometry.Radius * 2, geometry.Radius * 2);
        if (region == IntPtr.Zero) return false;
        if (SetWindowRgn(_hwnd, region, true) == 0)
        {
            DeleteObject(region);
            return false;
        }
        _currentRadius = geometry.Radius;
        return true;
    }

    private readonly record struct WindowGeometry(int X, int Y, int Width, int Height, int Radius);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}