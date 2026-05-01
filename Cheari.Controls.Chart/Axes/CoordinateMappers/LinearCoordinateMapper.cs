using Cheari.Controls.Core;

namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 线性坐标映射器，实现数据坐标与屏幕坐标的线性转换。
/// 这是默认的坐标映射策略，支持标准的线性图表。
/// </summary>
public class LinearCoordinateMapper : ICoordinateMapper
{
    /// <summary>
    /// 单例实例，避免重复创建。
    /// </summary>
    public static LinearCoordinateMapper Instance { get; } = new();

    /// <summary>
    /// 将数据坐标线性转换为屏幕坐标。
    /// </summary>
    /// <param name="dataValue">数据坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>屏幕坐标值</returns>
    public double DataToScreen(double dataValue, DataRange dataRange, double viewportSize)
    {
        double length = NormalizeRangeLength(dataRange.Length);
        return (dataValue - dataRange.Min) / length * viewportSize;
    }

    /// <summary>
    /// 将屏幕坐标线性转换为数据坐标。
    /// </summary>
    /// <param name="screenValue">屏幕坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>数据坐标值</returns>
    public double ScreenToData(double screenValue, DataRange dataRange, double viewportSize)
    {
        if (viewportSize <= 0)
            return dataRange.Min;

        double length = NormalizeRangeLength(dataRange.Length);
        return dataRange.Min + screenValue / viewportSize * length;
    }

    /// <summary>
    /// 规范化范围长度，避免除零错误。
    /// </summary>
    /// <param name="length">范围长度</param>
    /// <returns>规范化后的长度</returns>
    private static double NormalizeRangeLength(double length)
        => Math.Abs(length) > double.Epsilon ? length : 1.0;
}
