using Avalonia;
using Avalonia.Controls;

namespace NAIPromptReplace.Views;

public partial class GenerationParameterControl : UserControl
{
    public GenerationParameterControl()
    {
        InitializeComponent();

        foreach (var control in WrapPanel.Children)
        {
            control.Margin = new Thickness(0, 0, 5, 5);
        }
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
