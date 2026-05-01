using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;

namespace Cheari.Controls.Annotations;

public class AnnotationsPanel : Canvas
{
    public static readonly DependencyProperty AnnotationsProperty =
        DependencyProperty.Register(nameof(Annotations), typeof(ObservableCollection<IAnnotation>),
            typeof(AnnotationsPanel), new PropertyMetadata(null, OnAnnotationsChanged));

    public static readonly DependencyProperty XAxesProperty =
        DependencyProperty.Register(nameof(XAxes), typeof(ObservableCollection<IAxis>),
            typeof(AnnotationsPanel), new PropertyMetadata(null, OnAxesChanged));

    public static readonly DependencyProperty YAxesProperty =
        DependencyProperty.Register(nameof(YAxes), typeof(ObservableCollection<IAxis>),
            typeof(AnnotationsPanel), new PropertyMetadata(null, OnAxesChanged));

    public static readonly DependencyProperty PlotAreaWidthProperty =
        DependencyProperty.Register(nameof(PlotAreaWidth), typeof(double), typeof(AnnotationsPanel),
            new PropertyMetadata(0.0, OnLayoutChanged));

    public static readonly DependencyProperty PlotAreaHeightProperty =
        DependencyProperty.Register(nameof(PlotAreaHeight), typeof(double), typeof(AnnotationsPanel),
            new PropertyMetadata(0.0, OnLayoutChanged));

    private readonly Dictionary<IAnnotation, FrameworkElement> _visualMap = new();

    public ObservableCollection<IAnnotation>? Annotations
    {
        get => (ObservableCollection<IAnnotation>?)GetValue(AnnotationsProperty);
        set => SetValue(AnnotationsProperty, value);
    }

    public ObservableCollection<IAxis>? XAxes
    {
        get => (ObservableCollection<IAxis>?)GetValue(XAxesProperty);
        set => SetValue(XAxesProperty, value);
    }

    public ObservableCollection<IAxis>? YAxes
    {
        get => (ObservableCollection<IAxis>?)GetValue(YAxesProperty);
        set => SetValue(YAxesProperty, value);
    }

    public double PlotAreaWidth
    {
        get => (double)GetValue(PlotAreaWidthProperty);
        set => SetValue(PlotAreaWidthProperty, value);
    }

    public double PlotAreaHeight
    {
        get => (double)GetValue(PlotAreaHeightProperty);
        set => SetValue(PlotAreaHeightProperty, value);
    }

