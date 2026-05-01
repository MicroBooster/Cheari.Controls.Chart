using System.Windows.Media;
using Cheari.Controls.Core;

namespace Cheari.Controls.Series.Types;

/// <summary>
/// 线图渲染系列，用于绘制折线图。
/// </summary>
public class LineRenderableSeries : RenderableSeriesBase
{
    /// <inheritdoc />
    public override Color Stroke { get; set; } = Colors.Blue;

    private static readonly double[] s_dashPattern = [4, 2];
    private static readonly double[] s_dotPattern = [1, 2];
    private static readonly double[] s_dashDotPattern = [4, 2, 1, 2];
    private static readonly double[] s_dashDotDotPattern = [4, 2, 1, 2, 1, 2];

    /// <summary>
    /// 根据线条样式获取对应的虚线数组。
    /// </summary>
    /// <param name="style">线条样式</param>
    /// <returns>虚线数组，如果是实线则返回null</returns>
    public static double[]? GetDashArrayForStyle(LineStyle style) => style switch
    {
        LineStyle.Solid => null,
        LineStyle.Dash => s_dashPattern,
        LineStyle.Dot => s_dotPattern,
        LineStyle.DashDot => s_dashDotPattern,
        LineStyle.DashDotDot => s_dashDotDotPattern,
        _ => null
    };
}
