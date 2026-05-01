using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// 日期时间坐标轴，适用于时间序列数据。
/// </summary>
public class DateTimeAxis : AxisBase
{
    /// <summary>
    /// 获取轴的刻度类型，返回日期时间刻度。
    /// </summary>
    public override AxisScale Scale => AxisScale.DateTime;

    /// <summary>
    /// 获取坐标映射器，返回日期时间坐标映射器实例。
    /// </summary>
    public override ICoordinateMapper CoordinateMapper => DateTimeCoordinateMapper.Instance;

    /// <summary>
    /// 初始化 DateTimeAxis 类的新实例。
    /// </summary>
    public DateTimeAxis()
    {
        Placement = AxisPlacement.Bottom;
        var now = DateTime.Now;
        VisibleRange = new DataRange(
            DateTimeCoordinateMapper.DateTimeToDouble(now.AddHours(-1)),
            DateTimeCoordinateMapper.DateTimeToDouble(now));
    }

    /// <summary>
    /// 获取主刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMajorTicks(double viewportSize)
    {
        var range = VisibleRange;
        double maxTicks = Math.Max(2, viewportSize / 80.0);

        if (range.Length <= 0)
            return Array.Empty<TickInfo>();

        var minDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Min);
        var maxDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Max);
        var timeSpan = maxDate - minDate;

        var (interval, kind) = ChooseInterval(timeSpan, maxTicks);

        return GenerateDateTimeTicks(range, interval, kind);
    }

    /// <summary>
    /// 获取次刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public override TickInfo[] GetMinorTicks(double viewportSize)
    {
        var range = VisibleRange;
        double maxTicks = Math.Max(2, viewportSize / 40.0);

        if (range.Length <= 0)
            return Array.Empty<TickInfo>();

        var minDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Min);
        var maxDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Max);
        var timeSpan = maxDate - minDate;

        var (interval, kind) = ChooseInterval(timeSpan, maxTicks);

        var (minorInterval, minorKind) = GetMinorInterval(interval, kind);

        return GenerateDateTimeTicks(range, minorInterval, minorKind);
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

            var dsRange = ds.XRange;
            min = Math.Min(min, dsRange.Min);
            max = Math.Max(max, dsRange.Max);
        }

        if (min == double.MaxValue)
            return VisibleRange;

        double margin = (max - min) * 0.05;
        if (margin == 0) margin = 1.0 / 86400.0;

        return new DataRange(min - margin, max + margin);
    }

    /// <summary>
    /// 根据时间跨度和最大刻度数选择合适的刻度间隔。
    /// </summary>
    /// <param name="span">时间跨度</param>
    /// <param name="maxTicks">最大刻度数</param>
    /// <returns>间隔值和间隔类型</returns>
    private static (double Interval, DateTimeTickKind Kind) ChooseInterval(TimeSpan span, double maxTicks)
    {
        double totalSeconds = span.TotalSeconds;
        if (totalSeconds <= 0) return (1, DateTimeTickKind.Second);

        double roughInterval = totalSeconds / maxTicks;

        if (roughInterval < 1) return (0.001, DateTimeTickKind.Millisecond);
        if (roughInterval < 2) return (1, DateTimeTickKind.Second);
        if (roughInterval < 5) return (2, DateTimeTickKind.Second);
        if (roughInterval < 15) return (5, DateTimeTickKind.Second);
        if (roughInterval < 30) return (15, DateTimeTickKind.Second);
        if (roughInterval < 60) return (30, DateTimeTickKind.Second);
        if (roughInterval < 120) return (1, DateTimeTickKind.Minute);
        if (roughInterval < 300) return (2, DateTimeTickKind.Minute);
        if (roughInterval < 900) return (5, DateTimeTickKind.Minute);
        if (roughInterval < 1800) return (15, DateTimeTickKind.Minute);
        if (roughInterval < 3600) return (30, DateTimeTickKind.Minute);
        if (roughInterval < 7200) return (1, DateTimeTickKind.Hour);
        if (roughInterval < 21600) return (3, DateTimeTickKind.Hour);
        if (roughInterval < 43200) return (6, DateTimeTickKind.Hour);
        if (roughInterval < 86400) return (12, DateTimeTickKind.Hour);
        if (roughInterval < 172800) return (1, DateTimeTickKind.Day);
        if (roughInterval < 604800) return (1, DateTimeTickKind.Week);

        double totalDays = span.TotalDays;
        double dayInterval = totalDays / maxTicks;

        if (dayInterval < 15) return (1, DateTimeTickKind.Month);
        if (dayInterval < 60) return (3, DateTimeTickKind.Month);
        if (dayInterval < 180) return (6, DateTimeTickKind.Month);

        return (1, DateTimeTickKind.Year);
    }

    /// <summary>
    /// 根据主刻度间隔获取次刻度间隔。
    /// </summary>
    /// <param name="majorInterval">主刻度间隔</param>
    /// <param name="majorKind">主刻度类型</param>
    /// <returns>次刻度间隔和类型</returns>
    private static (double Interval, DateTimeTickKind Kind) GetMinorInterval(double majorInterval, DateTimeTickKind majorKind)
    {
        return majorKind switch
        {
            DateTimeTickKind.Year => (1, DateTimeTickKind.Month),
            DateTimeTickKind.Month => (1, DateTimeTickKind.Day),
            DateTimeTickKind.Week => (1, DateTimeTickKind.Day),
            DateTimeTickKind.Day => (6, DateTimeTickKind.Hour),
            DateTimeTickKind.Hour => (15, DateTimeTickKind.Minute),
            DateTimeTickKind.Minute => (15, DateTimeTickKind.Second),
            DateTimeTickKind.Second => (0.5, DateTimeTickKind.Second),
            DateTimeTickKind.Millisecond => (0.1, DateTimeTickKind.Millisecond),
            _ => (majorInterval / 5, majorKind)
        };
    }

    /// <summary>
    /// 生成日期时间刻度数组。
    /// </summary>
    /// <param name="range">数值范围（DateTime 的 double 表示）</param>
    /// <param name="interval">间隔值</param>
    /// <param name="kind">间隔类型</param>
    /// <returns>刻度信息数组</returns>
    private TickInfo[] GenerateDateTimeTicks(DataRange range, double interval, DateTimeTickKind kind)
    {
        var ticks = new List<TickInfo>();
        var minDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Min);
        var maxDate = DateTimeCoordinateMapper.DoubleToDateTime(range.Max);

        DateTime current = AlignToInterval(minDate, kind);
        int safetyCounter = 0;
        const int maxIterations = 10000;

        while (current <= maxDate)
        {
            if (++safetyCounter > maxIterations)
                break;

            double value = DateTimeCoordinateMapper.DateTimeToDouble(current);
            if (value >= range.Min && value <= range.Max)
            {
                string label = LabelFormat != null
                    ? current.ToString(LabelFormat)
                    : FormatDateTime(current, kind);
                ticks.Add(new TickInfo(value, label));
            }

            DateTime next = IncrementDateTime(current, interval, kind);
            if (next <= current)
                break;
            current = next;
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// 将日期时间对齐到指定的间隔类型。
    /// </summary>
    /// <param name="dt">日期时间</param>
    /// <param name="kind">间隔类型</param>
    /// <returns>对齐后的日期时间</returns>
    private static DateTime AlignToInterval(DateTime dt, DateTimeTickKind kind)
    {
        return kind switch
        {
            DateTimeTickKind.Year => new DateTime(dt.Year, 1, 1),
            DateTimeTickKind.Month => new DateTime(dt.Year, dt.Month, 1),
            DateTimeTickKind.Week => dt.Date.AddDays(-(int)dt.DayOfWeek),
            DateTimeTickKind.Day => dt.Date,
            DateTimeTickKind.Hour => dt.Date.AddHours(dt.Hour),
            DateTimeTickKind.Minute => dt.Date.AddHours(dt.Hour).AddMinutes(dt.Minute),
            DateTimeTickKind.Second => dt.Date.AddHours(dt.Hour).AddMinutes(dt.Minute).AddSeconds(dt.Second),
            DateTimeTickKind.Millisecond => dt,
            _ => dt
        };
    }

    /// <summary>
    /// 根据间隔类型递增日期时间。
    /// </summary>
    /// <param name="dt">日期时间</param>
    /// <param name="interval">间隔值</param>
    /// <param name="kind">间隔类型</param>
    /// <returns>递增后的日期时间</returns>
    private static DateTime IncrementDateTime(DateTime dt, double interval, DateTimeTickKind kind)
    {
        return kind switch
        {
            DateTimeTickKind.Year => dt.AddYears((int)interval),
            DateTimeTickKind.Month => dt.AddMonths((int)interval),
            DateTimeTickKind.Week => dt.AddDays(7 * interval),
            DateTimeTickKind.Day => dt.AddDays(interval),
            DateTimeTickKind.Hour => dt.AddHours(interval),
            DateTimeTickKind.Minute => dt.AddMinutes(interval),
            DateTimeTickKind.Second => dt.AddSeconds(interval),
            DateTimeTickKind.Millisecond => dt.AddMilliseconds(interval),
            _ => dt.AddSeconds(interval)
        };
    }

    /// <summary>
    /// 根据间隔类型格式化日期时间。
    /// </summary>
    /// <param name="dt">日期时间</param>
    /// <param name="kind">间隔类型</param>
    /// <returns>格式化后的字符串</returns>
    private static string FormatDateTime(DateTime dt, DateTimeTickKind kind)
    {
        return kind switch
        {
            DateTimeTickKind.Year => dt.ToString("yyyy"),
            DateTimeTickKind.Month => dt.ToString("yyyy-MM"),
            DateTimeTickKind.Week => dt.ToString("MM-dd"),
            DateTimeTickKind.Day => dt.ToString("MM-dd"),
            DateTimeTickKind.Hour => dt.ToString("HH:mm"),
            DateTimeTickKind.Minute => dt.ToString("HH:mm"),
            DateTimeTickKind.Second => dt.ToString("HH:mm:ss"),
            DateTimeTickKind.Millisecond => dt.ToString("HH:mm:ss.fff"),
            _ => dt.ToString("g")
        };
    }
}