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

/// <summary>
/// GPU线条实例结构，用于存储线条渲染所需的数据。
/// 内存布局必须与 HLSL 中的实例输入结构体对齐：
/// - TEXCOORD1: float4 Segment (X1,Y1,X2,Y2)   offset=0,  size=16
/// - COLOR0:    float4 Color (R,G,B,A)           offset=16, size=16
/// - TEXCOORD2: float Thickness                  offset=32, size=4
/// - Padding:   12 bytes 对齐到 16 字节边界       offset=36, size=12
/// 总大小: 48 字节（3 × 16）
/// </summary>
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

/// <summary>
/// 线图渲染器，负责将数据系列渲染为折线图。
/// 支持多系列缓存，每个系列独立维护缓存条目。
/// </summary>
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

        var cache = _seriesCaches.GetOrCreateValue(series);
        if (cache.Operation != null
            && cache.XRange.Min == context.XRange.Min && cache.XRange.Max == context.XRange.Max
            && cache.YRange.Min == context.YRange.Min && cache.YRange.Max == context.YRange.Max
            && cache.Width == width
            && cache.Height == height
            && cache.DataVersion == dataVersion
            && cache.StrokeColor == series.Stroke)
        {
            _cachedResult[0] = cache.Operation;
            return _cachedResult;
        }

        _sampledX.Clear();
        _sampledY.Clear();

        int targetBucketCount = Math.Min(frame.Count, Math.Max(1, width));
        float r = series.Stroke.ScR;
        float g = series.Stroke.ScG;
        float b = series.Stroke.ScB;
        float a = series.Stroke.ScA;
        float strokeThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        var op = new LineRenderOperation
        {
            StrokeColor = series.Stroke,
            StrokeThickness = strokeThickness
        };

        if (TryBuildDirectFromFrame(frame, targetBucketCount, r, g, b, a, strokeThickness, op))
        {
            if (op.LineInstances.Count == 0)
                return Array.Empty<IRenderCommand>();
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

        _cachedResult[0] = op;
        return _cachedResult;
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
