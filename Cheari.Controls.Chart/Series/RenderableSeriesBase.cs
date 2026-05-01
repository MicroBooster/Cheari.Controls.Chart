using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Data;

namespace Cheari.Controls.Series;

/// <summary>
/// 可渲染系列的基础实现，封装所有系列共享的元数据与轴绑定属性。
/// </summary>
public abstract class RenderableSeriesBase : IRenderableSeries, INotifyPropertyChanged
{
    private bool _isVisible = true;
    private IDataSeries? _dataSeries;
    private Color _stroke = Colors.White;

    /// <summary>
    /// 获取或设置系列标题，用于图例显示。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置系列是否可见。变更时触发 PropertyChanged 通知图表重绘。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// 获取或设置线条颜色。变更时触发 PropertyChanged 通知图例更新。
    /// </summary>
    public virtual Color Stroke
    {
        get => _stroke;
        set => SetProperty(ref _stroke, value);
    }

    /// <summary>
    /// 获取或设置线条宽度。
    /// </summary>
    public virtual double StrokeThickness { get; set; } = 1.0;

    /// <summary>
    /// 获取或设置线条样式。
    /// </summary>
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    /// <summary>
    /// 获取或设置自定义虚线数组。如果设置了此属性，则忽略 <see cref="LineStyle"/>。
    /// </summary>
    public double[]? StrokeDashArray { get; set; }

    /// <summary>
    /// 获取或设置数据系列。此属性为渲染所必需。
    /// </summary>
    public IDataSeries DataSeries
    {
        get => _dataSeries ?? throw new InvalidOperationException("DataSeries has not been set.");
        set
        {
            if (_dataSeries == value) return;

            // 清理旧的事件监听
            if (_dataSeries != null)
            {
                _dataSeries.PropertyChanged -= OnDataSeriesPropertyChanged;
            }

            _dataSeries = value;

            // 添加新的事件监听
            if (_dataSeries != null)
            {
                _dataSeries.PropertyChanged += OnDataSeriesPropertyChanged;
            }
        }
    }

    /// <summary>
    /// 获取或设置附加信息，用于在 Legend 中显示曲线的额外数据。
    /// 此属性是 <see cref="DataSeries"/> 的 <see cref="IDataSeries.Tag"/> 的快捷访问方式。
    /// </summary>
    public object? Tag
    {
        get => DataSeries.Tag;
        set => DataSeries.Tag = value;
    }

    /// <summary>
    /// 当 DataSeries 的属性变更时触发。
    /// </summary>
    private void OnDataSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 如果 Tag 属性变更，转发事件给 LegendItem
        if (e.PropertyName == nameof(IDataSeries.Tag))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tag)));
        }
    }

    /// <summary>
    /// 获取或设置关联的 X 轴 ID。
    /// </summary>
    public string XAxisId { get; set; } = Chart.DefaultXAxisId;

    /// <summary>
    /// 获取或设置关联的 Y 轴 ID。
    /// </summary>
    public string YAxisId { get; set; } = Chart.DefaultYAxisId;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 属性变更辅助方法，触发 PropertyChanged 事件。
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
