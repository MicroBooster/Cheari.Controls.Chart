using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Data;

namespace Cheari.Controls.Series;

/// <summary>
/// 可渲染系列的基础实现，封装所有系列共享的元数据与轴绑定属性。
/// </summary>
public abstract class RenderableSeriesBase : IRenderableSeries
{
    /// <summary>
    /// 获取或设置系列标题，用于图例显示。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置系列是否可见。
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 获取或设置线条颜色。
    /// </summary>
    public virtual Color Stroke { get; set; } = Colors.White;

    /// <summary>
    /// 获取或设置线条宽度。
    /// </summary>
    public virtual double StrokeThickness { get; set; } = 1.0;

    /// <summary>
    /// 获取或设置线条样式。
    /// </summary>
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    /// <summary>
    /// 获取或设置自定义虚线数组。如果设置了此属性，则忽略 <see cref="LineStyle"/>。
    /// </summary>
    public double[]? StrokeDashArray { get; set; }

    /// <summary>
    /// 获取或设置数据系列。此属性为渲染所必需。
    /// </summary>
    public IDataSeries DataSeries { get; set; } = null!;

    /// <summary>
    /// 获取或设置关联的 X 轴 ID。
    /// </summary>
    public string XAxisId { get; set; } = Chart.DefaultXAxisId;

    /// <summary>
    /// 获取或设置关联的 Y 轴 ID。
    /// </summary>
    public string YAxisId { get; set; } = Chart.DefaultYAxisId;
}
