using System;
using System.IO;
using Android.Content.Res;
using Android.Util;
using Avalonia.Media;
using NAIPromptReplace.Android.ViewModels;
using NAIPromptReplace.Views;

namespace NAIPromptReplace.Android.Views;

public class AndroidMainView : MainView
{
    protected override string ConfigPath => Path.Combine(MainActivity.Instance.GetExternalFilesDir(string.Empty).AbsolutePath, CONFIG_FILE);

    public AndroidMainView()
    {
        MainActivity.Instance.Stopped += delegate { SaveConfig(); };
        MainActivity.Instance.OrientationChanged += updateScale;
        updateScale(this, Orientation.Undefined);
    }

    protected override void InitializeDataContext()
    {
        DataContext = new AndroidMainViewModel
        {
            Config = LoadConfig()
        };
    }

    private void updateScale(object? sender, Orientation orientation)
    {
        const float minWidth = 700;
        const float minHeight = 600;
        var metrics = MainActivity.Instance.Application?.Resources?.DisplayMetrics ?? new DisplayMetrics();
        float density = 1;
        float widthScaled = metrics.WidthPixels / metrics.Density;
        float heightScaled = metrics.HeightPixels / metrics.Density;

        if (orientation == Orientation.Undefined)
            orientation = metrics.WidthPixels > metrics.HeightPixels ? Orientation.Landscape : Orientation.Portrait;

        if (widthScaled < minWidth)
            density = minWidth / widthScaled;

        if (orientation == Orientation.Landscape)
        {
            if (heightScaled < minHeight)
                density = Math.Max(density, minHeight / heightScaled);
        }

        LayoutTransform = new ScaleTransform(1 / density, 1 / density);
    }
}
