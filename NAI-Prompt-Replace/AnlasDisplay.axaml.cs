using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NAI_Prompt_Replace;

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
