using System.Collections.ObjectModel;
using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public class LegendDemoViewModel : ChartDemoViewModelBase
{
    public string Title { get; } = "图例演示";

    public LegendDemoViewModel()
    {
        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = new DataRange(0, 100),
            AutoRange = true,
            Title = "Time"
        });

        YAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = new DataRange(-2, 6),
            AutoRange = true,
            Title = "Value"
        });

        var sine = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var cosine = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var random = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var rng = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            double x = i * 0.1;
            sine.Append(Math.Sin(x) + 1);
            cosine.Append(Math.Cos(x) + 2);
            random.Append(rng.NextDouble() * 2 + 3);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Sine",
            Stroke = Colors.Cyan,
            StrokeThickness = 2,
            DataSeries = sine
        });

        Series.Add(new LineRenderableSeries
        {
            Title = "Cosine",
            Stroke = Colors.Orange,
            StrokeThickness = 2,
            DataSeries = cosine
        });

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Random",
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerColor = Colors.Magenta,
            DataSeries = random
        });

        Modifiers.Add(new PanModifier());
        Modifiers.Add(new ZoomModifier());

        Legend = new ChartLegend();
    }
}
