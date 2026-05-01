using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using System.Windows.Media;

namespace Demo.ViewModels;

public class AreaChartViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(0, 10), new DataRange(-1.2, 1.2));

        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.05,
            xToDouble: x => x,
            yToDouble: y => y);

        for (int i = 0; i < 200; i++)
        {
            double x = i * 0.05;
            double y = Math.Sin(x * 3) * Math.Exp(-x * 0.1);
            dataSeries.Append(y);
        }

        Series.Add(new AreaRenderableSeries
        {
            Title = "衰减正弦",
            Stroke = Colors.LimeGreen,
            StrokeThickness = 1.5,
            Fill = Colors.LimeGreen,
            FillOpacity = 0.3,
            BaselineY = 0,
            DataSeries = dataSeries
        });

        Description = "面积图 | 检查缩放后轴标题与刻度是否持续同步，绘图区内容不应覆盖轴区域";
    }
}
