using System;
using System.IO;
using Android.App;
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
        new AlertDialog.Builder(MainActivity.Instance).SetMessage(@"由于安卓存储权限的限制每个任务必须选择一个输出目录
输出目录留空会报错，暂不支持手动填写路径
第一次写安卓程序，所以你可能会遇到包括但不限于：排版错乱，莫名报错/闪退，配置不保存，请谅解").SetNeutralButton("OK", (_, _) => {}).Show();
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
        const float minWidth = 620;
        const float minHeight = 650;
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
