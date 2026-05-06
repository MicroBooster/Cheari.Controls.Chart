using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Cheari.Controls.Annotations;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.Controls;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;
using Vortice.Wpf;

namespace Cheari.Controls;

/// <summary>
/// Chart 的绘图区域布局逻辑：视口度量、轴展示器偏移、角画刷、渲染上下文更新。
/// </summary>
public partial class Chart
{
    private FrameworkElement? _topAxesPresenter;
    private FrameworkElement? _leftAxesPresenter;
    private FrameworkElement? _rightAxesPresenter;
    private FrameworkElement? _bottomAxesPresenter;
    private AnnotationsPanel? _annotationsPanel;
    private IAxis[] _topXAxesCache = s_emptyAxes;
    private IAxis[] _bottomXAxesCache = s_emptyAxes;
    private IAxis[] _leftYAxesCache = s_emptyAxes;
    private IAxis[] _rightYAxesCache = s_emptyAxes;
    private bool _axisPlacementCacheDirty = true;
    private bool _plotAreaMetricsDirty = true;

    private readonly record struct PlotAreaMetrics(double OffsetX, double OffsetY, int WidthPx, int HeightPx);
    private readonly record struct AutoFitPaddingPixels(double MinPixels, double MaxPixels)
    {
        public static AutoFitPaddingPixels None => default;

        public AutoFitPaddingPixels Merge(AutoFitPaddingPixels other)
            => new(Math.Max(MinPixels, other.MinPixels), Math.Max(MaxPixels, other.MaxPixels));
    }

    private readonly record struct AutoFitCoreBounds(double MinValue, double MaxValue)
    {
        public static AutoFitCoreBounds None => new(double.PositiveInfinity, double.NegativeInfinity);

        public bool HasMinValue => !double.IsPositiveInfinity(MinValue);
        public bool HasMaxValue => !double.IsNegativeInfinity(MaxValue);

        public AutoFitCoreBounds Merge(AutoFitCoreBounds other)
            => new(Math.Min(MinValue, other.MinValue), Math.Max(MaxValue, other.MaxValue));

        public DataRange Apply(DataRange range)
        {
            double min = HasMinValue ? Math.Min(range.Min, MinValue) : range.Min;
            double max = HasMaxValue ? Math.Max(range.Max, MaxValue) : range.Max;
            return new DataRange(min, max);
        }
    }

    private readonly record struct AutoFitAxisGeometry(AutoFitCoreBounds CoreBounds, AutoFitPaddingPixels PixelPadding)
    {
        public static AutoFitAxisGeometry None => new(AutoFitCoreBounds.None, AutoFitPaddingPixels.None);

        public AutoFitAxisGeometry Merge(AutoFitAxisGeometry other)
            => new(CoreBounds.Merge(other.CoreBounds), PixelPadding.Merge(other.PixelPadding));
    }

    private void InvalidateAxisPlacementCaches()
    {
        _axisPlacementCacheDirty = true;
    }

    private void RefreshAxisPlacementCaches()
    {
        if (!_axisPlacementCacheDirty)
            return;

        _topXAxesCache = FilterAxesByPlacement(XAxes, AxisPlacement.Top);
        _bottomXAxesCache = FilterAxesByPlacement(XAxes, AxisPlacement.Bottom);
        _leftYAxesCache = FilterAxesByPlacement(YAxes, AxisPlacement.Left);
        _rightYAxesCache = FilterAxesByPlacement(YAxes, AxisPlacement.Right);

        TopXAxes = _topXAxesCache;
        BottomXAxes = _bottomXAxesCache;
        LeftYAxes = _leftYAxesCache;
        RightYAxes = _rightYAxesCache;
        UpdateCornerBrushes();
        _axisPlacementCacheDirty = false;
    }

    private void InvalidatePlotAreaMetrics()
    {
        _plotAreaMetricsDirty = true;
    }

    private void RefreshPlotAreaMetrics()
    {
        if (!_plotAreaMetricsDirty)
            return;

        var metrics = ResolveInnerPlotAreaMetrics();
        _renderContext.PlotAreaOffsetX = metrics.WidthPx > 0 ? metrics.OffsetX : 0;
        _renderContext.PlotAreaOffsetY = metrics.HeightPx > 0 ? metrics.OffsetY : 0;
        _renderContext.PlotAreaWidth = metrics.WidthPx > 0 ? metrics.WidthPx : _renderContext.ViewportWidth;
        _renderContext.PlotAreaHeight = metrics.HeightPx > 0 ? metrics.HeightPx : _renderContext.ViewportHeight;

        PropagatePlotAreaSizeToAxes();
        _plotAreaMetricsDirty = false;
    }

