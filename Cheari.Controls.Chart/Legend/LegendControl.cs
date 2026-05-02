using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Cheari.Controls.Controls;
using Cheari.Controls.Core;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例控件，用于在图表中显示图例。
/// </summary>
public class LegendControl : Control
{
    private ILegend? _lastLegend;
    private Grid? _internalLegendGrid;
    private Grid? _chartGrid;
    private bool _updatingPosition;
    private readonly List<(ILegendItem item, PropertyChangedEventHandler handler)> _itemHandlers = [];

    static LegendControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(LegendControl),
            new FrameworkPropertyMetadata(typeof(LegendControl)));
    }

    /// <summary>
    /// 标识 Position 依赖属性。
    /// </summary>
    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(nameof(Position), typeof(LegendPosition), typeof(LegendControl),
            new FrameworkPropertyMetadata(LegendPosition.InternalTop, OnPositionChanged));

    /// <summary>
    /// 标识 Orientation 依赖属性。
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(LegendOrientation), typeof(LegendControl),
            new FrameworkPropertyMetadata(LegendOrientation.Vertical, OnOrientationChanged));

    /// <summary>
    /// 标识 HorizontalAlignment 依赖属性。
    /// </summary>
    public static new readonly DependencyProperty HorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(HorizontalAlignment), typeof(HorizontalAlignment), typeof(LegendControl),
            new FrameworkPropertyMetadata(HorizontalAlignment.Center, OnHorizontalAlignmentChanged));

    /// <summary>
    /// 标识 VerticalAlignment 依赖属性。
    /// </summary>
    public static new readonly DependencyProperty VerticalAlignmentProperty =
        DependencyProperty.Register(nameof(VerticalAlignment), typeof(VerticalAlignment), typeof(LegendControl),
            new FrameworkPropertyMetadata(VerticalAlignment.Center, OnVerticalAlignmentChanged));

    /// <summary>
    /// 标识 Legend 依赖属性。
    /// </summary>
    public static readonly DependencyProperty LegendProperty =
        DependencyProperty.Register(nameof(Legend), typeof(ILegend), typeof(LegendControl),
            new FrameworkPropertyMetadata(null, OnLegendChanged));

    /// <summary>
    /// 标识 LegendSpacing 依赖属性。
    /// </summary>
    public static readonly DependencyProperty LegendSpacingProperty =
        DependencyProperty.Register(nameof(LegendSpacing), typeof(double), typeof(LegendControl),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLegendSpacingChanged));

    /// <summary>
    /// 获取或设置图例的位置。
    /// </summary>
    public LegendPosition Position
    {
        get => (LegendPosition)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// 获取或设置图例的排列方向。
    /// </summary>
    public LegendOrientation Orientation
    {
        get => (LegendOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// 获取或设置图例的水平对齐方式。
    /// </summary>
    public new HorizontalAlignment HorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    /// <summary>
    /// 获取或设置图例的垂直对齐方式。
    /// </summary>
    public new VerticalAlignment VerticalAlignment
    {
        get => (VerticalAlignment)GetValue(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    /// <summary>
    /// 获取或设置图例数据源。
    /// </summary>
    public ILegend? Legend
    {
        get => (ILegend?)GetValue(LegendProperty);
        set => SetValue(LegendProperty, value);
    }

    /// <summary>
    /// 获取或设置图例的间距大小（像素）。
    /// </summary>
    public double LegendSpacing
    {
        get => (double)GetValue(LegendSpacingProperty);
        set => SetValue(LegendSpacingProperty, value);
    }

    /// <summary>
    /// 当应用模板时调用，重建视觉元素。
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RebuildVisuals(_lastLegend);
    }

    /// <summary>
    /// 当视觉父级变更时调用（添加到视觉树时），重建视觉元素。
    /// </summary>
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        // 确保当 LegendControl 被添加到视觉树后，重建视觉元素
        if (VisualParent != null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RebuildVisuals(_lastLegend);
            }),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    /// <summary>
    /// 设置图例容器（内部 + 外部），仅触发一次位置更新。
    /// </summary>
    public void SetContainers(Grid? internalGrid, Grid? chartGrid)
    {
        _internalLegendGrid = internalGrid;
        _chartGrid = chartGrid;
        UpdatePosition();
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            if (control.Legend != null && e.NewValue is LegendPosition newPos)
            {
                control.Legend.Position = newPos;
            }
            control.UpdatePosition();
            control.RebuildVisuals(control._lastLegend);
            control.ApplyDirectionalMargin();
        }
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            // 同步到 Legend 对象
            if (control.Legend != null && e.NewValue is LegendOrientation newOrient)
            {
                control.Legend.Orientation = newOrient;
            }
            control.RebuildVisuals(control._lastLegend);
        }
    }

    private static void OnHorizontalAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            if (control.Legend != null && e.NewValue is HorizontalAlignment newAlign)
            {
                control.Legend.HorizontalAlignment = newAlign;
            }
            control.SetValue(FrameworkElement.HorizontalAlignmentProperty, e.NewValue);
            control.ApplyDirectionalMargin();
        }
    }

    private static void OnVerticalAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            if (control.Legend != null && e.NewValue is VerticalAlignment newAlign)
            {
                control.Legend.VerticalAlignment = newAlign;
            }
            control.SetValue(FrameworkElement.VerticalAlignmentProperty, e.NewValue);
            control.ApplyDirectionalMargin();
        }
    }

    private static void OnLegendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LegendControl control) return;

        if (e.OldValue is ILegend oldLegend)
        {
            oldLegend.ItemsChanged -= control.OnLegendItemsChanged;
            if (oldLegend is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= control.OnLegendPropertyChanged;
        }

        if (e.NewValue is ILegend newLegend)
        {
            newLegend.ItemsChanged += control.OnLegendItemsChanged;
            if (newLegend is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += control.OnLegendPropertyChanged;

            newLegend.Position = control.Position;
            newLegend.Orientation = control.Orientation;
            newLegend.HorizontalAlignment = control.HorizontalAlignment;
            newLegend.VerticalAlignment = control.VerticalAlignment;
        }

        control._lastLegend = e.NewValue as ILegend;
        control.RebuildVisuals(control._lastLegend);
    }

    private void OnLegendPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ILegend legend) return;
        switch (e.PropertyName)
        {
            case nameof(ILegend.HorizontalAlignment):
                SetValue(HorizontalAlignmentProperty, legend.HorizontalAlignment);
                SetValue(FrameworkElement.HorizontalAlignmentProperty, legend.HorizontalAlignment);
                ApplyDirectionalMargin();
                break;
            case nameof(ILegend.VerticalAlignment):
                SetValue(VerticalAlignmentProperty, legend.VerticalAlignment);
                SetValue(FrameworkElement.VerticalAlignmentProperty, legend.VerticalAlignment);
                ApplyDirectionalMargin();
                break;
        }
    }

    private void OnLegendItemsChanged() => RebuildVisuals(_lastLegend);

    private static void OnLegendSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
            control.ApplyDirectionalMargin();
    }

    private void ApplyDirectionalMargin()
    {
        var pos = Position;
        var spacing = LegendSpacing;
        var h = HorizontalAlignment;
        var v = VerticalAlignment;

        double l = 0, t = 0, r = 0, b = 0;

        switch (pos)
        {
            case LegendPosition.InternalTop:
                t = spacing;
                if (h == HorizontalAlignment.Left || h == HorizontalAlignment.Stretch)
                    l = spacing;
                if (h == HorizontalAlignment.Right || h == HorizontalAlignment.Stretch)
                    r = spacing;
                break;
            case LegendPosition.InternalBottom:
                b = spacing;
                if (h == HorizontalAlignment.Left || h == HorizontalAlignment.Stretch)
                    l = spacing;
                if (h == HorizontalAlignment.Right || h == HorizontalAlignment.Stretch)
                    r = spacing;
                break;
            case LegendPosition.InternalLeft:
                l = spacing;
                if (v == VerticalAlignment.Top || v == VerticalAlignment.Stretch)
                    t = spacing;
                if (v == VerticalAlignment.Bottom || v == VerticalAlignment.Stretch)
                    b = spacing;
                break;
            case LegendPosition.InternalRight:
                r = spacing;
                if (v == VerticalAlignment.Top || v == VerticalAlignment.Stretch)
                    t = spacing;
                if (v == VerticalAlignment.Bottom || v == VerticalAlignment.Stretch)
                    b = spacing;
                break;
            case LegendPosition.ExternalTop:
                b = spacing;
                break;
            case LegendPosition.ExternalBottom:
                t = spacing;
                break;
            case LegendPosition.ExternalLeft:
                r = spacing;
                break;
            case LegendPosition.ExternalRight:
                l = spacing;
                break;
        }

        Margin = new Thickness(l, t, r, b);
    }

    private void RebuildVisuals(ILegend? legend)
    {
        if (GetTemplateChild("PART_ItemsHost") is not StackPanel host)
        {
            return;
        }

        foreach (var (item, handler) in _itemHandlers)
            item.PropertyChanged -= handler;
        _itemHandlers.Clear();

        var orientation = Orientation;
        host.Orientation = orientation == LegendOrientation.Vertical
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;

        host.Children.Clear();

        if (legend == null)
            return;

        foreach (var item in legend.Items)
            host.Children.Add(CreateLegendItemRow(item, orientation));
    }

    private FrameworkElement CreateLegendItemRow(ILegendItem item, LegendOrientation orientation)
    {
        bool isHorizontal = orientation == LegendOrientation.Horizontal;

        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(isHorizontal ? 6 : 4, 2, isHorizontal ? 6 : 4, 2),
            Cursor = Cursors.Hand
        };

        var icon = LegendIconHelper.CreateIcon(item);
        icon.Cursor = Cursors.Hand;
        row.Children.Add(icon);

        var label = new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212))
        };

        var tagLabel = new TextBlock
        {
            Text = item.Tag != null ? $" ({item.Tag})" : string.Empty,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            Visibility = item.Tag != null ? Visibility.Visible : Visibility.Collapsed
        };

        PropertyChangedEventHandler handler = null!;
        handler = (_, e) =>
        {
            if (!icon.Dispatcher.CheckAccess())
            {
                icon.Dispatcher.BeginInvoke(handler, new object?[] { null, e });
                return;
            }

            if (e.PropertyName == nameof(ILegendItem.IsVisible))
            {
                var opacity = item.IsVisible ? 1.0 : 0.4;
                icon.Opacity = opacity;
                label.Opacity = opacity;
                label.Foreground = item.IsVisible
                    ? new SolidColorBrush(Color.FromRgb(212, 212, 212))
                    : new SolidColorBrush(Color.FromRgb(120, 120, 130));
            }
            else if (e.PropertyName == nameof(ILegendItem.Stroke) || e.PropertyName == "Fill")
            {
                LegendIconHelper.UpdateIconColor(icon, item.Stroke);
            }
            else if (e.PropertyName == nameof(ILegendItem.Tag))
            {
                tagLabel.Text = item.Tag != null ? $" ({item.Tag})" : string.Empty;
                tagLabel.Visibility = item.Tag != null ? Visibility.Visible : Visibility.Collapsed;
            }
        };

        item.PropertyChanged += handler;
        _itemHandlers.Add((item, handler));

        var opacity = item.IsVisible ? 1.0 : 0.4;
        icon.Opacity = opacity;
        label.Opacity = opacity;

        row.Children.Add(label);
        row.Children.Add(tagLabel);

        row.MouseLeftButtonDown += (_, _) =>
        {
            item.IsVisible = !item.IsVisible;
        };

        icon.MouseLeftButtonDown += (sender, e) =>
        {
            e.Handled = true;
            ShowColorPicker(item, icon);
        };

        return row;
    }

    private static void ShowColorPicker(ILegendItem item, UIElement placementTarget)
    {
        if (item.Series is not RenderableSeriesBase series)
            return;

        var popup = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            IsOpen = true,
            StaysOpen = true,
            AllowsTransparency = true
        };

        var editor = new ColorEditor
        {
            SelectedColor = item.Stroke,
            PreviousColor = item.Stroke,
            ShowAlpha = true,
            ShowPresets = true
        };

        editor.OnClose = confirmed =>
        {
            if (confirmed)
            {
                var color = editor.SelectedColor;
                series.Stroke = color;

                if (series is Series.Types.AreaRenderableSeries areaSeries)
                    areaSeries.Fill = Color.FromArgb(
                        (byte)(color.A * areaSeries.FillOpacity),
                        color.R, color.G, color.B);
                else if (series is Series.Types.BarRenderableSeries barSeries)
                    barSeries.Fill = Color.FromArgb(
                        (byte)(color.A * 0.8),
                        color.R, color.G, color.B);

                var chart = FindParentChart(placementTarget);
                var legendControl = FindParentLegend(placementTarget);
                legendControl?.RefreshVisuals();
                chart?.ForceRedraw();
            }
            popup.IsOpen = false;
        };

        popup.Child = editor;
    }

    private static Chart? FindParentChart(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Chart chart)
                return chart;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static LegendControl? FindParentLegend(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is LegendControl legend)
                return legend;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    /// <summary>
    /// 强制重建图例视觉元素。
    /// </summary>
    internal void RefreshVisuals()
    {
        RebuildVisuals(_lastLegend);
    }

    /// <summary>
    /// 更新图例在图表中的位置
    /// </summary>
    public void UpdatePosition()
    {
        if (_updatingPosition)
            return;
        if (_internalLegendGrid == null && _chartGrid == null)
            return;

        _updatingPosition = true;
        try
        {
            var position = Legend?.Position ?? Position;
            bool isInternal = IsInternalPosition(position);
            var targetGrid = isInternal ? _internalLegendGrid : _chartGrid;

            if (targetGrid == null)
                return;

            if (Parent == targetGrid)
            {
                if (isInternal)
                    ApplyInternalPosition(position);
                else
                    ApplyExternalPosition(position);
                return;
            }

            if (Parent is Panel oldPanel)
                oldPanel.Children.Remove(this);

            Grid.SetRow(this, 0);
            Grid.SetColumn(this, 0);

            targetGrid.Children.Add(this);
            Panel.SetZIndex(this, 10);

            if (isInternal)
                ApplyInternalPosition(position);
            else
                ApplyExternalPosition(position);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LegendControl] UpdatePosition failed: {ex.Message}");
        }
        finally
        {
            _updatingPosition = false;
        }
    }

    private static bool IsInternalPosition(LegendPosition position)
    {
        return position == LegendPosition.InternalTop
            || position == LegendPosition.InternalBottom
            || position == LegendPosition.InternalLeft
            || position == LegendPosition.InternalRight;
    }

    private void ApplyInternalPosition(LegendPosition position)
    {
        switch (position)
        {
            case LegendPosition.InternalTop:
                Grid.SetRow(this, 0);
                Grid.SetColumn(this, 1);
                break;
            case LegendPosition.InternalBottom:
                Grid.SetRow(this, 2);
                Grid.SetColumn(this, 1);
                break;
            case LegendPosition.InternalLeft:
                Grid.SetRow(this, 1);
                Grid.SetColumn(this, 0);
                break;
            case LegendPosition.InternalRight:
                Grid.SetRow(this, 1);
                Grid.SetColumn(this, 2);
                break;
        }
    }

    private void ApplyExternalPosition(LegendPosition position)
    {
        switch (position)
        {
            case LegendPosition.ExternalTop:
                Grid.SetRow(this, 1);
                Grid.SetColumn(this, 2);
                break;
            case LegendPosition.ExternalBottom:
                Grid.SetRow(this, 5);
                Grid.SetColumn(this, 2);
                break;
            case LegendPosition.ExternalLeft:
                Grid.SetRow(this, 3);
                Grid.SetColumn(this, 0);
                break;
            case LegendPosition.ExternalRight:
                Grid.SetRow(this, 3);
                Grid.SetColumn(this, 4);
                break;
        }
    }
}