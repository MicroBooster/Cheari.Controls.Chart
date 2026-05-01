using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

internal sealed class LegendItem : ILegendItem
{
    private readonly RenderableSeriesBase _series;
    private bool _settingIsVisible;

    public LegendItem(RenderableSeriesBase series)
    {
        _series = series;
        _series.PropertyChanged += OnSeriesPropertyChanged;
    }

    public string Title => _series.Title;
    public Color Stroke => _series.Stroke;
    public IRenderableSeries Series => _series;

    public bool IsVisible
    {
        get => _series.IsVisible;
        set
        {
            _settingIsVisible = true;
            try
            {
                _series.IsVisible = value;
            }
            finally
            {
                _settingIsVisible = false;
            }
        }
    }

    public object? Tag => _series.Tag;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_settingIsVisible && e.PropertyName == nameof(RenderableSeriesBase.IsVisible))
            return;

        if (e.PropertyName == nameof(RenderableSeriesBase.IsVisible)
            || e.PropertyName == nameof(RenderableSeriesBase.Title)
            || e.PropertyName == nameof(RenderableSeriesBase.Stroke)
            || e.PropertyName == nameof(RenderableSeriesBase.Tag))
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