    private static void OnAnnotationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnnotationsPanel panel)
        {
            panel.UnwireAnnotations(e.OldValue as ObservableCollection<IAnnotation>);
            panel.WireAnnotations(e.NewValue as ObservableCollection<IAnnotation>);
            panel.RebuildAll();
        }
    }

    private static void OnAxesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as AnnotationsPanel)?.RebuildAll();
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as AnnotationsPanel)?.LayoutAll();
    }

    private void WireAnnotations(ObservableCollection<IAnnotation>? annotations)
    {
        if (annotations == null) return;
        annotations.CollectionChanged += OnAnnotationsCollectionChanged;
    }

    private void UnwireAnnotations(ObservableCollection<IAnnotation>? annotations)
    {
        if (annotations == null) return;
        annotations.CollectionChanged -= OnAnnotationsCollectionChanged;
    }

    private void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearVisuals();
            RebuildAll();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (IAnnotation a in e.OldItems)
                RemoveVisual(a);
        }

        if (e.NewItems != null)
        {
            foreach (IAnnotation a in e.NewItems)
                AddVisual(a);
        }
    }

    private void RebuildAll()
    {
        ClearVisuals();
        var annotations = Annotations;
        if (annotations == null) return;
        foreach (var a in annotations)
            AddVisual(a);
        LayoutAll();
    }

    private void AddVisual(IAnnotation annotation)
    {
        var visual = annotation.CreateVisual();
        _visualMap[annotation] = visual;
        Children.Add(visual);
        PositioningInfo.Register(annotation, visual);
    }

    private void RemoveVisual(IAnnotation annotation)
    {
        if (_visualMap.TryGetValue(annotation, out var visual))
        {
            Children.Remove(visual);
            _visualMap.Remove(annotation);
            PositioningInfo.Unregister(annotation);
        }
    }

    private void ClearVisuals()
    {
        foreach (var kv in _visualMap)
            PositioningInfo.Unregister(kv.Key);
        _visualMap.Clear();
        Children.Clear();
    }

    private void LayoutAll()
    {
        double w = PlotAreaWidth;
        double h = PlotAreaHeight;
        if (w <= 0 || h <= 0) return;

        var annotations = Annotations;
        if (annotations == null) return;

        foreach (var a in annotations)
        {
            if (_visualMap.TryGetValue(a, out var visual))
                LayoutAnnotation(a, visual, w, h);
        }
    }

    private void LayoutAnnotation(IAnnotation annotation, FrameworkElement visual, double w, double h)
    {
        if (!annotation.IsVisible)
        {
            visual.Visibility = Visibility.Collapsed;
            return;
        }
        visual.Visibility = Visibility.Visible;

        double x1 = annotation.X1, y1 = annotation.Y1;
        double x2 = annotation.X2, y2 = annotation.Y2;

        if (annotation.CoordinateMode == AnnotationCoordinateMode.Data)
        {
            x1 = DataToPixel(x1, Chart.DefaultXAxisId, GetXMapper(), GetXRange(), w);
            y1 = DataToPixelY(y1, Chart.DefaultYAxisId, GetYMapper(), GetYRange(), h);
            x2 = DataToPixel(x2, Chart.DefaultXAxisId, GetXMapper(), GetXRange(), w);
            y2 = DataToPixelY(y2, Chart.DefaultYAxisId, GetYMapper(), GetYRange(), h);
        }

        double left = Math.Min(x1, x2);
        double top = Math.Min(y1, y2);

        switch (annotation)
        {
            case TextAnnotation:
                SetLeft(visual, Math.Max(0, x1));
                SetTop(visual, Math.Max(0, y1));
                break;
            case RectangleAnnotation:
                SetLeft(visual, Math.Max(0, left));
                SetTop(visual, Math.Max(0, top));
                visual.Width = Math.Abs(x2 - x1);
                visual.Height = Math.Abs(y2 - y1);
                LayoutRectangleChildren(visual, Math.Abs(x2 - x1), Math.Abs(y2 - y1));
                break;
            case LineAnnotation:
                SetLeft(visual, Math.Max(0, left));
                SetTop(visual, Math.Max(0, top));
                visual.Width = Math.Abs(x2 - x1);
                visual.Height = Math.Abs(y2 - y1);
                LayoutLineChildren(visual, x1, y1, x2, y2, left, top);
                break;
            default:
                SetLeft(visual, Math.Max(0, x1));
                SetTop(visual, Math.Max(0, y1));
                break;
        }

        annotation.UpdateVisual(visual);
        PositioningInfo.UpdatePosition(annotation, left, top);
    }

    private static void LayoutRectangleChildren(FrameworkElement visual, double w, double h)
    {
        if (visual is Canvas c && c.Children.Count >= 2)
        {
            if (c.Children[0] is FrameworkElement rect)
            {
                rect.Width = w;
                rect.Height = h;
            }
        }
    }

    private static void LayoutLineChildren(FrameworkElement visual, double x1, double y1, double x2, double y2, double left, double top)
    {
        if (visual is Canvas c && c.Children.Count > 0 && c.Children[0] is System.Windows.Shapes.Line line)
        {
            if (x1 < x2)
            {
                line.X1 = 0;
                line.Y1 = y1 < y2 ? 0 : Math.Abs(y2 - y1);
                line.X2 = Math.Abs(x2 - x1);
                line.Y2 = y1 < y2 ? Math.Abs(y2 - y1) : 0;
            }
            else
            {
                line.X1 = Math.Abs(x2 - x1);
                line.Y1 = y1 < y2 ? 0 : Math.Abs(y2 - y1);
                line.X2 = 0;
                line.Y2 = y1 < y2 ? Math.Abs(y2 - y1) : 0;
            }
        }
    }

    private ICoordinateMapper GetXMapper()
    {
        var xAxes = XAxes;
        if (xAxes != null && xAxes.Count > 0)
            return xAxes[0].CoordinateMapper;
        return LinearCoordinateMapper.Instance;
    }

    private ICoordinateMapper GetYMapper()
    {
        var yAxes = YAxes;
        if (yAxes != null && yAxes.Count > 0)
            return yAxes[0].CoordinateMapper;
        return LinearCoordinateMapper.Instance;
    }

    private DataRange GetXRange()
    {
        var xAxes = XAxes;
        if (xAxes != null && xAxes.Count > 0)
            return xAxes[0].VisibleRange;
        return new DataRange(0, 100);
    }

    private DataRange GetYRange()
    {
        var yAxes = YAxes;
        if (yAxes != null && yAxes.Count > 0)
            return yAxes[0].VisibleRange;
        return new DataRange(-1, 1);
    }

    private static double DataToPixel(double value, string axisId, ICoordinateMapper mapper, DataRange range, double size)
    {
        if (size <= 0) return 0;
        return mapper.DataToScreen(value, range, size);
    }

    private static double DataToPixelY(double value, string axisId, ICoordinateMapper mapper, DataRange range, double size)
    {
        if (size <= 0) return 0;
        return size - mapper.DataToScreen(value, range, size);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        LayoutAll();
        return base.ArrangeOverride(arrangeSize);
    }

    /// <summary>
    /// 存储每个标注的当前屏幕位置，供交互修饰器使用。
    /// </summary>
    internal static class PositioningInfo
    {
        private static readonly Dictionary<IAnnotation, (double X, double Y)> _positions = new();

        public static void Register(IAnnotation a, FrameworkElement _) { }
        public static void Unregister(IAnnotation a) => _positions.Remove(a);
        public static void UpdatePosition(IAnnotation a, double x, double y) => _positions[a] = (x, y);
        public static bool TryGetPosition(IAnnotation a, out double x, out double y)
        {
            if (_positions.TryGetValue(a, out var pos))
            {
                x = pos.X;
                y = pos.Y;
                return true;
            }
            x = y = 0;
            return false;
        }
    }
}
