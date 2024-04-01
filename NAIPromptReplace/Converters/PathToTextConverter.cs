using System.Globalization;
using Avalonia.Data.Converters;

namespace NAIPromptReplace.Converters;

public class PathToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text)
        {
            int maxLength;
            int.TryParse(parameter as string ?? "40", out maxLength);
            return Util.TruncateString(text, maxLength);
        }
        
        return "Select image";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}