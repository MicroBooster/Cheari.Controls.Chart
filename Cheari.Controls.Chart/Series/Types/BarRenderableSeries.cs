using System.Windows.Media;

namespace Cheari.Controls.Series.Types;

/// <summary>
/// 柱状图渲染系列，用于绘制柱状图。
/// </summary>
public class BarRenderableSeries : RenderableSeriesBase
{
    /// <inheritdoc />
    public override Color Stroke { get; set; } = Colors.White;

    /// <summary>
     /// 获取或设置填充颜色。
    /// </summary>
    public Color Fill { get; set; } = Colors.DodgerBlue;

    /// <summary>
    /// 获取或设置柱宽度（像素）。如果为0，则根据数据密度自动计算。
    /// </summary>
    public double BarWidth { get; set; }

    /// <summary>
    /// 获取或设置柱之间的间距比例，范围0-1。
    /// </summary>
    public double BarSpacing { get; set; } = 0.2;
}
