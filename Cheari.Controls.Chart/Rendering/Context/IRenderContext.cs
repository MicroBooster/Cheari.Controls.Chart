using System.Windows;
using Cheari.Controls.Core;
using Cheari.Controls.Axes;
using Cheari.Controls.Series;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Rendering.Context;

/// <summary>
/// 视口信息接口，提供渲染视口的尺寸和 DPI 信息。
/// </summary>
public interface IRenderViewport
{
    /// <summary>
    /// 获取视口宽度（像素）。
    /// </summary>
    int ViewportWidth { get; }

    /// <summary>
    /// 获取视口高度（像素）。
    /// </summary>
    int ViewportHeight { get; }

    /// <summary>
    /// 获取X方向的DPI缩放因子。
    /// </summary>
    double DpiScaleX { get; }

    /// <summary>
    /// 获取Y方向的DPI缩放因子。
    /// </summary>
    double DpiScaleY { get; }

    /// <summary>
    /// 获取输入元素，用于鼠标事件处理。
    /// </summary>
    IInputElement? InputElement { get; }

    /// <summary>绘图区 X 偏移（逻辑像素，相对于 Chart 左上角）。</summary>
    double PlotAreaOffsetX { get; }

    /// <summary>绘图区 Y 偏移（逻辑像素，相对于 Chart 左上角）。</summary>
    double PlotAreaOffsetY { get; }

    /// <summary>绘图区宽度（物理像素）。</summary>
    int PlotAreaWidth { get; }

    /// <summary>绘图区高度（物理像素）。</summary>
    int PlotAreaHeight { get; }

    /// <summary>InputElement 是否就是绘图区（DrawingSurface），鼠标坐标已是绘图区相对坐标。</summary>
    bool IsInputElementPlotArea { get; }
}

/// <summary>
/// 轴访问接口，提供坐标轴的查询和范围获取功能。
/// </summary>
public interface IAxisProvider
{
    /// <summary>
    /// 获取所有X轴列表。
    /// </summary>
    IReadOnlyList<IAxis> XAxes { get; }

    /// <summary>
    /// 获取所有Y轴列表。
    /// </summary>
    IReadOnlyList<IAxis> YAxes { get; }

    /// <summary>
    /// 根据系列获取关联的X轴。
    /// </summary>
    /// <param name="series">渲染系列</param>
    /// <returns>X轴</returns>
    IAxis GetXAxisForSeries(IRenderableSeries series);

    /// <summary>
    /// 根据系列获取关联的Y轴。
    /// </summary>
    /// <param name="series">渲染系列</param>
    /// <returns>Y轴</returns>
    IAxis GetYAxisForSeries(IRenderableSeries series);

    /// <summary>
    /// 获取指定Y轴的数据范围。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <returns>数据范围</returns>
    DataRange GetYRangeForAxis(string axisId);

    /// <summary>
    /// 获取指定Y轴的坐标映射器。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <returns>坐标映射器</returns>
    ICoordinateMapper GetYMapperForAxis(string axisId);
}

/// <summary>
/// 坐标转换接口，提供数据坐标与屏幕坐标之间的双向转换。
/// </summary>
public interface ICoordinateConverter
{
    /// <summary>
    /// 获取X轴数据范围。
    /// </summary>
    DataRange XRange { get; }

    /// <summary>
    /// 获取Y轴数据范围。
    /// </summary>
    DataRange YRange { get; }

    /// <summary>
    /// 将屏幕X坐标转换为数据值。
    /// </summary>
    /// <param name="screenX">屏幕X坐标</param>
    /// <param name="width">视口宽度</param>
    /// <returns>数据值</returns>
    double ScreenToDataX(double screenX, int width);

    /// <summary>
    /// 将屏幕Y坐标转换为数据值。
    /// </summary>
    /// <param name="screenY">屏幕Y坐标</param>
    /// <param name="height">视口高度</param>
    /// <returns>数据值</returns>
    double ScreenToDataY(double screenY, int height);
}

/// <summary>
/// 范围控制接口，提供修改可见范围的能力。
/// </summary>
public interface IRangeController
{
    /// <summary>
    /// 设置默认的X和Y轴范围。
    /// </summary>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    void SetRange(DataRange xRange, DataRange yRange);

    /// <summary>
    /// 设置指定轴的数据范围。
    /// </summary>
    /// <param name="axisId">轴ID</param>
    /// <param name="range">数据范围</param>
    void SetAxisRange(string axisId, DataRange range);
}

/// <summary>
/// 渲染上下文接口，提供渲染所需的视图和坐标轴信息。
/// 继承自 <see cref="IRenderViewport"/>、<see cref="IAxisProvider"/>、
/// <see cref="ICoordinateConverter"/>、<see cref="IRangeController"/>，
/// 保持向后兼容。
/// </summary>
public interface IRenderContext : IRenderViewport, IAxisProvider, ICoordinateConverter, IRangeController
{
}
