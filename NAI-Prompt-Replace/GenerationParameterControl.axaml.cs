using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NAI_Prompt_Replace;

public partial class GenerationParameterControl : UserControl
{
    private static readonly List<string> models = ["nai-diffusion-3", "safe-diffusion", "nai-diffusion-furry", "nai-diffusion-inpainting", "nai-diffusion-3-inpainting", "safe-diffusion-inpainting", "furry-diffusion-inpainting", "kandinsky-vanilla", "nai-diffusion-2", "nai-diffusion"];
    private static readonly List<string> samplers = ["k_euler", "k_euler_ancestral", "k_dpmpp_2s_ancestral", "k_dpmpp_2m", "k_dpmpp_sde", "ddim_v3"];

    public static readonly StyledProperty<GenerationConfig> ConfigProperty = AvaloniaProperty.Register<GenerationParameterControl, GenerationConfig>(nameof(Config));

    public GenerationConfig Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    // For designer preview
    public GenerationParameterControl() : this(new GenerationConfig())
    {
    }

    public GenerationParameterControl(GenerationConfig config)
    {
        InitializeComponent();
        DataContext = config;
        SetValue(ConfigProperty, config);

        ModelComboBox.ItemsSource = models;
        ModelComboBox.SelectedIndex = models.IndexOf(config.Model);
        SamplerComboBox.SelectedIndex = samplers.IndexOf(config.GenerationParameter.Sampler);
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

        if (SamplerComboBox.SelectedIndex == 5 && ModelComboBox.SelectedIndex != 0)
        {
            Config.GenerationParameter.Sampler = "ddim";
        }
    }

    private void ModelComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Config.Model = models[ModelComboBox.SelectedIndex];
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await TopLevel.GetTopLevel(this)?.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
}
