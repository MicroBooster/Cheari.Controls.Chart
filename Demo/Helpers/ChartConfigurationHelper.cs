using System.Windows.Media;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.Helpers;

internal static class ChartConfigurationHelper
{
    private static readonly MediaColor DefaultAxisForegroundColor = MediaColor.FromRgb(230, 230, 230);
    private static readonly MediaColor DefaultGridLineColor = MediaColor.FromArgb(46, 255, 255, 255);
    private static readonly MediaColor DefaultMinorGridLineColor = MediaColor.FromArgb(22, 255, 255, 255);

    public static LinearAxis CreateDefaultXAxis(DataRange? visibleRange = null)
    {
        return new LinearAxis
        {
            Id = Cheari.Controls.Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = visibleRange ?? new DataRange(0, 10),
            AutoRange = true,
            Title = "Index",
            LabelFormat = "F1",
            AxisForeground = CreateAxisBrush(DefaultAxisForegroundColor),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = DefaultGridLineColor,
            MajorGridLineBrush = CreateAxisBrush(DefaultGridLineColor),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = CreateAxisBrush(DefaultMinorGridLineColor),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = CreateDashArray(2, 4)
        };
    }

    public static LinearAxis CreateDefaultYAxis(DataRange? visibleRange = null)
    {
        return new LinearAxis
        {
            Id = Cheari.Controls.Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = visibleRange ?? new DataRange(-1.5, 1.5),
            VisibleRangeLimitMode = VisibleRangeLimitMode.None,
            AutoRange = true,
            Title = "Value",
            LabelFormat = "F2",
            AxisForeground = CreateAxisBrush(DefaultAxisForegroundColor),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = DefaultGridLineColor,
            MajorGridLineBrush = CreateAxisBrush(DefaultGridLineColor),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = CreateAxisBrush(DefaultMinorGridLineColor),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = CreateDashArray(2, 4)
        };
    }

    public static SolidColorBrush CreateAxisBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static DoubleCollection CreateDashArray(params double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
    }
}
