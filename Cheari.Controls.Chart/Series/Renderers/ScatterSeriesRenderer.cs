using System.Runtime.CompilerServices;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Rendering.Downsampling;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

/// <summary>
/// 散点图渲染器，负责将数据系列渲染为散点图。
/// </summary>
internal sealed class ScatterSeriesRenderer
{
    private readonly List<double> _sampledX = new();
    private readonly List<double> _sampledY = new();

    private ScatterRenderOperation? _cachedOperation;
    private DataRange _cachedXRange;
    private DataRange _cachedYRange;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedDataVersion;
    private int _cachedSeriesIdentity;

    public IDownsamplingStrategy DownsamplingStrategy { get; set; } = new MinMaxDownsamplingStrategy();

    public IReadOnlyList<IRenderCommand> Render(ScatterRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count == 0)
            return Array.Empty<IRenderCommand>();

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
            return new IRenderCommand[] { _cachedOperation };
        }

        _sampledX.Clear();
        _sampledY.Clear();

        int targetBucketCount = Math.Min(frame.Count, Math.Max(1, width));
        DownsamplingStrategy.Downsample(
            new SnapshotDataSeriesAdapter(frame),
            targetBucketCount,
            context.XRange,
            _sampledX,
            _sampledY);

        if (_sampledX.Count == 0)
            return Array.Empty<IRenderCommand>();

        var op = new ScatterRenderOperation
        {
            MarkerType = series.MarkerType,
            MarkerSize = (float)series.MarkerSize,
            MarkerColor = series.MarkerColor
        };

        float r = series.MarkerColor.ScR;
        float g = series.MarkerColor.ScG;
        float b = series.MarkerColor.ScB;
        float a = series.MarkerColor.ScA;
        float size = Math.Max(1.0f, (float)series.MarkerSize);
        float markerType = (float)series.MarkerType;

        for (int i = 0; i < _sampledX.Count; i++)
        {
            double x = _sampledX[i];
            double y = _sampledY[i];

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                continue;

            op.Instances.Add(new GpuMarkerInstance
            {
                X = (float)x,
                Y = (float)y,
                Size = size,
                R = r,
                G = g,
                B = b,
                A = a,
                MarkerType = markerType
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

        return new IRenderCommand[] { op };
    }
}
