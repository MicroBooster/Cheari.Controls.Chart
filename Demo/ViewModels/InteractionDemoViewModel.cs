using Cheari.Controls.Data;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class InteractionDemoViewModel : ChartDemoViewModelBase
{
    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-0.5, 124.5), new DataRange(-2.5, 2.5));

        var data = new UniformDataSeries<double, double>(index => index * 0.1, _ => 0);
        var rng = new Random(42);

        for (int i = 0; i < 125; i++)
        {
            double v = Math.Sin(i * 0.12) + Math.Cos(i * 0.05) * 0.5;
            data.Append(v + (rng.NextDouble() - 0.5) * 0.15);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Signal",
            Stroke = MediaColor.FromRgb(0, 230, 118),
            StrokeThickness = 2,
            DataSeries = data
        });

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Markers",
            MarkerType = MarkerType.Circle,
            MarkerSize = 6,
            MarkerColor = MediaColor.FromRgb(255, 235, 59),
            Stroke = MediaColor.FromRgb(255, 193, 7),
            StrokeThickness = 1,
            DataSeries = data
        });

        Modifiers.Clear();
        Modifiers.Add(new PanModifier());
        Modifiers.Add(new ZoomModifier());
        Modifiers.Add(new TooltipModifier());
        Modifiers.Add(new CrosshairModifier());
        Modifiers.Add(new RubberBandModifier());

        Description = "交互修饰器演示 | 悬停Tooltip | 十字光标Crosshair | 框选放大RubberBand | 平移拖拽&滚轮缩放";
    }
}
