using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Axes.Controls;

/// <summary>
/// 网格线控件，负责在图表区域绘制主网格线和次网格线。
/// </summary>
public class GridLinesControl : Canvas
{
    private static readonly DependencyPropertyDescriptor[] s_axisDescriptors =
        new DependencyPropertyDescriptor?[]
        {
            DependencyPropertyDescriptor.FromProperty(AxisBase.PlacementProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.IsAxisVisibleProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorTickIntervalProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.ShowMajorGridLinesProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.ShowMinorGridLinesProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorGridLineColorProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorGridLineBrushProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorGridLineThicknessProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorGridLineDashArrayProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MinorGridLineBrushProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MinorGridLineThicknessProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MinorGridLineDashArrayProperty, typeof(AxisBase))
        }
        .Where(static descriptor => descriptor is not null)
        .Cast<DependencyPropertyDescriptor>()
        .ToArray();

    private INotifyCollectionChanged? _xAxesNotifier;
    private INotifyCollectionChanged? _yAxesNotifier;
    private IAxis? _defaultXAxis;
    private IAxis? _defaultYAxis;
    private readonly List<Line> _linePool = [];

    /// <summary>
    /// 初始化 GridLinesControl 类的新实例。
    /// </summary>
    public GridLinesControl()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == Chart.PlotAreaMarginProperty)
        {
            RefreshGridLines();
        }
    }

    /// <summary>
    /// 标识 XAxes 依赖属性。
    /// </summary>
    public static readonly DependencyProperty XAxesProperty =
        DependencyProperty.Register(nameof(XAxes), typeof(IEnumerable<IAxis>), typeof(GridLinesControl),
            new PropertyMetadata(null, OnAxesSourceChanged));

    /// <summary>
    /// 获取或设置X轴集合。
    /// </summary>
    public IEnumerable<IAxis>? XAxes
    {
        get => (IEnumerable<IAxis>?)GetValue(XAxesProperty);
        set => SetValue(XAxesProperty, value);
    }

    /// <summary>
    /// 标识 YAxes 依赖属性。
    /// </summary>
    public static readonly DependencyProperty YAxesProperty =
        DependencyProperty.Register(nameof(YAxes), typeof(IEnumerable<IAxis>), typeof(GridLinesControl),
            new PropertyMetadata(null, OnAxesSourceChanged));

    /// <summary>
    /// 获取或设置Y轴集合。
    /// </summary>
    public IEnumerable<IAxis>? YAxes
    {
        get => (IEnumerable<IAxis>?)GetValue(YAxesProperty);
        set => SetValue(YAxesProperty, value);
    }

    /// <summary>
    /// 当渲染尺寸改变时调用。
    /// </summary>
    /// <param name="sizeInfo">尺寸改变信息</param>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RefreshGridLines();
    }

    /// <summary>
    /// 当坐标轴源改变时调用的回调方法。
    /// </summary>
    /// <param name="d">依赖对象</param>
    /// <param name="e">属性改变事件参数</param>
    private static void OnAxesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GridLinesControl control)
            return;

        control.OnAxesSourceChanged(e.Property, e.OldValue as IEnumerable<IAxis>, e.NewValue as IEnumerable<IAxis>);
    }

    /// <summary>
    /// 处理坐标轴源改变。
    /// </summary>
    /// <param name="property">依赖属性</param>
    /// <param name="oldAxes">旧的坐标轴集合</param>
    /// <param name="newAxes">新的坐标轴集合</param>
    private void OnAxesSourceChanged(
        DependencyProperty property,
        IEnumerable<IAxis>? oldAxes,
        IEnumerable<IAxis>? newAxes)
    {
        if (property == XAxesProperty)
        {
            UpdateCollectionSubscription(ref _xAxesNotifier, oldAxes, newAxes);
        }
        else if (property == YAxesProperty)
        {
            UpdateCollectionSubscription(ref _yAxesNotifier, oldAxes, newAxes);
        }

        UpdateDefaultAxisSubscriptions();
        RefreshGridLines();
    }

    /// <summary>
    /// 更新集合订阅。
    /// </summary>
    /// <param name="notifier">通知器引用</param>
    /// <param name="oldAxes">旧的坐标轴集合</param>
    /// <param name="newAxes">新的坐标轴集合</param>
    private void UpdateCollectionSubscription(
        ref INotifyCollectionChanged? notifier,
        IEnumerable<IAxis>? oldAxes,
        IEnumerable<IAxis>? newAxes)
    {
        if (ReferenceEquals(oldAxes, newAxes))
            return;

        if (notifier != null)
            notifier.CollectionChanged -= OnAxesCollectionChanged;

        notifier = newAxes as INotifyCollectionChanged;

        if (notifier != null)
            notifier.CollectionChanged += OnAxesCollectionChanged;
    }

    /// <summary>
    /// 当坐标轴集合改变时调用。
    /// </summary>
    private void OnAxesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDefaultAxisSubscriptions();
        RefreshGridLines();
    }

    /// <summary>
    /// 更新默认轴的订阅。
    /// </summary>
    private void UpdateDefaultAxisSubscriptions()
    {
        ReplaceDefaultAxisSubscription(ref _defaultXAxis, ResolveDefaultAxis(XAxes, Chart.DefaultXAxisId));
        ReplaceDefaultAxisSubscription(ref _defaultYAxis, ResolveDefaultAxis(YAxes, Chart.DefaultYAxisId));
    }

    /// <summary>
    /// 替换默认轴的订阅。
    /// </summary>
    /// <param name="currentAxis">当前轴引用</param>
    /// <param name="newAxis">新轴</param>
    private void ReplaceDefaultAxisSubscription(ref IAxis? currentAxis, IAxis? newAxis)
    {
        if (ReferenceEquals(currentAxis, newAxis))
            return;

        DetachAxis(currentAxis);
        currentAxis = newAxis;
        AttachAxis(currentAxis);
    }

    /// <summary>
    /// 附加轴的事件监听。
    /// </summary>
    /// <param name="axis">要附加的轴</param>
    private void AttachAxis(IAxis? axis)
    {
        if (axis == null)
            return;

        axis.VisibleRangeChanged += OnAxisChanged;

        if (axis is AxisBase axisBase)
        {
            foreach (var descriptor in s_axisDescriptors)
                descriptor.AddValueChanged(axisBase, OnAxisChanged);
        }
    }

    /// <summary>
    /// 分离轴的事件监听。
    /// </summary>
    /// <param name="axis">要分离的轴</param>
    private void DetachAxis(IAxis? axis)
    {
        if (axis == null)
            return;

        axis.VisibleRangeChanged -= OnAxisChanged;

        if (axis is AxisBase axisBase)
        {
            foreach (var descriptor in s_axisDescriptors)
                descriptor.RemoveValueChanged(axisBase, OnAxisChanged);
        }
    }

    /// <summary>
    /// 当轴属性改变时调用。
    /// </summary>
    private void OnAxisChanged(object? sender, EventArgs e)
        => RefreshGridLines();

    /// <summary>
    /// 刷新网格线。
    /// </summary>
    private void RefreshGridLines()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            TrimUnusedLines(0);
            return;
        }

        UpdateDefaultAxisSubscriptions();

        var yLineSet = CreateAxisGridLineSet(_defaultYAxis, isXAxis: false, width, height);
        var xLineSet = CreateAxisGridLineSet(_defaultXAxis, isXAxis: true, width, height);

        int lineIndex = 0;
        lineIndex = ConfigureLines(lineIndex, yLineSet?.MinorPositions, isXAxis: false, height, width, yLineSet?.MinorBrush, yLineSet?.MinorThickness ?? 0, yLineSet?.MinorDashArray);
        lineIndex = ConfigureLines(lineIndex, xLineSet?.MinorPositions, isXAxis: true, height, width, xLineSet?.MinorBrush, xLineSet?.MinorThickness ?? 0, xLineSet?.MinorDashArray);
        lineIndex = ConfigureLines(lineIndex, yLineSet?.MajorPositions, isXAxis: false, height, width, yLineSet?.MajorBrush, yLineSet?.MajorThickness ?? 0, yLineSet?.MajorDashArray);
        lineIndex = ConfigureLines(lineIndex, xLineSet?.MajorPositions, isXAxis: true, height, width, xLineSet?.MajorBrush, xLineSet?.MajorThickness ?? 0, xLineSet?.MajorDashArray);

        TrimUnusedLines(lineIndex);
    }

    /// <summary>
    /// 创建轴网格线集合。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="isXAxis">是否为X轴</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>网格线集合</returns>
    private AxisGridLineSet? CreateAxisGridLineSet(IAxis? axis, bool isXAxis, double width, double height)
    {
        if (axis == null || !axis.IsAxisVisible)
            return null;

        double viewportSize = isXAxis ? width : height;
        var margin = Chart.GetPlotAreaMargin(this);
        double paddedViewportSize = isXAxis
            ? Math.Max(1, viewportSize - margin.Left - margin.Right)
            : Math.Max(1, viewportSize - margin.Top - margin.Bottom);
        double marginStart = isXAxis ? margin.Left : margin.Top;

        var majorPositions = GetVisibleTickPositions(axis, axis.GetMajorTicks(paddedViewportSize), isXAxis, width, height, marginStart, paddedViewportSize);
        var majorKeys = new HashSet<int>(majorPositions.Select(CreatePixelKey));
        var minorPositions = GetVisibleTickPositions(axis, axis.GetMinorTicks(paddedViewportSize), isXAxis, width, height, marginStart, paddedViewportSize)
            .Where(position => !majorKeys.Contains(CreatePixelKey(position)))
            .ToArray();

        return new AxisGridLineSet
        {
            MajorPositions = axis.ShowMajorGridLines && axis.MajorGridLineThickness > 0 ? majorPositions : Array.Empty<double>(),
            MinorPositions = axis.ShowMinorGridLines && axis.MinorGridLineThickness > 0 ? minorPositions : Array.Empty<double>(),
            MajorBrush = ResolveMajorBrush(axis),
            MinorBrush = ResolveMinorBrush(axis),
            MajorThickness = axis.MajorGridLineThickness,
            MinorThickness = axis.MinorGridLineThickness,
            MajorDashArray = NormalizeDashArray(axis.MajorGridLineDashArray),
            MinorDashArray = NormalizeDashArray(axis.MinorGridLineDashArray)
        };
    }

    /// <summary>
    /// 配置并复用网格线元素。
    /// </summary>
    /// <param name="lineIndex">当前线元素索引</param>
    /// <param name="positions">位置数组</param>
    /// <param name="isXAxis">是否为X轴</param>
    /// <param name="height">高度</param>
    /// <param name="width">宽度</param>
    /// <param name="stroke">画刷</param>
    /// <param name="thickness">粗细</param>
    /// <param name="dashArray">虚线数组</param>
    /// <returns>更新后的线元素索引</returns>
    private int ConfigureLines(
        int lineIndex,
        IReadOnlyList<double>? positions,
        bool isXAxis,
        double height,
        double width,
        Brush? stroke,
        double thickness,
        DoubleCollection? dashArray)
    {
        if (positions == null || positions.Count == 0 || stroke == null || thickness <= 0)
            return lineIndex;

        for (int i = 0; i < positions.Count; i++)
        {
            double position = positions[i];
            var line = GetOrCreateLine(lineIndex++);
            line.Stroke = stroke;
            line.StrokeThickness = thickness;
            line.StrokeDashArray = dashArray;
            line.SnapsToDevicePixels = true;

            if (isXAxis)
            {
                double x = Math.Round(position) + 0.5;
                line.X1 = x;
                line.Y1 = 0;
                line.X2 = x;
                line.Y2 = height;
            }
            else
            {
                double y = Math.Round(position) + 0.5;
                line.X1 = 0;
                line.Y1 = y;
                line.X2 = width;
                line.Y2 = y;
            }
        }

        return lineIndex;
    }

    /// <summary>
    /// 解析默认轴。
    /// </summary>
    /// <param name="axes">轴集合</param>
    /// <param name="axisId">轴ID</param>
    /// <returns>默认轴</returns>
    private static IAxis? ResolveDefaultAxis(IEnumerable<IAxis>? axes, string axisId)
    {
        if (axes == null)
            return null;

        foreach (var axis in axes)
        {
            if (axis.Id == axisId)
                return axis;
        }

        return null;
    }

    /// <summary>
    /// 获取可见刻度位置。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="ticks">刻度数组</param>
    /// <param name="isXAxis">是否为X轴</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="marginStart">margin起始偏移</param>
    /// <param name="paddedViewportSize">扣除margin后的视口尺寸</param>
    /// <returns>位置数组</returns>
    private static double[] GetVisibleTickPositions(
        IAxis axis,
        TickInfo[] ticks,
        bool isXAxis,
        double width,
        double height,
        double marginStart,
        double paddedViewportSize)
    {
        if (ticks.Length == 0)
            return Array.Empty<double>();

        var positions = new List<double>(ticks.Length);

        foreach (var tick in ticks)
        {
            double position = axis.CoordinateMapper.DataToScreen(tick.Position, axis.VisibleRange, paddedViewportSize);
            if (!isXAxis)
                position = paddedViewportSize - position;
            position = marginStart + position;

            if (double.IsNaN(position) || double.IsInfinity(position))
                continue;

            double max = isXAxis ? width : height;
            if (position < -0.5 || position > max + 0.5)
                continue;

            positions.Add(position);
        }

        return positions.ToArray();
    }

    /// <summary>
    /// 创建像素键。
    /// </summary>
    /// <param name="position">位置</param>
    /// <returns>像素键</returns>
    private static int CreatePixelKey(double position)
        => (int)Math.Round(position * 2.0);

    /// <summary>
    /// 解析主网格线画刷。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <returns>画刷</returns>
    private static Brush ResolveMajorBrush(IAxis axis)
        => axis.MajorGridLineBrush ?? CreateFrozenBrush(axis.MajorGridLineColor);

    /// <summary>
    /// 解析次网格线画刷。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <returns>画刷</returns>
    private static Brush ResolveMinorBrush(IAxis axis)
    {
        if (axis.MinorGridLineBrush != null)
            return axis.MinorGridLineBrush;

        var majorBrush = ResolveMajorBrush(axis);
        var brush = majorBrush.CloneCurrentValue();
        brush.Opacity *= 0.5;

        if (brush.CanFreeze)
            brush.Freeze();

        return brush;
    }

    /// <summary>
    /// 创建冻结的画刷。
    /// </summary>
    /// <param name="color">颜色</param>
    /// <returns>画刷</returns>
    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
            brush.Freeze();

        return brush;
    }

    /// <summary>
    /// 规范化虚线数组。
    /// </summary>
    /// <param name="dashArray">虚线数组</param>
    /// <returns>规范化后的虚线数组</returns>
    private static DoubleCollection? NormalizeDashArray(DoubleCollection? dashArray)
    {
        if (dashArray == null || dashArray.Count == 0)
            return null;

        var clone = dashArray.CloneCurrentValue();
        if (clone.CanFreeze)
            clone.Freeze();

        return clone;
    }

    private Line GetOrCreateLine(int index)
    {
        while (_linePool.Count <= index)
        {
            var line = new Line
            {
                SnapsToDevicePixels = true
            };
            _linePool.Add(line);
            Children.Add(line);
        }

        var existing = _linePool[index];
        if (!ReferenceEquals(existing.Parent, this))
            Children.Add(existing);

        return existing;
    }

    private void TrimUnusedLines(int usedLineCount)
    {
        for (int i = usedLineCount; i < _linePool.Count; i++)
        {
            var line = _linePool[i];
            if (ReferenceEquals(line.Parent, this))
                Children.Remove(line);
        }
    }

    /// <summary>
    /// 轴网格线集合结构。
    /// </summary>
    private sealed class AxisGridLineSet
    {
        /// <summary>
        /// 获取主刻度位置数组。
        /// </summary>
        public double[] MajorPositions { get; init; } = Array.Empty<double>();

        /// <summary>
        /// 获取次刻度位置数组。
        /// </summary>
        public double[] MinorPositions { get; init; } = Array.Empty<double>();

        /// <summary>
        /// 获取主网格线画刷。
        /// </summary>
        public Brush? MajorBrush { get; init; }

        /// <summary>
        /// 获取次网格线画刷。
        /// </summary>
        public Brush? MinorBrush { get; init; }

        /// <summary>
        /// 获取主网格线粗细。
        /// </summary>
        public double MajorThickness { get; init; }

        /// <summary>
        /// 获取次网格线粗细。
        /// </summary>
        public double MinorThickness { get; init; }

        /// <summary>
        /// 获取主网格线虚线数组。
        /// </summary>
        public DoubleCollection? MajorDashArray { get; init; }

        /// <summary>
        /// 获取次网格线虚线数组。
        /// </summary>
        public DoubleCollection? MinorDashArray { get; init; }
    }
}
