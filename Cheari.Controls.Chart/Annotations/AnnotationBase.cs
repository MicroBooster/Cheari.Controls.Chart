using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public double X1 { get => _x1; set => SetProperty(ref _x1, value); }
    public double Y1 { get => _y1; set => SetProperty(ref _y1, value); }
    public double X2 { get => _x2; set => SetProperty(ref _x2, value); }
    public double Y2 { get => _y2; set => SetProperty(ref _y2, value); }
    public string? Text { get => _text; set => SetProperty(ref _text, value); }
    public Brush Fill { get => _fill; set => SetProperty(ref _fill, value); }
    public Pen Stroke { get => _stroke; set => SetProperty(ref _stroke, value); }
    public AnnotationCoordinateMode CoordinateMode { get => _coordinateMode; set => SetProperty(ref _coordinateMode, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public abstract FrameworkElement CreateVisual();
    public abstract void UpdateVisual(FrameworkElement element);
}
