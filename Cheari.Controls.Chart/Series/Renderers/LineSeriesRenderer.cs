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

internal struct GpuLineInstance
{
    public float X1;
    public float Y1;
    public float X2;
    public float Y2;
    public float R;
    public float G;
    public float B;
    public float A;
    public float Thickness;
    public float Padding0;
    public float Padding1;
    public float Padding2;
}

internal sealed class LineSeriesRenderer
{
    private readonly List<double> _sampledX = new();
    private readonly List<double> _sampledY = new();
    private readonly IRenderCommand[] _cachedResult = new IRenderCommand[1];

    private sealed class SeriesCache
    {
        public LineRenderOperation? Operation;
        public DataRange XRange;
        public DataRange YRange;
        public int Width;
        public int Height;
        public int DataVersion;
        public Color StrokeColor;
        public bool UsedDownsampledFrame;

        public DownsampledFrame? DownsampledFrame;
        public int DownsampledSourceCount;
        public int DownsampledDataVersion;

        public int VisibleStart;
        public int VisibleEnd;

        public int DirectSourceCount;
        public int DirectInstanceCount;

        public double CachedDataXMin = double.NaN;
    }

    private readonly ConditionalWeakTable<LineRenderableSeries, SeriesCache> _seriesCaches = new();

    public IDownsamplingStrategy DownsamplingStrategy { get; set; } = new MinMaxDownsamplingStrategy();

    public IReadOnlyList<IRenderCommand> Render(ChartRenderContext context, int width, int height)
    {
        var commands = new List<IRenderCommand>();
        foreach (var series in context.Series.OfType<LineRenderableSeries>())
        {
            if (!series.IsVisible)
                continue;

            commands.AddRange(Render(series, context, width, height));
        }

        return commands;
    }

    public IReadOnlyList<IRenderCommand> Render(LineRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count < 2)
            return Array.Empty<IRenderCommand>();

        int dataVersion = frame.Version;
        int targetBucketCount = Math.Min(frame.Count, Math.Max(1, width));

        var cache = _seriesCaches.GetOrCreateValue(series);

        float r = series.Stroke.ScR;
        float g = series.Stroke.ScG;
        float b = series.Stroke.ScB;
        float a = series.Stroke.ScA;
        float strokeThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        if (cache.UsedDownsampledFrame && cache.DownsampledFrame != null)
        {
            if (frame.Count > targetBucketCount * 2)
            {
                int fastVisibleStart = GlobalMinMaxDownsampler.FindVisibleStart(
                    cache.DownsampledFrame, context.XRange.Min);
                int fastVisibleEnd = GlobalMinMaxDownsampler.FindVisibleEnd(
                    cache.DownsampledFrame, context.XRange.Max);

                if (cache.Operation != null
                    && cache.VisibleStart == fastVisibleStart
                    && cache.VisibleEnd == fastVisibleEnd
                    && cache.Width == width
                    && cache.Height == height
                    && cache.StrokeColor == series.Stroke
                    && Math.Abs(frame.XRange.Min - cache.CachedDataXMin) <= 1e-10)
                {
                    _cachedResult[0] = cache.Operation;
                    return _cachedResult;
                }
            }
        }
        else
        {
            if (cache.Operation != null
                && cache.DirectSourceCount > 0
                && cache.Width == width
                && cache.Height == height
                && cache.StrokeColor == series.Stroke
                && Math.Abs(frame.XRange.Min - cache.CachedDataXMin) <= 1e-10)
            {
                if (cache.DirectSourceCount == frame.Count)
                {
                    _cachedResult[0] = cache.Operation;
                    return _cachedResult;
                }
                else if (cache.DirectSourceCount < frame.Count
                    && frame.XValues != null && frame.YValues != null)
                {
                    AppendInstancesInPlace(frame, cache, r, g, b, a, strokeThickness);
                    _cachedResult[0] = cache.Operation;
                    return _cachedResult;
                }
            }
        }

        var op = new LineRenderOperation
        {
            StrokeColor = series.Stroke,
            StrokeThickness = strokeThickness
        };

        bool usedDownsampledFrame = false;
        int visStart = 0, visEnd = 0;

        if (frame.Count > targetBucketCount * 2
            && frame.XValues != null
            && frame.YValues != null)
        {
            usedDownsampledFrame = TryBuildFromDownsampledFrame(
                frame, context, width, r, g, b, a, strokeThickness, op, cache,
                out visStart, out visEnd);
        }

