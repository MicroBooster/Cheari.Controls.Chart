using System.Windows;
using System.Windows.Input;
using Cheari.Controls.Core;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Rendering.Context;

namespace Cheari.Controls.Modifiers;

/// <summary>
/// 缩放修饰器，允许用户通过鼠标滚轮来缩放图表的可视区域。
/// 默认同时缩放X和Y轴，按住Shift键只缩放Y轴，按住Ctrl键只缩放X轴。
/// </summary>
public class ZoomModifier : IChartModifier
{
    private IRenderContext? _context;

    /// <summary>
    /// 获取或设置缩放因子。默认值为 0.1，表示每次滚轮操作缩放 10%。
    /// 值越大缩放幅度越大，建议范围 0.05 ~ 0.3。
    /// </summary>
    public double ZoomStep { get; set; } = 0.1;

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
    /// 处理鼠标按下事件（缩放修饰器不处理此事件）。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseDown(MouseButtonEventArgs e) { }

    /// <summary>
    /// 处理鼠标释放事件（缩放修饰器不处理此事件）。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseUp(MouseButtonEventArgs e) { }

    /// <summary>
    /// 处理鼠标移动事件（缩放修饰器不处理此事件）。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    public void OnMouseMove(MouseEventArgs e) { }

    /// <summary>
    /// 处理鼠标滚轮事件，执行缩放操作。
    /// </summary>
    /// <param name="e">鼠标滚轮事件参数</param>
    public void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_context == null || _context.InputElement == null)
            return;

        if (_context.ViewportWidth <= 0 || _context.ViewportHeight <= 0)
            return;

        double zoomFactor = e.Delta > 0 ? 1.0 - ZoomStep : 1.0 + ZoomStep;

        // 以视口中心为缩放基点
        double pixelX = _context.ViewportWidth / 2.0;
        double pixelY = _context.ViewportHeight / 2.0;

        var defaultXAxis = ModifierUtilities.FindAxis(_context.XAxes, Chart.DefaultXAxisId) ?? _context.XAxes.FirstOrDefault();
        var defaultYAxis = ModifierUtilities.FindAxis(_context.YAxes, Chart.DefaultYAxisId) ?? _context.YAxes.FirstOrDefault();

        bool canZoomX = defaultXAxis == null || !defaultXAxis.AutoRange;
        bool canZoomY = defaultYAxis == null || !defaultYAxis.AutoRange;

        bool zoomX = Keyboard.Modifiers != ModifierKeys.Shift && canZoomX;
        bool zoomY = Keyboard.Modifiers != ModifierKeys.Control && canZoomY;

        DataRange newXRange = defaultXAxis?.VisibleRange ?? _context.XRange;
        DataRange newYRange = defaultYAxis?.VisibleRange ?? _context.YRange;

        if (zoomX)
        {
            newXRange = ZoomHorizontal(
                defaultXAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance,
                newXRange,
                _context.ViewportWidth,
                pixelX,
                zoomFactor);
            if (defaultXAxis is AxisBase xBase)
            {
                newXRange = xBase.ClampToVisibleRangeLimit(newXRange);
                newXRange = xBase.ApplyRelativeRangePadding(newXRange);
            }
        }

        if (zoomY)
        {
            newYRange = ZoomVertical(
                defaultYAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance,
                newYRange,
                 _context.ViewportHeight,
                pixelY,
                zoomFactor);
            if (defaultYAxis is AxisBase yBase)
            {
                newYRange = yBase.ClampToVisibleRangeLimit(newYRange);
                newYRange = yBase.ApplyRelativeRangePadding(newYRange);
            }
        }

        _context.SetRange(newXRange, newYRange);

        if (zoomX)
        {
            foreach (var axis in _context.XAxes)
            {
                if (axis.Id == Chart.DefaultXAxisId) continue;
                var range = ZoomHorizontal(axis.CoordinateMapper, axis.VisibleRange, _context.ViewportWidth, pixelX, zoomFactor);
                if (axis is AxisBase xb)
                {
                    range = xb.ClampToVisibleRangeLimit(range);
                    range = xb.ApplyRelativeRangePadding(range);
                }
                _context.SetAxisRange(axis.Id, range);
            }
        }

        if (zoomY)
        {
            foreach (var axis in _context.YAxes)
            {
                if (axis.Id == Chart.DefaultYAxisId) continue;
                var range = ZoomVertical(axis.CoordinateMapper, axis.VisibleRange, _context.ViewportHeight, pixelY, zoomFactor);
                if (axis is AxisBase yb)
                {
                    range = yb.ClampToVisibleRangeLimit(range);
                    range = yb.ApplyRelativeRangePadding(range);
                }
                _context.SetAxisRange(axis.Id, range);
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// 水平方向缩放计算，以指定点为中心进行缩放。
    /// </summary>
    /// <param name="mapper">坐标映射器</param>
    /// <param name="range">当前范围</param>
    /// <param name="viewportWidth">视口宽度</param>
    /// <param name="pivotX">缩放中心点的X坐标</param>
    /// <param name="zoomFactor">缩放因子</param>
    /// <returns>新的数据范围</returns>
    private static DataRange ZoomHorizontal(ICoordinateMapper mapper, DataRange range, int viewportWidth, double pivotX, double zoomFactor)
    {
        if (viewportWidth <= 0)
            return range;

        double newMin = mapper.ScreenToData(pivotX * (1.0 - zoomFactor), range, viewportWidth);
        double newMax = mapper.ScreenToData(pivotX + (viewportWidth - pivotX) * zoomFactor, range, viewportWidth);
        return new DataRange(newMin, newMax);
    }

    /// <summary>
    /// 垂直方向缩放计算，以指定点为中心进行缩放。
    /// </summary>
    /// <param name="mapper">坐标映射器</param>
    /// <param name="range">当前范围</param>
    /// <param name="viewportHeight">视口高度</param>
    /// <param name="pivotY">缩放中心点的Y坐标</param>
    /// <param name="zoomFactor">缩放因子</param>
    /// <returns>新的数据范围</returns>
    private static DataRange ZoomVertical(ICoordinateMapper mapper, DataRange range, int viewportHeight, double pivotY, double zoomFactor)
    {
        if (viewportHeight <= 0)
            return range;

        double pivotBottom = viewportHeight - pivotY;
        double newMin = mapper.ScreenToData(pivotBottom * (1.0 - zoomFactor), range, viewportHeight);
        double newMax = mapper.ScreenToData(pivotBottom + (viewportHeight - pivotBottom) * zoomFactor, range, viewportHeight);
        return new DataRange(newMin, newMax);
    }
}