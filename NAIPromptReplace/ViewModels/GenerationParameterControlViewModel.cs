using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class GenerationParameterControlViewModel : ReactiveObject
{
    public string Name { get; set; } = string.Empty;
    public GenerationConfig GenerationConfig
    {
        get => generationConfig;
        set
        {
            generationConfig.PropertyChanged -= GenerationConfigOnPropertyChanged;
            generationConfig.GenerationParameter.PropertyChanged -= GenerationConfigOnPropertyChanged;
            this.RaiseAndSetIfChanged(ref generationConfig, value);
            value.PropertyChanged += GenerationConfigOnPropertyChanged;
            value.GenerationParameter.PropertyChanged += GenerationConfigOnPropertyChanged;
        }
    }
    public NovelAIApi? Api { get; set; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand? OpenOutputFolderCommand { get; set; }
    public ICommand SaveCommand { get; }

    public bool DisableInputFolder
    {
        get => disableInputFolder;
        set => this.RaiseAndSetIfChanged(ref disableInputFolder, value);
    }

    public int AnlasCost
    {
        get => anlasCost;
        set => this.RaiseAndSetIfChanged(ref anlasCost, value);
    }

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
    private GenerationConfig generationConfig = new GenerationConfig();
    private int anlasCost;
    private bool disableInputFolder;

    public GenerationParameterControlViewModel()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(saveConfig);
        BrowseOutputFolderCommand = ReactiveCommand.Create(browseOutputFolder);
    }

    private void GenerationConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        int cost = AnlasCostCalculator.Calculate(GenerationConfig, Api?.SubscriptionInfo);
        var replaceLines = GenerationConfig.Replace.Split(Environment.NewLine).Select(l => Math.Max(l.Split(',').Length, 1));

        foreach (int line in replaceLines)
            cost *= line;

        AnlasCost = cost;
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

        var folder = folders[0];
        GenerationConfig.OutputPath = folder.Path.LocalPath;
        GenerationConfig.StorageFolder = folder;
    }
}
