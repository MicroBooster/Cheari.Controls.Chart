using Cheari.Controls.Data;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;

namespace Cheari.Controls.Rendering;

internal static class RenderSeriesData
{
    public static DataFrame? GetFrame(ChartRenderContext context, IRenderableSeries series)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(series);

        var frame = context.GetFrame(series);
        if (frame != null)
            return frame;

        return CreateFrame(series.DataSeries);
    }

    public static DataFrame CreateFrame(IDataSeries dataSeries)
    {
        ArgumentNullException.ThrowIfNull(dataSeries);

        return dataSeries is IDataFrameProvider frameProvider
            ? frameProvider.CreateFrame()
            : throw new InvalidOperationException(
                $"Data series type '{dataSeries.GetType().FullName}' does not support chart snapshot rendering.");
    }
}
