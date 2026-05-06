using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

internal struct GpuBarInstance
{
    public float X1;
    public float Y1;
    public float X2;
    public float Y2;
    public float R;
    public float G;
    public float B;
    public float A;
}

internal sealed class BarSeriesRenderer
{
    private BarRenderOperation? _cachedOperation;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedSeriesIdentity;
    private Color _cachedFillColor;
    private int _cachedSourceCount;
    private int _cachedVisibleStart;
    private int _cachedVisibleEnd;
    private double _cachedDataXMin = double.NaN;
    private double _cachedXRangeMin;
    private double _cachedXRangeMax;
    private double _cachedYRangeMin;
    private double _cachedYRangeMax;

    public IReadOnlyList<IRenderCommand> Render(BarRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count == 0)
            return Array.Empty<IRenderCommand>();

        int seriesIdentity = RuntimeHelpers.GetHashCode(series);

        if (_cachedOperation != null
            && _cachedSeriesIdentity == seriesIdentity
            && _cachedWidth == width && _cachedHeight == height
            && _cachedFillColor == series.Fill
            && Math.Abs(frame.XRange.Min - _cachedDataXMin) <= 1e-10
            && Math.Abs(context.XRange.Min - _cachedXRangeMin) <= 1e-10
            && Math.Abs(context.XRange.Max - _cachedXRangeMax) <= 1e-10
            && Math.Abs(context.YRange.Min - _cachedYRangeMin) <= 1e-10
            && Math.Abs(context.YRange.Max - _cachedYRangeMax) <= 1e-10)
        {
            if (_cachedSourceCount == frame.Count)
            {
                return new IRenderCommand[] { _cachedOperation };
            }
            else if (_cachedSourceCount < frame.Count
                && frame.XValues != null && frame.YValues != null)
            {
                AppendBarsInPlace(frame, series, context);
                if (_cachedSourceCount == frame.Count)
                    return new IRenderCommand[] { _cachedOperation };
            }
        }

        float r = series.Fill.ScR;
        float g = series.Fill.ScG;
        float b = series.Fill.ScB;
        float a = series.Fill.ScA;

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

        double xAxisSpan = frame.XValues[visibleEnd] - frame.XValues[visibleStart];
        double fallbackRangeLength = Math.Abs(context.XRange.Length) > double.Epsilon ? context.XRange.Length : 1.0;
        double barWidthData = SeriesGeometryHelper.ResolveBarWidthData(
            series,
            xAxisSpan,
            visibleCount,
            fallbackRangeLength);

        double baselineY = Math.Max(context.YRange.Min, Math.Min(context.YRange.Max, 0.0));

        var op = new BarRenderOperation();

        op.Instances.Capacity = Math.Max(op.Instances.Capacity, visibleCount);

        for (int i = visibleStart; i <= visibleEnd; i++)
        {
            double x = frame.XValues[i];
            double y = frame.YValues[i];

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                continue;

            double x1 = x - barWidthData * 0.5;
            double x2 = x + barWidthData * 0.5;
            double y1 = Math.Min(y, baselineY);
            double y2 = Math.Max(y, baselineY);

            op.Instances.Add(new GpuBarInstance
            {
                X1 = (float)x1,
                Y1 = (float)y1,
                X2 = (float)x2,
                Y2 = (float)y2,
                R = r,
                G = g,
                B = b,
                A = a
            });
        }

        if (op.Instances.Count == 0)
            return Array.Empty<IRenderCommand>();

        _cachedOperation = op;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedFillColor = series.Fill;
        _cachedSourceCount = frame.Count;
        _cachedVisibleStart = visibleStart;
        _cachedVisibleEnd = visibleEnd;
        _cachedDataXMin = frame.XRange.Min;
        _cachedXRangeMin = context.XRange.Min;
        _cachedXRangeMax = context.XRange.Max;
        _cachedYRangeMin = context.YRange.Min;
        _cachedYRangeMax = context.YRange.Max;

        return new IRenderCommand[] { op };
    }

    private void AppendBarsInPlace(
        DataFrame frame, BarRenderableSeries series, ChartRenderContext context)
    {
        var instances = _cachedOperation!.Instances;

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

        float r = series.Fill.ScR;
        float g = series.Fill.ScG;
        float b = series.Fill.ScB;
        float a = series.Fill.ScA;

        double xAxisSpan = frame.XValues![visibleEnd] - frame.XValues![visibleStart];
        double fallbackRangeLength = Math.Abs(context.XRange.Length) > double.Epsilon ? context.XRange.Length : 1.0;
        double barWidthData = SeriesGeometryHelper.ResolveBarWidthData(
            series,
            xAxisSpan,
            newVisibleCount,
            fallbackRangeLength);

        double baselineY = Math.Max(context.YRange.Min, Math.Min(context.YRange.Max, 0.0));

        int newInstanceCount = instances.Count + (visibleEnd - _cachedVisibleEnd);
        instances.Capacity = Math.Max(instances.Capacity, newInstanceCount);

        for (int i = _cachedVisibleEnd + 1; i <= visibleEnd; i++)
        {
            double x = frame.XValues![i];
            double y = frame.YValues![i];

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                continue;

            double x1 = x - barWidthData * 0.5;
            double x2 = x + barWidthData * 0.5;
            double y1 = Math.Min(y, baselineY);
            double y2 = Math.Max(y, baselineY);

            instances.Add(new GpuBarInstance
            {
                X1 = (float)x1, Y1 = (float)y1, X2 = (float)x2, Y2 = (float)y2,
                R = r, G = g, B = b, A = a
            });
        }

        _cachedSourceCount = frame.Count;
        _cachedVisibleEnd = visibleEnd;
        _cachedDataXMin = frame.XRange.Min;
        _cachedXRangeMin = context.XRange.Min;
        _cachedXRangeMax = context.XRange.Max;
        _cachedYRangeMin = context.YRange.Min;
        _cachedYRangeMax = context.YRange.Max;
    }
}
