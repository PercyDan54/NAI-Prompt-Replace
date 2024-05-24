using NAIPromptReplace.Views;

namespace NAIPromptReplace.ViewModels;

public class TabViewModel
{
    public string Name { get; set; } = string.Empty;
    public GenerationParameterControl Control { get; set; }
}
