using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Rendering.Strategy;

internal enum RenderStrategy
{
    Vector,
    Raster,
    Hybrid
}

internal enum SeriesType
{
    Line,
    Area,
    Bar,
    Scatter,
    Ohlc
}

/// <summary>
/// 渲染策略选择器，根据数据量和系列类型决定使用矢量渲染、光栅化渲染或混合渲染。
/// </summary>
internal sealed class RenderStrategySelector
{
    /// <summary>
    /// 获取或设置光栅化阈值倍数。
    /// 当数据点数超过 viewportWidth × RasterThresholdMultiplier 时切换到光栅化渲染。
    /// 默认值为 2.0。
    /// </summary>
    public double RasterThresholdMultiplier { get; set; } = 2.0;

    public RenderStrategy SelectStrategy(int pointCount, int viewportWidth, SeriesType seriesType)
    {
        if (pointCount > viewportWidth * RasterThresholdMultiplier)
        {
            return seriesType == SeriesType.Area ? RenderStrategy.Hybrid : RenderStrategy.Raster;
        }

        return RenderStrategy.Vector;
    }

    public static SeriesType GetSeriesType(IRenderableSeries series)
    {
        return series switch
        {
            OhlcRenderableSeries => SeriesType.Ohlc,
            BarRenderableSeries => SeriesType.Bar,
            AreaRenderableSeries => SeriesType.Area,
            ScatterRenderableSeries => SeriesType.Scatter,
            _ => SeriesType.Line
        };
    }
}
