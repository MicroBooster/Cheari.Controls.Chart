using System.Runtime.CompilerServices;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Rendering.Downsampling;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

internal sealed class ScatterSeriesRenderer
{
    private readonly List<double> _sampledX = new();
    private readonly List<double> _sampledY = new();

    private ScatterRenderOperation? _cachedOperation;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedSeriesIdentity;
    private int _cachedSourceCount;

    private DownsampledFrame? _cachedDsFrame;
    private int _cachedDsSourceCount;
    private double _cachedDataXMin = double.NaN;

    public IDownsamplingStrategy DownsamplingStrategy { get; set; } = new MinMaxDownsamplingStrategy();

    public IReadOnlyList<IRenderCommand> Render(ScatterRenderableSeries series, ChartRenderContext context, int width, int height)
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
            && Math.Abs(frame.XRange.Min - _cachedDataXMin) <= 1e-10)
        {
            if (_cachedSourceCount == frame.Count)
            {
                return new IRenderCommand[] { _cachedOperation };
            }
            else if (_cachedSourceCount < frame.Count
                && frame.XValues != null && frame.YValues != null)
            {
                AppendScatterInPlace(frame, series, context, width);
                if (_cachedSourceCount == frame.Count)
                    return new IRenderCommand[] { _cachedOperation };
            }
        }

        EnsureSampledData(frame, width);

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

        op.Instances.Capacity = Math.Max(op.Instances.Capacity, _sampledX.Count);

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
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedSourceCount = frame.Count;
        _cachedDataXMin = frame.XRange.Min;

        return new IRenderCommand[] { op };
    }

    private void EnsureSampledData(DataFrame frame, int width)
    {
        int oversampleFactor = GlobalMinMaxDownsampler.DefaultOversampleFactor;
        ReadOnlySpan<float> xValues = new ReadOnlySpan<float>(frame.XValues, 0, frame.Count);
        ReadOnlySpan<float> yValues = new ReadOnlySpan<float>(frame.YValues, 0, frame.Count);

        bool needFullRebuild = _cachedDsFrame == null
            || frame.Count < _cachedDsSourceCount
            || Math.Abs(frame.XRange.Min - _cachedDataXMin) > 1e-10;

        if (needFullRebuild)
        {
            var built = GlobalMinMaxDownsampler.Downsample(
                xValues, yValues, frame.Count, width, oversampleFactor);

            if (built != null)
            {
                _cachedDsFrame = built;
                _cachedDsSourceCount = frame.Count;
                _cachedDataXMin = frame.XRange.Min;
            }
        }
        else if (frame.Count > _cachedDsSourceCount && _cachedDsFrame != null)
        {
            double fixedBucketSize = (double)_cachedDsFrame.PointsPerSourceBucket;
            if (fixedBucketSize > 0)
            {
                var newPart = GlobalMinMaxDownsampler.DownsampleRange(
                    xValues, yValues,
                    _cachedDsSourceCount, frame.Count,
                    fixedBucketSize);

                if (newPart != null)
                    _cachedDsFrame.AppendFrom(newPart.XValues, newPart.YValues, newPart.Count);

                _cachedDsSourceCount = frame.Count;
                _cachedDataXMin = frame.XRange.Min;
            }
        }

        _sampledX.Clear();
        _sampledY.Clear();

        if (_cachedDsFrame != null)
        {
            for (int i = 0; i < _cachedDsFrame.Count; i++)
            {
                _sampledX.Add(_cachedDsFrame.XValues[i]);
                _sampledY.Add(_cachedDsFrame.YValues[i]);
            }
        }
        else
        {
            int targetBucketCount = Math.Min(frame.Count, Math.Max(1, width));
            DownsamplingStrategy.Downsample(
                new SnapshotDataSeriesAdapter(frame),
                targetBucketCount,
                new DataRange(double.MinValue, double.MaxValue),
                _sampledX,
                _sampledY);
        }
    }

    private void AppendScatterInPlace(
        DataFrame frame, ScatterRenderableSeries series, ChartRenderContext context, int width)
    {
        int oldSampledCount = _sampledX.Count;

        EnsureSampledData(frame, width);
        if (_sampledX.Count <= oldSampledCount)
            return;

        var instances = _cachedOperation!.Instances;
        float r = series.MarkerColor.ScR;
        float g = series.MarkerColor.ScG;
        float b = series.MarkerColor.ScB;
        float a = series.MarkerColor.ScA;
        float size = Math.Max(1.0f, (float)series.MarkerSize);
        float markerType = (float)series.MarkerType;

        int newPointCount = _sampledX.Count - oldSampledCount;
        instances.Capacity = Math.Max(instances.Capacity, instances.Count + newPointCount);

        for (int i = oldSampledCount; i < _sampledX.Count; i++)
        {
            double x = _sampledX[i];
            double y = _sampledY[i];
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                continue;

            instances.Add(new GpuMarkerInstance
            {
                X = (float)x, Y = (float)y, Size = size,
                R = r, G = g, B = b, A = a, MarkerType = markerType
            });
        }

        _cachedSourceCount = frame.Count;
        _cachedDataXMin = frame.XRange.Min;
    }
}
