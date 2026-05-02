using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Rendering.Downsampling;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Series.Renderers;

/// <summary>
/// GPU面积图顶点结构，用于存储面积填充所需的顶点数据。
/// </summary>
internal struct GpuAreaVertex
{
    public float X;
    public float Y;
    public float R;
    public float G;
    public float B;
    public float A;
}

/// <summary>
/// 面积图渲染器，负责将数据系列渲染为填充面积图。
/// </summary>
internal sealed class AreaSeriesRenderer
{
    private readonly List<double> _sampledX = new();
    private readonly List<double> _sampledY = new();

    private AreaRenderOperation? _cachedOperation;
    private DataRange _cachedXRange;
    private DataRange _cachedYRange;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedDataVersion;
    private int _cachedSeriesIdentity;
    private Color _cachedStrokeColor;
    private Color _cachedFillColor;
    private double _cachedFillOpacity;
    private double _cachedBaselineY;

    public IDownsamplingStrategy DownsamplingStrategy { get; set; } = new MinMaxDownsamplingStrategy();

    public IReadOnlyList<IRenderCommand> Render(AreaRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(context);

        var frame = RenderSeriesData.GetFrame(context, series);
        if (frame == null || frame.Count < 2)
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
            && _cachedStrokeColor == series.Stroke
            && _cachedFillColor == series.Fill
            && Math.Abs(_cachedFillOpacity - series.FillOpacity) < 0.0001
            && Math.Abs(_cachedBaselineY - series.BaselineY) < 0.0001)
        {
            var cached = new List<IRenderCommand>();
            if (_cachedOperation.FillVertices.Count > 0)
                cached.Add(_cachedOperation);
            if (_cachedOperation.BorderLine != null)
                cached.Add(_cachedOperation.BorderLine);
            return cached;
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

        if (_sampledX.Count < 2)
            return Array.Empty<IRenderCommand>();

        var op = new AreaRenderOperation();

        float fillR = series.Fill.ScR;
        float fillG = series.Fill.ScG;
        float fillB = series.Fill.ScB;
        float fillA = (float)series.FillOpacity;
        float baselineY = (float)series.BaselineY;

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

        float strokeR = series.Stroke.ScR;
        float strokeG = series.Stroke.ScG;
        float strokeB = series.Stroke.ScB;
        float strokeA = series.Stroke.ScA;
        float strokeThickness = Math.Max(1.0f, (float)series.StrokeThickness);

        var borderLine = new LineRenderOperation
        {
            StrokeColor = series.Stroke,
            StrokeThickness = strokeThickness
        };

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
        _cachedXRange = context.XRange;
        _cachedYRange = context.YRange;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedDataVersion = dataVersion;
        _cachedSeriesIdentity = seriesIdentity;
        _cachedStrokeColor = series.Stroke;
        _cachedFillColor = series.Fill;
        _cachedFillOpacity = series.FillOpacity;
        _cachedBaselineY = series.BaselineY;

        var commands = new List<IRenderCommand>();
        if (op.FillVertices.Count > 0)
            commands.Add(op);
        if (op.BorderLine != null)
            commands.Add(op.BorderLine);
        return commands;
    }
}
