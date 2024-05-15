using Avalonia.Controls;

namespace NAIPromptReplace.Controls;

public partial class Img2ImgControl : UserControl
{
    public Img2ImgControl()
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
