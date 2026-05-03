using System.Globalization;
using System.Windows.Data;
using Demo.ViewModels;

namespace Demo.Converters;

public class ChartTypeEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DemoChartType chartType && parameter is string paramStr
            && Enum.TryParse<DemoChartType>(paramStr, out var target))
            return chartType == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<DemoChartType>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}
