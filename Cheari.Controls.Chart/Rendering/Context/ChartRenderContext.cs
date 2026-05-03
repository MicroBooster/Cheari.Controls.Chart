using System.Windows;
using Cheari.Controls.Core;
using Cheari.Controls.Axes;
using Cheari.Controls.Series;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Data;

namespace Cheari.Controls.Rendering.Context;

/// <summary>
/// 图表渲染上下文实现，封装了渲染所需的视图和坐标轴信息。
/// 
/// <para><b>延迟绑定访问器模式：</b></para>
/// <para>本类使用 Func&lt;T&gt; 访问器（如 XRangeAccessor、SeriesAccessor）而非直接存储值，
/// 这样 SeriesRendererDispatcher 在为每个轴组创建 scoped 上下文时，只需替换访问器即可
/// 限定该组的可见范围和坐标映射器，而无需复制所有属性。</para>
/// 
/// <code>
/// 延迟绑定流程：
/// 
/// Chart.OnSurfaceDraw()
///     │
///     ▼
/// ChartRenderContext (全局上下文)
///   XRangeAccessor → () => XRange      ← 绑定到 Chart 的默认轴范围
///   SeriesAccessor → () => Series      ← 绑定到 Chart 的所有系列
///     │
///     ▼
/// SeriesRendererDispatcher.RenderAxisGroup()
///     │
///     ▼
/// _scopedContext (限定上下文)
///   XRangeAccessor → () => xRange      ← 替换为当前轴组的范围
///   SeriesAccessor → () => groupedSeries ← 替换为当前轴组的系列
///   其他访问器保持不变（从 source 复制）
/// </code>
/// 
/// <para><b>线程安全说明：</b></para>
/// <para>访问器在 UI 线程的 OnSurfaceDraw 回调中求值，因此无需额外同步。
/// 但如果后台线程（如 RenderLoop）访问此上下文，必须确保访问器的实现是线程安全的。</para>
/// </summary>
public class ChartRenderContext : IRenderContext
{
    private Func<DataRange> _xRangeAccessor = () => new DataRange(0, 100);
    private Func<DataRange> _yRangeAccessor = () => new DataRange(-1, 1);
    private Func<DataRange> _coreXRangeAccessor = () => new DataRange(0, 100);
    private Func<DataRange> _coreYRangeAccessor = () => new DataRange(-1, 1);
    private Func<IList<IRenderableSeries>> _seriesAccessor = () => Array.Empty<IRenderableSeries>();
    private Func<IList<IAxis>> _xAxesAccessor = () => Array.Empty<IAxis>();
    private Func<IList<IAxis>> _yAxesAccessor = () => Array.Empty<IAxis>();
    private Action<DataRange, DataRange>? _onRangeChanged;
    private Action<string, DataRange>? _onAxisRangeChanged;
    private Func<IRenderableSeries, DataFrame?>? _frameAccessor;

    /// <summary>
    /// 获取X轴数据范围。
    /// </summary>
    public DataRange XRange => _xRangeAccessor();

    /// <summary>
    /// 获取Y轴数据范围。
    /// </summary>
    public DataRange YRange => _yRangeAccessor();

    /// <summary>
    /// 获取X轴核心数据范围（不含留白），用于渲染时裁剪曲线数据。
    /// </summary>
    public DataRange CoreXRange => _coreXRangeAccessor();

    /// <summary>
    /// 获取Y轴核心数据范围（不含留白），用于渲染时裁剪曲线数据。
    /// </summary>
    public DataRange CoreYRange => _coreYRangeAccessor();

    /// <summary>
    /// 获取渲染系列列表。
    /// </summary>
    public IList<IRenderableSeries> Series => _seriesAccessor();

    /// <summary>
    /// 获取所有X轴列表。
    /// </summary>
    public IReadOnlyList<IAxis> XAxes => (IReadOnlyList<IAxis>)_xAxesAccessor();

