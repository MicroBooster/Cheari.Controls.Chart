using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cheari.Controls.Legend;

namespace Demo.Converters;

public class PositionEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LegendPosition pos && parameter is string paramStr
            && Enum.TryParse<LegendPosition>(paramStr, out var target))
            return pos == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<LegendPosition>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}

public class OrientationEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LegendOrientation orient && parameter is string paramStr
            && Enum.TryParse<LegendOrientation>(paramStr, out var target))
            return orient == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<LegendOrientation>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}

public class HAlignEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HorizontalAlignment align && parameter is string paramStr
            && Enum.TryParse<HorizontalAlignment>(paramStr, out var target))
            return align == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<HorizontalAlignment>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}

public class VAlignEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VerticalAlignment align && parameter is string paramStr
            && Enum.TryParse<VerticalAlignment>(paramStr, out var target))
            return align == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<VerticalAlignment>(paramStr, out var target))
            return target;
        return Binding.DoNothing;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
            return false;
        return Binding.DoNothing;
    }
}

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class VisibilityIndicatorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool visible ? (visible ? "ON" : "OFF") : "--";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
