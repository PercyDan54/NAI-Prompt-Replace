using Avalonia;
using Avalonia.Controls;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Views;

public partial class GenerationParameterControl : UserControl
{
    private static readonly List<string> schedulers = ["native", "karras", "exponential", "polyexponential"];

    // For designer preview
    public GenerationParameterControl()
    {
        InitializeComponent();
        ModelComboBox.ItemsSource = GenerationModelInfo.Models;

        foreach (var control in WrapPanel.Children)
        {
            control.Margin = new Thickness(0,0,5,5);
        }

        foreach (var control in MainGrid.Children)
        {
            control.Margin = new Thickness(0,0,0,5);
        }
    }

    /*
    private void GenerationParameterChanged(object? sender, EventArgs? e)
    {
        int cost = Util.CalculateCost(Config, api?.SubscriptionInfo);
        var replaceLines = Config.Replace.Split(Environment.NewLine).Select(l => Math.Max(l.Split(',').Length, 1));

        foreach (int line in replaceLines)
            cost *= line;

        Dispatcher.UIThread.Invoke(() =>
        {
            AnlasDisplay.Value = cost.ToString();
            AnlasChanged?.Invoke(this, null);
        });
    }*/

    private void preventNullValue(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null)
        {
            var numericUpDown = (NumericUpDown)sender;
            numericUpDown.Value = numericUpDown.Minimum;
        }
    }
}