    /// <summary>
    /// 获取所有Y轴列表。
    /// </summary>
    public IReadOnlyList<IAxis> YAxes => (IReadOnlyList<IAxis>)_yAxesAccessor();

    /// <summary>
    /// 获取或设置视口宽度（像素）。
    /// </summary>
    public int ViewportWidth { get; set; }

    /// <summary>
    /// 获取或设置视口高度（像素）。
    /// </summary>
    public int ViewportHeight { get; set; }

    /// <summary>
    /// 获取或设置X方向的DPI缩放因子。
    /// </summary>
    public double DpiScaleX { get; set; } = 1.0;

    /// <summary>
    /// 获取或设置Y方向的DPI缩放因子。
    /// </summary>
    public double DpiScaleY { get; set; } = 1.0;

    /// <summary>
    /// 获取或设置输入元素，用于鼠标事件处理。
    /// </summary>
    public IInputElement? InputElement { get; set; }

    /// <summary>
    /// 获取或设置绘图区域的X偏移量。
    /// </summary>
    public double PlotAreaOffsetX { get; set; }

    /// <summary>
    /// 获取或设置绘图区域的Y偏移量。
    /// </summary>
    public double PlotAreaOffsetY { get; set; }

    /// <summary>
    /// 获取或设置绘图区域的宽度。
    /// </summary>
    public int PlotAreaWidth { get; set; }

    /// <summary>
    /// 获取或设置绘图区域的高度。
    /// </summary>
    public int PlotAreaHeight { get; set; }

    /// <summary>InputElement 是否就是绘图区（DrawingSurface）。</summary>
    public bool IsInputElementPlotArea { get; set; }

    /// <summary>
    /// 获取或设置X轴坐标映射器。
    /// </summary>
    public ICoordinateMapper XMapper { get; set; } = LinearCoordinateMapper.Instance;

    /// <summary>
    /// 获取或设置Y轴坐标映射器。
    /// </summary>
    public ICoordinateMapper YMapper { get; set; } = LinearCoordinateMapper.Instance;

