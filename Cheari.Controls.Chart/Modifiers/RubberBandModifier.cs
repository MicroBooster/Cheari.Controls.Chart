using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Cheari.Controls.Core;

namespace Cheari.Controls.Modifiers;

/// <summary>框选放大修饰器，拖拽绘制矩形区域后放大到该区域。</summary>
public class RubberBandModifier : ChartModifierBase
{
    private Rectangle? _rubberBand;
    private Point _startPoint;
    private bool _isDragging;

    /// <inheritdoc />
    public override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.LeftButton != MouseButtonState.Pressed || Context == null || OverlayCanvas == null)
            return;

        _isDragging = true;
        _startPoint = GetRelativePosition(e);
        EnsureVisuals();
        e.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging || _rubberBand == null || Context == null)
            return;

        var current = GetRelativePosition(e);
        double x = Math.Min(_startPoint.X, current.X);
        double y = Math.Min(_startPoint.Y, current.Y);
        double w = Math.Abs(current.X - _startPoint.X);
        double h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(_rubberBand, x);
        Canvas.SetTop(_rubberBand, y);
        _rubberBand.Width = w;
        _rubberBand.Height = h;
        _rubberBand.Visibility = w > 2 && h > 2 ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_isDragging || Context == null)
            return;

        _isDragging = false;

        if (_rubberBand is { Visibility: Visibility.Visible })
        {
            var endPoint = GetRelativePosition(e);
            int vw = Context.ViewportWidth;
            int vh = Context.ViewportHeight;
            if (vw > 0 && vh > 0)
            {
                double x1 = Math.Min(_startPoint.X, endPoint.X);
                double x2 = Math.Max(_startPoint.X, endPoint.X);
                double y1 = Math.Min(_startPoint.Y, endPoint.Y);
                double y2 = Math.Max(_startPoint.Y, endPoint.Y);

                double dataX1 = Context.ScreenToDataX(x1, vw);
                double dataX2 = Context.ScreenToDataX(x2, vw);
                double dataY1 = Context.ScreenToDataY(y2, vh);
                double dataY2 = Context.ScreenToDataY(y1, vh);

                Context.SetRange(
                    new DataRange(Math.Min(dataX1, dataX2), Math.Max(dataX1, dataX2)),
                    new DataRange(Math.Min(dataY1, dataY2), Math.Max(dataY1, dataY2)));
            }

            _rubberBand.Visibility = Visibility.Collapsed;
        }

        e.Handled = true;
    }

    private void EnsureVisuals()
    {
        if (_rubberBand != null) return;

        _rubberBand = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 150, 255)),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 180, 255)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        OverlayCanvas!.Children.Add(_rubberBand);
    }

    /// <inheritdoc />
    public override void OnDetached()
    {
        base.OnDetached();
        if (_rubberBand != null && OverlayCanvas != null)
            OverlayCanvas.Children.Remove(_rubberBand);
        _rubberBand = null;
    }

    private Point GetRelativePosition(MouseEventArgs e)
        => Context?.InputElement is { } inputElement
            ? e.GetPosition(inputElement)
            : e.GetPosition(null);
}
