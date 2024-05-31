using Avalonia;
using Avalonia.Controls;

namespace NAIPromptReplace.Views;

public partial class GenerationParameterControl : UserControl
{
    public GenerationParameterControl()
    {
        InitializeComponent();
    }

    private void preventNullValue(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
        {
            var numericUpDown = (NumericUpDown)sender;
            numericUpDown.Value = numericUpDown.Minimum;
        }
    }
}
