using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using System.Windows.Media;

namespace Demo.ViewModels;

public class ScatterChartViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(0, 10), new DataRange(-2, 2));

        var dataSeries = new VariableDataSeries<double, double>(
            xToDouble: x => x,
            yToDouble: y => y);

        var random = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            double x = random.NextDouble() * 10;
            double y = Math.Sin(x * 2) + random.NextDouble() * 0.5 - 0.25;
            dataSeries.Append(x, y);
        }

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Scatter",
            MarkerType = MarkerType.Circle,
            MarkerSize = 8,
            MarkerColor = Colors.Orange,
            DataSeries = dataSeries
        });

        Description = "散点图 | 检查默认轴标题、刻度文本和主网格线是否保持清晰可读";
    }
}
