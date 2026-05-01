using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例默认实现，管理图例项列表并在系列集合变更时更新。
/// </summary>
public class ChartLegend : ILegend
{
    private readonly List<ILegendItem> _items = new();
    private IReadOnlyList<IRenderableSeries> _series = Array.Empty<IRenderableSeries>();

    /// <inheritdoc />
    public LegendPosition Position { get; set; } = LegendPosition.TopLeft;

    /// <inheritdoc />
    public IReadOnlyList<ILegendItem> Items => _items;

    /// <inheritdoc />
    public event Action? ItemsChanged;

    /// <inheritdoc />
    public void SetSeries(IReadOnlyList<IRenderableSeries> series)
    {
        _series = series;
        RebuildItems();
    }

    private void RebuildItems()
    {
        _items.Clear();
        foreach (var s in _series)
        {
            if (s is RenderableSeriesBase rs)
                _items.Add(new LegendItem(rs));
        }
        ItemsChanged?.Invoke();
    }
}
