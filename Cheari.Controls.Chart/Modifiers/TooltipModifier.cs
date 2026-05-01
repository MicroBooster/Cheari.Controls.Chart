using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cheari.Controls.Modifiers;

/// <summary>悬停提示修饰器，在鼠标位置显示当前数据坐标。</summary>
public class TooltipModifier : ChartModifierBase
{
    private Border? _tooltip;
    private TextBlock? _textBlock;

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

        _textBlock!.Text = $"X: {dataX:F3}\nY: {dataY:F3}";

        double tipX = pos.X + 12;
        double tipY = pos.Y - 30;
        if (tipX + 120 > w) tipX = pos.X - 130;
        if (tipY < 0) tipY = pos.Y + 12;

        Canvas.SetLeft(_tooltip!, tipX);
        Canvas.SetTop(_tooltip!, tipY);
        _tooltip!.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    /// <inheritdoc />
    public override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        HideTooltip();
    }

    private void EnsureVisuals()
    {
        if (_tooltip != null) return;

        _textBlock = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas, Courier New"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6, 3, 6, 3)
        };

        _tooltip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 25, 35)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 100, 150, 200)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = _textBlock,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        OverlayCanvas!.Children.Add(_tooltip);
    }

    private void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.Visibility = Visibility.Collapsed;
    }

    /// <inheritdoc />
    public override void OnDetached()
    {
        base.OnDetached();
        if (_tooltip != null && OverlayCanvas != null)
            OverlayCanvas.Children.Remove(_tooltip);
        _tooltip = null;
        _textBlock = null;
    }

    private Point GetRelativePosition(MouseEventArgs e)
        => Context?.InputElement is { } inputElement
            ? e.GetPosition(inputElement)
            : e.GetPosition(null);
}
