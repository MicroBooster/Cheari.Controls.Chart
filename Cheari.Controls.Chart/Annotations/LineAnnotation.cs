using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Annotations;

/// <summary>线条标注，在图表上绘制一条线。</summary>
public class LineAnnotation : AnnotationBase
{
    /// <inheritdoc />
    public override FrameworkElement CreateVisual()
    {
        var canvas = new Canvas();
        var line = new Line
        {
            Stroke = Stroke.Brush?.Clone() ?? Brushes.White,
            StrokeThickness = Stroke.Thickness,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            X1 = 0, Y1 = 0, X2 = 0, Y2 = 0
        };
        canvas.Children.Add(line);
        return canvas;
    }

    /// <inheritdoc />
    public override void UpdateVisual(FrameworkElement element)
    {
        if (element is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line line)
        {
            line.Stroke = Stroke.Brush?.Clone() ?? Brushes.White;
            line.StrokeThickness = Stroke.Thickness;
            canvas.Visibility = IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
