using System.Windows;
using System.Windows.Controls;

namespace Cheari.Controls.Axes.Controls;

/// <summary>
/// 坐标轴项控件，管理多个坐标轴控件的容器。
/// </summary>
public class AxisItemsControl : ItemsControl
{
    /// <summary>
    /// 确定指定项是否是其自己的容器。
    /// </summary>
    /// <param name="item">要检查的项</param>
    /// <returns>如果项是其自己的容器，则为 true；否则为 false</returns>
    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is AxisControl;

    /// <summary>
    /// 创建新的容器来显示项。
    /// </summary>
    /// <returns>新的容器对象</returns>
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new AxisControl
        {
            Focusable = false,
            IsHitTestVisible = false
        };
    }

    /// <summary>
    /// 准备用于显示指定项的容器。
    /// </summary>
    /// <param name="element">容器元素</param>
    /// <param name="item">要显示的项</param>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is AxisControl control)
        {
            control.Focusable = false;
            control.IsHitTestVisible = false;
            control.Axis = item as IAxis;
        }
    }

    /// <summary>
    /// 清除项的容器。
    /// </summary>
    /// <param name="element">容器元素</param>
    /// <param name="item">要清除的项</param>
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is AxisControl control)
            control.Axis = null;

        base.ClearContainerForItemOverride(element, item);
    }
}