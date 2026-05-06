using System.Globalization;
using System.Windows.Data;
using Demo.ViewModels;

namespace Demo.Converters;

public class LogChartTypeEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogDemoChartType chartType && parameter is string paramStr
            && Enum.TryParse<LogDemoChartType>(paramStr, out var target))
            return chartType == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<LogDemoChartType>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}
