using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Rendering.Downsampling;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

internal struct GpuAreaVertex
{
    public float X;
    public float Y;
    public float R;
    public float G;
    public float B;
    public float A;
}

internal sealed class AreaSeriesRenderer
{
    private readonly List<double> _sampledX = new();
    private readonly List<double> _sampledY = new();

    private AreaRenderOperation? _cachedOperation;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedSeriesIdentity;
    private Color _cachedStrokeColor;
    private Color _cachedFillColor;
    private double _cachedFillOpacity;
    private double _cachedBaselineY;
    private int _cachedSourceCount;

    private DownsampledFrame? _cachedDsFrame;
    private int _cachedDsSourceCount;
    private double _cachedDataXMin = double.NaN;

    public IDownsamplingStrategy DownsamplingStrategy { get; set; } = new MinMaxDownsamplingStrategy();

    public IReadOnlyList<IRenderCommand> Render(AreaRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count < 2)
            return Array.Empty<IRenderCommand>();

        int seriesIdentity = RuntimeHelpers.GetHashCode(series);

        if (_cachedOperation != null
            && _cachedSeriesIdentity == seriesIdentity
            && _cachedWidth == width && _cachedHeight == height
            && _cachedStrokeColor == series.Stroke
            && _cachedFillColor == series.Fill
            && Math.Abs(_cachedFillOpacity - series.FillOpacity) < 0.0001
            && Math.Abs(_cachedBaselineY - series.BaselineY) < 0.0001
            && Math.Abs(frame.XRange.Min - _cachedDataXMin) <= 1e-10)
        {
            if (_cachedSourceCount == frame.Count)
            {
                var cached = new List<IRenderCommand>();
                if (_cachedOperation.FillVertices.Count > 0)
                    cached.Add(_cachedOperation);
                if (_cachedOperation.BorderLine != null)
                    cached.Add(_cachedOperation.BorderLine);
                return cached;
            }
            else if (_cachedSourceCount < frame.Count
                && frame.XValues != null && frame.YValues != null)
            {
                AppendAreaInPlace(frame, series, context, width);
                if (_cachedSourceCount == frame.Count)
                {
                    var cached = new List<IRenderCommand>();
                    if (_cachedOperation.FillVertices.Count > 0)
                        cached.Add(_cachedOperation);
                    if (_cachedOperation.BorderLine != null)
                        cached.Add(_cachedOperation.BorderLine);
                    return cached;
                }
            }
        }

        EnsureSampledData(frame, width);

        if (_sampledX.Count < 2)
            return Array.Empty<IRenderCommand>();

        float fillR = series.Fill.ScR;
        float fillG = series.Fill.ScG;
        float fillB = series.Fill.ScB;
        float fillA = (float)series.FillOpacity;
        float baselineY = (float)series.BaselineY;

        float strokeR = series.Stroke.ScR;
        float strokeG = series.Stroke.ScG;
        float strokeB = series.Stroke.ScB;
        float strokeA = series.Stroke.ScA;
        float strokeThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        var op = new AreaRenderOperation();

        op.FillVertices.Capacity = Math.Max(op.FillVertices.Capacity, (_sampledX.Count - 1) * 6);