        if (!usedDownsampledFrame)
        {
            if (TryBuildDirectFromFrame(frame, targetBucketCount, r, g, b, a, strokeThickness, op))
            {
                cache.DirectSourceCount = frame.Count;
                cache.DirectInstanceCount = op.LineInstances.Count;
                cache.CachedDataXMin = frame.XRange.Min;
            }
            else
            {
                DownsamplingStrategy.Downsample(
                    new SnapshotDataSeriesAdapter(frame),
                    targetBucketCount,
                    context.XRange,
                    _sampledX,
                    _sampledY);

                if (_sampledX.Count < 2)
                    return Array.Empty<IRenderCommand>();

                op.LineInstances.Capacity = Math.Max(op.LineInstances.Capacity, _sampledX.Count - 1);

                for (int i = 0; i < _sampledX.Count - 1; i++)
                {
                    double x1 = _sampledX[i];
                    double y1 = _sampledY[i];
                    double x2 = _sampledX[i + 1];
                    double y2 = _sampledY[i + 1];

                    if (IsInvalidSegment(x1, y1, x2, y2))
                        continue;

                    op.LineInstances.Add(new GpuLineInstance
                    {
                        X1 = (float)x1,
                        Y1 = (float)y1,
                        X2 = (float)x2,
                        Y2 = (float)y2,
                        R = r,
                        G = g,
                        B = b,
                        A = a,
                        Thickness = strokeThickness
                    });
                }

                cache.DirectSourceCount = 0;
                cache.CachedDataXMin = frame.XRange.Min;
            }
        }

        if (op.LineInstances.Count == 0)
            return Array.Empty<IRenderCommand>();

        cache.Operation = op;
        cache.XRange = context.XRange;
        cache.YRange = context.YRange;
        cache.Width = width;
        cache.Height = height;
        cache.DataVersion = dataVersion;
        cache.StrokeColor = series.Stroke;
        cache.UsedDownsampledFrame = usedDownsampledFrame;
        cache.VisibleStart = visStart;
        cache.VisibleEnd = visEnd;

        if (usedDownsampledFrame)
        {
            cache.DirectSourceCount = 0;
            cache.DirectInstanceCount = 0;
        }

