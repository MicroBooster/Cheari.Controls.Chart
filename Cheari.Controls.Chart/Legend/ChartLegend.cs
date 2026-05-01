using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图表图例实现类，负责管理图例项的显示和数据绑定。
/// </summary>
public class ChartLegend : ILegend, INotifyPropertyChanged
{
    private readonly List<ILegendItem> _items = new();
    private IReadOnlyList<IRenderableSeries> _series = Array.Empty<IRenderableSeries>();
    private INotifyCollectionChanged? _seriesAsNotifiable;
    private LegendPosition _position = LegendPosition.ExternalRight;
    private LegendOrientation _orientation = LegendOrientation.Vertical;
    private HorizontalAlignment _hAlign = HorizontalAlignment.Center;
    private VerticalAlignment _vAlign = VerticalAlignment.Center;

    /// <summary>
    /// 获取或设置图例的位置。
    /// </summary>
    public LegendPosition Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            ApplyDefaultAlignment();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HorizontalAlignment));
            OnPropertyChanged(nameof(VerticalAlignment));
        }
    }

    /// <summary>
    /// 获取或设置图例的排列方向。
    /// </summary>
    public LegendOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value) return;
            _orientation = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 获取或设置图例的水平对齐方式。
    /// </summary>
    public HorizontalAlignment HorizontalAlignment
    {
        get => _hAlign;
        set
        {
            if (_hAlign == value) return;
            _hAlign = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 获取或设置图例的垂直对齐方式。
    /// </summary>
    public VerticalAlignment VerticalAlignment
    {
        get => _vAlign;
        set
        {
            if (_vAlign == value) return;
            _vAlign = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 获取图例项的只读列表。
    /// </summary>
    public IReadOnlyList<ILegendItem> Items => _items;

    /// <summary>
    /// 当图例项集合发生变化时触发。
    /// </summary>
    public event Action? ItemsChanged;

    /// <summary>
    /// 当属性值发生变化时触发。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 设置要显示的系列集合。
    /// </summary>
    /// <param name="series">系列集合。</param>
    public void SetSeries(IReadOnlyList<IRenderableSeries> series)
    {
        // 清理旧的事件监听
        if (_seriesAsNotifiable != null)
            _seriesAsNotifiable.CollectionChanged -= OnSeriesCollectionChanged;

        _series = series;

        // 注册新的事件监听
        _seriesAsNotifiable = series as INotifyCollectionChanged;
        if (_seriesAsNotifiable != null)
            _seriesAsNotifiable.CollectionChanged += OnSeriesCollectionChanged;

        RebuildItems();
    }

    private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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

    private void ApplyDefaultAlignment()
    {
        switch (_position)
        {
            case LegendPosition.InternalTop:
                _hAlign = HorizontalAlignment.Center;
                _vAlign = VerticalAlignment.Top;
                break;
            case LegendPosition.InternalBottom:
                _hAlign = HorizontalAlignment.Center;
                _vAlign = VerticalAlignment.Bottom;
                break;
            case LegendPosition.InternalLeft:
                _hAlign = HorizontalAlignment.Left;
                _vAlign = VerticalAlignment.Center;
                break;
            case LegendPosition.InternalRight:
                _hAlign = HorizontalAlignment.Right;
                _vAlign = VerticalAlignment.Center;
                break;
            case LegendPosition.ExternalTop:
                _hAlign = HorizontalAlignment.Stretch;
                _vAlign = VerticalAlignment.Top;
                break;
            case LegendPosition.ExternalBottom:
                _hAlign = HorizontalAlignment.Stretch;
                _vAlign = VerticalAlignment.Bottom;
                break;
            case LegendPosition.ExternalLeft:
                _hAlign = HorizontalAlignment.Left;
                _vAlign = VerticalAlignment.Stretch;
                break;
            case LegendPosition.ExternalRight:
                _hAlign = HorizontalAlignment.Right;
                _vAlign = VerticalAlignment.Stretch;
                break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
