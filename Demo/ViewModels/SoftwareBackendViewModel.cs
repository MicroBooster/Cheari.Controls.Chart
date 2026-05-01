using System.Windows.Media;
using System.Windows.Threading;
using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public class SoftwareBackendViewModel : ChartDemoViewModelBase
{
    private readonly DispatcherTimer _timer;
    private double _currentIndex;

    public SoftwareBackendViewModel()
    {
        RendererPreference = ChartRendererPreference.SoftwareOnly;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        _timer.Tick += Timer_Tick;
    }

    protected override void InitializeChart()
    {
        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.02,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = 120
        };

        Series.Add(new LineRenderableSeries
        {
            Title = "Software Wave",
            Stroke = Colors.Gold,
            StrokeThickness = 1.25,
            DataSeries = dataSeries
        });

        _timer.Start();
        Description = "强制软件后端 | 直接跳过 D3D 初始化，验证 GDI 输出链与交互行为。";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (Series.Count == 0)
            return;

        if (Series[0].DataSeries is not UniformDataSeries<double, double> dataSeries)
            return;

        double x = _currentIndex * 0.02;
        double y = Math.Cos(x * 6.0) * 0.8 + Math.Sin(x * 2.5) * 0.2;
        dataSeries.Append(y);
        _currentIndex++;
    }

    public override void Cleanup()
    {
        _timer.Stop();
        base.Cleanup();
    }
}
