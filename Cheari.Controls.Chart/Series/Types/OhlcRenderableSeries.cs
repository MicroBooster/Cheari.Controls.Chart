using System.Windows.Media;

namespace Cheari.Controls.Series.Types;

/// <summary>
/// OHLC（开盘-最高-最低-收盘）渲染系列，用于绘制K线图。
/// </summary>
public class OhlcRenderableSeries : RenderableSeriesBase
{
    /// <summary>
     /// 获取或设置线条颜色（已废弃，使用UpStroke和DownStroke）。
     /// </summary>
    public override Color Stroke { get; set; } = Colors.White;

    /// <summary>
     /// 获取或设置上涨K线的填充颜色。
    /// </summary>
    public Color UpFill { get; set; } = Color.FromRgb(0, 200, 83);

    /// <summary>
    /// 获取或设置下跌K线的填充颜色。
    /// </summary>
    public Color DownFill { get; set; } = Color.FromRgb(255, 61, 61);

    /// <summary>
    /// 获取或设置上涨K线的边框颜色。
    /// </summary>
    public Color UpStroke { get; set; } = Color.FromRgb(0, 200, 83);

    /// <summary>
    /// 获取或设置下跌K线的边框颜色。
    /// </summary>
    public Color DownStroke { get; set; } = Color.FromRgb(255, 61, 61);

    /// <summary>
    /// 获取或设置K线实体宽度（轴数据单位；对数轴下为变换后的轴空间宽度）。如果为0，则根据数据密度自动计算。
    /// </summary>
    public double BarWidth { get; set; }
}
