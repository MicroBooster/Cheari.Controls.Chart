using System.Runtime.CompilerServices;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

internal sealed class OhlcSeriesRenderer
{
    private OhlcRenderOperation? _cachedOperation;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedSeriesIdentity;
    private int _cachedSourceCount;
    private int _cachedVisibleStart;
    private int _cachedVisibleEnd;
    private double _cachedDataXMin = double.NaN;

    public IReadOnlyList<IRenderCommand> Render(OhlcRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count == 0 || frame.XValues == null)
            return Array.Empty<IRenderCommand>();

        int seriesIdentity = RuntimeHelpers.GetHashCode(series);

        if (_cachedOperation != null
            && _cachedSeriesIdentity == seriesIdentity
            && _cachedWidth == width && _cachedHeight == height
            && Math.Abs(frame.XRange.Min - _cachedDataXMin) <= 1e-10)
        {
            if (_cachedSourceCount == frame.Count)
            {
                var result = new List<IRenderCommand>();
                if (_cachedOperation.Bodies.Instances.Count > 0)
                    result.Add(_cachedOperation.Bodies);
                if (_cachedOperation.Wicks.LineInstances.Count > 0)
                    result.Add(_cachedOperation.Wicks);
                return result;
            }
            else if (_cachedSourceCount < frame.Count
                && frame.OpenValues != null && frame.HighValues != null
                && frame.LowValues != null && frame.CloseValues != null)
            {
                AppendOhlcInPlace(frame, series, context);
                if (_cachedSourceCount == frame.Count)
                {
                    var result = new List<IRenderCommand>();
                    if (_cachedOperation.Bodies.Instances.Count > 0)
                        result.Add(_cachedOperation.Bodies);
                    if (_cachedOperation.Wicks.LineInstances.Count > 0)
                        result.Add(_cachedOperation.Wicks);
                    return result;
                }
            }
        }

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

        double xRangeLength = Math.Abs(context.XRange.Length) > double.Epsilon ? context.XRange.Length : 1.0;
        double dataSpacing = visibleCount > 1
            ? (frame.XValues[visibleEnd] - frame.XValues[visibleStart]) / (visibleCount - 1)
            : xRangeLength / 10.0;

        double barWidthData = series.BarWidth > 0
            ? series.BarWidth
            : dataSpacing * 0.6;
        if (barWidthData <= 0)
            barWidthData = dataSpacing * 0.6;

        float wickThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        var bodies = new BarRenderOperation();
        var wicks = new LineRenderOperation
        {
            StrokeThickness = wickThickness
        };

        bodies.Instances.Capacity = Math.Max(bodies.Instances.Capacity, visibleCount);
        wicks.LineInstances.Capacity = Math.Max(wicks.LineInstances.Capacity, visibleCount);

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

            bodies.Instances.Add(new GpuBarInstance
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

            wicks.LineInstances.Add(new GpuLineInstance
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

        if (bodies.Instances.Count == 0 && wicks.LineInstances.Count == 0)
            return Array.Empty<IRenderCommand>();

        var op = new OhlcRenderOperation
        {
            Bodies = bodies,
            Wicks = wicks
        };

        _cachedOperation = op;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedSourceCount = frame.Count;
        _cachedVisibleStart = visibleStart;
        _cachedVisibleEnd = visibleEnd;
        _cachedDataXMin = frame.XRange.Min;

        var commands = new List<IRenderCommand>();
        if (bodies.Instances.Count > 0)
            commands.Add(bodies);
        if (wicks.LineInstances.Count > 0)
            commands.Add(wicks);
        return commands;
    }

    private void AppendOhlcInPlace(
        DataFrame frame, OhlcRenderableSeries series, ChartRenderContext context)
    {
        var op = _cachedOperation!;

        int visibleStart = _cachedVisibleStart;
        int visibleEnd = -1;

        for (int i = visibleStart; i < frame.Count; i++)
        {
            double x = frame.XValues![i];
            if (x >= context.XRange.Min && x <= context.XRange.Max)
                visibleEnd = i;
            else if (visibleEnd >= 0)
                break;
        }

        if (visibleEnd < 0)
            return;

        int oldVisibleCount = _cachedVisibleEnd - _cachedVisibleStart + 1;
        int newVisibleCount = visibleEnd - visibleStart + 1;

        if (series.BarWidth <= 0 && oldVisibleCount != newVisibleCount)
            return;

        double xRangeLength = Math.Abs(context.XRange.Length) > double.Epsilon ? context.XRange.Length : 1.0;
        double dataSpacing = newVisibleCount > 1
            ? (frame.XValues![visibleEnd] - frame.XValues![visibleStart]) / (newVisibleCount - 1)
            : xRangeLength / 10.0;

        double barWidthData = series.BarWidth > 0
            ? series.BarWidth
            : dataSpacing * 0.6;
        if (barWidthData <= 0)
            barWidthData = dataSpacing * 0.6;

        float wickThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        int newBarCount = visibleEnd - _cachedVisibleEnd;
        op.Bodies.Instances.Capacity = Math.Max(op.Bodies.Instances.Capacity,
            op.Bodies.Instances.Count + newBarCount);
        op.Wicks.LineInstances.Capacity = Math.Max(op.Wicks.LineInstances.Capacity,
            op.Wicks.LineInstances.Count + newBarCount);

        for (int i = _cachedVisibleEnd + 1; i <= visibleEnd; i++)
        {
            double x = frame.XValues![i];
            double open = frame.OpenValues![i];
            double high = frame.HighValues![i];
            double low = frame.LowValues![i];
            double close = frame.CloseValues![i];

            if (double.IsNaN(x) || double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
                continue;

            bool isUp = close >= open;
            var bodyColor = isUp ? series.UpFill : series.DownFill;
            var wickColor = isUp ? series.UpStroke : series.DownStroke;

            float br = bodyColor.ScR, bg = bodyColor.ScG, bb = bodyColor.ScB, ba = bodyColor.ScA;
            float wr = wickColor.ScR, wg = wickColor.ScG, wb = wickColor.ScB, wa = wickColor.ScA;

            double bodyTop = Math.Max(open, close);
            double bodyBottom = Math.Min(open, close);
            if (bodyTop == bodyBottom)
                bodyTop = bodyBottom + dataSpacing * 0.01;

            op.Bodies.Instances.Add(new GpuBarInstance
            {
                X1 = (float)(x - barWidthData * 0.5), Y1 = (float)bodyBottom,
                X2 = (float)(x + barWidthData * 0.5), Y2 = (float)bodyTop,
                R = br, G = bg, B = bb, A = ba
            });

            op.Wicks.LineInstances.Add(new GpuLineInstance
            {
                X1 = (float)x, Y1 = (float)low, X2 = (float)x, Y2 = (float)high,
                R = wr, G = wg, B = wb, A = wa, Thickness = wickThickness
            });
        }

        _cachedSourceCount = frame.Count;
        _cachedVisibleEnd = visibleEnd;
        _cachedDataXMin = frame.XRange.Min;
    }
}
