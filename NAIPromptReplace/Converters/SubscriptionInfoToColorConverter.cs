using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public class SubscriptionInfoToColorConverter : IMultiValueConverter
{
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is Control control && values[1] is SubscriptionInfo subscriptionInfo)
        {
            if (subscriptionInfo.Active)
            {
                control.TryFindResource("TextControlForeground", control.ActualThemeVariant, out var defaultBrush);
                return defaultBrush as IBrush ?? new SolidColorBrush(Colors.Black);
            }

            return new SolidColorBrush(Colors.Red);
        }

        return BindingOperations.DoNothing;
    }
}
