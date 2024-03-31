using System;
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Util;
using AndroidX.DocumentFile.Provider;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using NAIPromptReplace.Views;
using Uri = Android.Net.Uri;

namespace NAIPromptReplace.Android.Views;

public class AndroidMainView : MainView
{
    protected override string ConfigPath => Path.Combine(MainActivity.Instance.GetExternalFilesDir(string.Empty).AbsolutePath, CONFIG_FILE);

    public AndroidMainView()
    {
        SaveAllButton.IsEnabled = false;
        MainActivity.Instance.Stopped += delegate { SaveConfig(); };
        MainActivity.Instance.OrientationChanged += updateScale;
        updateScale(this, Orientation.Undefined);
        new AlertDialog.Builder(MainActivity.Instance).SetMessage(@"由于安卓存储权限的限制每个任务必须选择一个输出目录
输出目录留空会报错，暂不支持手动填写路径
第一次写安卓程序，所以你可能会遇到包括但不限于：排版错乱，莫名报错/闪退，配置不保存，请谅解").SetNeutralButton("OK", (_, _) => {}).Show();
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

    protected override GenerationParameterControl AddTab(string header, GenerationConfig config)
    {
        var control = base.AddTab(header, config);
        control.BrowseButton_OnClick(this, null);
        control.OutputPathTextBox.IsReadOnly = true;
        return control;
    }

    protected override IStorageFile GetOutputFileForTask(GenerationConfig task, Dictionary<string, string> placeholders)
    {
        var folder = DocumentFile.FromTreeUri(MainActivity.Instance, Uri.Parse(task.StorageFolder.Path.AbsoluteUri));
        string originalFileName = Util.ReplaceInvalidFileNameChars(ReplacePlaceHolders(task.OutputFilename, placeholders) + ".png");

        if (string.IsNullOrWhiteSpace(originalFileName))
            originalFileName = ReplacePlaceHolders(GenerationConfig.DEFAULT_OUTPUT_FILE_NAME, placeholders);

        string fileName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);
        string current = Path.ChangeExtension(fileName, extension);

        if (folder != null)
        {
            var currentFile = DocumentFile.FromSingleUri(MainActivity.Instance, Uri.WithAppendedPath(folder.Uri, current)!)!;
            int i = 0;

            while (currentFile.Exists() && currentFile.IsFile)
            {
                current = Path.ChangeExtension(fileName + " (" + ++i + ")", extension);
                currentFile = DocumentFile.FromSingleUri(MainActivity.Instance, Uri.WithAppendedPath(folder.Uri, current)!)!;
            }
        }

        return task.StorageFolder?.CreateFileAsync(current).Result;
    }

    protected override void PresentUri(string uri)
    {
        using (var intent = new Intent(Intent.ActionView, Uri.Parse(uri)))
        {
            MainActivity.Instance.StartActivity(intent);
        }
    }
}
