using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using System.Windows.Media;
using System.Windows.Threading;
using Prism.Commands;

namespace Demo.ViewModels;

public class FifoChartViewModel : ChartDemoViewModelBase
{
    private readonly DispatcherTimer _timer;
    private double _currentIndex;
    private int _fifoCapacity = 500;
    private int _updateInterval = 16;
    private double _frequency = 10;
    private double _decay = 0.01;
    private bool _isRunning = true;

    public DelegateCommand ResetCommand { get; }

    public int FifoCapacity
    {
        get => _fifoCapacity;
        set
        {
            if (SetProperty(ref _fifoCapacity, value))
            {
                UpdateFifoCapacity();
            }
        }
    }

    public int UpdateInterval
    {
        get => _updateInterval;
        set
        {
            if (SetProperty(ref _updateInterval, value))
            {
                _timer.Interval = TimeSpan.FromMilliseconds(value);
            }
        }
    }

    public double Frequency
    {
        get => _frequency;
        set => SetProperty(ref _frequency, value);
    }

    public double Decay
    {
        get => _decay;
        set => SetProperty(ref _decay, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                if (value)
                    _timer.Start();
                else
                    _timer.Stop();
            }
        }
    }

    public FifoChartViewModel()
    {
        ResetCommand = new DelegateCommand(ResetData);
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_updateInterval)
        };
        _timer.Tick += Timer_Tick;
    }

    protected override void InitializeChart()
    {
        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.01,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = _fifoCapacity
        };

        Series.Add(new LineRenderableSeries
        {
            Title = "FIFO Wave",
            Stroke = Colors.Cyan,
            StrokeThickness = 1,
            DataSeries = dataSeries
        });

        _timer.Start();
        Description = "FIFO滑动窗口演示 | 实时数据采集，支持容量限制";
    }

    private void UpdateFifoCapacity()
    {
        if (Series.Count > 0 && Series[0].DataSeries is DataSeriesBase dataSeries)
        {
            dataSeries.FifoCapacity = _fifoCapacity;
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (Series.Count == 0) return;

        var series = Series[0];
        if (series.DataSeries is not UniformDataSeries<double, double> dataSeries) return;

        double x = _currentIndex * 0.01;
        double y = Math.Sin(x * _frequency) * Math.Exp(-x * _decay);
        dataSeries.Append(y);
        _currentIndex++;
    }

    public override void Cleanup()
    {
        _timer.Stop();
        base.Cleanup();
    }

    public void ResetData()
    {
        if (Series.Count > 0 && Series[0] is LineRenderableSeries lineSeries)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y)
            {
                FifoCapacity = _fifoCapacity
            };
            lineSeries.DataSeries = dataSeries;
            _currentIndex = 0;
        }
    }
}