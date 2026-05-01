using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

/// <summary>标注抽象基类。</summary>
public abstract class AnnotationBase : IAnnotation, INotifyPropertyChanged
{
    private double _x1, _y1, _x2, _y2;
    private string? _text;
    private Brush _fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
    private Pen _stroke = new(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 2);
    private AnnotationCoordinateMode _coordinateMode;
    private bool _isSelected;
    private bool _isVisible = true;
    private bool _isLocked;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public double X1 { get => _x1; set => SetProperty(ref _x1, value); }
    /// <inheritdoc />
    public double Y1 { get => _y1; set => SetProperty(ref _y1, value); }
    /// <inheritdoc />
    public double X2 { get => _x2; set => SetProperty(ref _x2, value); }
    /// <inheritdoc />
    public double Y2 { get => _y2; set => SetProperty(ref _y2, value); }
    /// <inheritdoc />
    public string? Text { get => _text; set => SetProperty(ref _text, value); }
    /// <inheritdoc />
    public Brush Fill { get => _fill; set => SetProperty(ref _fill, value); }
    /// <inheritdoc />
    public Pen Stroke { get => _stroke; set => SetProperty(ref _stroke, value); }
    /// <inheritdoc />
    public AnnotationCoordinateMode CoordinateMode { get => _coordinateMode; set => SetProperty(ref _coordinateMode, value); }
    /// <inheritdoc />
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    /// <inheritdoc />
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    /// <inheritdoc />
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

    /// <summary>属性变更辅助方法。</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <inheritdoc />
    public abstract FrameworkElement CreateVisual();
    /// <inheritdoc />
    public abstract void UpdateVisual(FrameworkElement element);
}
