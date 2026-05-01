using System.Windows;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

public enum AnnotationCoordinateMode
{
    Data,
    Relative
}

public interface IAnnotation
{
    double X1 { get; set; }
    double Y1 { get; set; }
    double X2 { get; set; }
    double Y2 { get; set; }
    string? Text { get; set; }
    Brush Fill { get; set; }
    Pen Stroke { get; set; }
    AnnotationCoordinateMode CoordinateMode { get; set; }
    bool IsSelected { get; set; }
    bool IsVisible { get; set; }
    bool IsLocked { get; set; }

    FrameworkElement CreateVisual();
    void UpdateVisual(FrameworkElement element);
}
