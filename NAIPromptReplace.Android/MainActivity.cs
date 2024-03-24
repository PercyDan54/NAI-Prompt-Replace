using System;
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace NAIPromptReplace.Android;

[Activity(
    Label = "NovelAI Prompt Replace",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    public static MainActivity Instance = null!;
    public event EventHandler? Stopped;

    public MainActivity()
    {
        Instance = this;
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .LogToTrace()
            .WithInterFont();
    }

    protected override void OnStop()
    {
        Stopped?.Invoke(this, null);
        base.OnStop();
    }
}
