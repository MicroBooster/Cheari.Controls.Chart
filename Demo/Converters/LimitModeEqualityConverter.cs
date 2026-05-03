using System.Globalization;
using System.Windows.Data;
using Cheari.Controls.Core;

namespace Demo.Converters;

public class LimitModeEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VisibleRangeLimitMode mode && parameter is string paramStr
            && Enum.TryParse<VisibleRangeLimitMode>(paramStr, out var target))
            return mode == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<VisibleRangeLimitMode>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}
