using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Controls;
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
            generationConfig.PropertyChanged += GenerationConfigOnPropertyChanged;
            generationConfig.GenerationParameter.PropertyChanged += GenerationConfigOnPropertyChanged;
            loadVibeTransfer();
        }
    }
    public NovelAIApi? Api { get; set; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand? OpenOutputFolderCommand { get; set; }
    public ICommand SaveCommand { get; }
    public ICommand AddVibeTransferCommand { get; }

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

    public ObservableCollection<ReferenceImageViewModel> Img2ImgViewModels { get; }
    public ObservableCollection<VibeTransferViewModel> VibeTransferViewModels { get; private set; } = [];

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
    private List<IDisposable> vibeTransferSubscriptions = [];

    public GenerationParameterControlViewModel()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(saveConfig);
        BrowseOutputFolderCommand = ReactiveCommand.Create(browseOutputFolder);
        AddVibeTransferCommand = ReactiveCommand.Create(addVibeTransfer);
        Img2ImgViewModels =
        [
            new ReferenceImageViewModel
            {
                Title = "Img2Img",
                Content = new Img2ImgControl
                {
                    DataContext = this,
                }
            },
        ];
        GenerationConfigOnPropertyChanged(null, null);
    }

    private VibeTransferViewModel addVibeTransfer()
    {
        var vm = new VibeTransferViewModel
        {
            Id = VibeTransferViewModels.Count,
            RemoveSelfCommand = ReactiveCommand.Create<int>(removeVibeTransfer)
        };

        vm.Content = new VibeTransferControl
        {
            DataContext = vm,
        };

        var subscription = vm.WhenAnyValue(v => v.ImageData, v => v.ImagePath, v => v.ReferenceStrength, v => v.ReferenceInformationExtracted).Subscribe(_ => updateVibeTransfer());
        vibeTransferSubscriptions.Add(subscription);
        VibeTransferViewModels.Add(vm);
        return vm;
    }

    private void removeVibeTransfer(int id)
    {
        vibeTransferSubscriptions[id].Dispose();
        vibeTransferSubscriptions.RemoveAt(id);
        VibeTransferViewModels.RemoveAt(id);
    }

    private void loadVibeTransfer()
    {
        if (GenerationConfig.GenerationParameter.ReferenceStrength != null)
        {
            GenerationConfig.GenerationParameter.ReferenceImageMultiple = [GenerationConfig.GenerationParameter.ReferenceImage ?? string.Empty];
        }

        foreach (string referenceImage in GenerationConfig.GenerationParameter.ReferenceImageMultiple)
        {
            var vm = addVibeTransfer();

            vm.ReferenceStrength = GenerationConfig.GenerationParameter.ReferenceStrength.GetValueOrDefault();
            vm.ReferenceInformationExtracted = GenerationConfig.GenerationParameter.ReferenceInformationExtracted.GetValueOrDefault();
            GenerationConfig.GenerationParameter.ReferenceImage = null;
            GenerationConfig.GenerationParameter.ReferenceStrength = null;
            GenerationConfig.GenerationParameter.ReferenceInformationExtracted = null;

            if (!string.IsNullOrEmpty(referenceImage))
            {
                var file = App.StorageProvider?.TryGetFileFromPathAsync(referenceImage).Result;

                if (file != null)
                    vm.SetReferenceImage(file).ConfigureAwait(false);
            }
        }
    }

    private void updateVibeTransfer()
    {
        var vms = VibeTransferViewModels.Where(v => v.ImageData != null).ToArray();
        int count = vms.Length;
        double[] referenceInformationExtractedMultiple = new double[count];
        double[] referenceStrengthMultiple = new double[count];
        byte[][] imageDatas = new byte[count][];
        string[] imagePaths = new string[count];

        for (int i = 0; i < vms.Length; i++)
        {
            var vm = vms[i];
            referenceInformationExtractedMultiple[i] = vm.ReferenceInformationExtracted;
            referenceStrengthMultiple[i] = vm.ReferenceStrength;
            imageDatas[i] = vm.ImageData;
            imagePaths[i] = vm.ImagePath ?? string.Empty;
        }

        GenerationConfig.GenerationParameter.ReferenceInformationExtractedMultiple = referenceInformationExtractedMultiple;
        GenerationConfig.GenerationParameter.ReferenceStrengthMultiple = referenceStrengthMultiple;
        GenerationConfig.GenerationParameter.ReferenceImageData = imageDatas;
        GenerationConfig.GenerationParameter.ReferenceImageMultiple = imagePaths;
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
