using Avalonia;
using Avalonia.Controls;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Views;

public partial class GenerationParameterControl : UserControl
{
    private static readonly List<string> schedulers = ["native", "karras", "exponential", "polyexponential"];

    public GenerationParameterControl()
    {
        InitializeComponent();
        ModelComboBox.ItemsSource = GenerationModelInfo.Models;
        ScheduleComboBox.ItemsSource = schedulers;

        foreach (var control in WrapPanel.Children)
        {
            control.Margin = new Thickness(0,0,5,5);
        }

        foreach (var control in MainGrid.Children)
        {
            control.Margin = new Thickness(0,0,0,5);
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
