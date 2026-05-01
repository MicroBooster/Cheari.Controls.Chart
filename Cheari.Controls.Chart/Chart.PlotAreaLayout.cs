using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.Controls;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering.Context;
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
    private IAxis[] _topXAxesCache = s_emptyAxes;
    private IAxis[] _bottomXAxesCache = s_emptyAxes;
    private IAxis[] _leftYAxesCache = s_emptyAxes;
    private IAxis[] _rightYAxesCache = s_emptyAxes;
    private bool _axisPlacementCacheDirty = true;
    private bool _plotAreaMetricsDirty = true;

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

        if (_surface is FrameworkElement plotHost)
        {
            var pos = plotHost.TransformToAncestor(this).Transform(new Point(0, 0));
            _renderContext.PlotAreaOffsetX = pos.X;
            _renderContext.PlotAreaOffsetY = pos.Y;
            _renderContext.PlotAreaWidth = _surface.TextureWidth;
            _renderContext.PlotAreaHeight = _surface.TextureHeight;
        }
        else
        {
            _renderContext.PlotAreaOffsetX = 0;
            _renderContext.PlotAreaOffsetY = 0;
            _renderContext.PlotAreaWidth = _renderContext.ViewportWidth;
            _renderContext.PlotAreaHeight = _renderContext.ViewportHeight;
        }

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

        _renderContext.ViewportWidth = _surface?.TextureWidth ?? Math.Max(0, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        _renderContext.ViewportHeight = _surface?.TextureHeight ?? Math.Max(0, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        _renderContext.DpiScaleX = dpi.DpiScaleX;
        _renderContext.DpiScaleY = dpi.DpiScaleY;
        _renderContext.InputElement = _surface is not null ? _surface : this;
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

    private void UpdateAxisPresenterOverlap()
    {
        ApplyAxisPresenterOffset(_topAxesPresenter, 0, GetAxisOverlapOffset(PlotAreaBorderThickness.Top));
        ApplyAxisPresenterOffset(_leftAxesPresenter, GetAxisOverlapOffset(PlotAreaBorderThickness.Left), 0);
        ApplyAxisPresenterOffset(_rightAxesPresenter, -GetAxisOverlapOffset(PlotAreaBorderThickness.Right), 0);
        ApplyAxisPresenterOffset(_bottomAxesPresenter, 0, -GetAxisOverlapOffset(PlotAreaBorderThickness.Bottom));
    }

    private static void ApplyAxisPresenterOffset(FrameworkElement? presenter, double x, double y)
    {
        if (presenter == null)
            return;

        presenter.RenderTransform = x == 0 && y == 0
            ? Transform.Identity
            : new TranslateTransform(x, y);
    }

    private static double GetAxisOverlapOffset(double borderThickness)
    {
        if (borderThickness <= 0)
            return 0;

        return (borderThickness / 2.0) + 0.5;
    }

    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidatePlotAreaMetrics();
        PropagatePlotAreaSizeToAxes();
        SignalViewportChanged();
    }

    private void OnGridLinesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidatePlotAreaMetrics();
        PropagatePlotAreaSizeToAxes();
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
