using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NAI_Prompt_Replace;

public partial class GenerationParameterControl : UserControl
{
    private static readonly List<string> models = ["nai-diffusion-3", "safe-diffusion", "nai-diffusion-furry", "nai-diffusion-inpainting", "nai-diffusion-3-inpainting", "safe-diffusion-inpainting", "furry-diffusion-inpainting", "kandinsky-vanilla", "nai-diffusion-2", "nai-diffusion"];
    private static readonly List<string> samplers = ["k_euler", "k_euler_ancestral", "k_dpmpp_2s_ancestral", "k_dpmpp_2m", "k_dpmpp_sde", "ddim_v3"];
    private static readonly List<string> schedulers = ["native", "karras", "exponential", "polyexponential"];

    public static readonly StyledProperty<GenerationConfig> ConfigProperty = AvaloniaProperty.Register<GenerationParameterControl, GenerationConfig>(nameof(Config));

    public GenerationConfig Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    private readonly NovelAIApi? api;

    public event EventHandler? AnlasChanged;

    // For designer preview
    public GenerationParameterControl() : this(new GenerationConfig(), null)
    {
    }

    public GenerationParameterControl(GenerationConfig config, NovelAIApi? api)
    {
        InitializeComponent();
        DataContext = config;
        SetValue(ConfigProperty, config);
        this.api = api;
        Config.GenerationParameter.PropertyChanged += GenerationParameterOnPropertyChanged;
        Config.PropertyChanged += GenerationParameterOnPropertyChanged;

        ModelComboBox.ItemsSource = models;
        ScheduleComboBox.ItemsSource = schedulers;
        ModelComboBox.SelectedIndex = models.IndexOf(config.Model);
        SamplerComboBox.SelectedIndex = samplers.IndexOf(config.GenerationParameter.Sampler);
        ScheduleComboBox.SelectedIndex = schedulers.IndexOf(config.GenerationParameter.NoiseSchedule);
        GenerationParameterOnPropertyChanged(null, null);
    }

    private void GenerationParameterOnPropertyChanged(object? sender, PropertyChangedEventArgs? e)
    {
        var cost = Util.CalculateCost(Config, api?.SubscriptionInfo);
        var replaceLines = Config.Replace.Split(Environment.NewLine).Select(l => Math.Max(l.Split(',').Length, 1));

        foreach (int line in replaceLines)
            cost *= line;

        Dispatcher.UIThread.Invoke(() => AnlasDisplay.Value = cost);
        AnlasChanged?.Invoke(this, null);
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

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            FileTypeChoices =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (file == null)
            return;

        try
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
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
}
