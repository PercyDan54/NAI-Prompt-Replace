using System.Globalization;
using Avalonia.Data.Converters;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public class AnlasCountConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        switch (value)
        {
            case SubscriptionInfo subscriptionInfo:
                return subscriptionInfo.TotalTrainingStepsLeft.ToString();
            
            default:
                return value == null ? 0 : value.ToString();
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
