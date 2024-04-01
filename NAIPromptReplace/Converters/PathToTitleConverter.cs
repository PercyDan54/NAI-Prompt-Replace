using System.Globalization;
using Avalonia.Data.Converters;

namespace NAIPromptReplace.Converters;

public class PathToTitleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not string expanderName)
            return null;

        if (values.Count >= 2 && values[1] is string file)
        {
            int maxLength;
            int.TryParse(parameter as string ?? "32", out maxLength);
            return $"{expanderName} ({Util.TruncateString(Path.GetFileName(file), maxLength)})";
        }

        return expanderName;
    }
}
