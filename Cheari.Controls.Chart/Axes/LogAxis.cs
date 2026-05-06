using System.Windows;
using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// 对数坐标轴，支持自定义底数的对数刻度。
/// </summary>
public class LogAxis : AxisBase
{
    public static readonly DependencyProperty BaseProperty = DependencyProperty.Register(
        nameof(Base), typeof(double), typeof(LogAxis),
        new PropertyMetadata(10.0, OnBaseChanged));

    public override AxisScale Scale => AxisScale.Logarithmic;

    private LogCoordinateMapper? _coordinateMapper;

    public override ICoordinateMapper CoordinateMapper
    {
        get
        {
            double b = Base;
            if (b <= 1.0) b = 10.0;
            return _coordinateMapper ??= new LogCoordinateMapper(b);
        }
    }

    public double Base
    {
        get => (double)GetValue(BaseProperty);
        set => SetValue(BaseProperty, value);
    }

    public LogAxis()
    {
        Placement = AxisPlacement.Left;
        VisibleRange = new DataRange(0.1, 1000);
    }

    public override TickInfo[] GetMajorTicks(double viewportSize)
    {
        var range = VisibleRange;
        if (double.IsNaN(range.Min) || double.IsNaN(range.Max) ||
            double.IsInfinity(range.Min) || double.IsInfinity(range.Max) ||
            range.Min <= 0 || range.Max <= 0)
            return Array.Empty<TickInfo>();

        double b = Base <= 1.0 ? 10.0 : Base;
        double invLogB = 1.0 / Math.Log(b);

        double logMin = Math.Log(range.Min) * invLogB;
        double logMax = Math.Log(range.Max) * invLogB;
        double logRange = logMax - logMin;
        if (logRange <= 0)
            return Array.Empty<TickInfo>();

        switch (TickCalculationMode)
        {
            case TickCalculationMode.FixedCount:
            {
                if (MajorTickCount < 2)
                    goto case TickCalculationMode.Auto;
                double fixedLogInterval = logRange / (MajorTickCount - 1);
                return GenerateLogTicks(range, logMin, logMax, fixedLogInterval, b);
            }

            case TickCalculationMode.FixedInterval:
            {
                if (MajorTickInterval <= 0)
                    goto case TickCalculationMode.Auto;
                double logInterval = MajorTickInterval;
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(logRange / logInterval));
                    logInterval = logRange / nIntervals;
                }
                return GenerateLogTicks(range, logMin, logMax, logInterval, b);
            }

            case TickCalculationMode.Auto:
            default:
            {
                double maxTicks = Math.Max(2, viewportSize / 60.0);
                double logInterval = CalculateNiceInterval(logRange, maxTicks);
                if (AlignRangeToTicks)
                {
                    int nIntervals = Math.Max(1, (int)Math.Round(logRange / logInterval));
                    logInterval = logRange / nIntervals;
                }
                return GenerateLogTicks(range, logMin, logMax, logInterval, b);
            }
        }
    }

    public override TickInfo[] GetMinorTicks(double viewportSize)
    {
        var range = VisibleRange;
        if (double.IsNaN(range.Min) || double.IsNaN(range.Max) ||
            double.IsInfinity(range.Min) || double.IsInfinity(range.Max) ||
            range.Min <= 0 || range.Max <= 0)
            return Array.Empty<TickInfo>();

        double b = Base <= 1.0 ? 10.0 : Base;
        double invLogB = 1.0 / Math.Log(b);

        int minPow = (int)Math.Floor(Math.Log(range.Min) * invLogB);
        int maxPow = (int)Math.Ceiling(Math.Log(range.Max) * invLogB);

        int powerCount = maxPow - minPow + 1;
        const int maxPowerCount = 50;
        if (powerCount > maxPowerCount)
        {
            int step = (int)Math.Ceiling((double)powerCount / maxPowerCount);
            minPow = (minPow / step) * step;
            maxPow = (maxPow / step + 1) * step;
        }

        var ticks = new List<TickInfo>();
        const int maxMinorTicks = 200;
        double epsilon = range.Min * 1e-12;
        for (int p = minPow; p <= maxPow && ticks.Count < maxMinorTicks; p++)
        {
            double baseValue = Math.Pow(b, p);
            if (double.IsInfinity(baseValue))
                break;

            for (int i = 2; i <= 9 && ticks.Count < maxMinorTicks; i++)
            {
                double value = baseValue * i;
                if (value < range.Min - epsilon || value > range.Max + epsilon)
                    continue;

                ticks.Add(new TickInfo(value, string.Empty));
            }
        }

        return ticks.ToArray();
    }

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

        return new DataRange(min, max);
    }

    private TickInfo[] GenerateLogTicks(DataRange range, double logMin, double logMax,
        double logInterval, double b)
    {
        if (logInterval <= 0)
            return Array.Empty<TickInfo>();

        double firstLog;
        if (TickCalculationMode == TickCalculationMode.FixedCount || AlignRangeToTicks)
        {
            firstLog = logMin;
        }
        else
        {
            firstLog = Math.Ceiling(logMin / logInterval) * logInterval;
        }

        var ticks = new List<TickInfo>();
        double epsilon = logInterval * 0.001;

        for (double logPos = firstLog; logPos <= logMax + epsilon; logPos += logInterval)
        {
            double value = Math.Pow(b, logPos);
            if (logPos < logMin - epsilon || logPos > logMax + epsilon)
                continue;

            string label = LabelFormat != null
                ? value.ToString(LabelFormat)
                : FormatLogValue(value);
            ticks.Add(new TickInfo(value, label));
        }

        return ticks.ToArray();
    }

    private static string FormatLogValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return string.Empty;
        if (Math.Abs(value) >= 1e6 || (Math.Abs(value) < 1e-4 && value != 0))
            return value.ToString("0.##E+0");
        if (Math.Abs(value) >= 10000)
            return value.ToString("0.#E+0");
        return value.ToString("G3");
    }

    private static void OnBaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LogAxis axis && (double)e.NewValue > 1.0)
        {
            axis._coordinateMapper = null;
            axis.VisibleRange = new DataRange(0.1, 1000);
        }
    }
}
