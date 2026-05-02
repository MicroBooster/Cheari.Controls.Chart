using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

/// <summary>
/// GPU柱状图实例结构，用于存储柱状图渲染所需的数据。
/// </summary>
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

/// <summary>
/// 柱状图渲染器，负责将数据系列渲染为柱状图。
/// </summary>
internal sealed class BarSeriesRenderer
{
    private BarRenderOperation? _cachedOperation;
    private DataRange _cachedXRange;
    private DataRange _cachedYRange;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedDataVersion;
    private int _cachedSeriesIdentity;
    private Color _cachedFillColor;

    public IReadOnlyList<IRenderCommand> Render(BarRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count == 0 || frame.XValues == null || frame.YValues == null)
            return Array.Empty<IRenderCommand>();

        int dataVersion = frame.Version;
        int seriesIdentity = RuntimeHelpers.GetHashCode(series);

        if (_cachedOperation != null
            && _cachedXRange.Min == context.XRange.Min && _cachedXRange.Max == context.XRange.Max
            && _cachedYRange.Min == context.YRange.Min && _cachedYRange.Max == context.YRange.Max
            && _cachedWidth == width
            && _cachedHeight == height
            && _cachedDataVersion == dataVersion
            && _cachedSeriesIdentity == seriesIdentity
            && _cachedFillColor == series.Fill)
        {
            return new IRenderCommand[] { _cachedOperation };
        }

        var op = new BarRenderOperation();

        float r = series.Fill.ScR;
        float g = series.Fill.ScG;
        float b = series.Fill.ScB;
        float a = series.Fill.ScA;

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
            : dataSpacing * (1.0 - series.BarSpacing);

        if (barWidthData <= 0)
            barWidthData = dataSpacing * 0.8;

        double baselineY = Math.Max(context.YRange.Min, Math.Min(context.YRange.Max, 0.0));

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
        _cachedXRange = context.XRange;
        _cachedYRange = context.YRange;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedDataVersion = dataVersion;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedFillColor = series.Fill;

        return new IRenderCommand[] { op };
    }
}
