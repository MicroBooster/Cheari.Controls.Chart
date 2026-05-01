using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Series;

namespace Cheari.Controls.Synchronization;

/// <summary>轴组默认实现，管理注册的图表并在范围变更时同步其他图表。</summary>
public class SynchronizedAxisGroup : IAxisGroup
{
    private readonly List<Chart> _charts = new();
    private bool _isNotifying;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>创建指定ID的轴组。</summary>
    public SynchronizedAxisGroup(string id)
    {
        Id = id;
    }

    /// <inheritdoc />
    public void Register(Chart chart)
    {
        if (!_charts.Contains(chart))
            _charts.Add(chart);
    }

    /// <inheritdoc />
    public void Unregister(Chart chart)
    {
        _charts.Remove(chart);
    }

    /// <inheritdoc />
    public void NotifyRangeChanged(Chart source, string axisId, DataRange newRange)
    {
        if (_isNotifying) return;
        _isNotifying = true;

        try
        {
            foreach (var chart in _charts)
            {
                if (ReferenceEquals(chart, source)) continue;

                if (axisId == Chart.DefaultXAxisId)
                {
                    var xAxes = chart.XAxes;
                    if (xAxes != null)
                    {
                        foreach (var axis in xAxes)
                        {
                            if (axis is AxisBase axBase)
                                axBase.VisibleRange = newRange;
                        }
                    }
                }
                else if (axisId == Chart.DefaultYAxisId)
                {
                    var yAxes = chart.YAxes;
                    if (yAxes != null)
                    {
                        foreach (var axis in yAxes)
                        {
                            if (axis is AxisBase axBase)
                                axBase.VisibleRange = newRange;
                        }
                    }
                }
            }
        }
        finally
        {
            _isNotifying = false;
        }
    }
}
