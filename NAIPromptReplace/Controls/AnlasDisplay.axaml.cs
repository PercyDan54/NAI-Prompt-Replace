using Avalonia;
using Avalonia.Controls.Primitives;

namespace NAIPromptReplace.Controls;

public class AnlasDisplay : TemplatedControl
{
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<AnlasDisplay, string>(nameof(Value));

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
