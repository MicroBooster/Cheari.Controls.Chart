using System.Windows.Media;
using Cheari.Controls.Core;

namespace Cheari.Controls.Series.Types;

/// <summary>
/// 散点图渲染系列，用于绘制离散的数据点。
/// </summary>
public class ScatterRenderableSeries : RenderableSeriesBase
{
    /// <summary>
    /// 获取或设置标记点边框颜色。
    /// </summary>
    public override Color Stroke { get; set; } = Colors.Blue;

    /// <inheritdoc />
    public override double StrokeThickness { get; set; } = 0;

    /// <summary>
     /// 获取或设置标记点类型。
    /// </summary>
    public MarkerType MarkerType { get; set; } = MarkerType.Circle;

    /// <summary>
    /// 获取或设置标记点大小（像素）。
    /// </summary>
    public double MarkerSize { get; set; } = 6.0;

    /// <summary>
    /// 获取或设置标记点颜色。
    /// </summary>
    public Color MarkerColor { get; set; } = Colors.Cyan;
}
