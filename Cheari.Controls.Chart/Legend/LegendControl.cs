using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例 WPF 控件，展示图表系列的名称和颜色，支持点击切换可见性。
/// </summary>
public class LegendControl : Control
{
    static LegendControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LegendControl), new FrameworkPropertyMetadata(typeof(LegendControl)));
    }

    /// <summary>标识 <see cref="Legend"/> 依赖属性。</summary>
    public static readonly DependencyProperty LegendProperty =
        DependencyProperty.Register(nameof(Legend), typeof(ILegend), typeof(LegendControl),
            new PropertyMetadata(null, OnLegendChanged));

    /// <summary>获取或设置图例数据。</summary>
    public ILegend? Legend
    {
        get => (ILegend?)GetValue(LegendProperty);
        set => SetValue(LegendProperty, value);
    }

    private static void OnLegendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LegendControl control)
        {
            if (e.OldValue is ILegend oldLegend)
                oldLegend.ItemsChanged -= control.OnLegendItemsChanged;

            if (e.NewValue is ILegend newLegend)
            {
                newLegend.ItemsChanged += control.OnLegendItemsChanged;
                control.RebuildVisuals(newLegend);
            }
            else
            {
                control.ClearVisuals();
            }
        }
    }

    private void OnLegendItemsChanged() => RebuildVisuals(Legend);

    private void ClearVisuals()
    {
        if (GetTemplateChild("PART_ItemsHost") is Panel host)
            host.Children.Clear();
    }

    private void RebuildVisuals(ILegend? legend)
    {
        if (GetTemplateChild("PART_ItemsHost") is not Panel host)
            return;

        host.Children.Clear();

        if (legend == null)
            return;

        foreach (var item in legend.Items)
            host.Children.Add(CreateLegendItemRow(item));
    }

    private FrameworkElement CreateLegendItemRow(ILegendItem item)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 2, 4, 2),
            Cursor = Cursors.Hand
        };

        var colorBox = new Rectangle
        {
            Width = 14,
            Height = 10,
            Fill = new SolidColorBrush(item.Stroke),
            Stroke = new SolidColorBrush(item.Stroke),
            StrokeThickness = 1,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };

        var label = new TextBlock
        {
            Text = string.IsNullOrEmpty(item.Title) ? "Series" : item.Title,
            Foreground = item.IsVisible ? Brushes.White : new SolidColorBrush(Color.FromArgb(128, 128, 128, 128)),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };

        row.Children.Add(colorBox);
        row.Children.Add(label);

        row.MouseLeftButtonDown += (_, _) =>
        {
            item.IsVisible = !item.IsVisible;
            label.Foreground = item.IsVisible ? Brushes.White : new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
            colorBox.Opacity = item.IsVisible ? 1.0 : 0.3;
        };

        colorBox.Opacity = item.IsVisible ? 1.0 : 0.3;

        return row;
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RebuildVisuals(Legend);
    }
}
