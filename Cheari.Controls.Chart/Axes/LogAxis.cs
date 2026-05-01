using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// 对数坐标轴，以10为底的对数刻度。
/// </summary>
public class LogAxis : AxisBase
{
    /// <summary>
    /// 获取轴的刻度类型，返回对数刻度。
    /// </summary>
    public override AxisScale Scale => AxisScale.Logarithmic;

    /// <summary>
    /// 获取坐标映射器，返回对数坐标映射器实例。
    /// </summary>
    public override ICoordinateMapper CoordinateMapper => LogCoordinateMapper.Instance;

    /// <summary>
    /// 初始化 LogAxis 类的新实例。
    /// </summary>
    public LogAxis()
    {
        Placement = AxisPlacement.Left;
        VisibleRange = new DataRange(0.1, 1000);
    }

    /// <summary>
    /// 获取主刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMajorTicks(double viewportSize)
    {
        var range = VisibleRange;

        if (range.Min <= 0 || range.Max <= 0)
            return Array.Empty<TickInfo>();

        var ticks = new List<TickInfo>();

        int minPow = (int)Math.Floor(Math.Log10(range.Min));
        int maxPow = (int)Math.Ceiling(Math.Log10(range.Max));

        for (int p = minPow; p <= maxPow; p++)
        {
            double baseValue = Math.Pow(10, p);

            foreach (double multiplier in s_majorMultipliers)
            {
                double value = baseValue * multiplier;
                if (value < range.Min || value > range.Max)
                    continue;

                string label = LabelFormat != null
                    ? value.ToString(LabelFormat)
                    : FormatLogValue(value);
                ticks.Add(new TickInfo(value, label));
            }
        }

        if (AlignRangeToTicks && ticks.Count > 0)
        {
            double firstTick = ticks[0].Position;
            double lastTick = ticks[^1].Position;
            if (firstTick != range.Min || lastTick != range.Max)
            {
                ticks.Insert(0, new TickInfo(range.Min,
                    LabelFormat != null ? range.Min.ToString(LabelFormat) : FormatLogValue(range.Min)));
                ticks.Add(new TickInfo(range.Max,
                    LabelFormat != null ? range.Max.ToString(LabelFormat) : FormatLogValue(range.Max)));
            }
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
        var range = VisibleRange;

        if (range.Min <= 0 || range.Max <= 0)
            return Array.Empty<TickInfo>();

        var ticks = new List<TickInfo>();

        int minPow = (int)Math.Floor(Math.Log10(range.Min));
        int maxPow = (int)Math.Ceiling(Math.Log10(range.Max));

        for (int p = minPow; p <= maxPow; p++)
        {
            double baseValue = Math.Pow(10, p);

            for (int i = 2; i <= 9; i++)
            {
                double value = baseValue * i;
                if (value < range.Min || value > range.Max)
                    continue;

                ticks.Add(new TickInfo(value, string.Empty));
            }
        }

        return ticks.ToArray();
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

            if (dsRange.Min > 0)
                min = Math.Min(min, dsRange.Min);
            if (dsRange.Max > 0)
                max = Math.Max(max, dsRange.Max);
        }

        if (min == double.MaxValue || max == double.MinValue)
            return new DataRange(0.1, 1000);

        if (min <= 0)
            min = 0.1;

        int minPow = (int)Math.Floor(Math.Log10(min));
        int maxPow = (int)Math.Ceiling(Math.Log10(max));

        return new DataRange(Math.Pow(10, minPow), Math.Pow(10, maxPow));
    }

    /// <summary>
    /// 格式化对数值的显示标签。
    /// </summary>
    /// <param name="value">要格式化的数值</param>
    /// <returns>格式化后的字符串</returns>
    private static string FormatLogValue(double value)
    {
        double absValue = Math.Abs(value);
        if (absValue >= 1e6)
            return value.ToString("0.#e+0");
        if (absValue >= 1000)
            return value.ToString("F0");
        if (absValue >= 1)
            return value.ToString("F1");
        if (absValue >= 0.01)
            return value.ToString("F3");
        return value.ToString("0.#e+0");
    }

    /// <summary>
    /// 主刻度乘数数组（1, 2, 5），用于在每个数量级内生成主刻度。
    /// </summary>
    private static readonly double[] s_majorMultipliers = [1, 2, 5];
}
