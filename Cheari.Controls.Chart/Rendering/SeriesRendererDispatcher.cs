using System.Buffers;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 系列渲染调度器，负责将图表系列按轴分组并分派到对应的渲染器。
/// 
/// <para><b>架构说明：</b></para>
/// <para>当图表包含多个系列且它们绑定到不同的轴时，每个轴组合（xAxisId, yAxisId）
/// 形成一个独立的渲染组（AxisRenderGroup）。每个组拥有自己的坐标映射器和可见范围，
/// 确保不同轴的系列不会互相干扰。</para>
/// 
/// <para><b>渲染流程：</b></para>
/// <list type="number">
///   <item>按 (xAxisId, yAxisId) 对系列分组</item>
///   <item>为每组创建独立的 ChartRenderContext（限定 XRange/YRange/Mapper）</item>
///   <item>将每组分派到对应类型的 SeriesRenderer（Line/Scatter/Bar/Area/OHLC）</item>
///   <item>收集所有渲染命令，传递给 IRenderer 执行</item>
/// </list>
/// 
/// <para><b>性能说明：</b></para>
/// <para>所有集合和上下文对象均为预分配成员字段，每帧 Clear 后复用，
/// 避免在渲染热路径中产生 GC 分配。</para>
/// </summary>
internal sealed class SeriesRendererDispatcher
{
    private readonly LineSeriesRenderer _lineRenderer = new();
    private readonly ScatterSeriesRenderer _scatterRenderer = new();
    private readonly BarSeriesRenderer _barRenderer = new();
    private readonly AreaSeriesRenderer _areaRenderer = new();
    private readonly OhlcSeriesRenderer _ohlcRenderer = new();

    private readonly Dictionary<(string xAxisId, string yAxisId), FrameAxisGroupBuilder> _axisGroupBuilders = new(4);
    private readonly Dictionary<(string xAxisId, string yAxisId), AxisGroupBuilder> _axisGroupBuildersNoFrame = new(4);
    private readonly List<AxisRenderGroup> _renderGroups = new(4);
    private readonly List<IRenderCommand> _commands = new(8);
    private readonly ChartRenderContext _scopedContext = new();

    private readonly Dictionary<IRenderableSeries, (int Version, DataFrame Frame)> _logFrameCache = new();

    public IReadOnlyList<IRenderCommand> Render(ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seriesList = context.Series;
        _commands.Clear();
        for (int i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            if (!series.IsVisible)
                continue;

            _commands.AddRange(RenderSeries(series, context, width, height));
        }

        return _commands;
    }

    public IReadOnlyList<AxisRenderGroup> RenderGroups(
        IReadOnlyList<(IRenderableSeries series, DataFrame frame)> frames,
        ChartRenderContext context,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(context);

        if (frames.Count == 0)
            return Array.Empty<AxisRenderGroup>();

        _axisGroupBuilders.Clear();
        for (int i = 0; i < frames.Count; i++)
        {
            var item = frames[i];
            if (!item.series.IsVisible)
                continue;

            var key = (item.series.XAxisId, item.series.YAxisId);
            if (!_axisGroupBuilders.TryGetValue(key, out var axisGroup))
            {
                axisGroup = new FrameAxisGroupBuilder(item.series);
                _axisGroupBuilders.Add(key, axisGroup);
            }

            axisGroup.Add(item.series, item.frame);
        }

        if (_axisGroupBuilders.Count == 0)
            return Array.Empty<AxisRenderGroup>();

        _renderGroups.Clear();
        foreach (var axisGroup in _axisGroupBuilders.Values)
        {
            _renderGroups.Add(RenderAxisGroup(
                context,
                axisGroup.FirstSeries,
                axisGroup.Series,
                width,
                height,
                axisGroup.TryGetFrame));
        }

        return _renderGroups;
    }

