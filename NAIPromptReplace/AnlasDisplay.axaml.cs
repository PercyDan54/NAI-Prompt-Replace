using Avalonia;
using Avalonia.Controls;

namespace NAIPromptReplace;

public partial class AnlasDisplay : UserControl
{
    public static readonly StyledProperty<int> ValueProperty = AvaloniaProperty.Register<AnlasDisplay, int>(nameof(Value));

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public AnlasDisplay()
    {
        DataContext = this;
        InitializeComponent();
    }
}
