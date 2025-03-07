using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using CsvHelper;
using CsvHelper.Configuration;
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
            loadImages();
        }
    }
    public NovelAIApi? Api
    {
        get => api;
        set
        {
            this.RaiseAndSetIfChanged(ref api, value);
            updateCost();
        }
    }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand? OpenOutputFolderCommand { get; set; }
    public ICommand SaveCommand { get; }
    public ICommand AddVibeTransferCommand { get; }
    public ICommand AddCharacterCommand { get; }

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

    public ReferenceImageViewModel Img2ImgViewModel { get; }
    public ObservableCollection<VibeTransferViewModel> VibeTransferViewModels { get; } = [];
    public ObservableCollection<GenerationLogViewModel> GenerationLogs { get; } = [];

    private static readonly CsvConfiguration csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        BadDataFound = null,
        TrimOptions = TrimOptions.Trim
    };

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

    private static readonly FolderPickerOpenOptions folderPickerOpenOptions = new FolderPickerOpenOptions();
    private GenerationConfig generationConfig = new GenerationConfig();
    private int anlasCost;
    private bool disableInputFolder;
    private NovelAIApi? api;
    private int nextVibeTransferId;
    private int nextCharacterId;

    public GenerationParameterControlViewModel()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(saveConfig);
        BrowseOutputFolderCommand = ReactiveCommand.Create(browseOutputFolder);
        AddVibeTransferCommand = ReactiveCommand.Create(addVibeTransfer);
        AddCharacterCommand = ReactiveCommand.Create(addCharacter);

        var vm = new ReferenceImageViewModel
        {
            Title = "Img2Img",
            StrictImageFile = true,
            Content = new Img2ImgControl
            {
                DataContext = this
            }
        };
        vm.WhenAnyValue(v => v.ImageData, v => v.ImagePath).Subscribe(value =>
        {
            GenerationConfig.GenerationParameter.ImageData = value.Item1;
            GenerationConfig.GenerationParameter.Image = value.Item2;
        });
        Img2ImgViewModel = vm;
    }

    private VibeTransferViewModel addVibeTransfer()
    {
        var vm = new VibeTransferViewModel
        {
            Id = nextVibeTransferId++,
            RemoveSelfCommand = ReactiveCommand.Create<VibeTransferViewModel>(removeVibeTransfer),
        };

        vm.Content = new VibeTransferControl
        {
            DataContext = vm
        };
        vm.Subscription = vm.WhenAnyValue(v => v.ImageData, v => v.ImagePath, v => v.ReferenceStrength, v => v.ReferenceInformationExtracted).Subscribe(_ => updateVibeTransfer());

        VibeTransferViewModels.Add(vm);
        return vm;
    }

    private void addCharacter()
    {
        GenerationConfig.GenerationParameter.CharacterPrompts.Add(new V4CharPrompt
        {
            Id = ++nextCharacterId,
            MoveUpCommand = ReactiveCommand.Create<V4CharPrompt>(moveUpCharacter),
            MoveDownCommand = ReactiveCommand.Create<V4CharPrompt>(moveDownCharacter),
            RemoveSelfCommand = ReactiveCommand.Create<V4CharPrompt>(removeCharacter)
        });
    }

    private void moveUpCharacter(V4CharPrompt charPrompt) => moveCharacter(charPrompt, -1);

    private void moveDownCharacter(V4CharPrompt charPrompt) => moveCharacter(charPrompt, 1);

    private void moveCharacter(V4CharPrompt charPrompt, int offset)
    {
        int oldIndex = GenerationConfig.GenerationParameter.CharacterPrompts.IndexOf(charPrompt);
        int newIndex = Math.Clamp(oldIndex + offset, 0, GenerationConfig.GenerationParameter.CharacterPrompts.Count - 1);
        GenerationConfig.GenerationParameter.CharacterPrompts.RemoveAt(oldIndex);
        GenerationConfig.GenerationParameter.CharacterPrompts.Insert(newIndex, charPrompt);
    }

    private void removeCharacter(V4CharPrompt charPrompt)
    {
        GenerationConfig.GenerationParameter.CharacterPrompts.Remove(charPrompt);
    }

    private void removeVibeTransfer(VibeTransferViewModel vm)
    {
        vm.Subscription?.Dispose();
        VibeTransferViewModels.Remove(vm);
    }

    private void loadImages()
    {
        var img2ImgFile = App.StorageProvider?.TryGetFileFromPathAsync(GenerationConfig.GenerationParameter.Image ?? string.Empty).Result;

        if (img2ImgFile != null)
            Img2ImgViewModel.SetReferenceImage(img2ImgFile).ConfigureAwait(false);

        // Convert legacy single-image parameters
        if (GenerationConfig.GenerationParameter.ReferenceStrength != null)
        {
            GenerationConfig.GenerationParameter.ReferenceImageMultiple = [GenerationConfig.GenerationParameter.ReferenceImage ?? string.Empty];
            GenerationConfig.GenerationParameter.ReferenceStrengthMultiple = [GenerationConfig.GenerationParameter.ReferenceStrength.GetValueOrDefault(1)];
            GenerationConfig.GenerationParameter.ReferenceInformationExtractedMultiple = [GenerationConfig.GenerationParameter.ReferenceInformationExtracted.GetValueOrDefault(1)];
        }

        // Clear legacy single-image parameters
        GenerationConfig.GenerationParameter.ReferenceImage = null;
        GenerationConfig.GenerationParameter.ReferenceStrength = null;
        GenerationConfig.GenerationParameter.ReferenceInformationExtracted = null;

        // TODO: This is stupid. addVibeTransfer() creates subscriptions which resets ReferenceImageMultiple etc
        string[] referenceImageMultiple = GenerationConfig.GenerationParameter.ReferenceImageMultiple;
        double[] referenceStrengthMultiple = GenerationConfig.GenerationParameter.ReferenceStrengthMultiple;
        double[] referenceInformationExtractedMultiple = GenerationConfig.GenerationParameter.ReferenceInformationExtractedMultiple;

        for (int i = 0; i < referenceStrengthMultiple.Length; i++)
        {
            var vm = addVibeTransfer();

            vm.ReferenceStrength = referenceStrengthMultiple[i];
            vm.ReferenceInformationExtracted = referenceInformationExtractedMultiple[i];

            if (i < referenceImageMultiple.Length)
            {
                string referenceImage = referenceImageMultiple[i];

                if (!string.IsNullOrEmpty(referenceImage))
                {
                    var file = App.StorageProvider?.TryGetFileFromPathAsync(referenceImage).Result;

                    if (file != null)
                        vm.SetReferenceImage(file).ConfigureAwait(false);
                }
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

        for (int i = 0; i < count; i++)
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
        updateCost();
    }

    private void updateCost()
    {
        int cost = AnlasCostCalculator.Calculate(GenerationConfig, Api?.SubscriptionInfo);
        int replace = 1;

        // Trim spaces between words
        string[] tags = GenerationConfig.Prompt.Split(',', StringSplitOptions.TrimEntries);
        string prompt = string.Join(',', tags);
        using var reader = new StringReader(GenerationConfig.Replace);
        using (var csv = new CsvParser(reader, csvConfiguration))
        {
            while (csv.Read())
            {
                string[] records = csv.Record;
                string toReplace = records[0];
                int index = prompt.IndexOf(toReplace, StringComparison.Ordinal);
                int end = index + toReplace.Length;

                // Ensure the matched tag is a full word split by comma
                if (index >= 0 && (index == 0 || end == prompt.Length || Regex.IsMatch(prompt, $@",(?:\{{|\[)*{Regex.Escape(toReplace)}(?:\}}|\])*,")))
                {
                    replace *= records.Length;
                }
            }
        }

        AnlasCost = cost * replace;
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
