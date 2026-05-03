using System.Windows;
using System.Windows.Input;
using Cheari.Controls.Core;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Rendering.Context;

namespace Cheari.Controls.Modifiers;

/// <summary>
/// 平移修饰器，允许用户通过鼠标拖拽来平移图表的可视区域。
/// </summary>
public class PanModifier : IChartModifier
{
    private IRenderContext? _context;
    private Point _lastMousePosition;
    private bool _isPanning;

    /// <summary>
    /// 当修饰器附加到图表时调用。
    /// </summary>
    public void OnAttached() { }

    /// <summary>
    /// 当修饰器从图表分离时调用。
    /// </summary>
    public void OnDetached() { }

    /// <summary>
    /// 设置渲染上下文。
    /// </summary>
    /// <param name="context">渲染上下文</param>
    public void SetContext(IRenderContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 处理鼠标按下事件，开始平移操作。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _lastMousePosition = GetRelativePosition(e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 处理鼠标释放事件，结束平移操作。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 处理鼠标移动事件，执行平移操作。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseMove(MouseEventArgs e)
    {
        if (_isPanning && _context != null && _context.ViewportWidth > 0 && _context.ViewportHeight > 0)
        {
            Point currentPosition = GetRelativePosition(e);
            double deltaX = (currentPosition.X - _lastMousePosition.X) * _context.DpiScaleX;
            double deltaY = (currentPosition.Y - _lastMousePosition.Y) * _context.DpiScaleY;

            var defaultXAxis = ModifierUtilities.FindAxis(_context.XAxes, Chart.DefaultXAxisId) ?? _context.XAxes.FirstOrDefault();
            var defaultYAxis = ModifierUtilities.FindAxis(_context.YAxes, Chart.DefaultYAxisId) ?? _context.YAxes.FirstOrDefault();

            bool canPanX = defaultXAxis == null || !defaultXAxis.AutoRange;
            bool canPanY = defaultYAxis == null || !defaultYAxis.AutoRange;

            var newXRange = canPanX
                ? PanHorizontal(
                    defaultXAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance,
                    defaultXAxis?.VisibleRange ?? _context.XRange,
                    _context.ViewportWidth,
                    deltaX)
                : (defaultXAxis?.VisibleRange ?? _context.XRange);

            var newYRange = canPanY
                ? PanVertical(
                    defaultYAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance,
                    defaultYAxis?.VisibleRange ?? _context.YRange,
                    _context.ViewportHeight,
                    deltaY)
                : (defaultYAxis?.VisibleRange ?? _context.YRange);

            if (canPanX && defaultXAxis is AxisBase xBase)
            {
                newXRange = xBase.ClampToVisibleRangeLimit(newXRange);
                newXRange = xBase.ApplyRelativeRangePadding(newXRange);
            }
            if (canPanY && defaultYAxis is AxisBase yBase)
            {
                newYRange = yBase.ClampToVisibleRangeLimit(newYRange);
                newYRange = yBase.ApplyRelativeRangePadding(newYRange);
            }

            _context.SetRange(newXRange, newYRange);

            if (canPanX)
            {
                foreach (var axis in _context.XAxes)
                {
                    if (axis.Id == Chart.DefaultXAxisId) continue;
                    var range = PanHorizontal(axis.CoordinateMapper, axis.VisibleRange, _context.ViewportWidth, deltaX);
                    if (axis is AxisBase xb)
                    {
                        range = xb.ClampToVisibleRangeLimit(range);
                        range = xb.ApplyRelativeRangePadding(range);
                    }
                    _context.SetAxisRange(axis.Id, range);
                }
            }

            if (canPanY)
            {
                foreach (var axis in _context.YAxes)
                {
                    if (axis.Id == Chart.DefaultYAxisId) continue;
                    var range = PanVertical(axis.CoordinateMapper, axis.VisibleRange, _context.ViewportHeight, deltaY);
                    if (axis is AxisBase yb)
                    {
                        range = yb.ClampToVisibleRangeLimit(range);
                        range = yb.ApplyRelativeRangePadding(range);
                    }
                    _context.SetAxisRange(axis.Id, range);
                }
            }

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 处理鼠标滚轮事件（平移修饰器不处理滚轮事件）。
    /// </summary>
    /// <param name="e">鼠标滚轮事件参数</param>
    public void OnMouseWheel(MouseWheelEventArgs e) { }

    /// <summary>
    /// 获取鼠标相对于输入元素的位置。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    /// <returns>相对位置</returns>
    private Point GetRelativePosition(MouseEventArgs e)
        => _context?.InputElement is { } inputElement
            ? e.GetPosition(inputElement)
            : e.GetPosition(null);

    /// <summary>
    /// 水平方向平移计算。
    /// </summary>
    /// <param name="mapper">坐标映射器</param>
    /// <param name="range">当前范围</param>
    /// <param name="viewportWidth">视口宽度</param>
    /// <param name="deltaX">水平位移</param>
    /// <returns>新的数据范围</returns>
    private static DataRange PanHorizontal(ICoordinateMapper mapper, DataRange range, int viewportWidth, double deltaX)
    {
        if (viewportWidth <= 0)
            return range;

        double newMin = mapper.ScreenToData(-deltaX, range, viewportWidth);
        double newMax = mapper.ScreenToData(viewportWidth - deltaX, range, viewportWidth);
        return new DataRange(newMin, newMax);
    }

    /// <summary>
    /// 垂直方向平移计算。
    /// </summary>
    /// <param name="mapper">坐标映射器</param>
    /// <param name="range">当前范围</param>
    /// <param name="viewportHeight">视口高度</param>
    /// <param name="deltaY">垂直位移</param>
    /// <returns>新的数据范围</returns>
    private static DataRange PanVertical(ICoordinateMapper mapper, DataRange range, int viewportHeight, double deltaY)
    {
        if (viewportHeight <= 0)
            return range;

        double newMin = mapper.ScreenToData(deltaY, range, viewportHeight);
        double newMax = mapper.ScreenToData(viewportHeight + deltaY, range, viewportHeight);
        return new DataRange(newMin, newMax);
    }
}