    private void UpdateRenderContext()
    {
        var dpi = VisualTreeHelper.GetDpi(_surface is not null ? (Visual)_surface : this);
        var xAxes = GetCurrentXAxesList();
        var yAxes = GetCurrentYAxesList();
        var defaultXAxis = GetDefaultAxis(xAxes, DefaultXAxisId);
        var defaultYAxis = GetDefaultAxis(yAxes, DefaultYAxisId);

        _renderContext.DpiScaleX = dpi.DpiScaleX;
        _renderContext.DpiScaleY = dpi.DpiScaleY;

        var metrics = ResolveInnerPlotAreaMetrics();

        _renderContext.ViewportWidth = metrics.WidthPx > 0
            ? metrics.WidthPx
            : Math.Max(0, (int)Math.Round(ActualWidth * _renderContext.DpiScaleX));
        _renderContext.ViewportHeight = metrics.HeightPx > 0
            ? metrics.HeightPx
            : Math.Max(0, (int)Math.Round(ActualHeight * _renderContext.DpiScaleY));
        _renderContext.InputElement = _surface is not null ? _surface : this;
        _renderContext.IsInputElementPlotArea = _surface is not null;
        _renderContext.XMapper = defaultXAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance;
        _renderContext.YMapper = defaultYAxis?.CoordinateMapper ?? LinearCoordinateMapper.Instance;
        _renderContext.FrameAccessor = null;

        RefreshAxisPlacementCaches();
        RefreshPlotAreaMetrics();
    }

