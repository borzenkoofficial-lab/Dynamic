using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Dynamic;

public partial class App : Application
{
    private MainWindow? _window;

    private static bool IsSmokeTest =>
        string.Equals(Environment.GetEnvironmentVariable("DYNAMIC_SMOKE_TEST"), "1", StringComparison.Ordinal);

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

            // CI exercises the exact same startup path as a normal launch.
            // It writes a marker only after XAML parsing and native HWND setup both succeed.
            if (IsSmokeTest)
            {
                var markerPath = Environment.GetEnvironmentVariable("DYNAMIC_SMOKE_TEST_MARKER");
                if (string.IsNullOrWhiteSpace(markerPath))
                    throw new InvalidOperationException("DYNAMIC_SMOKE_TEST_MARKER is required for startup smoke tests.");

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markerPath))!);
                File.WriteAllText(
                    markerPath,
                    $"pid={Environment.ProcessId}; initialized=true; utc={DateTimeOffset.UtcNow:O}");
                Environment.Exit(0);
                return;
            }
        }
        catch (Exception exception)
        {
            var logPath = StartupDiagnostics.Write(exception);

            // Do not display a modal dialog on a CI runner. A non-zero process exit
            // makes the smoke-test step fail and keeps the diagnostic log available.
            if (IsSmokeTest)
            {
                Environment.Exit(1);
                return;
            }

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
