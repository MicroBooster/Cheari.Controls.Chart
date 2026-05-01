using System.ComponentModel;
using System.Globalization;

namespace Cheari.Controls.Core;

/// <summary>
/// 数据范围结构体，表示一个连续的数值区间。
/// 用于定义图表轴的可见范围、数据系列的数据边界等。
/// 支持在 XAML 中使用 "min,max" 格式字符串（如 "0,10"）。
/// </summary>
[TypeConverter(typeof(DataRangeTypeConverter))]
public struct DataRange
{
    /// <summary>
    /// 获取或设置范围的最小值。
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// 获取或设置范围的最大值。
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// 获取范围的长度（Max - Min）。
    /// </summary>
    public double Length => Max - Min;

    /// <summary>
    /// 使用指定的最小值和最大值初始化 DataRange 结构。
    /// </summary>
    /// <param name="min">范围的最小值</param>
    /// <param name="max">范围的最大值</param>
    public DataRange(double min, double max)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>
/// DataRange 的类型转换器，支持从 "min,max" 格式字符串转换。
/// </summary>
public class DataRangeTypeConverter : TypeConverter
{
    /// <summary>
    /// 判断是否可以从字符串转换为 DataRange。
    /// </summary>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// 将 "min,max" 格式字符串转换为 DataRange。
    /// </summary>
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            var parts = s.Split(',');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double min) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
            {
                return new DataRange(min, max);
            }
        }
        throw new FormatException("DataRange format: 'min,max' (e.g. '0,10')");
    }
}
