using System;
using System.Globalization;
using System.Windows.Data;

namespace Cheari.Controls;

/// <summary>
/// 数学减法值转换器，将绑定值减去参数值。
/// </summary>
public class MathSubtractConverter : IValueConverter
{
    /// <summary>获取单例实例。</summary>
    public static readonly MathSubtractConverter Instance = new MathSubtractConverter();

    /// <summary>将值减去参数。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double dValue && double.TryParse(parameter?.ToString(), out double subtract))
        {
            return dValue - subtract;
        }
        return value;
    }

    /// <summary>不支持反向转换。</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
