using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Cheari.Controls.Converters;

public class ColorToHexConverter : IValueConverter
{
    public static readonly ColorToHexConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color c)
        {
            return c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return "#000000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
