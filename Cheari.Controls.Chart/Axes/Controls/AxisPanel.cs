using System.Windows;
using System.Windows.Controls;

namespace Cheari.Controls.Axes.Controls;

/// <summary>
/// 坐标轴面板，负责排列多个坐标轴控件。
/// 根据轴的位置（左、右、上、下）以不同方式排列子元素。
/// </summary>
public class AxisPanel : Panel
{
    /// <summary>
    /// 标识 AxisPlacement 依赖属性。
    /// </summary>
    public static readonly DependencyProperty AxisPlacementProperty =
        DependencyProperty.Register(nameof(AxisPlacement), typeof(AxisPlacement), typeof(AxisPanel),
            new PropertyMetadata(AxisPlacement.Left));

    /// <summary>
    /// 获取或设置轴的位置，决定面板的布局方向。
    /// </summary>
    public AxisPlacement AxisPlacement
    {
        get => (AxisPlacement)GetValue(AxisPlacementProperty);
        set => SetValue(AxisPlacementProperty, value);
    }

    /// <summary>
    /// 测量阶段，计算面板所需的尺寸。
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>期望尺寸</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        double totalWidth = 0;
        double totalHeight = 0;
        double maxWidth = 0;
        double maxHeight = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(availableSize);
            var desired = child.DesiredSize;

            if (AxisPlacement is AxisPlacement.Left or AxisPlacement.Right)
            {
                totalWidth += desired.Width;
                maxHeight = Math.Max(maxHeight, desired.Height);
            }
            else
            {
                totalHeight += desired.Height;
                maxWidth = Math.Max(maxWidth, desired.Width);
            }
        }

        return AxisPlacement is AxisPlacement.Left or AxisPlacement.Right
            ? new Size(totalWidth, maxHeight)
            : new Size(maxWidth, totalHeight);
    }

    /// <summary>
    /// 排列阶段，定位子元素。
    /// </summary>
    /// <param name="finalSize">最终尺寸</param>
    /// <returns>实际尺寸</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (AxisPlacement is AxisPlacement.Left or AxisPlacement.Right)
        {
            double currentX = 0;
            foreach (UIElement child in InternalChildren)
            {
                var desired = child.DesiredSize;
                double x = AxisPlacement == AxisPlacement.Right
                    ? finalSize.Width - currentX - desired.Width
                    : currentX;
                child.Arrange(new Rect(x, 0, desired.Width, finalSize.Height));
                currentX += desired.Width;
            }
        }
        else
        {
            double currentY = 0;
            foreach (UIElement child in InternalChildren)
            {
                var desired = child.DesiredSize;
                double y = AxisPlacement == AxisPlacement.Bottom
                    ? finalSize.Height - currentY - desired.Height
                    : currentY;
                child.Arrange(new Rect(0, y, finalSize.Width, desired.Height));
                currentY += desired.Height;
            }
        }

        return finalSize;
    }
}