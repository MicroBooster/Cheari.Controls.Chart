using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

public class TextAnnotation : AnnotationBase
{
    public string? Label { get; set; }

    public override FrameworkElement CreateVisual()
    {
        var tb = new TextBlock
        {
            Text = Label ?? Text ?? "Text",
            Foreground = Stroke.Brush?.Clone() ?? Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(tb, 0);
        Canvas.SetTop(tb, 0);
        return tb;
    }

    public override void UpdateVisual(FrameworkElement element)
    {
        if (element is TextBlock tb)
        {
            tb.Text = Label ?? Text ?? "Text";
            tb.Foreground = Stroke.Brush?.Clone() ?? Brushes.White;
            tb.Visibility = IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
