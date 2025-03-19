using Avalonia.Controls.ApplicationLifetimes;

namespace NAIPromptReplace.Desktop;

public class DesktopApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