    public IReadOnlyList<AxisRenderGroup> RenderGroups(ChartRenderContext context, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seriesList = context.Series;
        if (seriesList.Count == 0)
            return Array.Empty<AxisRenderGroup>();

        _axisGroupBuildersNoFrame.Clear();
        for (int i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            if (!series.IsVisible)
                continue;

            var key = (series.XAxisId, series.YAxisId);
            if (!_axisGroupBuildersNoFrame.TryGetValue(key, out var axisGroup))
            {
                axisGroup = new AxisGroupBuilder(series);
                _axisGroupBuildersNoFrame.Add(key, axisGroup);
            }

            axisGroup.Add(series);
        }

        if (_axisGroupBuildersNoFrame.Count == 0)
            return Array.Empty<AxisRenderGroup>();

        _renderGroups.Clear();
        foreach (var axisGroup in _axisGroupBuildersNoFrame.Values)
        {
            _renderGroups.Add(RenderAxisGroup(
                context,
                axisGroup.FirstSeries,
                axisGroup.Series,
                width,
                height,
                context.FrameAccessor));
        }

        return _renderGroups;
    }

    private IReadOnlyList<IRenderCommand> RenderSeries(IRenderableSeries series, ChartRenderContext context, int width, int height)
    {
        return series switch
        {
            OhlcRenderableSeries ohlc => _ohlcRenderer.Render(ohlc, context, width, height),
            BarRenderableSeries bar => _barRenderer.Render(bar, context, width, height),
            AreaRenderableSeries area => _areaRenderer.Render(area, context, width, height),
            ScatterRenderableSeries scatter => _scatterRenderer.Render(scatter, context, width, height),
            LineRenderableSeries line => _lineRenderer.Render(line, context, width, height),
            _ => _lineRenderer.Render((LineRenderableSeries)series, context, width, height)
        };
    }

    private AxisRenderGroup RenderAxisGroup(
        ChartRenderContext source,
        IRenderableSeries firstSeries,
        List<IRenderableSeries> groupedSeries,
        int width,
        int height,
        Func<IRenderableSeries, DataFrame?>? frameAccessor)
    {
        var xAxis = source.GetXAxisForSeries(firstSeries);
        var yAxis = source.GetYAxisForSeries(firstSeries);
        var xRange = xAxis.VisibleRange;
        var yRange = yAxis.VisibleRange;
        var xMapper = xAxis.CoordinateMapper;
        var yMapper = yAxis.CoordinateMapper;

        // 对数轴：范围变换到 log 空间，与 GPU 着色器线性计算配合
        var gpuXRange = TransformRangeForGpu(xRange, xMapper);
        var gpuYRange = TransformRangeForGpu(yRange, yMapper);

        // 对数轴：包装 frameAccessor，返回 log 变换后的帧数据
        var needsXTransform = xMapper is LogCoordinateMapper;
        var needsYTransform = yMapper is LogCoordinateMapper;
        var wrappedAccessor = needsXTransform || needsYTransform
            ? CreateLogFrameAccessor(frameAccessor, xMapper, yMapper, source)
            : frameAccessor;

        ResetScopedContext(
            source,
            groupedSeries,
            gpuXRange,
            gpuYRange,
            xMapper,
            yMapper,
            wrappedAccessor);

        _commands.Clear();
        for (int i = 0; i < groupedSeries.Count; i++)
            _commands.AddRange(RenderSeries(groupedSeries[i], _scopedContext, width, height));

        return new AxisRenderGroup
        {
            XRange = gpuXRange,
            YRange = gpuYRange,
            XMapper = xMapper,
            YMapper = yMapper,
            Commands = new List<IRenderCommand>(_commands),
            XAxis = xAxis,
            YAxis = yAxis
        };
    }

    private static DataRange TransformRangeForGpu(DataRange range, ICoordinateMapper mapper)
    {
        if (mapper is not LogCoordinateMapper logMapper)
            return range;

        if (range.Min <= 0 || range.Max <= 0)
            return range;

        double invLogBase = 1.0 / Math.Log(logMapper.LogBase);
        double logMin = Math.Log(range.Min) * invLogBase;
        double logMax = Math.Log(range.Max) * invLogBase;
        return new DataRange(logMin, logMax);
    }

