using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Annotations;

public class RectangleAnnotation : AnnotationBase
{
    public override FrameworkElement CreateVisual()
    {
        var rect = new Rectangle
        {
            Fill = Fill.Clone(),
            Stroke = Stroke.Brush?.Clone(),
            StrokeThickness = Stroke.Thickness,
            RadiusX = 2,
            RadiusY = 2
        };
        Canvas.SetLeft(rect, 0);
        Canvas.SetTop(rect, 0);
        rect.Width = 0;
        rect.Height = 0;

        var label = new TextBlock
        {
            Text = Text ?? "",
            Foreground = Stroke.Brush?.Clone() ?? Brushes.White,
            FontSize = 12,
            Margin = new Thickness(4, 2, 4, 2),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(label, 4);
        Canvas.SetTop(label, 2);

        var canvas = new Canvas();
        canvas.Children.Add(rect);
        canvas.Children.Add(label);
        return canvas;
    }

    public override void UpdateVisual(FrameworkElement element)
    {
        if (element is Canvas canvas && canvas.Children.Count >= 2)
        {
            if (canvas.Children[0] is Rectangle rect)
            {
                rect.Fill = Fill.Clone();
                rect.Stroke = Stroke.Brush?.Clone();
                rect.StrokeThickness = Stroke.Thickness;
            }
            if (canvas.Children[1] is TextBlock label)
            {
                label.Text = Text ?? "";
                label.Foreground = Stroke.Brush?.Clone() ?? Brushes.White;
            }
            canvas.Visibility = IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
