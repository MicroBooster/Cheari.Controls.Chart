using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

/// <summary>文本标注，在图表上显示文本。</summary>
public class TextAnnotation : AnnotationBase
{
    /// <summary>标注标签。</summary>
    public string? Label { get; set; }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
