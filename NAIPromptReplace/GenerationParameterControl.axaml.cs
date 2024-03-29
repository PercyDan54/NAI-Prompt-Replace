using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NAIPromptReplace.Models;
using SkiaSharp;

namespace NAIPromptReplace;

public partial class GenerationParameterControl : UserControl
{
    private static readonly List<string> models =
    [
        "nai-diffusion-3", "safe-diffusion", "nai-diffusion-furry", "nai-diffusion-inpainting",
        "nai-diffusion-3-inpainting", "safe-diffusion-inpainting", "furry-diffusion-inpainting", "kandinsky-vanilla",
        "nai-diffusion-2", "nai-diffusion"
    ];

    private static readonly List<string> samplers = ["k_euler", "k_euler_ancestral", "k_dpmpp_2s_ancestral", "k_dpmpp_2m", "k_dpmpp_sde", "ddim_v3"];

    private static readonly List<string> schedulers = ["native", "karras", "exponential", "polyexponential"];

    public static readonly StyledProperty<GenerationConfig> ConfigProperty = AvaloniaProperty.Register<GenerationParameterControl, GenerationConfig>(nameof(Config));

    public GenerationConfig Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    private readonly NovelAIApi? api;

    private static readonly FilePickerOpenOptions anyFilePickerOptions = new FilePickerOpenOptions
    {
        FileTypeFilter =
        [
            new FilePickerFileType("Any")
            {
                Patterns = ["*.*"]
            }
        ]
    };

    public static readonly FilePickerSaveOptions SaveConfigFilePickerOptions = new FilePickerSaveOptions
    {
        FileTypeChoices =
        [
            new FilePickerFileType("JSON")
            {
                Patterns = ["*.json"]
            }
        ]
    };

    public event EventHandler? AnlasChanged;

    // For designer preview
    public GenerationParameterControl() : this(new GenerationConfig())
    {
    }

    public GenerationParameterControl(GenerationConfig config, NovelAIApi? api = null)
    {
        InitializeComponent();
        DataContext = config;
        SetValue(ConfigProperty, config);
        loadReferenceImage();
        VibeTransferExpander.AddHandler(DragDrop.DropEvent, onDrop);
        this.api = api;

        foreach (var control in WrapPanel.Children)
        {
            control.Margin = new Thickness(0,0,5,5);
        }

        foreach (var control in MainGrid.Children)
        {
            control.Margin = new Thickness(0,0,0,5);
        }

        if (api != null)
            api.SubscriptionChanged += GenerationParameterChanged;

        Config.GenerationParameter.PropertyChanged += GenerationParameterChanged;
        Config.PropertyChanged += GenerationParameterChanged;

        ModelComboBox.ItemsSource = models;
        ScheduleComboBox.ItemsSource = schedulers;
        ModelComboBox.SelectedIndex = models.IndexOf(config.Model);
        SamplerComboBox.SelectedIndex = samplers.IndexOf(config.GenerationParameter.Sampler);
        ScheduleComboBox.SelectedIndex = schedulers.IndexOf(config.GenerationParameter.NoiseSchedule);
        GenerationParameterChanged(null, null);
    }

    private void GenerationParameterChanged(object? sender, EventArgs? e)
    {
        int cost = Util.CalculateCost(Config, api?.SubscriptionInfo);
        var replaceLines = Config.Replace.Split(Environment.NewLine).Select(l => Math.Max(l.Split(',').Length, 1));

        foreach (int line in replaceLines)
            cost *= line;

        Dispatcher.UIThread.Invoke(() =>
        {
            AnlasDisplay.Value = cost;
            AnlasChanged?.Invoke(this, null);
        });
    }

    private void preventNullValue(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
        {
            var numericUpDown = (NumericUpDown)sender;
            numericUpDown.Value = numericUpDown.Minimum;
        }
    }

    private void SamplerComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Config.GenerationParameter.Sampler = samplers[SamplerComboBox.SelectedIndex];
        SmeaCheckBox.IsEnabled = DynCheckBox.IsEnabled = true;

        if (SamplerComboBox.SelectedIndex == 5)
        {
            if (ModelComboBox.SelectedIndex != 0)
                Config.GenerationParameter.Sampler = "ddim";
            Config.GenerationParameter.Smea = Config.GenerationParameter.Dyn = false;
            SmeaCheckBox.IsEnabled = DynCheckBox.IsEnabled = false;
        }
    }

    private void ModelComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Config.Model = models[ModelComboBox.SelectedIndex];
    }

    private void ScheduleComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Config.GenerationParameter.NoiseSchedule = schedulers[ScheduleComboBox.SelectedIndex];
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(SaveConfigFilePickerOptions);

        if (file == null)
            return;

        SaveConfig(file.Path.LocalPath);
    }

    public void SaveConfig(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });
        
        if (file.Count < 1)
            return;

        OutputPathTextBox.Text = file[0].Path.LocalPath;
    }

    private void OpenButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                string path = string.IsNullOrEmpty(Config.OutputPath) ? Environment.CurrentDirectory : Config.OutputPath;

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            });
        }
        catch
        {
        }
    }

    private async void BrowseRefImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(anyFilePickerOptions);

        if (files.Count == 0)
            return;

        Config.GenerationParameter.ReferenceImage = files[0].Path.LocalPath;
        loadReferenceImage();
    }

    private void onDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();

            foreach (var item in files)
            {
                if (item is IStorageFile file)
                {
                    Config.GenerationParameter.ReferenceImage = file.Path.LocalPath;
                }
            }

            loadReferenceImage();
            e.Handled = true;
        }
    }

    private void loadReferenceImage()
    {
        string? file = Config.GenerationParameter.ReferenceImage;

        if (!File.Exists(file))
        {
            Config.GenerationParameter.ReferenceImage = null;
            ReferenceImage.Source = null;
            RefImagePathText.Text = "Select reference image";
            VibeTransferExpander.Header = "Vibe Transfer (None)";
            return;
        }

        using var fileStream = File.OpenRead(file);
        using var im = SKImage.FromEncodedData(fileStream);
        fileStream.Position = 0;

        if (im != null)
        {
            fileStream.Position = 0;
            ReferenceImage.Source = new Bitmap(fileStream);
            Config.GenerationParameter.ReferenceImage = file;
            RefImagePathText.Text = Util.TruncateString(file, 75);
            VibeTransferExpander.Header = $"Vibe Transfer ({Util.TruncateString(Path.GetFileName(file), 75)})";
        }
    }

    private void removeReferenceImage()
    {
        Config.GenerationParameter.ReferenceImage = null;
        loadReferenceImage();
    }

    private void RemoveRefImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        removeReferenceImage();
    }
}
