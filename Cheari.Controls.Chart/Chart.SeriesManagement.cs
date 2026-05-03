using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Series;

namespace Cheari.Controls;

/// <summary>
/// Chart 的系列管理逻辑：系列订阅/取消订阅、数据变更处理。
/// </summary>
public partial class Chart
{
    private readonly Dictionary<IDataSeries, int> _dataSeriesSubscriptions = new();

    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            if (e.OldValue is ObservableCollection<IRenderableSeries> oldSeries)
            {
                oldSeries.CollectionChanged -= chart.OnSeriesCollectionChanged;
                chart.UnsubscribeFromSeries(oldSeries);
            }

            if (e.NewValue is ObservableCollection<IRenderableSeries> newSeries)
            {
                newSeries.CollectionChanged += chart.OnSeriesCollectionChanged;
                chart.SubscribeToSeries(newSeries);

                for (int i = 0; i < newSeries.Count; i++)
                {
                    if (newSeries[i].DataSeries != null && newSeries[i].DataSeries!.Count > 0)
                        chart.QueueAutoRangeForNewSeries(newSeries[i]);
                }
            }

            chart.UpdateLegendSeries();
            chart.UpdateRenderContext();
            chart.MarkDirty();
        }
    }

    /// <summary>
    /// 处理系列集合的变更通知，包括添加、移除、重置等操作。
    /// </summary>
    private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnsubscribeFromAllDataSeries();
            if (Series != null)
                SubscribeToSeries(Series);

            UpdateLegendSeries();
            MarkDirty();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (IRenderableSeries series in e.OldItems)
            {
                UnsubscribeFromSeries(series);
                if (series.DataSeries != null)
                    QueueAutoRangeUpdate(series.DataSeries);
            }
        }

        if (e.NewItems != null)
        {
            foreach (IRenderableSeries series in e.NewItems)
            {
                SubscribeToSeries(series);
                if (series.DataSeries != null && series.DataSeries.Count > 0)
                    QueueAutoRangeForNewSeries(series);
            }
        }

        UpdateLegendSeries();
        MarkDirty();
    }

    /// <summary>处理 DataSeries 集合变更，触发 AutoRange 更新和渲染循环快照刷新。</summary>
    private void OnDataSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueAutoRangeUpdate(sender as IDataSeries);

        if (sender is IDataSeries dataSeries)
            _renderLoop?.SignalDataChanged(dataSeries);

        NotifyStreamingActivityFromAnyThread();
    }

    /// <summary>处理 DataSeries 范围变更，触发 AutoRange 更新。</summary>
    private void OnDataSeriesRangeChanged(object? sender, EventArgs e)
    {
        QueueAutoRangeUpdate(sender as IDataSeries);

        if (_renderLoop == null)
            RequestRedrawFromAnyThread();
    }

    /// <summary>处理系列属性变更，目前仅监听 IsVisible 变更。</summary>
    private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IRenderableSeries.IsVisible))
        {
            if (sender is IRenderableSeries series)
                QueueAutoRangeForNewSeries(series);
            MarkDirty();
        }
        else if (e.PropertyName == nameof(IRenderableSeries.Stroke))
        {
            MarkDirty();
        }
    }

    /// <summary>订阅系列集合中的所有系列。</summary>
    private void SubscribeToSeries(IEnumerable<IRenderableSeries> seriesCollection)
    {
        foreach (var series in seriesCollection)
            SubscribeToSeries(series);
    }

    /// <summary>订阅单个系列：监听 PropertyChanged 和对应的 DataSeries 事件。</summary>
    private void SubscribeToSeries(IRenderableSeries series)
    {
        if (series is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnSeriesPropertyChanged;

        if (series.DataSeries == null)
            return;

        if (_dataSeriesSubscriptions.TryGetValue(series.DataSeries, out int count))
        {
            _dataSeriesSubscriptions[series.DataSeries] = count + 1;
            return;
        }

        series.DataSeries.CollectionChanged += OnDataSeriesCollectionChanged;
        series.DataSeries.RangeChanged += OnDataSeriesRangeChanged;
        _dataSeriesSubscriptions.Add(series.DataSeries, 1);
    }

    /// <summary>取消订阅系列集合中的所有系列。</summary>
    private void UnsubscribeFromSeries(IEnumerable<IRenderableSeries> seriesCollection)
    {
        foreach (var series in seriesCollection)
            UnsubscribeFromSeries(series);
    }

    /// <summary>取消订阅单个系列：移除所有事件监听并清理渲染循环数据。</summary>
    private void UnsubscribeFromSeries(IRenderableSeries series)
    {
        if (series is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnSeriesPropertyChanged;

        if (series.DataSeries == null)
            return;

        if (!_dataSeriesSubscriptions.TryGetValue(series.DataSeries, out int count))
            return;

        if (count > 1)
        {
            _dataSeriesSubscriptions[series.DataSeries] = count - 1;
            return;
        }

        series.DataSeries.CollectionChanged -= OnDataSeriesCollectionChanged;
        series.DataSeries.RangeChanged -= OnDataSeriesRangeChanged;
        _dataSeriesSubscriptions.Remove(series.DataSeries);
        _renderLoop?.RemoveSeries(series.DataSeries);
    }

    /// <summary>取消订阅所有 DataSeries 的事件并清空订阅字典。</summary>
    private void UnsubscribeFromAllDataSeries()
    {
        foreach (var dataSeries in _dataSeriesSubscriptions.Keys.ToArray())
        {
            dataSeries.CollectionChanged -= OnDataSeriesCollectionChanged;
            dataSeries.RangeChanged -= OnDataSeriesRangeChanged;
            _renderLoop?.RemoveSeries(dataSeries);
        }

        _dataSeriesSubscriptions.Clear();

        if (Series != null)
        {
            foreach (var series in Series)
            {
                if (series is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnSeriesPropertyChanged;
            }
        }
    }
}
