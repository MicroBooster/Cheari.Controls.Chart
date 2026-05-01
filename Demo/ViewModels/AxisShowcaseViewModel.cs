using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Demo.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class AxisShowcaseViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        InitialXRange = new DataRange(0, 12);
        InitialYRange = new DataRange(-1.6, 1.6);

        var primarySeriesData = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.05,
            xToDouble: x => x,
            yToDouble: y => y);

        var secondarySeriesData = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.05,
            xToDouble: x => x,
            yToDouble: y => y);

        for (int i = 0; i <= 240; i++)
        {
            double x = i * 0.05;
            double primary = Math.Sin(x * 1.2) + 0.35 * Math.Cos(x * 2.6);
            double secondary = 520 + 180 * Math.Cos(x * 0.7) + 70 * Math.Sin(x * 2.3);
            primarySeriesData.Append(primary);
            secondarySeriesData.Append(secondary);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Primary Signal",
            Stroke = System.Windows.Media.Colors.DeepSkyBlue,
            StrokeThickness = 2,
            DataSeries = primarySeriesData,
            XAxisId = Chart.DefaultXAxisId,
            YAxisId = Chart.DefaultYAxisId
        });

        Series.Add(new LineRenderableSeries
        {
            Title = "Energy",
            Stroke = System.Windows.Media.Colors.Orange,
            StrokeThickness = 2,
            DataSeries = secondarySeriesData,
            XAxisId = Chart.DefaultXAxisId,
            YAxisId = "RightYAxis"
        });

        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = InitialXRange,
            AutoRange = false,
            Title = "Bottom Axis / Time (s)",
            LabelFormat = "F1",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(120, 205, 255)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(46, 120, 205, 255),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(46, 120, 205, 255)),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(22, 120, 205, 255)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(2, 4),
            VisibleRangeLimit = new DataRange(0, 10),
            VisibleRangeLimitMode = VisibleRangeLimitMode.MinOnly
        });

        XAxes.Add(new LinearAxis
        {
            Id = "TopXAxis",
            Placement = AxisPlacement.Top,
            VisibleRange = new DataRange(0, 12),
            AutoRange = false,
            Title = "Top Axis / Mirror Scale",
            LabelFormat = "F2",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(255, 210, 120)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(90, 255, 90, 140),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(90, 255, 90, 140)),
            MajorGridLineThickness = 2.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(50, 255, 90, 140)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(1, 5)
        });

        YAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = InitialYRange,
            AutoRange = false,
            Title = "Left Axis / Signal",
            LabelFormat = "F2",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(140, 255, 180)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(42, 140, 255, 180),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(42, 140, 255, 180)),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(22, 140, 255, 180)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(2, 4)
        });

        YAxes.Add(new LinearAxis
        {
            Id = "RightYAxis",
            Placement = AxisPlacement.Right,
            VisibleRange = new DataRange(220, 820),
            AutoRange = false,
            Title = "Right Axis / Energy",
            LabelFormat = "F0",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(255, 150, 120)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(96, 255, 160, 80),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(96, 255, 160, 80)),
            MajorGridLineThickness = 2.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(48, 255, 160, 80)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(1, 5)
        });

        Description = "四轴演示 | 检查内外双层背景/边框；只有默认底部 X 轴和默认左侧 Y 轴应绘制网格线，顶部/右侧轴即使配置了网格样式也不应出线";
    }
}
