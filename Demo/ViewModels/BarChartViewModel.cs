using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class BarChartViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-1, 12), new DataRange(0, 100));

        var dataSeries = new VariableDataSeries<double, double>(
            xToDouble: x => x,
            yToDouble: y => y);

        double[] values = [23, 45, 67, 34, 89, 56, 78, 43, 91, 65, 38, 72];
        for (int i = 0; i < values.Length; i++)
        {
            dataSeries.Append(i, values[i]);
        }

        Series.Add(new BarRenderableSeries
        {
            Title = "月度数据",
            Fill = MediaColor.FromRgb(70, 130, 230),
            Stroke = System.Windows.Media.Colors.White,
            StrokeThickness = 1,
            BarSpacing = 0.3,
            DataSeries = dataSeries
        });

        Description = "柱状图 | 检查底部索引轴和左侧数值轴是否仍位于绘图区外，文本不应压入图像区";
    }
}