    private Func<IRenderableSeries, DataFrame?>? CreateLogFrameAccessor(
        Func<IRenderableSeries, DataFrame?>? originalAccessor,
        ICoordinateMapper xMapper,
        ICoordinateMapper yMapper,
        ChartRenderContext source)
    {
        var xLogMapper = xMapper as LogCoordinateMapper;
        var yLogMapper = yMapper as LogCoordinateMapper;
        double xInvLogBase = xLogMapper?.InvLogBase ?? 0.0;
        double yInvLogBase = yLogMapper?.InvLogBase ?? 0.0;

        return series =>
        {
            if (_logFrameCache.TryGetValue(series, out var cached)
                && cached.Version == (series.DataSeries?.Version ?? 0))
                return cached.Frame;

            var rawFrame = originalAccessor?.Invoke(series);

            if (rawFrame == null)
                rawFrame = CreateFrameDirectlyFromSeries(series);
            if (rawFrame == null || rawFrame.Count == 0)
                return null;

            if (_logFrameCache.TryGetValue(series, out cached) && cached.Version == rawFrame.Version)
            {
                rawFrame.Return();
                return cached.Frame;
            }

            if (cached.Frame != null)
                cached.Frame.Return();

            var transformed = TransformFrameForGpu(rawFrame, xLogMapper != null, yLogMapper != null,
                xInvLogBase, yInvLogBase);

            rawFrame.Return();

            _logFrameCache[series] = (rawFrame.Version, transformed);
            return transformed;
        };
    }

    private static DataFrame? CreateFrameDirectlyFromSeries(IRenderableSeries series)
    {
        var ds = series.DataSeries;
        if (ds == null || ds.Count == 0)
            return null;

        var count = ds.Count;
        var xArr = ArrayPool<float>.Shared.Rent(count);
        var yArr = ArrayPool<float>.Shared.Rent(count);

        var xBuf = ArrayPool<double>.Shared.Rent(count);
        var yBuf = ArrayPool<double>.Shared.Rent(count);
        ds.CopyXValues(new Span<double>(xBuf, 0, count));
        ds.CopyYValues(new Span<double>(yBuf, 0, count));

        for (int i = 0; i < count; i++)
        {
            xArr[i] = (float)xBuf[i];
            yArr[i] = (float)yBuf[i];
        }

        ArrayPool<double>.Shared.Return(xBuf);
        ArrayPool<double>.Shared.Return(yBuf);

        return new DataFrame
        {
            Count = count,
            Version = ds.Version,
            XRange = ds.XRange,
            YRange = ds.YRange,
            Type = DataFrameType.Xy,
            XValues = xArr,
            YValues = yArr,
            RentedXLength = count,
            RentedYLength = count
        };
    }

    private static DataFrame TransformFrameForGpu(
        DataFrame source, bool transformX, bool transformY,
        double xInvLogBase, double yInvLogBase)
    {
        var count = source.Count;
        float[]? newX = null, newY = null;
        float[]? newOpen = null, newHigh = null, newLow = null, newClose = null;

        if (source.XValues != null)
        {
            newX = ArrayPool<float>.Shared.Rent(count);
            if (transformX)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.XValues[i];
                    newX[i] = val > 0 ? (float)(Math.Log(val) * xInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.XValues, newX, count);
            }
        }

