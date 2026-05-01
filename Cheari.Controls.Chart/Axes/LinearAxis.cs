using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// 线性坐标轴，支持等间距刻度。
/// </summary>
public class LinearAxis : AxisBase
{
    /// <summary>
    /// 获取轴的刻度类型，返回线性刻度。
    /// </summary>
    public override AxisScale Scale => AxisScale.Linear;

    /// <summary>
    /// 获取坐标映射器，返回线性坐标映射器实例。
    /// </summary>
    public override ICoordinateMapper CoordinateMapper => LinearCoordinateMapper.Instance;

    /// <summary>
    /// 初始化 LinearAxis 类的新实例。
    /// </summary>
    public LinearAxis()
    {
        Placement = AxisPlacement.Left;
    }

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

        switch (TickCalculationMode)
        {
            case TickCalculationMode.FixedCount:
            {
                if (MajorTickCount < 2)
                    goto default;
                double fixedInterval = range.Length / (MajorTickCount - 1);
                return GenerateAlignedTicks(range, fixedInterval);
            }

            case TickCalculationMode.FixedInterval:
            {
                if (MajorTickInterval <= 0)
                    goto default;
                double interval = MajorTickInterval;
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(range.Length / interval));
                    double adjustedInterval = range.Length / nIntervals;
                    return GenerateAlignedTicks(range, adjustedInterval);
                }
                return GenerateTicks(range, interval);
            }

            default:
            {
                double maxTicks = Math.Max(2, viewportSize / 60.0);
                double interval = CalculateNiceInterval(range.Length, maxTicks);
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(range.Length / interval));
                    double adjustedInterval = range.Length / nIntervals;
                    return GenerateAlignedTicks(range, adjustedInterval);
                }
                return GenerateTicks(range, interval);
            }
        }
    }

    /// <summary>
    /// 获取次刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMinorTicks(double viewportSize)
    {
        var range = VisibleRange;
        if (range.Length <= 0)
            return Array.Empty<TickInfo>();

        double majorInterval;

        switch (TickCalculationMode)
        {
            case TickCalculationMode.FixedCount:
            {
                if (MajorTickCount < 2)
                    goto default;
                majorInterval = range.Length / (MajorTickCount - 1);
                break;
            }

            case TickCalculationMode.FixedInterval:
            {
                if (MajorTickInterval <= 0)
                    goto default;
                majorInterval = MajorTickInterval;
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(range.Length / majorInterval));
                    majorInterval = range.Length / nIntervals;
                }
                break;
            }

            default:
            {
                double maxTicks = Math.Max(2, viewportSize / 60.0);
                majorInterval = CalculateNiceInterval(range.Length, maxTicks);
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(range.Length / majorInterval));
                    majorInterval = range.Length / nIntervals;
                }
                break;
            }
        }

        double minorInterval = majorInterval / 5.0;
        if (minorInterval <= 0)
            return Array.Empty<TickInfo>();

        if (TickCalculationMode == TickCalculationMode.FixedCount || AlignRangeToTicks)
            return GenerateAlignedTicks(range, minorInterval);

        return GenerateTicks(range, minorInterval);
    }

    /// <summary>
    /// 根据数据系列计算自动范围。
    /// </summary>
    /// <param name="dataSeries">数据系列集合</param>
    /// <returns>计算得到的自动范围</returns>
    public override DataRange CalculateAutoRange(IEnumerable<IDataSeries> dataSeries)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var ds in dataSeries)
        {
            if (ds == null || ds.Count == 0) continue;

            var dsRange = Placement is AxisPlacement.Top or AxisPlacement.Bottom
                ? ds.XRange
                : ds.YRange;

            min = Math.Min(min, dsRange.Min);
            max = Math.Max(max, dsRange.Max);
        }

        if (min == double.MaxValue)
            return new DataRange(0, 100);

        return CalculateNiceRange(min, max);
    }

    /// <summary>
    /// 生成从范围 Min 到 Max 的对齐刻度，第一个和最后一个刻度分别在 Min 和 Max。
    /// </summary>
    private TickInfo[] GenerateAlignedTicks(DataRange range, double interval)
    {
        if (interval <= 0 || range.Length <= 0)
            return Array.Empty<TickInfo>();

        int count = (int)Math.Round(range.Length / interval) + 1;
        var ticks = new List<TickInfo>(count);

        for (int i = 0; i < count; i++)
        {
            double value = range.Min + i * interval;
            string label = LabelFormat != null
                ? value.ToString(LabelFormat)
                : FormatDouble(value);
            ticks.Add(new TickInfo(value, label));
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// 生成指定范围和间隔的刻度数组。
    /// </summary>
    /// <param name="range">数值范围</param>
    /// <param name="interval">刻度间隔</param>
    /// <returns>刻度信息数组</returns>
    private TickInfo[] GenerateTicks(DataRange range, double interval)
    {
        if (interval <= 0 || range.Length <= 0)
            return Array.Empty<TickInfo>();

        var ticks = new List<TickInfo>();
        double start = Math.Ceiling(range.Min / interval) * interval;
        int startIndex = (int)Math.Round((start - range.Min) / interval);
        int maxSteps = (int)((range.Max - start) / interval) + 2;

        for (int i = 0; i <= maxSteps; i++)
        {
            double value = start + i * interval;
            if (value > range.Max + interval * 0.001)
                break;

            string label = LabelFormat != null
                ? value.ToString(LabelFormat)
                : FormatDouble(value);
            ticks.Add(new TickInfo(value, label));
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// 根据数值大小选择合适的格式化方式。
    /// </summary>
    /// <param name="value">要格式化的数值</param>
    /// <returns>格式化后的字符串</returns>
    private static string FormatDouble(double value)
    {
        double absValue = Math.Abs(value);
        if (absValue == 0)
            return "0";

        if (absValue >= 1e6 || (absValue < 1e-3 && absValue > 0))
            return value.ToString("G4");

        if (absValue >= 100)
            return value.ToString("F0");

        if (absValue >= 1)
            return value.ToString("F2");

        return value.ToString("G4");
    }
}
