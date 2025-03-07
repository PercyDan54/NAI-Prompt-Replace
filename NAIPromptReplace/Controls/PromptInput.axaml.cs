using Avalonia;
using Avalonia.Controls.Primitives;

namespace NAIPromptReplace.Controls;

public class PromptInput : TemplatedControl
{
    public static readonly StyledProperty<string> PromptProperty = AvaloniaProperty.Register<PromptInput, string>(nameof(Prompt));
    public static readonly StyledProperty<string> NegativePromptProperty = AvaloniaProperty.Register<PromptInput, string>(nameof(NegativePrompt));
    public static readonly StyledProperty<Dictionary<string, string>> ReplacementsProperty = AvaloniaProperty.Register<PromptInput, Dictionary<string, string>>(nameof(Replacements));

    public string Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    public string NegativePrompt
    {
        get => GetValue(NegativePromptProperty);
        set => SetValue(NegativePromptProperty, value);
    }

    public Dictionary<string, string> Replacements
    {
        get => GetValue(ReplacementsProperty);
        set => SetValue(ReplacementsProperty, value);
    }
}
