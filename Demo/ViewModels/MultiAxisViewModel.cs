using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Demo.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class MultiAxisViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        InitialXRange = new DataRange(0, 10);
        InitialYRange = new DataRange(-1.5, 1.5);

        var sinData = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.01,
            xToDouble: x => x,
            yToDouble: y => y);

        for (int i = 0; i < 1000; i++)
        {
            double x = i * 0.01;
            double y = Math.Sin(x * 10);
            sinData.Append(y);
        }

        var volumeData = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.01,
            xToDouble: x => x,
            yToDouble: y => y);

        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            double x = i * 0.01;
            double y = 500 + Math.Abs(Math.Sin(x * 10)) * 300 + random.NextDouble() * 100;
            volumeData.Append(y);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Sin Wave",
            Stroke = System.Windows.Media.Colors.Cyan,
            StrokeThickness = 1.5,
            DataSeries = sinData,
            YAxisId = Chart.DefaultYAxisId
        });

        Series.Add(new LineRenderableSeries
        {
            Title = "Volume",
            Stroke = System.Windows.Media.Colors.Orange,
            StrokeThickness = 1.5,
            DataSeries = volumeData,
            YAxisId = "RightYAxis"
        });

        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = InitialXRange,
            AutoRange = false,
            Title = "X",
            LabelFormat = "F1",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(120, 205, 255)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(46, 120, 205, 255),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(46, 120, 205, 255)),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(22, 120, 205, 255)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(2, 4)
        });

        YAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = InitialYRange,
            AutoRange = false,
            Title = "Amplitude",
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
            VisibleRange = new DataRange(400, 1000),
            AutoRange = false,
            Title = "Volume",
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

        Description = "多轴图表 | 默认左 Y 轴负责网格线，右 Y 轴只显示自身轴元素；检查双轴文字布局和默认轴网格线是否稳定";
    }
}
