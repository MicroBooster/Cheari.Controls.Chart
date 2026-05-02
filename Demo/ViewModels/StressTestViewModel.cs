using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public enum StressTestMode
{
    StaticBigData,
    StreamingHighFreq,
    MixedSeries,
    MultiSeriesStream
}

public class StressTestViewModel : ChartDemoViewModelBase
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private static readonly Color[] Palette =
    [
        Colors.Cyan, Colors.Orange, Colors.LimeGreen, Colors.Magenta, Colors.Gold,
        Colors.DeepSkyBlue, Colors.HotPink, Colors.MediumPurple, Colors.Tomato, Colors.SpringGreen,
        Colors.DodgerBlue, Colors.OrangeRed, Colors.LawnGreen, Colors.Orchid, Colors.Khaki,
        Colors.CornflowerBlue, Colors.Salmon, Colors.Chartreuse, Colors.Violet, Colors.PeachPuff,
        Colors.Aquamarine, Colors.Coral, Colors.YellowGreen, Colors.Plum, Colors.SandyBrown,
        Colors.Turquoise, Colors.IndianRed, Colors.LightGreen, Colors.MediumOrchid, Colors.BurlyWood,
        Colors.MediumAquamarine, Colors.DarkOrange, Colors.PaleGreen, Colors.MediumVioletRed, Colors.Tan,
        Colors.LightSeaGreen, Colors.Firebrick, Colors.GreenYellow, Colors.DarkOrchid, Colors.Wheat,
        Colors.DarkTurquoise, Colors.DarkSalmon, Colors.OliveDrab, Colors.BlueViolet, Colors.Goldenrod,
        Colors.CadetBlue, Colors.Sienna, Colors.ForestGreen, Colors.SlateBlue, Colors.DarkKhaki,
        Colors.Teal, Colors.Peru, Colors.SeaGreen, Colors.DarkSlateBlue, Colors.Olive,
        Colors.SteelBlue, Colors.Chocolate, Colors.Lime, Colors.Navy, Colors.DarkGoldenrod,
        Colors.RoyalBlue, Colors.DarkRed, Colors.MediumSeaGreen, Colors.Indigo, Colors.DarkOliveGreen,
        Colors.MidnightBlue, Colors.Brown, Colors.DarkGreen, Colors.DarkMagenta, Colors.DarkCyan,
        Colors.Navy, Colors.Crimson, Colors.Green, Colors.Purple, Colors.DarkSlateGray,
        Colors.SlateGray, Colors.DarkSeaGreen, Colors.MediumSlateBlue, Colors.DimGray, Colors.Gray,
        Colors.LightSlateGray, Colors.SlateGray, Colors.DarkGray, Colors.Silver, Colors.LightGray,
    ];

    private int _pointCount = 100_000;
    private int _seriesCount = 10;
    private int _updateRateHz = 60;
    private int _fifoCapacity = 10_000;
    private StressTestMode _testMode = StressTestMode.StaticBigData;
    private bool _isRunning;
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private long _sampleIndex;
    private string _statusText = "就绪";

    public int PointCount
    {
        get => _pointCount;
        set => SetProperty(ref _pointCount, value);
    }

    public int SeriesCount
    {
        get => _seriesCount;
        set => SetProperty(ref _seriesCount, value);
    }

    public int UpdateRateHz
    {
        get => _updateRateHz;
        set => SetProperty(ref _updateRateHz, value);
    }

    public int FifoCapacity
    {
        get => _fifoCapacity;
        set => SetProperty(ref _fifoCapacity, value);
    }

    public StressTestMode TestMode
    {
        get => _testMode;
        set => SetProperty(ref _testMode, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public DelegateCommand RunTestCommand { get; }
    public DelegateCommand StopTestCommand { get; }
    public DelegateCommand ResetCommand { get; }

    public StressTestViewModel()
    {
        RendererPreference = ChartRendererPreference.Auto;
        RunTestCommand = new DelegateCommand(ExecuteRunTest, () => !IsRunning);
        StopTestCommand = new DelegateCommand(ExecuteStopTest, () => IsRunning);
        ResetCommand = new DelegateCommand(ExecuteReset);
    }

    protected override void InitializeChart()
    {
        StopStreaming();
        ResetDefaultAxes(new DataRange(0, 1000), new DataRange(-2, 2));
        Description = "极限压力测试 | 选择测试模式和参数后点击运行";
    }

    public override void Cleanup()
    {
        StopStreaming();
        base.Cleanup();
    }

    private void ExecuteRunTest()
    {
        StopStreaming();
        Series.Clear();
        _sampleIndex = 0;

        switch (TestMode)
        {
            case StressTestMode.StaticBigData:
                RunStaticBigData();
                break;
            case StressTestMode.StreamingHighFreq:
                RunStreamingHighFreq();
                break;
            case StressTestMode.MixedSeries:
                RunMixedSeries();
                break;
            case StressTestMode.MultiSeriesStream:
                RunMultiSeriesStream();
                break;
        }

        IsRunning = true;
        RunTestCommand.RaiseCanExecuteChanged();
        StopTestCommand.RaiseCanExecuteChanged();
    }

    private void ExecuteStopTest()
    {
        StopStreaming();
        IsRunning = false;
        StatusText = "已停止";
        RunTestCommand.RaiseCanExecuteChanged();
        StopTestCommand.RaiseCanExecuteChanged();
    }

    private void ExecuteReset()
    {
        StopStreaming();
        Series.Clear();
        IsRunning = false;
        StatusText = "就绪";
        RunTestCommand.RaiseCanExecuteChanged();
        StopTestCommand.RaiseCanExecuteChanged();
    }

    private void RunStaticBigData()
    {
        var count = PointCount;
        var numSeries = SeriesCount;
        var sw = Stopwatch.StartNew();

        ResetDefaultAxes(new DataRange(0, count * 0.01), new DataRange(-2, 2));

        for (int s = 0; s < numSeries; s++)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y);

            double freq = 1.0 + s * 0.5;
            double phase = s * Math.PI / numSeries;
            double amplitude = 1.0 - s * 0.02;
            var random = new Random(s);

            var buffer = new double[count];
            for (int i = 0; i < count; i++)
            {
                double x = i * 0.01;
                buffer[i] = amplitude * Math.Sin(x * freq + phase) + (random.NextDouble() - 0.5) * 0.3;
            }
            dataSeries.Append(buffer);

            Series.Add(new LineRenderableSeries
            {
                Title = $"Series {s + 1}",
                Stroke = Palette[s % Palette.Length],
                StrokeThickness = 1,
                DataSeries = dataSeries,
            });
        }

        sw.Stop();
        var totalPoints = (long)count * numSeries;
        StatusText = $"静态加载 | {totalPoints:N0} 点 × {numSeries} 系列 | 耗时 {sw.ElapsedMilliseconds} ms";
    }

    private void RunStreamingHighFreq()
    {
        var fifo = FifoCapacity;
        var rateHz = UpdateRateHz;

        ResetDefaultAxes(new DataRange(0, fifo * 0.01), new DataRange(-2, 2));

        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.01,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = fifo
        };

        Series.Add(new LineRenderableSeries
        {
            Title = "High Freq Stream",
            Stroke = Colors.Cyan,
            StrokeThickness = 1,
            DataSeries = dataSeries,
        });

        StatusText = $"高频流式 | {rateHz} Hz | FIFO {fifo:N0}";

        StartStreaming(dataSeries, rateHz);
    }

    private void RunMixedSeries()
    {
        var count = Math.Min(PointCount, 50_000);
        var sw = Stopwatch.StartNew();

        ResetDefaultAxes(new DataRange(0, count * 0.01), new DataRange(-2, 2));

        for (int i = 0; i < 5; i++)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y);

            double freq = 1.0 + i * 0.7;
            double phase = i * Math.PI / 5;
            var buffer = new double[count];
            for (int j = 0; j < count; j++)
            {
                double x = j * 0.01;
                buffer[j] = Math.Sin(x * freq + phase);
            }
            dataSeries.Append(buffer);

            Series.Add(new LineRenderableSeries
            {
                Title = $"Line {i + 1}",
                Stroke = Palette[i % Palette.Length],
                StrokeThickness = 1,
                DataSeries = dataSeries,
            });
        }

        for (int i = 0; i < 5; i++)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y);

            double freq = 0.5 + i * 0.3;
            var buffer = new double[count];
            for (int j = 0; j < count; j++)
            {
                double x = j * 0.01;
                buffer[j] = Math.Sin(x * freq) * 0.8;
            }
            dataSeries.Append(buffer);

            var strokeColor = Palette[(i + 5) % Palette.Length];
            Series.Add(new AreaRenderableSeries
            {
                Title = $"Area {i + 1}",
                Stroke = strokeColor,
                Fill = Color.FromArgb(76, strokeColor.R, strokeColor.G, strokeColor.B),
                FillOpacity = 0.3,
                StrokeThickness = 1,
                DataSeries = dataSeries,
            });
        }

        for (int i = 0; i < 5; i++)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y);

            var random = new Random(i + 100);
            var barCount = Math.Min(count, 500);
            var buffer = new double[barCount];
            for (int j = 0; j < barCount; j++)
            {
                buffer[j] = random.NextDouble() * 1.5 - 0.5;
            }
            dataSeries.Append(buffer);

            var fillColor = Palette[(i + 10) % Palette.Length];
            Series.Add(new BarRenderableSeries
            {
                Title = $"Bar {i + 1}",
                Stroke = fillColor,
                Fill = Color.FromArgb(204, fillColor.R, fillColor.G, fillColor.B),
                BarWidth = 0.6,
                DataSeries = dataSeries,
            });
        }

        for (int i = 0; i < 5; i++)
        {
            var dataSeries = new VariableDataSeries<double, double>(
                xToDouble: x => x,
                yToDouble: y => y);

            var random = new Random(i + 200);
            var scatterCount = Math.Min(count, 2000);
            for (int j = 0; j < scatterCount; j++)
            {
                double x = random.NextDouble() * count * 0.01;
                double y = Math.Sin(x * (1.0 + i * 0.5)) + (random.NextDouble() - 0.5) * 0.5;
                dataSeries.Append(x, y);
            }

            Series.Add(new ScatterRenderableSeries
            {
                Title = $"Scatter {i + 1}",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerColor = Palette[(i + 15) % Palette.Length],
                DataSeries = dataSeries,
            });
        }

        sw.Stop();
        StatusText = $"混合系列 | 20 系列 | 耗时 {sw.ElapsedMilliseconds} ms";
    }

    private void RunMultiSeriesStream()
    {
        var numSeries = SeriesCount;
        var fifo = FifoCapacity;
        var rateHz = UpdateRateHz;

        ResetDefaultAxes(new DataRange(0, fifo * 0.01), new DataRange(-2, 2));

        var allSeries = new List<UniformDataSeries<double, double>>(numSeries);

        for (int s = 0; s < numSeries; s++)
        {
            var dataSeries = new UniformDataSeries<double, double>(
                xSelector: index => index * 0.01,
                xToDouble: x => x,
                yToDouble: y => y)
            {
                FifoCapacity = fifo
            };

            allSeries.Add(dataSeries);

            Series.Add(new LineRenderableSeries
            {
                Title = $"Stream {s + 1}",
                Stroke = Palette[s % Palette.Length],
                StrokeThickness = 1,
                DataSeries = dataSeries,
            });
        }

        StatusText = $"多系列流式 | {numSeries} 系列 × {rateHz} Hz | FIFO {fifo:N0}";

        StartMultiStreaming(allSeries, rateHz);
    }

    private void StartStreaming(UniformDataSeries<double, double> dataSeries, int rateHz)
    {
        var cts = new CancellationTokenSource();
        _streamCts = cts;
        _streamTask = Task.Factory.StartNew(
            () => StreamSingleData(dataSeries, rateHz, cts.Token),
            cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void StartMultiStreaming(List<UniformDataSeries<double, double>> dataSeriesList, int rateHz)
    {
        var cts = new CancellationTokenSource();
        _streamCts = cts;
        _streamTask = Task.Factory.StartNew(
            () => StreamMultiData(dataSeriesList, rateHz, cts.Token),
            cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void StreamSingleData(UniformDataSeries<double, double> dataSeries, int rateHz, CancellationToken cancellationToken)
    {
        TimeBeginPeriod(1);
        try
        {
            var interval = 1000.0 / rateHz;
            var sw = Stopwatch.StartNew();
            double nextTimestamp = interval;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (WaitForNextSample(sw, nextTimestamp, cancellationToken))
                    return;

                double remaining = nextTimestamp - sw.Elapsed.TotalMilliseconds;
                int batch = 1 + (int)Math.Floor(-remaining / interval);
                batch = Math.Min(batch, 3);
                nextTimestamp += batch * interval;

                for (int i = 0; i < batch; i++)
                {
                    long idx = Interlocked.Increment(ref _sampleIndex);
                    double x = idx * 0.01;
                    double y = Math.Sin(x * 10) * Math.Exp(-x * 0.001) + (Random.Shared.NextDouble() - 0.5) * 0.1;
                    dataSeries.Append(y);
                }
            }
        }
        finally
        {
            TimeEndPeriod(1);
        }
    }

    private void StreamMultiData(List<UniformDataSeries<double, double>> dataSeriesList, int rateHz, CancellationToken cancellationToken)
    {
        TimeBeginPeriod(1);
        try
        {
            var interval = 1000.0 / rateHz;
            var sw = Stopwatch.StartNew();
            double nextTimestamp = interval;
            int numSeries = dataSeriesList.Count;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (WaitForNextSample(sw, nextTimestamp, cancellationToken))
                    return;

                double remaining = nextTimestamp - sw.Elapsed.TotalMilliseconds;
                int batch = 1 + (int)Math.Floor(-remaining / interval);
                batch = Math.Min(batch, 3);
                nextTimestamp += batch * interval;

                for (int b = 0; b < batch; b++)
                {
                    long idx = Interlocked.Increment(ref _sampleIndex);
                    double x = idx * 0.01;

                    for (int s = 0; s < numSeries; s++)
                    {
                        double freq = 1.0 + s * 0.5;
                        double phase = s * Math.PI / numSeries;
                        double y = Math.Sin(x * freq + phase) + (Random.Shared.NextDouble() - 0.5) * 0.1;
                        dataSeriesList[s].Append(y);
                    }
                }
            }
        }
        finally
        {
            TimeEndPeriod(1);
        }
    }

    private static bool WaitForNextSample(Stopwatch stopwatch, double targetTimestamp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            double remaining = targetTimestamp - stopwatch.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
                return false;

            if (remaining >= 2.0)
            {
                int sleepMs = Math.Max(1, (int)Math.Floor(remaining - 1.0));
                if (cancellationToken.WaitHandle.WaitOne(sleepMs))
                    return true;
            }
            else if (remaining >= 0.1)
            {
                Thread.Yield();
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void StopStreaming()
    {
        var cts = Interlocked.Exchange(ref _streamCts, null);
        var streamTask = _streamTask;
        _streamTask = null;

        if (cts != null)
            cts.Cancel();

        if (streamTask != null)
        {
            try
            {
                streamTask.Wait(1000);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
    }
}
