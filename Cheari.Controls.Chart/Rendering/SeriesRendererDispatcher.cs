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

        ResetScopedContext(
            source,
            groupedSeries,
            xRange,
            yRange,
            xMapper,
            yMapper,
            frameAccessor);

        _commands.Clear();
        for (int i = 0; i < groupedSeries.Count; i++)
            _commands.AddRange(RenderSeries(groupedSeries[i], _scopedContext, width, height));

        return new AxisRenderGroup
        {
            XRange = xRange,
            YRange = yRange,
            XMapper = xMapper,
            YMapper = yMapper,
            Commands = new List<IRenderCommand>(_commands)
        };
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