        if (source.YValues != null)
        {
            newY = ArrayPool<float>.Shared.Rent(count);
            if (transformY)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.YValues[i];
                    newY[i] = val > 0 ? (float)(Math.Log(val) * yInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.YValues, newY, count);
            }
        }

        if (source.OpenValues != null)
        {
            newOpen = ArrayPool<float>.Shared.Rent(count);
            if (transformY)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.OpenValues[i];
                    newOpen[i] = val > 0 ? (float)(Math.Log(val) * yInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.OpenValues, newOpen, count);
            }
        }
        if (source.HighValues != null)
        {
            newHigh = ArrayPool<float>.Shared.Rent(count);
            if (transformY)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.HighValues[i];
                    newHigh[i] = val > 0 ? (float)(Math.Log(val) * yInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.HighValues, newHigh, count);
            }
        }
        if (source.LowValues != null)
        {
            newLow = ArrayPool<float>.Shared.Rent(count);
            if (transformY)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.LowValues[i];
                    newLow[i] = val > 0 ? (float)(Math.Log(val) * yInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.LowValues, newLow, count);
            }
        }
        if (source.CloseValues != null)
        {
            newClose = ArrayPool<float>.Shared.Rent(count);
            if (transformY)
            {
                for (int i = 0; i < count; i++)
                {
                    float val = source.CloseValues[i];
                    newClose[i] = val > 0 ? (float)(Math.Log(val) * yInvLogBase) : 0f;
                }
            }
            else
            {
                Array.Copy(source.CloseValues, newClose, count);
            }
        }

        return new DataFrame
        {
            Count = count,
            Version = source.Version,
            XRange = transformX ? LogTransformRange(source.XRange, xInvLogBase) : source.XRange,
            YRange = transformY ? LogTransformRange(source.YRange, yInvLogBase) : source.YRange,
            Type = source.Type,
            XValues = newX, YValues = newY,
            OpenValues = newOpen, HighValues = newHigh,
            LowValues = newLow, CloseValues = newClose,
            RentedXLength = newX != null ? count : 0,
            RentedYLength = newY != null ? count : 0,
            RentedOhlcLength = newOpen != null ? count : 0
        };
    }

    private static DataRange LogTransformRange(DataRange range, double invLogBase)
    {
        if (range.Min <= 0 || range.Max <= 0)
            return range;
        return new DataRange(Math.Log(range.Min) * invLogBase, Math.Log(range.Max) * invLogBase);
    }

    private void ResetScopedContext(
        ChartRenderContext source,
        IList<IRenderableSeries> series,
        DataRange xRange,
        DataRange yRange,
        ICoordinateMapper xMapper,
        ICoordinateMapper yMapper,
        Func<IRenderableSeries, DataFrame?>? frameAccessor)
    {
        _scopedContext.XRangeAccessor = () => xRange;
        _scopedContext.YRangeAccessor = () => yRange;
        _scopedContext.SeriesAccessor = () => series;
        _scopedContext.XAxesAccessor = source.XAxesAccessor;
        _scopedContext.YAxesAccessor = source.YAxesAccessor;
        _scopedContext.OnRangeChanged = source.OnRangeChanged;
        _scopedContext.OnAxisRangeChanged = source.OnAxisRangeChanged;
        _scopedContext.ViewportWidth = source.ViewportWidth;
        _scopedContext.ViewportHeight = source.ViewportHeight;
        _scopedContext.DpiScaleX = source.DpiScaleX;
        _scopedContext.DpiScaleY = source.DpiScaleY;
        _scopedContext.InputElement = source.InputElement;
        _scopedContext.PlotAreaOffsetX = source.PlotAreaOffsetX;
        _scopedContext.PlotAreaOffsetY = source.PlotAreaOffsetY;
        _scopedContext.PlotAreaWidth = source.PlotAreaWidth;
        _scopedContext.PlotAreaHeight = source.PlotAreaHeight;
        _scopedContext.IsInputElementPlotArea = source.IsInputElementPlotArea;
        _scopedContext.XMapper = xMapper;
        _scopedContext.YMapper = yMapper;
        _scopedContext.FrameAccessor = frameAccessor;
    }

    private sealed class AxisGroupBuilder(IRenderableSeries firstSeries)
    {
        public IRenderableSeries FirstSeries { get; } = firstSeries;

        public List<IRenderableSeries> Series { get; } = new();

        public void Add(IRenderableSeries series)
        {
            Series.Add(series);
        }

        public void Reset(IRenderableSeries first)
        {
            Series.Clear();
        }
    }

    private sealed class FrameAxisGroupBuilder(IRenderableSeries firstSeries)
    {
        private readonly List<DataFrame> _frames = new();

        public IRenderableSeries FirstSeries { get; } = firstSeries;

        public List<IRenderableSeries> Series { get; } = new();

        public void Add(IRenderableSeries series, DataFrame frame)
        {
            Series.Add(series);
            _frames.Add(frame);
        }

        public DataFrame? TryGetFrame(IRenderableSeries series)
        {
            for (int i = 0; i < Series.Count; i++)
            {
                if (ReferenceEquals(Series[i], series))
                    return _frames[i];
            }

            return null;
        }
    }
}
