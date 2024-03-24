using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Android.Content;
using Android.Net;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using NAIPromptReplace.Views;

namespace NAIPromptReplace.Android.Views;

public class AndroidMainView : MainView
{
    private string baseDir = string.Empty;
    protected override string ConfigPath => Path.Combine(baseDir, CONFIG_FILE);

    public AndroidMainView()
    {
        SaveAllButton.IsEnabled = false;
        baseDir = MainActivity.Instance.GetExternalFilesDir(string.Empty).AbsolutePath;
        MainActivity.Instance.Stopped += delegate { SaveConfig(); };
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
        return task.StorageFolder?.CreateFileAsync(Util.ReplaceInvalidFileNameChars(replacePlaceHolders(task.OutputFilename, placeholders)) + ".png").Result;
    }

    protected override void PresentUri(string uri)
    {
        using (var intent = new Intent(Intent.ActionView, Uri.Parse(HELP_URL)))
        {
            MainActivity.Instance.StartActivity(intent);
        }
    }
}
