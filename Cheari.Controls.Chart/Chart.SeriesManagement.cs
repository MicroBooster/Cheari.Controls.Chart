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
        }

        chart.UpdateLegendSeries();
        chart.UpdateRenderContext();
        chart.MarkDirty();
        }
    }

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
                UnsubscribeFromSeries(series);
        }

        if (e.NewItems != null)
        {
            foreach (IRenderableSeries series in e.NewItems)
                SubscribeToSeries(series);
        }

        if (e.Action == NotifyCollectionChangedAction.Reset && Series != null)
        {
            SubscribeToSeries(Series);
        }

        UpdateLegendSeries();
        MarkDirty();
    }

    private void OnDataSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueAutoRangeUpdate(sender as IDataSeries);

        if (sender is IDataSeries dataSeries)
            _renderLoop?.SignalDataChanged(dataSeries);

        NotifyStreamingActivityFromAnyThread();
    }

    private void OnDataSeriesRangeChanged(object? sender, EventArgs e)
    {
        QueueAutoRangeUpdate(sender as IDataSeries);

        if (_renderLoop == null)
            RequestRedrawFromAnyThread();
    }

    private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IRenderableSeries.IsVisible))
            MarkDirty();
    }

    private void SubscribeToSeries(IEnumerable<IRenderableSeries> seriesCollection)
    {
        foreach (var series in seriesCollection)
            SubscribeToSeries(series);
    }

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

    private void UnsubscribeFromSeries(IEnumerable<IRenderableSeries> seriesCollection)
    {
        foreach (var series in seriesCollection)
            UnsubscribeFromSeries(series);
    }

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
