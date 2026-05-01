using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Axes.Controls;

/// <summary>
/// 坐标轴控件，负责渲染单个坐标轴的视觉表现（刻度线、标签、标题）。
/// </summary>
public class AxisControl : Control
{
    private const double FallbackMeasureViewportSize = 400.0;
    private const double TickLength = 6.0;
    private const double LabelOffset = 4.0;
    private const double TitleGap = 6.0;
    private const double AxisPadding = 4.0;
    private const double MinimumVerticalWidth = 60.0;
    private const double MinimumHorizontalHeight = 24.0;

    private static readonly DependencyPropertyDescriptor[] s_axisDescriptors =
        new DependencyPropertyDescriptor?[]
        {
            DependencyPropertyDescriptor.FromProperty(AxisBase.PlacementProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.TitleProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.IsAxisVisibleProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.MajorTickIntervalProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.LabelFormatProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.AxisForegroundProperty, typeof(AxisBase)),
            DependencyPropertyDescriptor.FromProperty(AxisBase.VisibleRangeProperty, typeof(AxisBase))
        }.Where(static descriptor => descriptor is not null).Cast<DependencyPropertyDescriptor>().ToArray();

    private Grid? _root;
    private Border? _titleHost;
    private TextBlock? _titleElement;
    private Canvas? _contentHost;
    private Line? _axisLine;
    private Canvas? _tickHost;
    private readonly List<Line> _tickLines = [];
    private readonly List<TextBlock> _tickLabels = [];

