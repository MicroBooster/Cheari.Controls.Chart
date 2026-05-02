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
    }

    private void UnsubscribeFromAxis(IAxis axis)
    {
        axis.VisibleRangeChanged -= OnAxisVisibleRangeChanged;

        if (axis is not AxisBase axisBase)
            return;

        s_axisPlacementDescriptor?.RemoveValueChanged(axisBase, OnAxisPlacementChanged);
        s_axisForegroundDescriptor?.RemoveValueChanged(axisBase, OnAxisForegroundChanged);
        s_axisAutoRangeDescriptor?.RemoveValueChanged(axisBase, OnAxisAutoRangeChanged);
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
        if (XAxes is null || XAxes.Count == 0)
        {
            XAxes = CreateDefaultXAxes();
        }

        if (YAxes is null || YAxes.Count == 0)
        {
            YAxes = CreateDefaultYAxes();
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

        var relatedSeries = Series
            .Where(s => s.IsVisible
                && s.DataSeries != null
                && (isXAxis ? s.XAxisId == axisId : s.YAxisId == axisId))
            .Select(s => s.DataSeries!)
            .ToArray();

        if (relatedSeries.Length == 0)
            return;

        if (!isXAxis)
        {
            var defaultXAxis = FindAxis(XAxes, DefaultXAxisId);
            if (defaultXAxis != null && !defaultXAxis.AutoRange)
            {
                var visibleRange = ComputeVisibleYRange(
                    relatedSeries, defaultXAxis.VisibleRange, axis);
                YRange = visibleRange;
                return;
            }
        }

        var range = axis.CalculateAutoRange(relatedSeries);
        if (isXAxis)
        {
            if (axis.Id == DefaultXAxisId)
                XRange = range;
            else
                axis.VisibleRange = range;
        }
        else
        {
            if (axis.Id == DefaultYAxisId)
                YRange = range;
            else
                axis.VisibleRange = range;
        }
    }

    private DataRange ComputeVisibleYRange(
        IDataSeries[] dataSeries,
        DataRange visibleXRange,
        IAxis yAxis)
    {
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        bool hasData = false;

        for (int s = 0; s < dataSeries.Length; s++)
        {
            var series = dataSeries[s];
            DataFrame? frame = null;

            if (_renderLoop != null
                && _renderLoop.TryGetLatestFrame(series, series.Version, out var cachedFrame)
                && cachedFrame != null)
            {
                frame = cachedFrame;
            }
            else if (series is IDataFrameProvider provider)
            {
                frame = provider.CreateFrame();
            }

            if (frame == null || frame.XValues == null || frame.YValues == null || frame.Count < 1)
                continue;

            int startIdx = BinarySearchFirstGreaterOrEqual(
                new ReadOnlySpan<float>(frame.XValues, 0, frame.Count),
                (float)visibleXRange.Min);

            int endIdx = BinarySearchLastLessOrEqual(
                new ReadOnlySpan<float>(frame.XValues, 0, frame.Count),
                (float)visibleXRange.Max);

            if (endIdx < startIdx)
                continue;

            for (int i = startIdx; i <= endIdx && i < frame.Count; i++)
            {
                float y = frame.YValues[i];
                if (float.IsNaN(y) || float.IsInfinity(y))
                    continue;

                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                hasData = true;
            }
        }

        return hasData
            ? AxisBase.CalculateStaticNiceRange(minY, maxY)
            : new DataRange(-1, 1);
    }

    private static int BinarySearchFirstGreaterOrEqual(ReadOnlySpan<float> values, float target)
    {
        int lo = 0;
        int hi = values.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (values[mid] < target)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static int BinarySearchLastLessOrEqual(ReadOnlySpan<float> values, float target)
    {
        int lo = 0;
        int hi = values.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (values[mid] > target)
                hi = mid - 1;
            else
                lo = mid;
        }
        return lo;
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
