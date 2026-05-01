using System.Windows.Media;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

internal sealed class LegendItem : ILegendItem
{
    private readonly RenderableSeriesBase _series;

    public LegendItem(RenderableSeriesBase series)
    {
        _series = series;
    }

    public string Title => _series.Title;
    public Color Stroke => _series.Stroke;
    public IRenderableSeries Series => _series;

    public bool IsVisible
    {
        get => _series.IsVisible;
        set => _series.IsVisible = value;
    }
}