    static AxisControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AxisControl), new FrameworkPropertyMetadata(typeof(AxisControl)));
    }

    /// <summary>
    /// 标识 Axis 依赖属性。
    /// </summary>
    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.Register(nameof(Axis), typeof(IAxis), typeof(AxisControl),
            new PropertyMetadata(null, OnAxisChanged));

    /// <summary>
    /// 获取或设置与此控件关联的坐标轴。
    /// </summary>
    public IAxis? Axis
    {
        get => (IAxis?)GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    /// <summary>
    /// 获取或设置绘图区域尺寸。
    /// 用于同步轴刻度与网格线的坐标映射计算。
    /// 当GridLinesControl和AxisControl使用不同尺寸时会导致刻度对齐偏差。
    /// </summary>
    public static readonly DependencyProperty PlotAreaSizeProperty =
        DependencyProperty.Register(nameof(PlotAreaSize), typeof(Size), typeof(AxisControl),
            new PropertyMetadata(Size.Empty, OnPlotAreaSizeChanged));

    /// <summary>
    /// 获取或设置绘图区域尺寸。
    /// 用于同步轴刻度与网格线的坐标映射计算。
    /// 当GridLinesControl和AxisControl使用不同尺寸时会导致刻度对齐偏差。
    /// </summary>
    public Size PlotAreaSize
    {
        get => (Size)GetValue(PlotAreaSizeProperty);
        set => SetValue(PlotAreaSizeProperty, value);
    }

    /// <summary>
    /// 应用模板时调用，初始化控件的视觉元素。
    /// </summary>
    public override void OnApplyTemplate()
    {
        if (_contentHost != null)
            _contentHost.SizeChanged -= OnContentHostSizeChanged;

        _tickLines.Clear();
        _tickLabels.Clear();

        base.OnApplyTemplate();

        _root = GetTemplateChild("PART_Root") as Grid;
        _titleHost = GetTemplateChild("PART_TitleHost") as Border;
        _titleElement = GetTemplateChild("PART_Title") as TextBlock;
        _contentHost = GetTemplateChild("PART_ContentHost") as Canvas;
        _axisLine = GetTemplateChild("PART_AxisLine") as Line;
        _tickHost = GetTemplateChild("PART_TickHost") as Canvas;

        if (_contentHost != null)
            _contentHost.SizeChanged += OnContentHostSizeChanged;

        UpdateAxisVisuals();
    }

    /// <summary>
    /// 当依赖属性改变时调用。
    /// </summary>
    /// <param name="e">属性改变事件参数</param>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == FontFamilyProperty
            || e.Property == FontSizeProperty
            || e.Property == FontStretchProperty
            || e.Property == FontStyleProperty
            || e.Property == FontWeightProperty
            || e.Property == ForegroundProperty)
        {
            InvalidateMeasure();
            UpdateAxisVisuals();
        }
    }

    /// <summary>
    /// 当渲染尺寸改变时调用。
    /// </summary>
    /// <param name="sizeInfo">尺寸改变信息</param>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateAxisVisuals();
    }

    /// <summary>
    /// 测量阶段，计算控件所需的尺寸。
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>期望尺寸</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Axis == null || !Axis.IsAxisVisible)
            return new Size(0, 0);

        bool isVertical = Axis.Placement is AxisPlacement.Left or AxisPlacement.Right;
        double pixelsPerDip = GetPixelsPerDip();

        double maxLabelWidth = 0;
        double maxLabelHeight = 0;
        double viewportSize = NormalizeViewportSize(isVertical ? availableSize.Height : availableSize.Width);
        var ticks = Axis.GetMajorTicks(viewportSize);

        foreach (var tick in ticks)
        {
            if (string.IsNullOrEmpty(tick.Label))
                continue;

            var labelText = CreateFormattedText(tick.Label, FontSize, GetAxisBrush(), pixelsPerDip);
            maxLabelWidth = Math.Max(maxLabelWidth, labelText.Width);
            maxLabelHeight = Math.Max(maxLabelHeight, labelText.Height);
        }

        double titleThickness = MeasureTitleThickness();

        if (isVertical)
        {
            double contentWidth = Math.Max(
                MinimumVerticalWidth,
                AxisPadding * 2 + TickLength + LabelOffset + maxLabelWidth + 1);
            return new Size(contentWidth + titleThickness, NormalizeStretchSize(availableSize.Height));
        }

        double contentHeight = Math.Max(
            MinimumHorizontalHeight,
            AxisPadding * 2 + TickLength + LabelOffset + maxLabelHeight + 1);
        return new Size(NormalizeStretchSize(availableSize.Width), contentHeight + titleThickness);
    }

    /// <summary>
    /// 当 Axis 属性改变时调用的回调方法。
    /// </summary>
    /// <param name="d">依赖对象</param>
    /// <param name="e">属性改变事件参数</param>
    private static void OnPlotAreaSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisControl control)
            control.UpdateAxisVisuals();
    }

    private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AxisControl control)
            return;

        control.DetachAxis(e.OldValue as IAxis);
        control.AttachAxis(e.NewValue as IAxis);
        control.InvalidateMeasure();
        control.UpdateAxisVisuals();
    }

    /// <summary>
    /// 当轴的可见范围改变时调用。
    /// </summary>
    private void OnAxisRangeChanged(object? sender, EventArgs e)
    {
        UpdateAxisVisuals();
    }

    /// <summary>
    /// 当轴的外观属性改变时调用。
    /// </summary>
    private void OnAxisPresentationPropertyChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        UpdateAxisVisuals();
    }

    /// <summary>
    /// 当内容宿主尺寸改变时调用。
    /// </summary>
    private void OnContentHostSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateAxisVisuals();

    /// <summary>
    /// 附加轴的事件监听。
    /// </summary>
    /// <param name="axis">要附加的轴</param>
    private void AttachAxis(IAxis? axis)
    {
        if (axis == null)
            return;

        axis.VisibleRangeChanged += OnAxisRangeChanged;

        if (axis is AxisBase axisBase)
        {
            foreach (var descriptor in s_axisDescriptors)
                descriptor.AddValueChanged(axisBase, OnAxisPresentationPropertyChanged);
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

        axis.VisibleRangeChanged -= OnAxisRangeChanged;

        if (axis is AxisBase axisBase)
        {
            foreach (var descriptor in s_axisDescriptors)
                descriptor.RemoveValueChanged(axisBase, OnAxisPresentationPropertyChanged);
        }
    }

    /// <summary>
    /// 更新坐标轴的视觉表现。
    /// </summary>
    private void UpdateAxisVisuals()
    {
        if (_root == null || _titleHost == null || _titleElement == null || _contentHost == null || _axisLine == null || _tickHost == null)
            return;

        if (Axis == null || !Axis.IsAxisVisible)
        {
            _root.Visibility = Visibility.Collapsed;
            _axisLine.Visibility = Visibility.Collapsed;
            _titleHost.Visibility = Visibility.Collapsed;
            HideUnusedTickVisuals(0, 0);
            return;
        }

        _root.Visibility = Visibility.Visible;

        var axis = Axis;
        var placement = axis.Placement;
        bool hasTitle = !string.IsNullOrWhiteSpace(axis.Title);

        ConfigureRootLayout(placement, hasTitle);
        ConfigureTitleVisual(axis, placement, hasTitle);
        ConfigureContentVisual(axis, placement);
    }

    /// <summary>
    /// 配置根布局。
    /// </summary>
    /// <param name="placement">轴的位置</param>
    /// <param name="hasTitle">是否有标题</param>
    private void ConfigureRootLayout(AxisPlacement placement, bool hasTitle)
    {
        if (_root == null || _titleHost == null || _contentHost == null)
            return;

        _root.RowDefinitions.Clear();
        _root.ColumnDefinitions.Clear();
        _titleHost.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;

        if (placement is AxisPlacement.Left or AxisPlacement.Right)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            if (hasTitle)
            {
                if (placement == AxisPlacement.Left)
                {
                    _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(_titleHost, 0);
                    Grid.SetColumn(_contentHost, 1);
                    _titleHost.Margin = new Thickness(0, 0, TitleGap, 0);
                }
                else
                {
                    _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(_contentHost, 0);
                    Grid.SetColumn(_titleHost, 1);
                    _titleHost.Margin = new Thickness(TitleGap, 0, 0, 0);
                }
            }
            else
            {
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(_contentHost, 0);
                Grid.SetColumn(_titleHost, 0);
                _titleHost.Margin = new Thickness(0);
            }

            Grid.SetRow(_titleHost, 0);
            Grid.SetRow(_contentHost, 0);
        }
        else
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (hasTitle)
            {
                if (placement == AxisPlacement.Top)
                {
                    _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    Grid.SetRow(_titleHost, 0);
                    Grid.SetRow(_contentHost, 1);
                    _titleHost.Margin = new Thickness(0, 0, 0, TitleGap);
                }
                else
                {
                    _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Grid.SetRow(_contentHost, 0);
                    Grid.SetRow(_titleHost, 1);
                    _titleHost.Margin = new Thickness(0, TitleGap, 0, 0);
                }
            }
            else
            {
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(_contentHost, 0);
                Grid.SetRow(_titleHost, 0);
                _titleHost.Margin = new Thickness(0);
            }

            Grid.SetColumn(_titleHost, 0);
            Grid.SetColumn(_contentHost, 0);
        }
    }

    /// <summary>
    /// 配置标题视觉元素。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="placement">轴的位置</param>
    /// <param name="hasTitle">是否有标题</param>
    private void ConfigureTitleVisual(IAxis axis, AxisPlacement placement, bool hasTitle)
    {
        if (_titleElement == null || _titleHost == null)
            return;

        if (!hasTitle)
        {
            _titleHost.Visibility = Visibility.Collapsed;
            _titleElement.Text = string.Empty;
            return;
        }

        _titleHost.Visibility = Visibility.Visible;
        _titleElement.Text = axis.Title;
        _titleElement.Foreground = GetAxisBrush();
        _titleElement.FontFamily = FontFamily;
        _titleElement.FontStretch = FontStretch;
        _titleElement.FontStyle = FontStyle;
        _titleElement.FontWeight = FontWeight;
        _titleElement.FontSize = FontSize + 2;
        _titleElement.HorizontalAlignment = HorizontalAlignment.Center;
        _titleElement.VerticalAlignment = VerticalAlignment.Center;
        _titleElement.TextAlignment = TextAlignment.Center;
        _titleElement.LayoutTransform = placement switch
        {
            AxisPlacement.Left => new RotateTransform(-90),
            AxisPlacement.Right => new RotateTransform(90),
            _ => Transform.Identity
        };
    }

    /// <summary>
    /// 配置内容视觉元素（刻度线和标签）。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="placement">轴的位置</param>
    private void ConfigureContentVisual(IAxis axis, AxisPlacement placement)
    {
        if (_contentHost == null || _axisLine == null || _tickHost == null)
            return;

        double width = _contentHost.ActualWidth;
        double height = _contentHost.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            _axisLine.Visibility = Visibility.Collapsed;
            HideUnusedTickVisuals(0, 0);
            return;
        }

        Size plotSize = PlotAreaSize;
        if (plotSize == Size.Empty)
            plotSize = FindPlotAreaSize();

        double mappingWidth = plotSize != Size.Empty ? plotSize.Width : width;
        double mappingHeight = plotSize != Size.Empty ? plotSize.Height : height;

        _tickHost.Width = width;
        _tickHost.Height = height;

        Brush axisBrush = GetAxisBrush();
        _axisLine.Visibility = Visibility.Visible;
        _axisLine.Stroke = axisBrush;
        _axisLine.StrokeThickness = 1;
        _axisLine.SnapsToDevicePixels = true;

        switch (placement)
        {
            case AxisPlacement.Left:
                ConfigureVerticalAxis(axis, width, height, mappingHeight, true, axisBrush);
                break;
            case AxisPlacement.Right:
                ConfigureVerticalAxis(axis, width, height, mappingHeight, false, axisBrush);
                break;
            case AxisPlacement.Top:
                ConfigureHorizontalAxis(axis, width, height, mappingWidth, false, axisBrush);
                break;
            default:
                ConfigureHorizontalAxis(axis, width, height, mappingWidth, true, axisBrush);
                break;
        }
    }

    /// <summary>
    /// 配置垂直轴（左或右）。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="mappingHeight">用于坐标映射的绘图区高度</param>
    /// <param name="isLeft">是否在左侧</param>
    /// <param name="axisBrush">轴的画刷</param>
    private void ConfigureVerticalAxis(IAxis axis, double width, double height, double mappingHeight, bool isLeft, Brush axisBrush)
    {
        if (_axisLine == null || _tickHost == null)
            return;

        double axisX = isLeft ? width - 0.5  : 0.5;
        _axisLine.X1 = axisX;
        _axisLine.X2 = axisX;
        _axisLine.Y1 = 0;
        _axisLine.Y2 = height;

        double pixelsPerDip = GetPixelsPerDip();
        var ticks = axis.GetMajorTicks(NormalizeViewportSize(mappingHeight));
        int lineIndex = 0;
        int labelIndex = 0;

        foreach (var tick in ticks)
        {
            // 坐标映射：将数据值转换为屏幕坐标
            // DataToScreen 返回 0 到 mappingHeight 之间的位置（0=最小值，mappingHeight=最大值）
            double position = axis.CoordinateMapper.DataToScreen(tick.Position, axis.VisibleRange, mappingHeight);

            // 翻转Y坐标：WPF坐标系原点在左上角，而图表坐标原点在左下角
            position = mappingHeight - position;

            // 确保刻度线位置在有效范围内
            position = Math.Clamp(position, 0, mappingHeight);

            if (double.IsNaN(position) || double.IsInfinity(position))
                continue;

            // 像素对齐：WPF在0.5像素边界上渲染线条以获得锐利效果
            // 例如：Math.Round(186) + 0.5 = 186.5，表示从像素186到187的线条中心
            double lineY = Math.Round(position) + 0.5;
            // 边界检查：确保刻度线不超出控件边界
            // 当AlignRangeToTicks=True时，边界刻度可能映射到控件边缘（position=0或height），
            // 此时lineY会超出范围，需要钳制到[0.5, height-0.5]以防止刻度线被裁剪
            lineY = Math.Min(lineY, height - 0.5);
            lineY = Math.Max(lineY, 0.5);
            double tickX2 = isLeft ? axisX - TickLength : axisX + TickLength;

            var tickLine = GetOrCreateTickLine(lineIndex++);
            tickLine.X1 = axisX;
            tickLine.Y1 = lineY;
            tickLine.X2 = tickX2;
            tickLine.Y2 = lineY;
            tickLine.Stroke = axisBrush;
            tickLine.StrokeThickness = 1;
            tickLine.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(tick.Label))
                continue;

            var labelMetrics = CreateFormattedText(tick.Label, FontSize, axisBrush, pixelsPerDip);
            var label = GetOrCreateTickLabel(labelIndex++);
            ConfigureLabelTextBlock(label, tick.Label, axisBrush, FontSize);
            double labelX = isLeft
                ? axisX - TickLength - LabelOffset - labelMetrics.Width
                : axisX + TickLength + LabelOffset;
            double labelY = position - labelMetrics.Height / 2;

            if (labelY < 0)
                labelY = 0;
            if (labelY + labelMetrics.Height > height)
                labelY = height - labelMetrics.Height;

            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);
            label.Visibility = Visibility.Visible;
        }

        HideUnusedTickVisuals(lineIndex, labelIndex);
    }

    /// <summary>
    /// 配置水平轴（上或下）。
    /// </summary>
    /// <param name="axis">坐标轴</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="mappingWidth">用于坐标映射的绘图区宽度</param>
    /// <param name="isBottom">是否在底部</param>
    /// <param name="axisBrush">轴的画刷</param>
    private void ConfigureHorizontalAxis(IAxis axis, double width, double height, double mappingWidth, bool isBottom, Brush axisBrush)
    {
        if (_axisLine == null || _tickHost == null)
            return;

        double axisY = isBottom ? 0.5 : height - 0.5;
        _axisLine.X1 = 0;
        _axisLine.X2 = width;
        _axisLine.Y1 = axisY;
        _axisLine.Y2 = axisY;

        double pixelsPerDip = GetPixelsPerDip();
        var ticks = axis.GetMajorTicks(NormalizeViewportSize(mappingWidth));
        int lineIndex = 0;
        int labelIndex = 0;

        foreach (var tick in ticks)
        {
            double position = axis.CoordinateMapper.DataToScreen(tick.Position, axis.VisibleRange, mappingWidth);
            position = Math.Clamp(position, 0, mappingWidth);

            if (double.IsNaN(position) || double.IsInfinity(position))
                continue;

            double lineX = Math.Round(position) + 0.5;
            double tickY2 = isBottom ? axisY + TickLength : axisY - TickLength;

            var tickLine = GetOrCreateTickLine(lineIndex++);
            tickLine.X1 = lineX;
            tickLine.Y1 = axisY;
            tickLine.X2 = lineX;
            tickLine.Y2 = tickY2;
            tickLine.Stroke = axisBrush;
            tickLine.StrokeThickness = 1;
            tickLine.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(tick.Label))
                continue;

            var labelMetrics = CreateFormattedText(tick.Label, FontSize, axisBrush, pixelsPerDip);
            var label = GetOrCreateTickLabel(labelIndex++);
            ConfigureLabelTextBlock(label, tick.Label, axisBrush, FontSize);
            double labelX = position - labelMetrics.Width / 2;
            double labelY = isBottom
                ? axisY + TickLength + LabelOffset
                : axisY - TickLength - LabelOffset - labelMetrics.Height;

            if (labelX < 0)
                labelX = 0;
            if (labelX + labelMetrics.Width > width)
                labelX = width - labelMetrics.Width;

            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);
            label.Visibility = Visibility.Visible;
        }

        HideUnusedTickVisuals(lineIndex, labelIndex);
    }

    /// <summary>
    /// 配置标签文本块。
    /// </summary>
    private void ConfigureLabelTextBlock(TextBlock label, string text, Brush foreground, double fontSize)
    {
        label.Text = text;
        label.Foreground = foreground;
        label.FontFamily = FontFamily;
        label.FontSize = fontSize;
        label.FontStretch = FontStretch;
        label.FontStyle = FontStyle;
        label.FontWeight = FontWeight;
        label.TextAlignment = TextAlignment.Center;
    }

    /// <summary>
    /// 测量标题厚度。
    /// </summary>
    /// <returns>标题厚度</returns>
    private double MeasureTitleThickness()
    {
        if (Axis == null || string.IsNullOrWhiteSpace(Axis.Title))
            return 0;

        var titleText = CreateFormattedText(Axis.Title, FontSize + 2, GetAxisBrush(), GetPixelsPerDip());
        return titleText.Height + TitleGap;
    }

    /// <summary>
    /// 获取轴的画刷。
    /// </summary>
    /// <returns>轴的画刷</returns>
    private Brush GetAxisBrush()
    {
        if (Axis is AxisBase axisBase)
            return axisBase.AxisForeground;

        return Foreground;
    }

    /// <summary>
    /// 获取每DIP的像素数。
    /// </summary>
    /// <returns>每DIP的像素数</returns>
    private double GetPixelsPerDip()
    {
        if (!IsInitialized)
            return 1.0;

        return VisualTreeHelper.GetDpi(this).PixelsPerDip;
    }

    /// <summary>
    /// 创建格式化文本对象。
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="brush">画刷</param>
    /// <param name="pixelsPerDip">每DIP的像素数</param>
    /// <returns>格式化文本</returns>
    private FormattedText CreateFormattedText(string text, double fontSize, Brush brush, double pixelsPerDip)
    {
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            fontSize,
            brush,
            pixelsPerDip);
    }

    /// <summary>
    /// 规范化视口尺寸。
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>规范化后的尺寸</returns>
    private static double NormalizeViewportSize(double availableSize)
    {
        if (double.IsNaN(availableSize) || double.IsInfinity(availableSize) || availableSize <= 0)
            return FallbackMeasureViewportSize;

        return availableSize;
    }

    /// <summary>
    /// 查找绘图区域尺寸。
    /// 通过可视树向上查找Chart，然后获取GridLinesControl的尺寸。
    /// 这确保轴刻度与网格线使用相同的视口尺寸进行坐标映射。
    /// </summary>
    private Size FindPlotAreaSize()
    {
        DependencyObject current = this;
        while (current != null)
        {
            if (current is Chart chart)
            {
                var gl = chart.Template?.FindName("PART_GridLines", chart) as GridLinesControl;
                if (gl != null && gl.ActualWidth > 0 && gl.ActualHeight > 0)
                    return new Size(gl.ActualWidth, gl.ActualHeight);
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return Size.Empty;
    }

    /// <summary>
    /// 规范化拉伸尺寸。
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>规范化后的尺寸</returns>
    private static double NormalizeStretchSize(double availableSize)
    {
        if (double.IsNaN(availableSize) || double.IsInfinity(availableSize) || availableSize <= 0)
            return 0;

        return availableSize;
    }

    private Line GetOrCreateTickLine(int index)
    {
        if (_tickHost == null)
            throw new InvalidOperationException("Tick host is not initialized.");

        while (_tickLines.Count <= index)
        {
            var line = new Line
            {
                SnapsToDevicePixels = true
            };
            _tickLines.Add(line);
        }

        var tickLine = _tickLines[index];
        AttachTickVisual(tickLine);
        return tickLine;
    }

    private TextBlock GetOrCreateTickLabel(int index)
    {
        if (_tickHost == null)
            throw new InvalidOperationException("Tick host is not initialized.");

        while (_tickLabels.Count <= index)
        {
            var label = new TextBlock
            {
                TextAlignment = TextAlignment.Center
            };
            _tickLabels.Add(label);
        }

        var tickLabel = _tickLabels[index];
        AttachTickVisual(tickLabel);
        return tickLabel;
    }

    private void HideUnusedTickVisuals(int usedLineCount, int usedLabelCount)
    {
        for (int i = usedLineCount; i < _tickLines.Count; i++)
            DetachTickVisual(_tickLines[i]);

        for (int i = usedLabelCount; i < _tickLabels.Count; i++)
            DetachTickVisual(_tickLabels[i]);
    }

    private void AttachTickVisual(UIElement element)
    {
        if (_tickHost == null)
            throw new InvalidOperationException("Tick host is not initialized.");

        if (element is FrameworkElement frameworkElement
            && ReferenceEquals(frameworkElement.Parent, _tickHost))
        {
            return;
        }

        if (element is FrameworkElement existingElement
            && existingElement.Parent is Panel oldPanel)
        {
            oldPanel.Children.Remove(existingElement);
        }

        _tickHost.Children.Add(element);
    }

    private static void DetachTickVisual(UIElement element)
    {
        if (element is FrameworkElement frameworkElement
            && frameworkElement.Parent is Panel panel)
        {
            panel.Children.Remove(frameworkElement);
        }
    }
}
