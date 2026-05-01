using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// TimeSpan坐标轴，使用TotalSeconds进行内部计算。
/// 支持时间间隔数据的显示，如_duration、X轴时间戳等。
/// </summary>
public class TimeSpanAxis : AxisBase
{
    /// <summary>
    /// 获取或设置可见范围的起始 TimeSpan。
    /// getter 返回 VisibleRange.Min 对应的 TimeSpan；
    /// setter 以传入的 TimeSpan 作为起始，保持现有范围长度不变，
    /// 若当前范围长度为零则默认使用 100 秒。
    /// </summary>
    public TimeSpan VisibleTimeSpanRange
    {
        get => TimeSpan.FromSeconds(VisibleRange.Min);
        set
        {
            double totalSeconds = value.TotalSeconds;
            double existingLength = VisibleRange.Length;
            double length = existingLength > 0 ? existingLength : 100.0;
            VisibleRange = new DataRange(totalSeconds, totalSeconds + length);
        }
    }

    /// <summary>
    /// 初始化 TimeSpanAxis 类的新实例。
    /// </summary>
    public TimeSpanAxis()
    {
        Placement = AxisPlacement.Bottom;
    }

    /// <summary>
    /// 获取轴的刻度类型，返回线性刻度。
    /// </summary>
    public override AxisScale Scale => AxisScale.Linear;

    /// <summary>
    /// 获取坐标映射器，返回线性坐标映射器实例。
    /// </summary>
    public override ICoordinateMapper CoordinateMapper => LinearCoordinateMapper.Instance;

    /// <summary>
    /// 获取主刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMajorTicks(double viewportSize)
    {
        var range = VisibleRange;
        if (range.Length <= 0)
            return Array.Empty<TickInfo>();

        double maxTicks = Math.Max(2, viewportSize / 80.0);
        double interval = CalculateNiceInterval(range.Length, maxTicks);

        var ticks = new List<TickInfo>();
        double start = Math.Floor(range.Min / interval) * interval;

        for (double value = start; value <= range.Max; value += interval)
        {
            if (value < range.Min)
                continue;

            var timeSpan = TimeSpan.FromSeconds(value);
            string label = FormatTimeSpan(timeSpan);
            ticks.Add(new TickInfo(value, label));
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// 获取次刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMinorTicks(double viewportSize)
    {
        var majorTicks = GetMajorTicks(viewportSize);
        if (majorTicks.Length < 2)
            return Array.Empty<TickInfo>();

        double majorInterval = majorTicks.Length > 1
            ? majorTicks[1].Position - majorTicks[0].Position
            : CalculateNiceInterval(VisibleRange.Length, Math.Max(2, viewportSize / 60.0));

        if (majorInterval <= 0)
            return Array.Empty<TickInfo>();

        double minorInterval = majorInterval / 5.0;
        var minorTicks = new List<TickInfo>();
        double start = Math.Floor(VisibleRange.Min / minorInterval) * minorInterval;

        for (double value = start; value <= VisibleRange.Max; value += minorInterval)
        {
            if (value < VisibleRange.Min || value > VisibleRange.Max)
                continue;

            if (majorTicks.Any(t => Math.Abs(t.Position - value) < minorInterval * 0.1))
                continue;

            minorTicks.Add(new TickInfo(value, string.Empty));
        }

        return minorTicks.ToArray();
    }

    /// <summary>
    /// 计算自动范围。
    /// </summary>
    /// <param name="dataSeries">数据系列集合</param>
    /// <returns>计算的数值范围</returns>
    public override DataRange CalculateAutoRange(IEnumerable<IDataSeries> dataSeries)
    {
        double min = double.MaxValue;
        double max = double.MinValue;
        int count = 0;

        foreach (var ds in dataSeries)
        {
            if (ds == null || ds.Count == 0)
                continue;

            var xRange = ds.XRange;
            if (xRange.Min < min) min = xRange.Min;
            if (xRange.Max > max) max = xRange.Max;
            count++;
        }

        if (count == 0)
            return new DataRange(0, 100);

        double padding = (max - min) * 0.1;
        if (padding == 0) padding = 1.0;
        return new DataRange(min - padding, max + padding);
    }

    /// <summary>
    /// 格式化TimeSpan为可读标签。
    /// </summary>
    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        if (ts.TotalSeconds >= 1)
            return $"{ts.Seconds:F1}s";
        if (ts.TotalMilliseconds >= 1)
            return $"{ts.Milliseconds:F0}ms";
        return $"{ts.Ticks * 100:F0}ns";
    }
}
