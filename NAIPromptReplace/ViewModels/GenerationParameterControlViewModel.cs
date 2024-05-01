using System.Windows.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class GenerationParameterControlViewModel : ReactiveObject
{
    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }
    public GenerationConfig GenerationConfig { get; set; } = new GenerationConfig();
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand? OpenOutputFolderCommand { get; set; }
    public ICommand SaveCommand { get; }

    private static readonly FilePickerSaveOptions saveConfigFilePickerOptions = new FilePickerSaveOptions
    {
        FileTypeChoices =
        [
            new FilePickerFileType("JSON")
            {
                Patterns = ["*.json"]
            }
        ]
    };
    private static readonly FolderPickerOpenOptions folderPickerOpenOptions = new FolderPickerOpenOptions
    {
        AllowMultiple = false
    };
    private string name;

    public GenerationParameterControlViewModel()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(saveConfig);
        BrowseOutputFolderCommand = ReactiveCommand.Create(browseOutputFolder);
    }

    private async Task saveConfig()
    {
        if (App.StorageProvider == null)
            return;

        var file = await App.StorageProvider.SaveFilePickerAsync(saveConfigFilePickerOptions);

        if (file == null)
            return;

        await GenerationConfig.SaveAsync(file);
    }

    private async void browseOutputFolder()
    {
        if (App.StorageProvider == null)
            return;

        var folders = await App.StorageProvider.OpenFolderPickerAsync(folderPickerOpenOptions);

        if (folders.Count < 1)
            return;

        GenerationConfig.StorageFolder = folders[0];
    }
}
