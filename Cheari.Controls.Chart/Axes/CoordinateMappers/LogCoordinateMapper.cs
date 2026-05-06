using Cheari.Controls.Core;

namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 对数坐标映射器，实现数据坐标与屏幕坐标的对数转换。
/// 适用于显示数量级跨度较大的数据。
/// </summary>
public class LogCoordinateMapper : ICoordinateMapper
{
    private readonly double _base;
    private readonly double _invLogBase;

    /// <summary>
    /// 底数为 10 的单例实例。
    /// </summary>
    public static LogCoordinateMapper Instance { get; } = new(10.0);

    /// <summary>
    /// 用指定底数创建对数坐标映射器。
    /// </summary>
    public LogCoordinateMapper(double logBase)
    {
        _base = logBase;
        _invLogBase = 1.0 / Math.Log(logBase);
    }

    public double LogBase => _base;

    internal double InvLogBase => _invLogBase;

    /// <inheritdoc/>
    public double DataToScreen(double dataValue, DataRange dataRange, double viewportSize)
    {
        if (dataRange.Min <= 0 || dataRange.Max <= 0)
            return LinearCoordinateMapper.Instance.DataToScreen(dataValue, dataRange, viewportSize);

        if (dataValue <= 0)
            return 0;

        double logMin = Math.Log(dataRange.Min) * _invLogBase;
        double logMax = Math.Log(dataRange.Max) * _invLogBase;
        double logValue = Math.Log(dataValue) * _invLogBase;
        double logLength = logMax - logMin;

        if (Math.Abs(logLength) < double.Epsilon)
            return 0;

        double result = (logValue - logMin) / logLength * viewportSize;

        return result;
    }

    /// <inheritdoc/>
    public double ScreenToData(double screenValue, DataRange dataRange, double viewportSize)
    {
        if (dataRange.Min <= 0 || dataRange.Max <= 0)
            return LinearCoordinateMapper.Instance.ScreenToData(screenValue, dataRange, viewportSize);

        if (viewportSize <= 0)
            return dataRange.Min;

        double logMin = Math.Log(dataRange.Min) * _invLogBase;
        double logMax = Math.Log(dataRange.Max) * _invLogBase;
        double logLength = logMax - logMin;

        if (Math.Abs(logLength) < double.Epsilon)
            return dataRange.Min;

        double ratio = screenValue / viewportSize;
        double logValue = logMin + ratio * logLength;
        return Math.Pow(_base, logValue);
    }
}
