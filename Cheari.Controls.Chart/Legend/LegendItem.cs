using System.ComponentModel;
using System.Windows.Media;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

internal sealed class LegendItem : ILegendItem
{
    private readonly RenderableSeriesBase _series;

    public LegendItem(RenderableSeriesBase series)
    {
        _series = series;
        _series.PropertyChanged += OnSeriesPropertyChanged;
    }

    public string Title => _series.Title;
    public Color Stroke => _series.Stroke;
    public Color Fill => GetFillColor();
    public IRenderableSeries Series => _series;

    public bool IsVisible
    {
        get => _series.IsVisible;
        set => _series.IsVisible = value;
    }

    public object? Tag => _series.Tag;

    public event PropertyChangedEventHandler? PropertyChanged;

    private Color GetFillColor()
    {
        if (_series is Series.Types.AreaRenderableSeries area)
            return area.Fill;
        if (_series is Series.Types.BarRenderableSeries bar)
            return bar.Fill;
        return _series.Stroke;
    }

    private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RenderableSeriesBase.IsVisible)
            || e.PropertyName == nameof(RenderableSeriesBase.Title)
            || e.PropertyName == nameof(RenderableSeriesBase.Stroke)
            || e.PropertyName == nameof(RenderableSeriesBase.Tag)
            || e.PropertyName == "Fill")
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