    /// <summary>
    /// 获取或设置X范围访问器。
    /// </summary>
    public Func<DataRange> XRangeAccessor
    {
        get => _xRangeAccessor;
        set => _xRangeAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置Y范围访问器。
    /// </summary>
    public Func<DataRange> YRangeAccessor
    {
        get => _yRangeAccessor;
        set => _yRangeAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置X轴核心范围访问器（不含留白）。
    /// </summary>
    public Func<DataRange> CoreXRangeAccessor
    {
        get => _coreXRangeAccessor;
        set => _coreXRangeAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置Y轴核心范围访问器（不含留白）。
    /// </summary>
    public Func<DataRange> CoreYRangeAccessor
    {
        get => _coreYRangeAccessor;
        set => _coreYRangeAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置系列访问器。
    /// </summary>
    public Func<IList<IRenderableSeries>> SeriesAccessor
    {
        get => _seriesAccessor;
        set => _seriesAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置X轴访问器。
    /// </summary>
    public Func<IList<IAxis>> XAxesAccessor
    {
        get => _xAxesAccessor;
        set => _xAxesAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置Y轴访问器。
    /// </summary>
    public Func<IList<IAxis>> YAxesAccessor
    {
        get => _yAxesAccessor;
        set => _yAxesAccessor = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 获取或设置范围变更回调。
    /// </summary>
    public Action<DataRange, DataRange>? OnRangeChanged
    {
        get => _onRangeChanged;
        set => _onRangeChanged = value;
    }

    /// <summary>
    /// 获取或设置轴范围变更回调。
    /// </summary>
    public Action<string, DataRange>? OnAxisRangeChanged
    {
        get => _onAxisRangeChanged;
        set => _onAxisRangeChanged = value;
    }

    internal Func<IRenderableSeries, DataFrame?>? FrameAccessor
    {
        get => _frameAccessor;
        set => _frameAccessor = value;
    }

    /// <summary>
    /// 设置默认的X和Y轴范围。
    /// </summary>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    public void SetRange(DataRange xRange, DataRange yRange)
    {
        _onRangeChanged?.Invoke(xRange, yRange);
    }

    /// <summary>
    /// 设置指定轴的数据范围。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <param name="range">数据范围</param>
    public void SetAxisRange(string axisId, DataRange range)
    {
        _onAxisRangeChanged?.Invoke(axisId, range);
    }

    /// <summary>
    /// 根据系列获取关联的X轴。
    /// </summary>
    /// <param name="series">渲染系列</param>
    /// <returns>X轴</returns>
    public IAxis GetXAxisForSeries(IRenderableSeries series)
    {
        return Chart.FindAxisOrDefault(_xAxesAccessor(), series.XAxisId);
    }

    /// <summary>
    /// 根据系列获取关联的Y轴。
    /// </summary>
    /// <param name="series">渲染系列</param>
    /// <returns>Y轴</returns>
    public IAxis GetYAxisForSeries(IRenderableSeries series)
    {
        return Chart.FindAxisOrDefault(_yAxesAccessor(), series.YAxisId);
    }

    /// <summary>
    /// 获取指定Y轴的数据范围。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <returns>数据范围</returns>
    public DataRange GetYRangeForAxis(string axisId)
    {
        var axis = Chart.FindAxis(_yAxesAccessor(), axisId);
        return axis?.VisibleRange ?? YRange;
    }

    /// <summary>
    /// 获取指定Y轴的坐标映射器。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <returns>坐标映射器</returns>
    public ICoordinateMapper GetYMapperForAxis(string axisId)
    {
        var axis = Chart.FindAxis(_yAxesAccessor(), axisId);
        return axis?.CoordinateMapper ?? YMapper;
    }

    internal DataFrame? GetFrame(IRenderableSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return _frameAccessor?.Invoke(series);
    }

    /// <summary>
    /// 将数据坐标转换为屏幕坐标。
    /// </summary>
    /// <param name="dataX">数据X值</param>
    /// <param name="dataY">数据Y值</param>
    /// <param name="width">视口宽度</param>
    /// <param name="height">视口高度</param>
    /// <returns>屏幕坐标</returns>
    public Point DataToScreen(double dataX, double dataY, int width, int height)
    {
        var xRange = XRange;
        var yRange = YRange;
        double pixelX = XMapper.DataToScreen(dataX, xRange, width);
        double pixelY = height - YMapper.DataToScreen(dataY, yRange, height);
        return new Point(
            pixelX / NormalizeDpiScale(DpiScaleX),
            pixelY / NormalizeDpiScale(DpiScaleY));
    }

    /// <summary>
    /// 将屏幕X坐标转换为数据值。
    /// </summary>
    /// <param name="screenX">屏幕X坐标</param>
    /// <param name="width">视口宽度</param>
    /// <returns>数据值</returns>
    public double ScreenToDataX(double screenX, int width)
    {
        if (width <= 0)
            return XRange.Min;

        double pixelX = Math.Clamp(screenX * NormalizeDpiScale(DpiScaleX), 0.0, width);
        return XMapper.ScreenToData(pixelX, XRange, width);
    }

    /// <summary>
    /// 将屏幕Y坐标转换为数据值。
    /// </summary>
    /// <param name="screenY">屏幕Y坐标</param>
    /// <param name="height">视口高度</param>
    /// <returns>数据值</returns>
    public double ScreenToDataY(double screenY, int height)
    {
        if (height <= 0)
            return YRange.Min;

        double pixelY = Math.Clamp(screenY * NormalizeDpiScale(DpiScaleY), 0.0, height);
        return YMapper.ScreenToData(height - pixelY, YRange, height);
    }

    /// <summary>
    /// 规范化DPI缩放因子，确保不为负数。
    /// </summary>
    /// <param name="dpiScale">DPI缩放因子</param>
    /// <returns>规范化后的缩放因子</returns>
    private static double NormalizeDpiScale(double dpiScale)
        => dpiScale > 0 ? dpiScale : 1.0;
}
