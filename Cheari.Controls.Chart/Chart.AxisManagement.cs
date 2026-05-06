using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls;

/// <summary>
/// Chart 的轴管理逻辑：轴订阅/取消订阅、范围同步、AutoRange 计算。
/// </summary>
public partial class Chart
{
    private bool _syncingRange;
    private int _hasAnyAutoRangeAxisState;
    private readonly HashSet<string> _pendingAutoRangeXAxisIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingAutoRangeYAxisIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<IDataSeries, byte> _pendingBackgroundAutoRangeSeries = new();
    private int _pendingBackgroundAutoRangeRequest;
    private bool _autoRangeUpdateScheduled;

    private static void OnAxesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            if (e.OldValue is ObservableCollection<IAxis> oldAxes)
            {
                oldAxes.CollectionChanged -= chart.OnAxesCollectionChanged;
                chart.UnsubscribeFromAxes(oldAxes);
            }

            if (e.NewValue is ObservableCollection<IAxis> newAxes)
            {
                newAxes.CollectionChanged += chart.OnAxesCollectionChanged;
                chart.SubscribeToAxes(newAxes);
            }

            chart.InvalidateAxisPlacementCaches();
            chart.RefreshAutoRangeAxisState();
            chart.SyncAxesToRange();
            chart.UpdateRenderContext();
            chart.SignalViewportChanged();
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            if (!chart._syncingRange && !Equals(e.NewValue, e.OldValue))
            {
                var isXRange = e.Property == XRangeProperty;
                var defaultAxis = isXRange
                    ? FindAxis(chart.XAxes, DefaultXAxisId)
                    : FindAxis(chart.YAxes, DefaultYAxisId);

                if (defaultAxis != null && defaultAxis.AutoRange)
                {
                    chart.SetCurrentValue(e.Property, defaultAxis.VisibleRange);
                    return;
                }
            }

