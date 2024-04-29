using System.Globalization;
using Avalonia.Data.Converters;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public class SubscriptionInfoToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SubscriptionInfo subscriptionInfo)
            return subscriptionInfo.ToString();

        return "Not logged in";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
