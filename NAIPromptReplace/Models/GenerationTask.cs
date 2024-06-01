using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Models;

public class GenerationTask
{
    public GenerationTask(GenerationConfig generationConfig, GenerationParameterControlViewModel viewModel)
    {
        GenerationConfig = generationConfig;
        ViewModel = viewModel;
    }

    public GenerationConfig GenerationConfig { get; set; }
    public GenerationParameterControlViewModel ViewModel { get; set; }
    public string Filename { get; set; } = string.Empty;
    public Dictionary<string, string> Placeholders { get; set; } = [];
}
