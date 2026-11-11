using System.Runtime.InteropServices;
using Dynamic.Core;
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
    private readonly CapsuleStateMachine _stateMachine = new();
    private readonly CapsuleMotionController _motion = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        Capsule.PointerPressed += OnCapsulePointerPressed;

        // Configure the HWND before it is shown to avoid activating a normal window first.
        if (!ConfigureOverlayWindow())
            throw new InvalidOperationException("Dynamic could not initialize its overlay HWND.");
    }

    private void OnCapsulePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var expand = _stateMachine.Target != CapsuleState.Expanded;
        _stateMachine.Request(expand ? CapsuleState.Expanded : CapsuleState.Compact);
        _motion.SetEmphasis(Capsule, expand);
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

        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
            return false;

        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));

        var display = DisplayArea.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd),
            DisplayAreaFallback.Primary);

        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        var width = (int)Math.Round(390 * scale);
        var height = (int)Math.Round(76 * scale);
        var work = display.WorkArea;
        var x = work.X + (work.Width - width) / 2;
        var y = work.Y + (int)Math.Round(8 * scale);

        return SetWindowPos(hwnd, HwndTopmost, x, y, width, height,
            SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}