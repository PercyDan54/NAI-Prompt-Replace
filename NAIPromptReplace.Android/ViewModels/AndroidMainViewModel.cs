using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.Net;
using AndroidX.DocumentFile.Provider;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Android.ViewModels;

public class AndroidMainViewModel : MainViewModel
{
    /*protected override GenerationParameterControl AddTab(string header, GenerationConfig config)
    {
        var control = base.AddTab(header, config);
        control.BrowseButton_OnClick(this, null);
        control.OutputPathTextBox.IsReadOnly = true;
        return control;
    }*/

    protected override IStorageFile? GetOutputFileForTask(GenerationConfig task, Dictionary<string, string> placeholders)
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
