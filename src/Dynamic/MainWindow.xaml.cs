using System.Numerics;
using System.Runtime.InteropServices;
using Dynamic.Core;
using Dynamic.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
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
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _proximityTimer;
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
        _media.Changed += OnMediaChanged;
        Closed += OnWindowClosed;
        _ = _media.InitializeAsync();

        if (!ConfigureOverlayWindow())
            throw new InvalidOperationException("Dynamic could not initialize its overlay HWND.");

        StartProximityTracking();
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
            ApplyMagneticPose(Vector3.Zero, 1.0f);
            return;
        }

        var proximity = 1.0 - distance / MagneticRadius;
        var eased = (float)(proximity * proximity * (3.0 - 2.0 * proximity));
        var pull = MagneticStrength * eased;
        var translation = new Vector3(
            (float)(dx / distance * pull / _scale),
            (float)(dy / distance * pull * 0.72 / _scale),
            0.0f);
        var scale = 1.0f + 0.018f * eased;
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

    private void OnMediaChanged(object? sender, EventArgs args)
    {
        _dispatcherQueue.TryEnqueue(UpdateMediaUi);
    }

    private void UpdateMediaUi()
    {
        var media = _media.Snapshot;
        if (media is null)
        {
            CapsuleSubtitle.Text = "Рабочая капсула";
            MediaTitle.Text = "Медиа не обнаружено";
            MediaArtist.Text = "Запустите музыку в поддерживаемом приложении";
            PlayPauseButton.Content = "▶";
            PlayPauseButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            return;
        }

        CapsuleSubtitle.Text = media.Title;
        MediaTitle.Text = media.Title;
        MediaArtist.Text = media.Artist;
        PlayPauseButton.Content = media.IsPlaying ? "Ⅱ" : "▶";
        PlayPauseButton.IsEnabled = media.CanTogglePlayback;
        NextButton.IsEnabled = media.CanSkipNext;
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
        if (_proximityTimer is not null)
        {
            _proximityTimer.Stop();
            _proximityTimer.Tick -= OnProximityTick;
            _proximityTimer = null;
        }

        _media.Changed -= OnMediaChanged;
        _media.Dispose();
    }

    private void OnCapsulePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        ToggleExpanded();
        args.Handled = true;
    }

    private void OnCollapseClick(object sender, RoutedEventArgs args)
    {
        ToggleExpanded(forceCompact: true);
    }

    private void ToggleExpanded(bool forceCompact = false)
    {
        var expand = !forceCompact && _stateMachine.Target != CapsuleState.Expanded;
        var target = expand ? CapsuleState.Expanded : CapsuleState.Compact;
        if (!_stateMachine.Request(target))
            return;

        ExpandedPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;

        if (!ResizeOverlay(expand))
        {
            ExpandedPanel.Visibility = expand ? Visibility.Collapsed : Visibility.Visible;
            _stateMachine.Request(expand ? CapsuleState.Compact : CapsuleState.Expanded);
            _stateMachine.CompleteTransition();
            return;
        }

        _motion.SetEmphasis(Capsule, expand);
        _stateMachine.CompleteTransition();
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
        var height = (int)Math.Round((expanded ? 420 : 76) * _scale);
        var topOffset = (int)Math.Round(8 * _scale);
        var work = display.WorkArea;
        var x = work.X + (work.Width - width) / 2;
        var y = work.Y + topOffset;

        if (!SetWindowPos(_hwnd, HwndTopmost, x, y, width, height, SwpNoActivate | SwpShowWindow))
            return false;

        var radius = (int)Math.Round((expanded ? 30 : 38) * _scale);
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        if (region == IntPtr.Zero)
            return false;

        if (SetWindowRgn(_hwnd, region, true) == 0)
        {
            DeleteObject(region);
            return false;
        }

        return true;
    }

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