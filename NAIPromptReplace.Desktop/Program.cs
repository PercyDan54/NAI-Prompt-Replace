using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.ReactiveUI;

namespace NAIPromptReplace.Desktop;

class Program
{
    [DllImport("user32.dll")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string file = Util.ReplaceInvalidFileNameChars($"error-{DateTime.Now}.txt");

        errorMessageBox($"An unhandled exception occured. Error will be saved to {file}\n{e.ExceptionObject}");

#if !DEBUG
        try
        {
            File.WriteAllText(file, e.ExceptionObject.ToString());
        }
        catch (Exception ex)
        {
            errorMessageBox($"Error writing crash log to {file}:\n{ex}");
        }
#endif

        Environment.Exit(-1);
    }

    private static void errorMessageBox(string message, string caption = "Error")
    {
        if (!OperatingSystem.IsWindows())
            return;

        MessageBox(IntPtr.Zero, message, caption, 0x10);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
