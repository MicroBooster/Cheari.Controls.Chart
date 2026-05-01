using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class ThemeCustomizationViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-2, 64), new DataRange(-2.5, 2.5));

        var data = new UniformDataSeries<double, double>(index => index * 0.2, _ => 0);
        var rng = new Random(42);

        for (int i = 0; i < 62; i++)
        {
            double v = Math.Cos(i * 0.15) * Math.Exp(-i * 0.02);
            data.Append(v + (rng.NextDouble() - 0.5) * 0.3);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Damped Cosine",
            Stroke = MediaColor.FromRgb(0, 220, 220),
            StrokeThickness = 2.5
,

            DataSeries = data
        });

        Description = "自定义主题演示 | 深蓝背景 + 亮色线条 + 高对比度网格";
    }

    public string ThemeName { get; } = "Ocean Dark";
}