            chart.SyncRangeToAxes();
            chart.SignalViewportChanged();
        }
    }

    private void OnAxesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (IAxis axis in e.OldItems)
                UnsubscribeFromAxis(axis);
        }

        if (e.NewItems != null)
        {
            foreach (IAxis axis in e.NewItems)
                SubscribeToAxis(axis);
        }

        InvalidateAxisPlacementCaches();
        RefreshAutoRangeAxisState();
        SyncAxesToRange();
        UpdateRenderContext();
        MarkDirty();
    }

    private void OnAxisVisibleRangeChanged(object? sender, EventArgs e)
    {
        if (_syncingRange) return;
        if (sender is IAxis axis)
        {
            SyncAxesToRange();
            SignalViewportChanged();
        }
    }

    private void OnAxisPlacementChanged(object? sender, EventArgs e)
    {
        InvalidateAxisPlacementCaches();
        UpdateRenderContext();
        SignalViewportChanged();
    }

    private void OnAxisForegroundChanged(object? sender, EventArgs e)
    {
        InvalidateAxisPlacementCaches();
        RefreshAxisPlacementCaches();
    }

    private void OnAxisAutoRangeChanged(object? sender, EventArgs e)
    {
        RefreshAutoRangeAxisState();
        if (sender is IAxis axis && axis.AutoRange)
            ScheduleAutoRangeRecalcForAxis(axis.Id);
    }

    private void OnAxisVisibleRangeLimitChanged(object? sender, EventArgs e)
    {
        if (sender is IAxis axis && axis.AutoRange)
            ScheduleAutoRangeRecalcForAxis(axis.Id);
    }

    private void OnAxisVisibleRangeLimitModeChanged(object? sender, EventArgs e)
    {
        if (sender is IAxis axis && axis.AutoRange)
            ScheduleAutoRangeRecalcForAxis(axis.Id);
    }

    private void ScheduleAutoRangeRecalcForAxis(string axisId)
    {
        if (FindAxis(XAxes, axisId) != null)
            _pendingAutoRangeXAxisIds.Add(axisId);
        if (FindAxis(YAxes, axisId) != null)
            _pendingAutoRangeYAxisIds.Add(axisId);

        if (_autoRangeUpdateScheduled
            || (_pendingAutoRangeXAxisIds.Count == 0 && _pendingAutoRangeYAxisIds.Count == 0))
        {
            return;
        }

        _autoRangeUpdateScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(FlushPendingAutoRangeUpdates));
    }

    private void SubscribeToAxes(IEnumerable<IAxis> axes)
    {
        foreach (var axis in axes)
            SubscribeToAxis(axis);
    }

    private void UnsubscribeFromAxes(IEnumerable<IAxis> axes)
    {
        foreach (var axis in axes)
            UnsubscribeFromAxis(axis);
    }

    private void SubscribeToAxis(IAxis axis)
    {
        axis.VisibleRangeChanged += OnAxisVisibleRangeChanged;

        if (axis is not AxisBase axisBase)
            return;

        s_axisPlacementDescriptor?.AddValueChanged(axisBase, OnAxisPlacementChanged);
        s_axisForegroundDescriptor?.AddValueChanged(axisBase, OnAxisForegroundChanged);
        s_axisAutoRangeDescriptor?.AddValueChanged(axisBase, OnAxisAutoRangeChanged);
        s_axisVisibleRangeLimitDescriptor?.AddValueChanged(axisBase, OnAxisVisibleRangeLimitChanged);
        s_axisVisibleRangeLimitModeDescriptor?.AddValueChanged(axisBase, OnAxisVisibleRangeLimitModeChanged);
    }

    private void UnsubscribeFromAxis(IAxis axis)
    {
        axis.VisibleRangeChanged -= OnAxisVisibleRangeChanged;

        if (axis is not AxisBase axisBase)
            return;

        s_axisPlacementDescriptor?.RemoveValueChanged(axisBase, OnAxisPlacementChanged);
        s_axisForegroundDescriptor?.RemoveValueChanged(axisBase, OnAxisForegroundChanged);
        s_axisAutoRangeDescriptor?.RemoveValueChanged(axisBase, OnAxisAutoRangeChanged);
        s_axisVisibleRangeLimitDescriptor?.RemoveValueChanged(axisBase, OnAxisVisibleRangeLimitChanged);
        s_axisVisibleRangeLimitModeDescriptor?.RemoveValueChanged(axisBase, OnAxisVisibleRangeLimitModeChanged);
    }

    private void SyncRangeToAxes()
    {
        if (_syncingRange) return;
        _syncingRange = true;
        try
        {
            var defaultXAxis = FindAxis(XAxes, DefaultXAxisId);
            if (defaultXAxis != null)
            {
                defaultXAxis.VisibleRange = XRange;
                XRange = defaultXAxis.VisibleRange;
            }

            var defaultYAxis = FindAxis(YAxes, DefaultYAxisId);
            if (defaultYAxis != null)
            {
                defaultYAxis.VisibleRange = YRange;
                YRange = defaultYAxis.VisibleRange;
            }
        }
        finally
        {
            _syncingRange = false;
        }
    }

    private void SyncAxesToRange()
    {
        if (_syncingRange) return;
        _syncingRange = true;
        try
        {
            var defaultXAxis = FindAxis(XAxes, DefaultXAxisId);
            if (defaultXAxis != null)
                XRange = defaultXAxis.VisibleRange;

            var defaultYAxis = FindAxis(YAxes, DefaultYAxisId);
            if (defaultYAxis != null)
                YRange = defaultYAxis.VisibleRange;
        }
        finally
        {
            _syncingRange = false;
        }
    }

    internal static IAxis? FindAxis(IList<IAxis>? axes, string axisId)
    {
        if (axes == null)
            return null;

        for (int i = 0; i < axes.Count; i++)
        {
            if (axes[i].Id == axisId)
                return axes[i];
        }
        return null;
    }

    internal static IAxis FindAxisOrDefault(IList<IAxis> axes, string axisId)
    {
        var axis = FindAxis(axes, axisId);
        if (axis != null)
            return axis;

        return axes.Count > 0 ? axes[0] : new LinearAxis { Id = axisId, VisibleRange = new DataRange(0, 100) };
    }

    private static void SyncUntouchedAxesToDefaultRange(
        IList<IAxis>? axes,
        ISet<string> touchedAxisIds,
        string defaultAxisId)
    {
        if (axes == null || axes.Count == 0)
            return;

        var defaultAxis = FindAxis(axes, defaultAxisId);
        if (defaultAxis == null)
            return;

        var defaultRange = defaultAxis.VisibleRange;
        for (int i = 0; i < axes.Count; i++)
        {
            var axis = axes[i];
            if (axis.Id == defaultAxisId || touchedAxisIds.Contains(axis.Id))
                continue;

            axis.VisibleRange = defaultRange;
        }
    }

    private static ObservableCollection<IAxis> CreateDefaultXAxes()
    {
        return new ObservableCollection<IAxis>
        {
            new LinearAxis
            {
                Id = DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 100),
                AutoRange = true
            }
        };
    }

    private static ObservableCollection<IAxis> CreateDefaultYAxes()
    {
        return new ObservableCollection<IAxis>
        {
            new LinearAxis
            {
                Id = DefaultYAxisId,
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(-1, 1),
                AutoRange = true
            }
        };
    }

    private void EnsureDefaultAxes()
    {
        if (XAxes is null)
        {
            XAxes = CreateDefaultXAxes();
        }
        else if (XAxes.Count == 0)
        {
            XAxes.Add(new LinearAxis
            {
                Id = DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 100),
                AutoRange = true
            });
        }

        if (YAxes is null)
        {
            YAxes = CreateDefaultYAxes();
        }
        else if (YAxes.Count == 0)
        {
            YAxes.Add(new LinearAxis
            {
                Id = DefaultYAxisId,
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(-1, 1),
                AutoRange = true
            });
        }
    }

    private bool HasAnyAutoRangeAxis()
        => Volatile.Read(ref _hasAnyAutoRangeAxisState) != 0;

    private void RefreshAutoRangeAxisState()
    {
        bool hasAutoRangeAxis = HasAutoRangeAxis(XAxes) || HasAutoRangeAxis(YAxes);
        Volatile.Write(ref _hasAnyAutoRangeAxisState, hasAutoRangeAxis ? 1 : 0);

        if (hasAutoRangeAxis)
            return;

        _pendingAutoRangeXAxisIds.Clear();
        _pendingAutoRangeYAxisIds.Clear();
        _pendingBackgroundAutoRangeSeries.Clear();
        Interlocked.Exchange(ref _pendingBackgroundAutoRangeRequest, 0);
        _autoRangeUpdateScheduled = false;
    }

    private static bool HasAutoRangeAxis(IEnumerable<IAxis>? axes)
    {
        if (axes == null)
            return false;

        foreach (var axis in axes)
        {
            if (axis.AutoRange)
                return true;
        }

        return false;
    }

    private void QueueAutoRangeUpdateForAllAxes()
    {
        QueueAutoRangeUpdateForAxes(XAxes);
        QueueAutoRangeUpdateForAxes(YAxes);
    }

    private void QueueAutoRangeUpdateForAxes(IEnumerable<IAxis>? axes)
    {
        if (axes == null)
            return;

        foreach (var axis in axes)
        {
            if (axis.AutoRange)
                ScheduleAutoRangeRecalcForAxis(axis.Id);
        }
    }

    private void QueueAutoRangeUpdate(IDataSeries? dataSeries)
    {
        if (dataSeries == null || !HasAnyAutoRangeAxis())
            return;

        if (!Dispatcher.CheckAccess())
        {
            QueueBackgroundAutoRangeUpdate(dataSeries);
            return;
        }

        if (Series == null)
            return;

        for (int i = 0; i < Series.Count; i++)
        {
            var series = Series[i];
            if (!series.IsVisible || !ReferenceEquals(series.DataSeries, dataSeries))
                continue;

            var xAxis = FindAxisOrDefault(XAxes, series.XAxisId);
            if (xAxis.AutoRange)
                _pendingAutoRangeXAxisIds.Add(series.XAxisId);

            var yAxis = FindAxisOrDefault(YAxes, series.YAxisId);
            if (yAxis.AutoRange)
                _pendingAutoRangeYAxisIds.Add(series.YAxisId);
        }

        if (_autoRangeUpdateScheduled
            || (_pendingAutoRangeXAxisIds.Count == 0 && _pendingAutoRangeYAxisIds.Count == 0))
        {
            return;
        }

        _autoRangeUpdateScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(FlushPendingAutoRangeUpdates));
    }

    private void QueueAutoRangeForNewSeries(IRenderableSeries renderableSeries)
    {
        var xAxisId = renderableSeries.XAxisId;
        var yAxisId = renderableSeries.YAxisId;

        if (FindAxis(XAxes, xAxisId) is { AutoRange: true })
            _pendingAutoRangeXAxisIds.Add(xAxisId);
        if (FindAxis(YAxes, yAxisId) is { AutoRange: true })
            _pendingAutoRangeYAxisIds.Add(yAxisId);

        if ((_pendingAutoRangeXAxisIds.Count > 0 || _pendingAutoRangeYAxisIds.Count > 0)
            && !_autoRangeUpdateScheduled)
        {
            _autoRangeUpdateScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(FlushPendingAutoRangeUpdates));
        }
    }

    private void QueueBackgroundAutoRangeUpdate(IDataSeries dataSeries)
    {
        if (!HasAnyAutoRangeAxis())
            return;

        _pendingBackgroundAutoRangeSeries[dataSeries] = 0;
        if (Interlocked.Exchange(ref _pendingBackgroundAutoRangeRequest, 1) != 0)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(FlushPendingBackgroundAutoRangeUpdates));
    }

    private void FlushPendingBackgroundAutoRangeUpdates()
    {
        Interlocked.Exchange(ref _pendingBackgroundAutoRangeRequest, 0);
        if (_pendingBackgroundAutoRangeSeries.IsEmpty)
            return;

        if (!HasAnyAutoRangeAxis())
        {
            _pendingBackgroundAutoRangeSeries.Clear();
            return;
        }

        var pendingSeries = _pendingBackgroundAutoRangeSeries.Keys.ToArray();
        _pendingBackgroundAutoRangeSeries.Clear();

        for (int i = 0; i < pendingSeries.Length; i++)
            QueueAutoRangeUpdate(pendingSeries[i]);
    }

    private void FlushPendingAutoRangeUpdates()
    {
        _autoRangeUpdateScheduled = false;

        var pendingXAxisIds = _pendingAutoRangeXAxisIds.ToArray();
        var pendingYAxisIds = _pendingAutoRangeYAxisIds.ToArray();
        _pendingAutoRangeXAxisIds.Clear();
        _pendingAutoRangeYAxisIds.Clear();

        for (int i = 0; i < pendingXAxisIds.Length; i++)
        {
            ApplyQueuedAutoRange(pendingXAxisIds[i], isXAxis: true);
        }

        for (int i = 0; i < pendingYAxisIds.Length; i++)
        {
            ApplyQueuedAutoRange(pendingYAxisIds[i], isXAxis: false);
        }

        SyncAxesToRange();
        SignalViewportChanged();
    }

    private void ApplyQueuedAutoRange(string axisId, bool isXAxis)
    {
        if (Series == null)
            return;

        var axis = isXAxis
            ? FindAxisOrDefault(XAxes, axisId)
            : FindAxisOrDefault(YAxes, axisId);

        if (!axis.AutoRange)
            return;

        var relatedRenderableSeries = Series
            .Where(s => s.IsVisible
                && s.DataSeries != null
                && (isXAxis ? s.XAxisId == axisId : s.YAxisId == axisId))
            .ToArray();

        if (relatedRenderableSeries.Length == 0)
            return;

        var relatedSeries = relatedRenderableSeries
            .Select(s => s.DataSeries!)
            .ToArray();

        var autoRange = axis.CalculateAutoRange(relatedSeries);
        var (clampedCore, visibleRange) = ComputeAutoFitRanges(autoRange, axis, relatedRenderableSeries, isXAxis);

        axis.CoreRange = clampedCore;
        axis.VisibleRange = visibleRange;

        if (isXAxis && axisId == DefaultXAxisId)
            XRange = visibleRange;
        else if (!isXAxis && axisId == DefaultYAxisId)
            YRange = visibleRange;
    }

    private void ApplyRenderContextAxisRangeChange(string axisId, DataRange range)
    {
        var axis = FindAxis(YAxes, axisId) ?? FindAxis(XAxes, axisId);
        if (axis == null)
            return;

        axis.VisibleRange = range;
        SignalViewportChanged();
    }
}