        _cachedResult[0] = op;
        return _cachedResult;
    }

    private static bool TryBuildFromDownsampledFrame(
        DataFrame frame,
        ChartRenderContext context,
        int width,
        float r,
        float g,
        float b,
        float a,
        float strokeThickness,
        LineRenderOperation op,
        SeriesCache cache,
        out int outVisibleStart,
        out int outVisibleEnd)
    {
        int oversampleFactor = GlobalMinMaxDownsampler.DefaultOversampleFactor;
        int totalBuckets = width * oversampleFactor;
        if (totalBuckets <= 0)
        {
            outVisibleStart = 0;
            outVisibleEnd = 0;
            return false;
        }

        ReadOnlySpan<float> xValues = new ReadOnlySpan<float>(frame.XValues, 0, frame.Count);
        ReadOnlySpan<float> yValues = new ReadOnlySpan<float>(frame.YValues, 0, frame.Count);

        bool needFullRebuild = cache.DownsampledFrame == null
            || frame.Count < cache.DownsampledSourceCount
            || Math.Abs(frame.XRange.Min - cache.CachedDataXMin) > 1e-10;

        if (needFullRebuild)
        {
            double bucketSize = (double)frame.Count / totalBuckets;
            cache.DownsampledFrame = Downsample(
                xValues, yValues, frame.Count, width, oversampleFactor);
            cache.DownsampledSourceCount = frame.Count;
            cache.DownsampledDataVersion = frame.Version;
            cache.CachedDataXMin = frame.XRange.Min;
        }
        else if (frame.Count > cache.DownsampledSourceCount && cache.DownsampledFrame != null)
        {
            double fixedBucketSize = (double)cache.DownsampledFrame.PointsPerSourceBucket;
            if (fixedBucketSize > 0)
            {
                var newPart = GlobalMinMaxDownsampler.DownsampleRange(
                    xValues, yValues,
                    cache.DownsampledSourceCount, frame.Count,
                    fixedBucketSize);

                if (newPart != null)
                    cache.DownsampledFrame.AppendFrom(newPart.XValues, newPart.YValues, newPart.Count);

                cache.DownsampledSourceCount = frame.Count;
                cache.DownsampledDataVersion = frame.Version;
                cache.CachedDataXMin = frame.XRange.Min;
            }
        }

        var dsFrame = cache.DownsampledFrame;
        if (dsFrame == null)
        {
            outVisibleStart = 0;
            outVisibleEnd = 0;
            return false;
        }

        int visibleStart = GlobalMinMaxDownsampler.FindVisibleStart(dsFrame, context.XRange.Min);
        int visibleEnd = GlobalMinMaxDownsampler.FindVisibleEnd(dsFrame, context.XRange.Max);

        outVisibleStart = visibleStart;
        outVisibleEnd = visibleEnd;

        if (visibleStart >= visibleEnd)
            return true;

        int segmentCount = visibleEnd - visibleStart - 1;
        if (segmentCount <= 0)
            return true;

        BuildInstancesFromSlice(dsFrame, visibleStart, visibleEnd, r, g, b, a, strokeThickness, op);
        return true;
    }

    private static void BuildInstancesFromSlice(
        DownsampledFrame dsFrame,
        int visibleStart,
        int visibleEnd,
        float r,
        float g,
        float b,
        float a,
        float strokeThickness,
        LineRenderOperation op)
    {
        int segmentCount = visibleEnd - visibleStart - 1;
        op.LineInstances.Capacity = Math.Max(op.LineInstances.Capacity, segmentCount);

        var dsX = dsFrame.XValues;
        var dsY = dsFrame.YValues;

        for (int i = visibleStart; i < visibleEnd - 1; i++)
        {
            float x1 = dsX[i];
            float y1 = dsY[i];
            float x2 = dsX[i + 1];
            float y2 = dsY[i + 1];

            if (IsInvalidSegment(x1, y1, x2, y2))
                continue;

            op.LineInstances.Add(new GpuLineInstance
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                R = r,
                G = g,
                B = b,
                A = a,
                Thickness = strokeThickness
            });
        }
    }

    private static DownsampledFrame? Downsample(
        ReadOnlySpan<float> xValues,
        ReadOnlySpan<float> yValues,
        int dataCount,
        int viewportWidth,
        int oversampleFactor)
    {
        return GlobalMinMaxDownsampler.Downsample(xValues, yValues, dataCount, viewportWidth, oversampleFactor);
    }

    private static bool IsInvalidSegment(double x1, double y1, double x2, double y2)
    {
        return (x1 == x2 && y1 == y2)
            || double.IsNaN(x1) || double.IsNaN(y1)
            || double.IsNaN(x2) || double.IsNaN(y2)
            || double.IsInfinity(x1) || double.IsInfinity(y1)
            || double.IsInfinity(x2) || double.IsInfinity(y2);
    }

    private static bool IsInvalidSegment(float x1, float y1, float x2, float y2)
    {
        return (x1 == x2 && y1 == y2)
            || float.IsNaN(x1) || float.IsNaN(y1)
            || float.IsNaN(x2) || float.IsNaN(y2)
            || float.IsInfinity(x1) || float.IsInfinity(y1)
            || float.IsInfinity(x2) || float.IsInfinity(y2);
    }

    private static void AppendInstancesInPlace(
        DataFrame frame,
        SeriesCache cache,
        float r, float g, float b, float a, float strokeThickness)
    {
        if (frame.XValues == null || frame.YValues == null)
            return;

        int oldCount = cache.DirectSourceCount;
        var op = cache.Operation!;
        var instances = op.LineInstances;

        int newInstanceCount = instances.Count + (frame.Count - oldCount);
        instances.Capacity = Math.Max(instances.Capacity, newInstanceCount);

        for (int i = oldCount - 1; i < frame.Count - 1; i++)
        {
            float x1 = frame.XValues[i];
            float y1 = frame.YValues[i];
            float x2 = frame.XValues[i + 1];
            float y2 = frame.YValues[i + 1];

            if (IsInvalidSegment(x1, y1, x2, y2))
                continue;

            instances.Add(new GpuLineInstance
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                R = r, G = g, B = b, A = a,
                Thickness = strokeThickness
            });
        }

        cache.DirectSourceCount = frame.Count;
        cache.DirectInstanceCount = instances.Count;
    }

    private static bool TryBuildDirectFromFrame(
        DataFrame frame,
        int targetBucketCount,
        float r,
        float g,
        float b,
        float a,
        float strokeThickness,
        LineRenderOperation op)
    {
        if (frame.Count > targetBucketCount * 2 || frame.XValues == null || frame.YValues == null)
            return false;

        ReadOnlySpan<float> xValues = new ReadOnlySpan<float>(frame.XValues, 0, frame.Count);
        ReadOnlySpan<float> yValues = new ReadOnlySpan<float>(frame.YValues, 0, frame.Count);
        if (xValues.Length < 2 || yValues.Length < 2)
            return true;

        op.LineInstances.Capacity = Math.Max(op.LineInstances.Capacity, xValues.Length - 1);

        for (int i = 0; i < xValues.Length - 1; i++)
        {
            float x1 = xValues[i];
            float y1 = yValues[i];
            float x2 = xValues[i + 1];
            float y2 = yValues[i + 1];

            if (IsInvalidSegment(x1, y1, x2, y2))
                continue;

            op.LineInstances.Add(new GpuLineInstance
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                R = r,
                G = g,
                B = b,
                A = a,
                Thickness = strokeThickness
            });
        }

        return true;
    }
}
