using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cheari.Controls.Modifiers;

/// <summary>十字光标修饰器，跟随鼠标显示十字线和坐标标签。</summary>
public class CrosshairModifier : ChartModifierBase
{
    private Line? _verticalLine;
    private Line? _horizontalLine;
    private Border? _xLabel;
    private TextBlock? _xLabelText;
    private Border? _yLabel;
    private TextBlock? _yLabelText;

    /// <inheritdoc />
    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Context == null || OverlayCanvas == null)
            return;

        var pos = GetRelativePosition(e);
        int w = Context.ViewportWidth;
        int h = Context.ViewportHeight;
        if (w <= 0 || h <= 0) return;

        double dataX = Context.ScreenToDataX(pos.X, w);
        double dataY = Context.ScreenToDataY(pos.Y, h);

        EnsureVisuals();

        _verticalLine!.X1 = pos.X;
        _verticalLine.Y1 = 0;
        _verticalLine.X2 = pos.X;
        _verticalLine.Y2 = h;
        _verticalLine.Visibility = Visibility.Visible;

        _horizontalLine!.X1 = 0;
        _horizontalLine.Y1 = pos.Y;
        _horizontalLine.X2 = w;
        _horizontalLine.Y2 = pos.Y;
        _horizontalLine.Visibility = Visibility.Visible;

        _xLabelText!.Text = $"{dataX:F2}";
        Canvas.SetLeft(_xLabel!, pos.X - 25);
        Canvas.SetTop(_xLabel!, h - 20);
        _xLabel!.Visibility = Visibility.Visible;

        _yLabelText!.Text = $"{dataY:F2}";
        Canvas.SetLeft(_yLabel!, 4);
        Canvas.SetTop(_yLabel!, pos.Y - 12);
        _yLabel!.Visibility = Visibility.Visible;

        e.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        HideAll();
    }

    private void EnsureVisuals()
    {
        if (_verticalLine != null) return;

        var dashStyle = new DoubleCollection { 4, 3 };

        _verticalLine = new Line
        {
            Stroke = new SolidColorBrush(Color.FromArgb(120, 180, 180, 180)),
            StrokeThickness = 1,
            StrokeDashArray = dashStyle,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        _horizontalLine = new Line
        {
            Stroke = new SolidColorBrush(Color.FromArgb(120, 180, 180, 180)),
            StrokeThickness = 1,
            StrokeDashArray = dashStyle,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        _xLabelText = new TextBlock { Foreground = Brushes.White, FontSize = 10 };
        _xLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 35, 45)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(3, 1, 3, 1),
            Child = _xLabelText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        _yLabelText = new TextBlock { Foreground = Brushes.White, FontSize = 10 };
        _yLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 35, 45)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(3, 1, 3, 1),
            Child = _yLabelText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        OverlayCanvas!.Children.Add(_verticalLine);
        OverlayCanvas.Children.Add(_horizontalLine);
        OverlayCanvas.Children.Add(_xLabel);
        OverlayCanvas.Children.Add(_yLabel);
    }

    private void HideAll()
    {
        if (_verticalLine != null) _verticalLine.Visibility = Visibility.Collapsed;
        if (_horizontalLine != null) _horizontalLine.Visibility = Visibility.Collapsed;
        if (_xLabel != null) _xLabel.Visibility = Visibility.Collapsed;
        if (_yLabel != null) _yLabel.Visibility = Visibility.Collapsed;
    }

    /// <inheritdoc />
    public override void OnDetached()
    {
        base.OnDetached();
        if (OverlayCanvas == null) return;
        if (_verticalLine != null) OverlayCanvas.Children.Remove(_verticalLine);
        if (_horizontalLine != null) OverlayCanvas.Children.Remove(_horizontalLine);
        if (_xLabel != null) OverlayCanvas.Children.Remove(_xLabel);
        if (_yLabel != null) OverlayCanvas.Children.Remove(_yLabel);
        _verticalLine = null;
        _horizontalLine = null;
        _xLabel = null;
        _yLabel = null;
    }

    private Point GetRelativePosition(MouseEventArgs e)
        => Context?.InputElement is { } inputElement
            ? e.GetPosition(inputElement)
            : e.GetPosition(null);
}