        for (int i = 0; i < _sampledX.Count - 1; i++)
        {
            double x1 = _sampledX[i];
            double y1 = _sampledY[i];
            double x2 = _sampledX[i + 1];
            double y2 = _sampledY[i + 1];

            if (double.IsNaN(x1) || double.IsNaN(y1) || double.IsNaN(x2) || double.IsNaN(y2)
                || double.IsInfinity(x1) || double.IsInfinity(y1) || double.IsInfinity(x2) || double.IsInfinity(y2))
            {
                continue;
            }

            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = (float)y1, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });

            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = (float)y1, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = (float)y2, R = fillR, G = fillG, B = fillB, A = fillA });
        }

        var borderLine = new LineRenderOperation
        {
            StrokeColor = series.Stroke,
            StrokeThickness = strokeThickness
        };

        borderLine.LineInstances.Capacity = Math.Max(borderLine.LineInstances.Capacity, _sampledX.Count - 1);

        for (int i = 0; i < _sampledX.Count - 1; i++)
        {
            double x1 = _sampledX[i];
            double y1 = _sampledY[i];
            double x2 = _sampledX[i + 1];
            double y2 = _sampledY[i + 1];

            if ((x1 == x2 && y1 == y2)
                || double.IsNaN(x1) || double.IsNaN(y1)
                || double.IsNaN(x2) || double.IsNaN(y2)
                || double.IsInfinity(x1) || double.IsInfinity(y1)
                || double.IsInfinity(x2) || double.IsInfinity(y2))
            {
                continue;
            }

            borderLine.LineInstances.Add(new GpuLineInstance
            {
                X1 = (float)x1,
                Y1 = (float)y1,
                X2 = (float)x2,
                Y2 = (float)y2,
                R = strokeR,
                G = strokeG,
                B = strokeB,
                A = strokeA,
                Thickness = strokeThickness
            });
        }

        op.BorderLine = borderLine.LineInstances.Count > 0 ? borderLine : null;
        if (op.FillVertices.Count == 0 && op.BorderLine == null)
            return Array.Empty<IRenderCommand>();

        _cachedOperation = op;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedStrokeColor = series.Stroke;
        _cachedFillColor = series.Fill;
        _cachedFillOpacity = series.FillOpacity;
        _cachedBaselineY = series.BaselineY;
        _cachedSourceCount = frame.Count;
        _cachedDataXMin = frame.XRange.Min;

        var commands = new List<IRenderCommand>();
        if (op.FillVertices.Count > 0)
            commands.Add(op);
        if (op.BorderLine != null)
            commands.Add(op.BorderLine);
        return commands;
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

    private void AppendAreaInPlace(
        DataFrame frame, AreaRenderableSeries series, ChartRenderContext context, int width)
    {
        int oldSampledCount = _sampledX.Count;

        EnsureSampledData(frame, width);
        if (_sampledX.Count <= oldSampledCount)
            return;

        float fillR = series.Fill.ScR;
        float fillG = series.Fill.ScG;
        float fillB = series.Fill.ScB;
        float fillA = (float)series.FillOpacity;
        float baselineY = (float)series.BaselineY;

        float strokeR = series.Stroke.ScR;
        float strokeG = series.Stroke.ScG;
        float strokeB = series.Stroke.ScB;
        float strokeA = series.Stroke.ScA;
        float strokeThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        var op = _cachedOperation!;
        int newSegmentCount = _sampledX.Count - oldSampledCount;
        op.FillVertices.Capacity = Math.Max(op.FillVertices.Capacity,
            op.FillVertices.Count + newSegmentCount * 6);

        for (int i = oldSampledCount - 1; i < _sampledX.Count - 1; i++)
        {
            double x1 = _sampledX[i];
            double y1 = _sampledY[i];
            double x2 = _sampledX[i + 1];
            double y2 = _sampledY[i + 1];

            if (double.IsNaN(x1) || double.IsNaN(y1) || double.IsNaN(x2) || double.IsNaN(y2)
                || double.IsInfinity(x1) || double.IsInfinity(y1) || double.IsInfinity(x2) || double.IsInfinity(y2))
            {
                continue;
            }

            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = (float)y1, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });

            op.FillVertices.Add(new GpuAreaVertex { X = (float)x1, Y = (float)y1, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = baselineY, R = fillR, G = fillG, B = fillB, A = fillA });
            op.FillVertices.Add(new GpuAreaVertex { X = (float)x2, Y = (float)y2, R = fillR, G = fillG, B = fillB, A = fillA });
        }

        if (op.BorderLine != null)
        {
            op.BorderLine.LineInstances.Capacity = Math.Max(op.BorderLine.LineInstances.Capacity,
                op.BorderLine.LineInstances.Count + newSegmentCount);

            for (int i = oldSampledCount - 1; i < _sampledX.Count - 1; i++)
            {
                double x1 = _sampledX[i];
                double y1 = _sampledY[i];
                double x2 = _sampledX[i + 1];
                double y2 = _sampledY[i + 1];

                if ((x1 == x2 && y1 == y2)
                    || double.IsNaN(x1) || double.IsNaN(y1)
                    || double.IsNaN(x2) || double.IsNaN(y2)
                    || double.IsInfinity(x1) || double.IsInfinity(y1)
                    || double.IsInfinity(x2) || double.IsInfinity(y2))
                {
                    continue;
                }

                op.BorderLine.LineInstances.Add(new GpuLineInstance
                {
                    X1 = (float)x1, Y1 = (float)y1, X2 = (float)x2, Y2 = (float)y2,
                    R = strokeR, G = strokeG, B = strokeB, A = strokeA,
                    Thickness = strokeThickness
                });
            }
        }

        _cachedSourceCount = frame.Count;
    }
}
