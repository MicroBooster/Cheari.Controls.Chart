using System.Windows.Media;
using Cheari.Controls.Axes;
using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class FinancialChartViewModel : ChartDemoViewModelBase
{
    private const string PriceAxisId = "price";
    private const string VolumeAxisId = "volume";

    protected override void InitializeChart()
    {
        XAxes.Clear();
        YAxes.Clear();

        XAxes.Add(new DateTimeAxis
        {
            Id = Cheari.Controls.Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = new DataRange(0, 31),
            AutoRange = false,
            Title = "Date (Jan 2025)",
            AxisForeground = Helpers.ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(200, 200, 200))
        });

        YAxes.Add(new LinearAxis
        {
            Id = PriceAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = new DataRange(70, 135),
            AutoRange = false,
            Title = "Price ($)",
            AxisForeground = Helpers.ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(0, 210, 255))
        });

        YAxes.Add(new LinearAxis
        {
            Id = VolumeAxisId,
            Placement = AxisPlacement.Right,
            VisibleRange = new DataRange(0, 30000),
            AutoRange = false,
            Title = "Volume",
            AxisForeground = Helpers.ChartConfigurationHelper.CreateAxisBrush(MediaColor.FromRgb(255, 180, 0))
        });

        var ohlc = new OhlcDataSeries();
        var volume = new UniformDataSeries<double, double>(index => index, _ => 0);
        var rng = new Random(42);
        double price = 100;

        for (int i = 0; i < 30; i++)
        {
            double open = price;
            double change = (rng.NextDouble() - 0.47) * 6;
            double close = open + change;
            double high = Math.Max(open, close) + rng.NextDouble() * 3;
            double low = Math.Min(open, close) - rng.NextDouble() * 3;
            ohlc.Append(i, open, high, low, close);
            volume.Append(5000 + rng.NextDouble() * 20000);
            price = close;
        }

        Series.Add(new OhlcRenderableSeries
        {
            Title = "AAPL",
            YAxisId = PriceAxisId,
            UpFill = MediaColor.FromRgb(0, 200, 83),
            DownFill = MediaColor.FromRgb(255, 61, 61),
            UpStroke = MediaColor.FromRgb(0, 200, 83),
            DownStroke = MediaColor.FromRgb(255, 61, 61),
            StrokeThickness = 1,
            DataSeries = ohlc
        });

        Series.Add(new BarRenderableSeries
        {
            Title = "Volume",
            YAxisId = VolumeAxisId,
            Stroke = MediaColor.FromArgb(140, 255, 180, 0),
            StrokeThickness = 0,
            DataSeries = volume
        });

        Legend = new ChartLegend();
        Description = "K线图 + 成交量柱状图 | 左轴价格 右轴成交量 | 双Y轴独立缩放，双击自动适配";
    }
}
