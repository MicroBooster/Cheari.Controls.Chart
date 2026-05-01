using System.Windows.Media;

namespace Cheari.Controls.Series.Types;

/// <summary>
/// 面积图渲染系列，用于绘制填充区域的面积图。
/// </summary>
public class AreaRenderableSeries : RenderableSeriesBase
{
    /// <inheritdoc />
    public override Color Stroke { get; set; } = Colors.Cyan;

    /// <summary>
     /// 获取或设置填充颜色。
    /// </summary>
    public Color Fill { get; set; } = Colors.Cyan;

    /// <summary>
    /// 获取或设置填充透明度，范围0-1。
    /// </summary>
    public double FillOpacity { get; set; } = 0.3;

    /// <summary>
    /// 获取或设置基线Y值，面积从该值向上填充到数据点。
    /// </summary>
    public double BaselineY { get; set; }
}
