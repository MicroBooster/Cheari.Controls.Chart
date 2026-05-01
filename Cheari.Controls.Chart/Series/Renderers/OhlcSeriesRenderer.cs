using System.Runtime.CompilerServices;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

/// <summary>
/// OHLC（K线）渲染器，负责将OHLC数据系列渲染为K线图。
/// </summary>
internal sealed class OhlcSeriesRenderer
{
    private OhlcRenderOperation? _cachedOperation;
    private DataRange _cachedXRange;
    private DataRange _cachedYRange;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedDataVersion;
    private int _cachedSeriesIdentity;

    public IReadOnlyList<IRenderCommand> Render(OhlcRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null
            || frame.Count == 0
            || frame.XValues == null
            || frame.OpenValues == null
            || frame.HighValues == null
            || frame.LowValues == null
            || frame.CloseValues == null)
        {
            return Array.Empty<IRenderCommand>();
        }

        int dataVersion = frame.Version;
        int seriesIdentity = RuntimeHelpers.GetHashCode(series);

        if (_cachedOperation != null
            && _cachedXRange.Min == context.XRange.Min && _cachedXRange.Max == context.XRange.Max
            && _cachedYRange.Min == context.YRange.Min && _cachedYRange.Max == context.YRange.Max
            && _cachedWidth == width
            && _cachedHeight == height
            && _cachedDataVersion == dataVersion
            && _cachedSeriesIdentity == seriesIdentity)
        {
            var result = new List<IRenderCommand>();
            if (_cachedOperation.Bodies.Instances.Count > 0)
                result.Add(_cachedOperation.Bodies);
            if (_cachedOperation.Wicks.LineInstances.Count > 0)
                result.Add(_cachedOperation.Wicks);
            return result;
        }

        var op = new OhlcRenderOperation();
        double xRangeLength = Math.Abs(context.XRange.Length) > double.Epsilon ? context.XRange.Length : 1.0;

        int visibleStart = -1;
        int visibleEnd = -1;

        for (int i = 0; i < frame.Count; i++)
        {
            double x = frame.XValues[i];
            if (x >= context.XRange.Min && x <= context.XRange.Max)
            {
                if (visibleStart < 0)
                    visibleStart = i;
                visibleEnd = i;
            }
        }

        if (visibleStart < 0)
            return Array.Empty<IRenderCommand>();

        int visibleCount = visibleEnd - visibleStart + 1;
        double dataSpacing = visibleCount > 1
            ? (frame.XValues[visibleEnd] - frame.XValues[visibleStart]) / (visibleCount - 1)
            : xRangeLength / 10.0;

        double barWidthData = series.BarWidth > 0
            ? series.BarWidth
            : dataSpacing * 0.6;

        if (barWidthData <= 0)
            barWidthData = dataSpacing * 0.6;

        float wickThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        for (int i = visibleStart; i <= visibleEnd; i++)
        {
            double x = frame.XValues[i];
            double open = frame.OpenValues[i];
            double high = frame.HighValues[i];
            double low = frame.LowValues[i];
            double close = frame.CloseValues[i];

            if (double.IsNaN(x) || double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
                continue;

            bool isUp = close >= open;
            var bodyColor = isUp ? series.UpFill : series.DownFill;
            var wickColor = isUp ? series.UpStroke : series.DownStroke;

            float br = bodyColor.ScR;
            float bg = bodyColor.ScG;
            float bb = bodyColor.ScB;
            float ba = bodyColor.ScA;

            float wr = wickColor.ScR;
            float wg = wickColor.ScG;
            float wb = wickColor.ScB;
            float wa = wickColor.ScA;

            double bodyTop = Math.Max(open, close);
            double bodyBottom = Math.Min(open, close);
            if (bodyTop == bodyBottom)
                bodyTop = bodyBottom + dataSpacing * 0.01;

            op.Bodies.Instances.Add(new GpuBarInstance
            {
                X1 = (float)(x - barWidthData * 0.5),
                Y1 = (float)bodyBottom,
                X2 = (float)(x + barWidthData * 0.5),
                Y2 = (float)bodyTop,
                R = br,
                G = bg,
                B = bb,
                A = ba
            });

            op.Wicks.LineInstances.Add(new GpuLineInstance
            {
                X1 = (float)x,
                Y1 = (float)low,
                X2 = (float)x,
                Y2 = (float)high,
                R = wr,
                G = wg,
                B = wb,
                A = wa,
                Thickness = wickThickness
            });
        }

        if (op.Bodies.Instances.Count == 0 && op.Wicks.LineInstances.Count == 0)
            return Array.Empty<IRenderCommand>();

        _cachedOperation = op;
        _cachedXRange = context.XRange;
        _cachedYRange = context.YRange;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedDataVersion = dataVersion;
        _cachedSeriesIdentity = seriesIdentity;

        var commands = new List<IRenderCommand>();
        if (op.Bodies.Instances.Count > 0)
            commands.Add(op.Bodies);
        if (op.Wicks.LineInstances.Count > 0)
            commands.Add(op.Wicks);
        return commands;
    }
}