    private void UpdateCornerBrushes()
    {
        TopLeftCornerBrush = _leftYAxesCache.Length > 0 ? _leftYAxesCache[0].AxisForeground : Brushes.Transparent;
        TopRightCornerBrush = _topXAxesCache.Length > 0 ? _topXAxesCache[0].AxisForeground : Brushes.Transparent;
        BottomLeftCornerBrush = _bottomXAxesCache.Length > 0 ? _bottomXAxesCache[0].AxisForeground : Brushes.Transparent;
        BottomRightCornerBrush = _rightYAxesCache.Length > 0 ? _rightYAxesCache[0].AxisForeground : Brushes.Transparent;
    }



    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidatePlotAreaMetrics();
        PropagatePlotAreaSizeToAxes();
        QueueAutoRangeUpdateForAllAxes();
        SignalViewportChanged();
    }

    private void OnGridLinesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidatePlotAreaMetrics();
        PropagatePlotAreaSizeToAxes();
        QueueAutoRangeUpdateForAllAxes();
        SignalViewportChanged();
    }

    private void PropagatePlotAreaSizeToAxes()
    {
        double width = _gridLines?.ActualWidth ?? _surface?.ActualWidth ?? 0;
        double height = _gridLines?.ActualHeight ?? _surface?.ActualHeight ?? 0;

        if (width <= 0 || height <= 0)
            return;

        var plotSize = new Size(width, height);
        SetAxisControlsPlotAreaSize(_leftAxesPresenter, plotSize);
        SetAxisControlsPlotAreaSize(_rightAxesPresenter, plotSize);
        SetAxisControlsPlotAreaSize(_topAxesPresenter, plotSize);
        SetAxisControlsPlotAreaSize(_bottomAxesPresenter, plotSize);

        if (_annotationsPanel != null)
        {
            _annotationsPanel.PlotAreaWidth = width;
            _annotationsPanel.PlotAreaHeight = height;
        }
    }

    private static void SetAxisControlsPlotAreaSize(FrameworkElement? presenter, Size plotSize)
    {
        if (presenter is ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is AxisControl axisControl)
                    axisControl.PlotAreaSize = plotSize;
            }
        }
    }

    private static IAxis? GetDefaultAxis(IList<IAxis> axes, string defaultAxisId)
    {
        var axis = FindAxis(axes, defaultAxisId);
        if (axis != null)
            return axis;

        return axes.Count > 0 ? axes[0] : null;
    }

    private void OnPlotAreaMarginChanged()
    {
        InvalidatePlotAreaMetrics();
        QueueAutoRangeUpdateForAllAxes();
        SignalViewportChanged();
    }

    private PlotAreaMetrics ResolveInnerPlotAreaMetrics()
    {
        if (_surface is FrameworkElement plotHost)
        {
            var pos = plotHost.TransformToAncestor(this).Transform(new Point(0, 0));
            int widthPx = ResolvePhysicalPixels(_surface.TextureWidth, plotHost.ActualWidth, _renderContext.DpiScaleX);
            int heightPx = ResolvePhysicalPixels(_surface.TextureHeight, plotHost.ActualHeight, _renderContext.DpiScaleY);
            if (widthPx > 0 && heightPx > 0)
                return new PlotAreaMetrics(pos.X, pos.Y, widthPx, heightPx);
        }

        if (_gridLines != null)
        {
            var pos = _gridLines.TransformToAncestor(this).Transform(new Point(0, 0));
            Thickness margin = PlotAreaMargin;
            double width = Math.Max(0, _gridLines.ActualWidth - margin.Left - margin.Right);
            double height = Math.Max(0, _gridLines.ActualHeight - margin.Top - margin.Bottom);
            int widthPx = ResolvePhysicalPixels(0, width, _renderContext.DpiScaleX);
            int heightPx = ResolvePhysicalPixels(0, height, _renderContext.DpiScaleY);
            if (widthPx > 0 && heightPx > 0)
                return new PlotAreaMetrics(pos.X + margin.Left, pos.Y + margin.Top, widthPx, heightPx);
        }

        return default;
    }

    private (DataRange ClampedCore, DataRange VisibleRange) ComputeAutoFitRanges(
        DataRange autoRange,
        IAxis axis,
        IReadOnlyCollection<IRenderableSeries> relatedSeries,
        bool isXAxis)
    {
        var geometry = CalculateAutoFitAxisGeometry(autoRange, axis, relatedSeries, isXAxis);

        DataRange expandedCore = geometry.CoreBounds.Apply(autoRange);

        DataRange clampedCore = expandedCore;
        if (axis is AxisBase axisBase)
            clampedCore = axisBase.ClampToVisibleRangeLimit(expandedCore);

        var padding = geometry.PixelPadding;
        if (padding.MinPixels <= 0 && padding.MaxPixels <= 0)
            return (clampedCore, clampedCore);

        var metrics = ResolveInnerPlotAreaMetrics();
        int viewportPixels = isXAxis ? metrics.WidthPx : metrics.HeightPx;
        if (viewportPixels <= 0)
            viewportPixels = isXAxis ? _renderContext.ViewportWidth : _renderContext.ViewportHeight;

        DataRange paddedRange = axis.CoordinateMapper switch
        {
            LogCoordinateMapper logMapper => ExpandLogRange(clampedCore, padding.MinPixels, padding.MaxPixels, viewportPixels, logMapper.LogBase),
            _ => ExpandLinearRange(clampedCore, padding.MinPixels, padding.MaxPixels, viewportPixels)
        };

        if (axis is AxisBase clampAxis)
            paddedRange = clampAxis.ClampToVisibleRangeLimit(paddedRange);

        return (clampedCore, paddedRange);
    }

    private AutoFitAxisGeometry CalculateAutoFitAxisGeometry(
        DataRange autoRange,
        IAxis axis,
        IReadOnlyCollection<IRenderableSeries> relatedSeries,
        bool isXAxis)
    {
        if (relatedSeries.Count == 0)
            return AutoFitAxisGeometry.None;

        AutoFitAxisGeometry geometry = AutoFitAxisGeometry.None;
        foreach (var series in relatedSeries)
        {
            geometry = geometry.Merge(CalculateSeriesAutoFitGeometry(autoRange, axis, series, isXAxis));
        }

        return geometry;
    }

    private AutoFitAxisGeometry CalculateSeriesAutoFitGeometry(
        DataRange autoRange,
        IAxis axis,
        IRenderableSeries series,
        bool isXAxis)
    {
        return series switch
        {
            ScatterRenderableSeries scatter => CreateSymmetricPixelPaddingGeometry(
                SeriesGeometryHelper.CalculateMarkerPixelPadding(scatter.MarkerSize)),
            AreaRenderableSeries area => (isXAxis ? AutoFitAxisGeometry.None : CreateCoreValueGeometry(area.BaselineY, axis))
                .Merge(CreateSymmetricPixelPaddingGeometry(
                    SeriesGeometryHelper.CalculateLineEndpointPixelPadding(area.StrokeThickness, EnableAntialiasing))),
            BarRenderableSeries bar => isXAxis
                ? CreateBarBodyCoreGeometry(bar, axis, autoRange)
                : CreateCoreValueGeometry(0.0, axis),
            OhlcRenderableSeries ohlc => isXAxis
                ? CreateOhlcXGeometry(ohlc, axis, autoRange)
                : CreateOhlcYGeometry(ohlc),
            _ => CreateSymmetricPixelPaddingGeometry(
                SeriesGeometryHelper.CalculateLineEndpointPixelPadding(series.StrokeThickness, EnableAntialiasing))
        };
    }

    private static AutoFitAxisGeometry CreateSymmetricPixelPaddingGeometry(double paddingPixels)
    {
        if (paddingPixels <= 0 || double.IsNaN(paddingPixels) || double.IsInfinity(paddingPixels))
            return AutoFitAxisGeometry.None;

        return new AutoFitAxisGeometry(
            AutoFitCoreBounds.None,
            new AutoFitPaddingPixels(paddingPixels, paddingPixels));
    }

    private static AutoFitAxisGeometry CreateCoreValueGeometry(double value, IAxis axis)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return AutoFitAxisGeometry.None;

        if (axis.CoordinateMapper is LogCoordinateMapper && value <= 0)
            return AutoFitAxisGeometry.None;

        return new AutoFitAxisGeometry(new AutoFitCoreBounds(value, value), AutoFitPaddingPixels.None);
    }

    private AutoFitAxisGeometry CreateBarBodyCoreGeometry(
        BarRenderableSeries series,
        IAxis axis,
        DataRange autoRange)
    {
        if (!TryGetTransformedSeriesAxisStats(series.DataSeries, axis.CoordinateMapper, isXAxis: true, out double minValue, out double maxValue, out int count))
            return AutoFitAxisGeometry.None;

        double fallbackRangeLength = SeriesGeometryHelper.TransformRange(autoRange, axis.CoordinateMapper).Length;
        double widthData = SeriesGeometryHelper.ResolveBarWidthData(series, maxValue - minValue, count, fallbackRangeLength);
        return CreateTransformedCoreBoundsGeometry(minValue, maxValue, widthData * 0.5, axis.CoordinateMapper);
    }

    private AutoFitAxisGeometry CreateOhlcXGeometry(
        OhlcRenderableSeries series,
        IAxis axis,
        DataRange autoRange)
    {
        if (!TryGetTransformedSeriesAxisStats(series.DataSeries, axis.CoordinateMapper, isXAxis: true, out double minValue, out double maxValue, out int count))
            return AutoFitAxisGeometry.None;

        double fallbackRangeLength = SeriesGeometryHelper.TransformRange(autoRange, axis.CoordinateMapper).Length;
        double widthData = SeriesGeometryHelper.ResolveOhlcBodyWidthData(series, maxValue - minValue, count, fallbackRangeLength);
        var coreGeometry = CreateTransformedCoreBoundsGeometry(minValue, maxValue, widthData * 0.5, axis.CoordinateMapper);
        double wickXPadding = SeriesGeometryHelper.CalculateLineCrossAxisPixelPadding(series.StrokeThickness, EnableAntialiasing);
        return coreGeometry.Merge(CreateSymmetricPixelPaddingGeometry(wickXPadding));
    }

    private AutoFitAxisGeometry CreateOhlcYGeometry(OhlcRenderableSeries series)
    {
        double wickYPadding = SeriesGeometryHelper.CalculateLineAlongAxisPixelPadding(EnableAntialiasing);
        return CreateSymmetricPixelPaddingGeometry(wickYPadding);
    }

    private static AutoFitAxisGeometry CreateTransformedCoreBoundsGeometry(
        double transformedMin,
        double transformedMax,
        double halfWidth,
        ICoordinateMapper mapper)
    {
        if (halfWidth < 0
            || double.IsNaN(halfWidth)
            || double.IsInfinity(halfWidth))
        {
            return AutoFitAxisGeometry.None;
        }

        double expandedMin = transformedMin - halfWidth;
        double expandedMax = transformedMax + halfWidth;
        double minValue = SeriesGeometryHelper.InverseTransformAxisValue(expandedMin, mapper);
        double maxValue = SeriesGeometryHelper.InverseTransformAxisValue(expandedMax, mapper);

        if (double.IsNaN(minValue) || double.IsInfinity(minValue)
            || double.IsNaN(maxValue) || double.IsInfinity(maxValue))
        {
            return AutoFitAxisGeometry.None;
        }

        if (mapper is LogCoordinateMapper)
        {
            if (minValue <= 0)
                minValue = double.Epsilon;
            if (maxValue <= 0)
                return AutoFitAxisGeometry.None;
        }

        return new AutoFitAxisGeometry(new AutoFitCoreBounds(minValue, maxValue), AutoFitPaddingPixels.None);
    }

    private static bool TryGetTransformedSeriesAxisStats(
        IDataSeries dataSeries,
        ICoordinateMapper mapper,
        bool isXAxis,
        out double minValue,
        out double maxValue,
        out int count)
    {
        minValue = double.PositiveInfinity;
        maxValue = double.NegativeInfinity;
        count = 0;

        for (int i = 0; i < dataSeries.Count; i++)
        {
            double rawValue = isXAxis ? dataSeries.GetX(i) : dataSeries.GetY(i);
            if (!SeriesGeometryHelper.TryTransformAxisValue(rawValue, mapper, out double transformedValue))
                continue;

            minValue = Math.Min(minValue, transformedValue);
            maxValue = Math.Max(maxValue, transformedValue);
            count++;
        }

        return count > 0
            && !double.IsPositiveInfinity(minValue)
            && !double.IsNegativeInfinity(maxValue);
    }

    private static DataRange ExpandLinearRange(DataRange coreRange, double minPixelPadding, double maxPixelPadding, int viewportPixels)
    {
        if ((minPixelPadding <= 0 && maxPixelPadding <= 0) || viewportPixels <= 0 || coreRange.Length <= 0)
            return coreRange;

        double denominator = viewportPixels - minPixelPadding - maxPixelPadding;
        if (denominator <= 0)
            return coreRange;

        double minDataPadding = coreRange.Length * minPixelPadding / denominator;
        double maxDataPadding = coreRange.Length * maxPixelPadding / denominator;
        if (double.IsNaN(minDataPadding) || double.IsInfinity(minDataPadding) || minDataPadding < 0
            || double.IsNaN(maxDataPadding) || double.IsInfinity(maxDataPadding) || maxDataPadding < 0)
        {
            return coreRange;
        }

        return new DataRange(coreRange.Min - minDataPadding, coreRange.Max + maxDataPadding);
    }

    private static DataRange ExpandLogRange(DataRange coreRange, double minPixelPadding, double maxPixelPadding, int viewportPixels, double logBase)
    {
        if ((minPixelPadding <= 0 && maxPixelPadding <= 0)
            || viewportPixels <= 0
            || coreRange.Min <= 0
            || coreRange.Max <= 0
            || coreRange.Length <= 0
            || logBase <= 1.0)
        {
            return coreRange;
        }

        double denominator = viewportPixels - minPixelPadding - maxPixelPadding;
        if (denominator <= 0)
            return coreRange;

        double invLogBase = 1.0 / Math.Log(logBase);
        double logMin = Math.Log(coreRange.Min) * invLogBase;
        double logMax = Math.Log(coreRange.Max) * invLogBase;
        double logLength = logMax - logMin;
        if (logLength <= 0 || double.IsNaN(logLength) || double.IsInfinity(logLength))
            return coreRange;

        double minLogPadding = logLength * minPixelPadding / denominator;
        double maxLogPadding = logLength * maxPixelPadding / denominator;
        if (double.IsNaN(minLogPadding) || double.IsInfinity(minLogPadding) || minLogPadding < 0
            || double.IsNaN(maxLogPadding) || double.IsInfinity(maxLogPadding) || maxLogPadding < 0)
            return coreRange;

        double paddedMin = Math.Pow(logBase, logMin - minLogPadding);
        double paddedMax = Math.Pow(logBase, logMax + maxLogPadding);
        if (double.IsNaN(paddedMin) || double.IsInfinity(paddedMin)
            || double.IsNaN(paddedMax) || double.IsInfinity(paddedMax))
        {
            return coreRange;
        }

        return new DataRange(paddedMin, paddedMax);
    }

    private static int ResolvePhysicalPixels(int preferredTexturePixels, double actualSize, double dpiScale)
    {
        if (preferredTexturePixels > 0)
            return preferredTexturePixels;

        if (actualSize <= 0)
            return 0;

        return Math.Max(0, (int)Math.Round(actualSize * (dpiScale > 0 ? dpiScale : 1.0)));
    }

    private static IAxis[] FilterAxesByPlacement(IList<IAxis>? axes, AxisPlacement placement)
    {
        if (axes == null || axes.Count == 0)
            return s_emptyAxes;

        int count = 0;
        for (int i = 0; i < axes.Count; i++)
        {
            if (axes[i].Placement == placement)
                count++;
        }

        if (count == 0)
            return s_emptyAxes;

        var filtered = new IAxis[count];
        int index = 0;
        for (int i = 0; i < axes.Count; i++)
        {
            if (axes[i].Placement == placement)
                filtered[index++] = axes[i];
        }

        return filtered;
    }
}
