using Cheari.Controls.Core;

namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 对数坐标映射器，实现数据坐标与屏幕坐标的对数转换。
/// 适用于显示数量级跨度较大的数据。
/// </summary>
public class LogCoordinateMapper : ICoordinateMapper
{
    /// <summary>
    /// 单例实例，避免重复创建。
    /// </summary>
    public static LogCoordinateMapper Instance { get; } = new();

    /// <summary>
    /// 将数据坐标通过对数转换映射到屏幕坐标。
    /// </summary>
    /// <param name="dataValue">数据坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>屏幕坐标值</returns>
    public double DataToScreen(double dataValue, DataRange dataRange, double viewportSize)
    {
        if (dataRange.Min <= 0 || dataRange.Max <= 0)
            return LinearCoordinateMapper.Instance.DataToScreen(dataValue, dataRange, viewportSize);

        if (dataValue <= 0)
            return 0;

        double logMin = Math.Log10(dataRange.Min);
        double logMax = Math.Log10(dataRange.Max);
        double logValue = Math.Log10(dataValue);
        double logLength = logMax - logMin;

        if (Math.Abs(logLength) < double.Epsilon)
            return 0;

        return (logValue - logMin) / logLength * viewportSize;
    }

    /// <summary>
    /// 将屏幕坐标通过反对数转换映射到数据坐标。
    /// </summary>
    /// <param name="screenValue">屏幕坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>数据坐标值</returns>
    public double ScreenToData(double screenValue, DataRange dataRange, double viewportSize)
    {
        if (dataRange.Min <= 0 || dataRange.Max <= 0)
            return LinearCoordinateMapper.Instance.ScreenToData(screenValue, dataRange, viewportSize);

        if (viewportSize <= 0)
            return dataRange.Min;

        double logMin = Math.Log10(dataRange.Min);
        double logMax = Math.Log10(dataRange.Max);
        double logLength = logMax - logMin;

        if (Math.Abs(logLength) < double.Epsilon)
            return dataRange.Min;

        double ratio = screenValue / viewportSize;
        double logValue = logMin + ratio * logLength;
        return Math.Pow(10, logValue);
    }
}