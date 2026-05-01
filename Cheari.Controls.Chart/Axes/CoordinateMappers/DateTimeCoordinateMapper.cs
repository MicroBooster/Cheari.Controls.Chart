using Cheari.Controls.Core;

namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 日期时间坐标映射器，将日期时间转换为数值进行坐标映射。
/// 使用 OLE Automation 日期格式进行转换。
/// </summary>
public class DateTimeCoordinateMapper : ICoordinateMapper
{
    /// <summary>
    /// 单例实例，避免重复创建。
    /// </summary>
    public static DateTimeCoordinateMapper Instance { get; } = new();

    /// <summary>
    /// 将日期时间数据坐标线性转换为屏幕坐标。
    /// </summary>
    /// <param name="dataValue">数据坐标值（OLE Automation 日期格式）</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>屏幕坐标值</returns>
    public double DataToScreen(double dataValue, DataRange dataRange, double viewportSize)
    {
        return LinearCoordinateMapper.Instance.DataToScreen(dataValue, dataRange, viewportSize);
    }

    /// <summary>
    /// 将屏幕坐标线性转换为日期时间数据坐标。
    /// </summary>
    /// <param name="screenValue">屏幕坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>数据坐标值（OLE Automation 日期格式）</returns>
    public double ScreenToData(double screenValue, DataRange dataRange, double viewportSize)
    {
        return LinearCoordinateMapper.Instance.ScreenToData(screenValue, dataRange, viewportSize);
    }

    /// <summary>
    /// 将 DateTime 转换为 OLE Automation 日期格式的 double 值。
    /// </summary>
    /// <param name="dateTime">日期时间</param>
    /// <returns>OLE Automation 日期格式的 double 值</returns>
    public static double DateTimeToDouble(DateTime dateTime)
    {
        return dateTime.ToOADate();
    }

    /// <summary>
    /// 将 OLE Automation 日期格式的 double 值转换为 DateTime。
    /// </summary>
    /// <param name="value">OLE Automation 日期格式的 double 值</param>
    /// <returns>日期时间</returns>
    public static DateTime DoubleToDateTime(double value)
    {
        return DateTime.FromOADate(value);
    }
}