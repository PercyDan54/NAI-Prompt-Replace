﻿using Android.App;
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
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}