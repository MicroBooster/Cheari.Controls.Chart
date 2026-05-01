using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class OhlcChartViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-2, 62), new DataRange(70, 130));

        var dataSeries = new OhlcDataSeries();

        var random = new Random(42);
        double price = 100;
        for (int i = 0; i < 60; i++)
        {
            double open = price;
            double change = (random.NextDouble() - 0.48) * 5;
            double close = open + change;
            double high = Math.Max(open, close) + random.NextDouble() * 2;
            double low = Math.Min(open, close) - random.NextDouble() * 2;
            dataSeries.Append(i, open, high, low, close);
            price = close;
        }

        Series.Add(new OhlcRenderableSeries
        {
            Title = "K线图",
            UpFill = MediaColor.FromRgb(0, 200, 83),
            DownFill = MediaColor.FromRgb(255, 61, 61),
            UpStroke = MediaColor.FromRgb(0, 200, 83),
            DownStroke = MediaColor.FromRgb(255, 61, 61),
            StrokeThickness = 1,
            DataSeries = dataSeries
        });

        Description = "蜡烛图 | 检查默认轴在高密度数据下仍保持可读，双击自动适配后刻度应重算";
    }
}
