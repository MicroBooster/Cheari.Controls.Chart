using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class DataReuseDemoViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-0.5, 62.5), new DataRange(-2.2, 2.2));

        var shared = new UniformDataSeries<double, double>(index => index, _ => 0);
        var rng = new Random(42);

        for (int i = 0; i < 60; i++)
        {
            double v = Math.Sin(i * 0.2) + (rng.NextDouble() - 0.5) * 0.4;
            shared.Append(v);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Sine+Noise (Line)",
            Stroke = MediaColor.FromRgb(0, 188, 212),
            StrokeThickness = 2,
            DataSeries = shared
        });

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Sine+Noise (Scatter)",
            MarkerType = MarkerType.Diamond,
            MarkerSize = 7,
            MarkerColor = MediaColor.FromRgb(255, 152, 0),
            Stroke = MediaColor.FromRgb(255, 193, 7),
            StrokeThickness = 1.5,
            DataSeries = shared
        });

        Legend = new ChartLegend();
        Description = "同一个 UniformDataSeries 被 Line 和 Scatter 两个系列共享，验证快照和渲染管线复用。";
    }
}
