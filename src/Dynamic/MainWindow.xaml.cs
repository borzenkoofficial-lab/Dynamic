using System.Runtime.InteropServices;
using Dynamic.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    private readonly CapsuleStateMachine _stateMachine = new();
    private readonly CapsuleMotionController _motion = new();
    private IntPtr _hwnd;
    private double _scale = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        Capsule.PointerPressed += OnCapsulePointerPressed;

        if (!ConfigureOverlayWindow())
            throw new InvalidOperationException("Dynamic could not initialize its overlay HWND.");
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
        var height = (int)Math.Round((expanded ? 236 : 76) * _scale);
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
