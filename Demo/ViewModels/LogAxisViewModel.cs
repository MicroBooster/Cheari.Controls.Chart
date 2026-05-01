using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Demo.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class LogAxisViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        InitialXRange = new DataRange(-1, 12);
        InitialYRange = new DataRange(0.1, 10000);

        var dataSeries = new VariableDataSeries<double, double>(
            xToDouble: x => x,
            yToDouble: y => y);

        double[] values = [1, 1.5, 2.3, 5, 8, 15, 30, 75, 150, 400, 900, 2000];
        for (int i = 0; i < values.Length; i++)
        {
            dataSeries.Append(i, values[i]);
        }

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Log Data",
            MarkerType = MarkerType.Circle,
            MarkerSize = 8,
            MarkerColor = System.Windows.Media.Colors.LimeGreen,
            DataSeries = dataSeries,
            YAxisId = Chart.DefaultYAxisId
        });

        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = InitialXRange,
            AutoRange = false,
            Title = "Index",
            LabelFormat = "F0",
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

        YAxes.Add(new LogAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = InitialYRange,
            AutoRange = false,
            Title = "Value (Log)",
            AxisForeground = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(170, 255, 170)),
            ShowMajorGridLines = true,
            ShowMinorGridLines = true,
            MajorGridLineColor = MediaColor.FromArgb(42, 170, 255, 170),
            MajorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(42, 170, 255, 170)),
            MajorGridLineThickness = 1.0,
            MinorGridLineBrush = ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromArgb(22, 170, 255, 170)),
            MinorGridLineThickness = 1.0,
            MinorGridLineDashArray = ChartConfigurationHelper.CreateDashArray(2, 4)
        });

        Description = "对数轴图表 | 默认 X/Y 轴都应继续驱动网格线；检查对数 Y 轴网格线与刻度是否同步而且位于曲线下方";
    }
}
