using System.Windows.Media;
using System.Windows.Threading;
using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public class HardwareOnlyDiagnosticsViewModel : ChartDemoViewModelBase
{
    private readonly DispatcherTimer _timer;
    private double _currentIndex;

    public HardwareOnlyDiagnosticsViewModel()
    {
        RendererPreference = ChartRendererPreference.HardwareOnly;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += Timer_Tick;
    }

    protected override void InitializeChart()
    {
        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.025,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = 160
        };

        Series.Add(new LineRenderableSeries
        {
            Title = "Hardware Diagnostics",
            Stroke = Colors.OrangeRed,
            StrokeThickness = 1.5,
            DataSeries = dataSeries
        });

        _timer.Start();
        Description = "硬件专用诊断 | 禁止自动回退，若 D3D11 初始化失败则保留错误状态与叠层信息。";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (Series.Count == 0)
            return;

        if (Series[0].DataSeries is not UniformDataSeries<double, double> dataSeries)
            return;

        double x = _currentIndex * 0.025;
        double y = Math.Sin(x * 4.0) * 0.5 + Math.Sin(x * 10.0) * 0.2;
        dataSeries.Append(y);
        _currentIndex++;
    }

    public override void Cleanup()
    {
        _timer.Stop();
        base.Cleanup();
    }
}
