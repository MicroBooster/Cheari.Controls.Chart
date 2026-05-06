using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using System.Windows;
using System.Windows.Media;

namespace Cheari.Controls.Axes;

/// <summary>
/// 坐标轴基类，提供坐标轴的通用功能和依赖属性定义。
/// </summary>
public abstract class AxisBase : DependencyObject, IAxis
{
    private static readonly DoubleCollection s_defaultMinorGridLineDashArray = CreateFrozenDoubleCollection(2, 2);

    /// <summary>
    /// 标识 Id 依赖属性。
    /// </summary>
    public static readonly DependencyProperty IdProperty =
        DependencyProperty.Register(nameof(Id), typeof(string), typeof(AxisBase),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置轴的唯一标识符。
    /// </summary>
    public string Id
    {
        get => (string)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>
    /// 标识 Placement 依赖属性。
    /// </summary>
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(AxisPlacement), typeof(AxisBase),
            new PropertyMetadata(AxisPlacement.Left));

    /// <summary>
    /// 获取或设置轴的位置。
    /// </summary>
    public AxisPlacement Placement
    {
        get => (AxisPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// 获取轴的刻度类型，由派生类实现。
    /// </summary>
    public abstract AxisScale Scale { get; }

    /// <summary>
    /// 标识 VisibleRange 依赖属性。
    /// </summary>
    public static readonly DependencyProperty VisibleRangeProperty =
        DependencyProperty.Register(nameof(VisibleRange), typeof(DataRange), typeof(AxisBase),
            new FrameworkPropertyMetadata(new DataRange(0, 100), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisibleRangeChanged));

    /// <summary>
    /// 获取或设置轴的可见范围。
    /// </summary>
    public DataRange VisibleRange
    {
        get => (DataRange)GetValue(VisibleRangeProperty);
        set => SetValue(VisibleRangeProperty, value);
    }

    private DataRange _coreRange;
    /// <summary>
    /// 获取或设置核心数据范围（不含留白），用于渲染时裁剪曲线数据。
    /// </summary>
    public DataRange CoreRange
    {
        get => _coreRange;
        set => _coreRange = value;
    }

    /// <summary>
    /// 标识 VisibleRangeLimit 依赖属性。
    /// </summary>
    public static readonly DependencyProperty VisibleRangeLimitProperty =
        DependencyProperty.Register(nameof(VisibleRangeLimit), typeof(DataRange), typeof(AxisBase),
            new PropertyMetadata(new DataRange(double.MinValue, double.MaxValue), OnVisibleRangeLimitChanged));

    /// <summary>
    /// 获取或设置可见范围的限制边界。
    /// </summary>
    public DataRange VisibleRangeLimit
    {
        get => (DataRange)GetValue(VisibleRangeLimitProperty);
        set => SetValue(VisibleRangeLimitProperty, value);
    }

    /// <summary>
    /// 标识 VisibleRangeLimitMode 依赖属性。
    /// </summary>
    public static readonly DependencyProperty VisibleRangeLimitModeProperty =
        DependencyProperty.Register(nameof(VisibleRangeLimitMode), typeof(VisibleRangeLimitMode), typeof(AxisBase),
            new PropertyMetadata(VisibleRangeLimitMode.None, OnVisibleRangeLimitModeChanged));

    /// <summary>
    /// 获取或设置可见范围的限制模式。
    /// </summary>
    public VisibleRangeLimitMode VisibleRangeLimitMode
    {
        get => (VisibleRangeLimitMode)GetValue(VisibleRangeLimitModeProperty);
        set => SetValue(VisibleRangeLimitModeProperty, value);
    }

    /// <summary>
    /// 获取坐标映射器，由派生类实现。
    /// </summary>
    public abstract ICoordinateMapper CoordinateMapper { get; }

    /// <summary>
    /// 标识 Title 依赖属性。
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AxisBase),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置轴的标题。
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 标识 IsAxisVisible 依赖属性。
    /// </summary>
    public static readonly DependencyProperty IsAxisVisibleProperty =
        DependencyProperty.Register(nameof(IsAxisVisible), typeof(bool), typeof(AxisBase),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置轴是否可见。
    /// </summary>
    public bool IsAxisVisible
    {
        get => (bool)GetValue(IsAxisVisibleProperty);
        set => SetValue(IsAxisVisibleProperty, value);
    }

    /// <summary>
    /// 标识 AutoRange 依赖属性。
    /// </summary>
    public static readonly DependencyProperty AutoRangeProperty =
        DependencyProperty.Register(nameof(AutoRange), typeof(bool), typeof(AxisBase),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置是否自动调整范围。
    /// </summary>
    public bool AutoRange
    {
        get => (bool)GetValue(AutoRangeProperty);
        set => SetValue(AutoRangeProperty, value);
    }

    /// <summary>
    /// 标识 TickCalculationMode 依赖属性。
    /// </summary>
    public static readonly DependencyProperty TickCalculationModeProperty =
        DependencyProperty.Register(nameof(TickCalculationMode), typeof(TickCalculationMode), typeof(AxisBase),
            new PropertyMetadata(TickCalculationMode.Auto));

    /// <summary>
    /// 获取或设置主刻度计算模式。
    /// </summary>
    public TickCalculationMode TickCalculationMode
    {
        get => (TickCalculationMode)GetValue(TickCalculationModeProperty);
        set => SetValue(TickCalculationModeProperty, value);
    }

    /// <summary>
    /// 标识 MajorTickInterval 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorTickIntervalProperty =
        DependencyProperty.Register(nameof(MajorTickInterval), typeof(double), typeof(AxisBase),
            new PropertyMetadata(double.NaN));

    /// <summary>
    /// 获取或设置主刻度间隔。
    /// </summary>
    public double MajorTickInterval
    {
        get => (double)GetValue(MajorTickIntervalProperty);
        set => SetValue(MajorTickIntervalProperty, value);
    }

    /// <summary>
    /// 标识 MajorTickCount 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorTickCountProperty =
        DependencyProperty.Register(nameof(MajorTickCount), typeof(int), typeof(AxisBase),
            new PropertyMetadata(0));

    /// <summary>
    /// 获取或设置固定主刻度数量。
    /// 大于 0 时启用固定刻度模式，忽略 MajorTickInterval 和自动计算，
    /// 刻度间隔 = VisibleRange.Length / (MajorTickCount - 1)。
    /// </summary>
    public int MajorTickCount
    {
        get => (int)GetValue(MajorTickCountProperty);
        set => SetValue(MajorTickCountProperty, value);
    }

    /// <summary>
    /// 标识 AlignRangeToTicks 依赖属性。
    /// </summary>
    public static readonly DependencyProperty AlignRangeToTicksProperty =
        DependencyProperty.Register(nameof(AlignRangeToTicks), typeof(bool), typeof(AxisBase),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置是否将可见范围对齐到主刻度。
    /// 启用后，VisibleRange 的 Min/Max 会自动调整到最近的刻度值，
    /// 使第一个和最后一个主刻度始终对齐到轴的两端。
    /// </summary>
    public bool AlignRangeToTicks
    {
        get => (bool)GetValue(AlignRangeToTicksProperty);
        set => SetValue(AlignRangeToTicksProperty, value);
    }

    /// <summary>
    /// 标识 LabelFormat 依赖属性。
    /// </summary>
    public static readonly DependencyProperty LabelFormatProperty =
        DependencyProperty.Register(nameof(LabelFormat), typeof(string), typeof(AxisBase),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置刻度标签的格式化字符串。
    /// </summary>
    public string? LabelFormat
    {
        get => (string?)GetValue(LabelFormatProperty);
        set => SetValue(LabelFormatProperty, value);
    }

    /// <summary>
    /// 标识 ShowMajorGridLines 依赖属性。
    /// </summary>
    public static readonly DependencyProperty ShowMajorGridLinesProperty =
        DependencyProperty.Register(nameof(ShowMajorGridLines), typeof(bool), typeof(AxisBase),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置是否显示主网格线。
    /// </summary>
    public bool ShowMajorGridLines
    {
        get => (bool)GetValue(ShowMajorGridLinesProperty);
        set => SetValue(ShowMajorGridLinesProperty, value);
    }

    /// <summary>
    /// 标识 ShowMinorGridLines 依赖属性。
    /// </summary>
    public static readonly DependencyProperty ShowMinorGridLinesProperty =
        DependencyProperty.Register(nameof(ShowMinorGridLines), typeof(bool), typeof(AxisBase),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置是否显示次网格线。
    /// </summary>
    public bool ShowMinorGridLines
    {
        get => (bool)GetValue(ShowMinorGridLinesProperty);
        set => SetValue(ShowMinorGridLinesProperty, value);
    }

    /// <summary>
    /// 标识 AxisForeground 依赖属性。
    /// </summary>
    public static readonly DependencyProperty AxisForegroundProperty =
        DependencyProperty.Register(nameof(AxisForeground), typeof(Brush), typeof(AxisBase),
            new PropertyMetadata(new SolidColorBrush(Colors.White)));

    /// <summary>
    /// 获取或设置轴的前景色（刻度标签和轴线颜色）。
    /// </summary>
    public Brush AxisForeground
    {
        get => (Brush)GetValue(AxisForegroundProperty);
        set => SetValue(AxisForegroundProperty, value);
    }

    /// <summary>
    /// 标识 MajorGridLineColor 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorGridLineColorProperty =
        DependencyProperty.Register(nameof(MajorGridLineColor), typeof(Color), typeof(AxisBase),
            new PropertyMetadata(Color.FromArgb(40, 255, 255, 255)));

    /// <summary>
    /// 获取或设置主网格线的颜色。
    /// </summary>
    public Color MajorGridLineColor
    {
        get => (Color)GetValue(MajorGridLineColorProperty);
        set => SetValue(MajorGridLineColorProperty, value);
    }

    /// <summary>
    /// 标识 MajorGridLineBrush 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorGridLineBrushProperty =
        DependencyProperty.Register(nameof(MajorGridLineBrush), typeof(Brush), typeof(AxisBase),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置主网格线的画刷。
    /// </summary>
    public Brush? MajorGridLineBrush
    {
        get => (Brush?)GetValue(MajorGridLineBrushProperty);
        set => SetValue(MajorGridLineBrushProperty, value);
    }

    /// <summary>
    /// 标识 MajorGridLineThickness 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorGridLineThicknessProperty =
        DependencyProperty.Register(nameof(MajorGridLineThickness), typeof(double), typeof(AxisBase),
            new PropertyMetadata(1.0));

    /// <summary>
    /// 获取或设置主网格线的粗细。
    /// </summary>
    public double MajorGridLineThickness
    {
        get => (double)GetValue(MajorGridLineThicknessProperty);
        set => SetValue(MajorGridLineThicknessProperty, value);
    }

    /// <summary>
    /// 标识 MajorGridLineDashArray 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MajorGridLineDashArrayProperty =
        DependencyProperty.Register(nameof(MajorGridLineDashArray), typeof(DoubleCollection), typeof(AxisBase),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置主网格线的虚线数组。
    /// </summary>
    public DoubleCollection? MajorGridLineDashArray
    {
        get => (DoubleCollection?)GetValue(MajorGridLineDashArrayProperty);
        set => SetValue(MajorGridLineDashArrayProperty, value);
    }

    /// <summary>
    /// 标识 MinorGridLineBrush 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MinorGridLineBrushProperty =
        DependencyProperty.Register(nameof(MinorGridLineBrush), typeof(Brush), typeof(AxisBase),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置次网格线的画刷。
    /// </summary>
    public Brush? MinorGridLineBrush
    {
        get => (Brush?)GetValue(MinorGridLineBrushProperty);
        set => SetValue(MinorGridLineBrushProperty, value);
    }

    /// <summary>
    /// 标识 MinorGridLineThickness 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MinorGridLineThicknessProperty =
        DependencyProperty.Register(nameof(MinorGridLineThickness), typeof(double), typeof(AxisBase),
            new PropertyMetadata(1.0));

    /// <summary>
    /// 获取或设置次网格线的粗细。
    /// </summary>
    public double MinorGridLineThickness
    {
        get => (double)GetValue(MinorGridLineThicknessProperty);
        set => SetValue(MinorGridLineThicknessProperty, value);
    }

    /// <summary>
    /// 标识 MinorGridLineDashArray 依赖属性。
    /// </summary>
    public static readonly DependencyProperty MinorGridLineDashArrayProperty =
        DependencyProperty.Register(nameof(MinorGridLineDashArray), typeof(DoubleCollection), typeof(AxisBase),
            new PropertyMetadata(s_defaultMinorGridLineDashArray));

    /// <summary>
    /// 获取或设置次网格线的虚线数组。
    /// </summary>
    public DoubleCollection? MinorGridLineDashArray
    {
        get => (DoubleCollection?)GetValue(MinorGridLineDashArrayProperty);
        set => SetValue(MinorGridLineDashArrayProperty, value);
    }

    /// <summary>
    /// 当可见范围改变时触发的事件。
    /// </summary>
    public event EventHandler<EventArgs>? VisibleRangeChanged;

    /// <summary>
    /// 当 VisibleRange 属性改变时调用的回调方法。
    /// </summary>
    /// <param name="d">依赖对象</param>
    /// <param name="e">属性改变事件参数</param>
    private static void OnVisibleRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisBase axis)
        {
            var range = (DataRange)e.NewValue;
            var clamped = axis.ClampToVisibleRangeLimit(range);
            if (clamped.Min != range.Min || clamped.Max != range.Max)
            {
                axis.CoreRange = clamped;
                axis.SetCurrentValue(VisibleRangeProperty, clamped);
                return;
            }

            axis.VisibleRangeChanged?.Invoke(axis, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 获取主刻度信息数组，由派生类实现。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public abstract TickInfo[] GetMajorTicks(double viewportSize);

    /// <summary>
    /// 获取次刻度信息数组，由派生类实现。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    public abstract TickInfo[] GetMinorTicks(double viewportSize);

    /// <summary>
    /// 根据数据系列计算自动范围，由派生类实现。
    /// </summary>
    /// <param name="dataSeries">数据系列集合</param>
    /// <returns>计算得到的自动范围</returns>
    public abstract DataRange CalculateAutoRange(IEnumerable<IDataSeries> dataSeries);

    /// <summary>
    /// 根据当前的限制模式和限制边界裁剪指定的数据范围。
    /// 平移时保持视口长度不变，缩放时裁剪到限制范围内。
    /// </summary>
    /// <param name="range">待裁剪的数据范围</param>
    /// <returns>裁剪后的数据范围</returns>
    public DataRange ClampToVisibleRangeLimit(DataRange range)
    {
        if (VisibleRangeLimitMode == VisibleRangeLimitMode.None)
            return range;

        double min = range.Min;
        double max = range.Max;
        double length = max - min;

        switch (VisibleRangeLimitMode)
        {
            case VisibleRangeLimitMode.MinOnly:
                if (min < VisibleRangeLimit.Min)
                {
                    min = VisibleRangeLimit.Min;
                    max = min + length;
                }
                break;
            case VisibleRangeLimitMode.MaxOnly:
                if (max > VisibleRangeLimit.Max)
                {
                    max = VisibleRangeLimit.Max;
                    min = max - length;
                }
                break;
            case VisibleRangeLimitMode.MinAndMax:
                min = Math.Max(min, VisibleRangeLimit.Min);
                max = Math.Min(max, VisibleRangeLimit.Max);
                if (min > max)
                {
                    min = VisibleRangeLimit.Min;
                    max = VisibleRangeLimit.Max;
                }
                break;
        }

        return new DataRange(min, max);
    }

    private static void OnVisibleRangeLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisBase axis && !Equals(e.NewValue, e.OldValue))
        {
            var coreRange = axis.CoreRange.Length > 0
                ? axis.CoreRange
                : axis.VisibleRange;

            var clamped = axis.ClampToVisibleRangeLimit(coreRange);
            if (clamped.Min != coreRange.Min || clamped.Max != coreRange.Max)
            {
                axis.CoreRange = clamped;
                axis.VisibleRange = clamped;
            }
        }
    }

    private static void OnVisibleRangeLimitModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisBase axis && !Equals(e.NewValue, e.OldValue))
        {
            var coreRange = axis.CoreRange;
            if (coreRange.Length > 0)
            {
                var clamped = axis.ClampToVisibleRangeLimit(coreRange);
                if (clamped.Min != coreRange.Min || clamped.Max != coreRange.Max)
                {
                    axis.CoreRange = clamped;
                    axis.VisibleRange = clamped;
                }
            }
        }
    }

    /// <summary>
    /// 计算美观的刻度间隔。
    /// </summary>
    /// <param name="range">数值范围</param>
    /// <param name="maxTicks">最大刻度数</param>
    /// <returns>美观的刻度间隔</returns>
    protected static double CalculateNiceInterval(double range, double maxTicks)
    {
        if (range <= 0 || maxTicks <= 0)
            return 1;

        double roughInterval = range / maxTicks;
        double exponent = Math.Floor(Math.Log10(roughInterval));
        double fraction = roughInterval / Math.Pow(10, exponent);

        double niceFraction;
        if (fraction <= 1.0)
            niceFraction = 1.0;
        else if (fraction <= 2.0)
            niceFraction = 2.0;
        else if (fraction <= 5.0)
            niceFraction = 5.0;
        else
            niceFraction = 10.0;

        return niceFraction * Math.Pow(10, exponent);
    }

    /// <summary>
    /// 计算美观的数值范围。
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="maxTicks">最大刻度数</param>
    /// <returns>美观的数值范围</returns>
    internal static DataRange CalculateStaticNiceRange(double min, double max)
    {
        return CalculateNiceRange(min, max);
    }

    protected static DataRange CalculateNiceRange(double min, double max, double maxTicks = 10)
    {
        if (double.IsInfinity(min) || double.IsInfinity(max) || min == max)
        {
            if (min == max)
            {
                min -= 1;
                max += 1;
            }
            else
            {
                return new DataRange(min, max);
            }
        }

        double range = max - min;
        double interval = CalculateNiceInterval(range, maxTicks);
        double niceMin = Math.Floor(min / interval) * interval;
        double niceMax = Math.Ceiling(max / interval) * interval;

        if (niceMin == niceMax)
        {
            niceMin -= interval;
            niceMax += interval;
        }

        return new DataRange(niceMin, niceMax);
    }

    /// <summary>
    /// 创建冻结的 DoubleCollection。
    /// </summary>
    /// <param name="values">double 值数组</param>
    /// <returns>冻结的 DoubleCollection</returns>
    private static DoubleCollection CreateFrozenDoubleCollection(params double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
    }
}