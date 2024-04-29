using System;
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;

namespace NAIPromptReplace.Android;

[Activity(
    Label = "NovelAI Prompt Replace",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    public static MainActivity Instance = null!;
    public event EventHandler? Stopped;
    public event EventHandler<Orientation>? OrientationChanged;

    public MainActivity()
    {
        Instance = this;
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .UseReactiveUI()
            .LogToTrace()
            .WithInterFont();
    }

    public override void OnConfigurationChanged(Configuration newConfig) {
        base.OnConfigurationChanged(newConfig);
        OrientationChanged?.Invoke(this, newConfig.Orientation);
    }

    protected override void OnStop()
    {
        Stopped?.Invoke(this, null);
        OrientationChanged = null;
        base.OnStop();
    }
}
