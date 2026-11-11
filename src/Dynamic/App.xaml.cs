using Microsoft.UI.Xaml;

namespace Dynamic;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // MainWindow configures and shows its HWND with SWP_NOACTIVATE.
        // Do not call Window.Activate(): that can steal foreground focus.
        _window ??= new MainWindow();
    }
}