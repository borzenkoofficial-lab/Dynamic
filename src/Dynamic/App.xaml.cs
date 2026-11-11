using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Dynamic;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => StartupDiagnostics.Write(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                StartupDiagnostics.Write(exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupDiagnostics.Write(args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window ??= new MainWindow();

            // Complete the normal WinUI window lifecycle before applying overlay styles.
            _window.Activate();
            _window.InitializeOverlay();
        }
        catch (Exception exception)
        {
            var logPath = StartupDiagnostics.Write(exception);
            MessageBoxW(
                IntPtr.Zero,
                $"Dynamic не удалось запустить.\n\nПодробности записаны в файл:\n{logPath}",
                "Ошибка запуска Dynamic",
                0x10);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}