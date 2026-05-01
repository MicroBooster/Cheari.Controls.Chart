using System;
using System.Globalization;
using System.Windows.Data;

namespace Cheari.Controls;

/// <summary>
/// 数学加法值转换器，将绑定值与参数值相加。
/// </summary>
public class MathAddConverter : IValueConverter
{
    /// <summary>获取单例实例。</summary>
    public static readonly MathAddConverter Instance = new MathAddConverter();

    /// <summary>将值与参数相加。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double dValue && double.TryParse(parameter?.ToString(), out double add))
        {
            return dValue + add;
        }
        return value;
    }

    /// <summary>不支持反向转换。</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
