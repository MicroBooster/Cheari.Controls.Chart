using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
            // 同步到 Legend 对象
            if (control.Legend != null && e.NewValue is LegendPosition newPos)
            {
                control.Legend.Position = newPos;
            }
            control.RebuildVisuals(control._lastLegend);
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
            // 同步到 Legend 对象
            if (control.Legend != null && e.NewValue is HorizontalAlignment newAlign)
            {
                control.Legend.HorizontalAlignment = newAlign;
            }
        }
    }

    private static void OnVerticalAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            // 同步到 Legend 对象
            if (control.Legend != null && e.NewValue is VerticalAlignment newAlign)
            {
                control.Legend.VerticalAlignment = newAlign;
            }
        }
    }

    private static void OnLegendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LegendControl control) return;

        if (e.OldValue is ILegend oldLegend)
            oldLegend.ItemsChanged -= control.OnLegendItemsChanged;

        if (e.NewValue is ILegend newLegend)
        {
            newLegend.ItemsChanged += control.OnLegendItemsChanged;
            // 将当前 LegendControl 的属性值同步到新的 Legend 对象
            newLegend.Position = control.Position;
            newLegend.Orientation = control.Orientation;
            newLegend.HorizontalAlignment = control.HorizontalAlignment;
            newLegend.VerticalAlignment = control.VerticalAlignment;
        }

        control._lastLegend = e.NewValue as ILegend;
        control.RebuildVisuals(control._lastLegend);
    }

    private void OnLegendItemsChanged() => RebuildVisuals(_lastLegend);

    private void RebuildVisuals(ILegend? legend)
    {
        if (GetTemplateChild("PART_ItemsHost") is not StackPanel host)
        {
            // 如果还没有应用模板，我们稍后再试
            return;
        }

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

    private static FrameworkElement CreateLegendItemRow(ILegendItem item, LegendOrientation orientation)
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

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ILegendItem.IsVisible))
            {
                label.Dispatcher.Invoke(() =>
                {
                    label.Opacity = item.IsVisible ? 1.0 : 0.35;
                });
            }
            else if (e.PropertyName == nameof(ILegendItem.Stroke))
            {
                icon.Dispatcher.Invoke(() =>
                {
                    LegendIconHelper.UpdateIconColor(icon, item.Stroke);
                });
            }
        };

        label.Opacity = item.IsVisible ? 1.0 : 0.35;

        row.Children.Add(label);

        var tagLabel = new TextBlock
        {
            Text = item.Tag != null ? $" ({item.Tag})" : string.Empty,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            Visibility = item.Tag != null ? Visibility.Visible : Visibility.Collapsed
        };

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ILegendItem.Tag))
            {
                tagLabel.Dispatcher.Invoke(() =>
                {
                    tagLabel.Text = item.Tag != null ? $" ({item.Tag})" : string.Empty;
                    tagLabel.Visibility = item.Tag != null ? Visibility.Visible : Visibility.Collapsed;
                });
            }
        };

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
            StaysOpen = false,
            AllowsTransparency = true
        };

        var colorPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 34, 56)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(73, 87, 110)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        var grid = new UniformGrid
        {
            Columns = 6,
            Width = 180,
            Height = 120
        };

        var presetColors = new[]
        {
            Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Cyan, Colors.Blue,
            Colors.Purple, Colors.Pink, Colors.White, Colors.Gray, Colors.LightBlue, Colors.LightGreen,
            Colors.DarkRed, Colors.DarkOrange, Color.FromRgb(139, 119, 0), Colors.DarkGreen, Colors.DarkCyan, Colors.DarkBlue,
            Color.FromRgb(148, 0, 211), Color.FromRgb(238, 18, 137), Colors.Black, Colors.LightGray, Colors.SkyBlue, Colors.LimeGreen
        };

        foreach (var color in presetColors)
        {
            var colorButton = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(color),
                BorderBrush = color == item.Stroke ? new SolidColorBrush(Colors.White) : null,
                BorderThickness = color == item.Stroke ? new Thickness(2) : new Thickness(1)
            };

            colorButton.Click += (_, _) =>
            {
                series.Stroke = color;
                popup.IsOpen = false;
            };

            grid.Children.Add(colorButton);
        }

        colorPanel.Child = grid;
        popup.Child = colorPanel;
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
        catch (Exception)
        {
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