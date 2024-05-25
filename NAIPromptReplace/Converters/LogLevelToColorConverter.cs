using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Logging;
using Avalonia.Media;

namespace NAIPromptReplace.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    private static SolidColorBrush warningBrush = new SolidColorBrush(new Color(255, 225, 170, 0));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogEventLevel logEventLevel)
            return BindingOperations.DoNothing;
        
        switch (logEventLevel)
        {
            default:
                return BindingOperations.DoNothing;

            case LogEventLevel.Error:
                return Brushes.Red;
            
            case LogEventLevel.Warning:
                return warningBrush;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
