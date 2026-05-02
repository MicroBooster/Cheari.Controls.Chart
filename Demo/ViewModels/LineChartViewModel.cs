using Cheari.Controls;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Demo.ViewModels;

public class LineChartViewModel : ChartDemoViewModelBase
{
    private const double SampleStep = 0.01;
    private const double SampleIntervalMilliseconds = 1000.0 / 90.0;
    private const int MaxSamplesPerBatch = 3;
    private const double SleepThresholdMilliseconds = 2.0;
    private const double YieldThresholdMilliseconds = 0.1;

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private UniformDataSeries<double, double>? _dataSeries;
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private long _sampleIndex;

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    public LineChartViewModel()
    {
        RendererPreference = ChartRendererPreference.Auto;
    }

    protected override void InitializeChart()
    {
        StopStreaming();
        _sampleIndex = 0;

        var dataSeries = new UniformDataSeries<double, double>(
            xSelector: index => index * 0.01,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = 100
        };
        _dataSeries = dataSeries;

        Series.Add(new LineRenderableSeries
        {
            Title = "Sin Wave",
            Stroke = System.Windows.Media.Colors.Cyan,
            StrokeThickness = 1,
            DataSeries = dataSeries,
        });

        StartStreaming();
        Description = "自动后端 | 固定视口基准页，后台独立时钟批量补样，避免 UI 补样与 D3D11 绘制争抢同一帧预算。";
    }

    public override void Cleanup()
    {
        StopStreaming();
        _dataSeries = null;
        base.Cleanup();
    }

    private void StartStreaming()
    {
        StopStreaming();

        IsStreaming = true;

        var cts = new CancellationTokenSource();
        _streamCts = cts;
        _streamTask = Task.Factory.StartNew(
            () => StreamData(cts.Token),
            cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void StopStreaming()
    {
        IsStreaming = false;

        var cts = Interlocked.Exchange(ref _streamCts, null);
        var streamTask = _streamTask;
        _streamTask = null;

        if (cts != null)
        {
            cts.Cancel();
        }

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

    /// <summary>
    /// 使用独立后台时钟推进采样，避免把补样成本压到 UI 渲染线程上。
    /// </summary>
    private void StreamData(CancellationToken cancellationToken)
    {
        var dataSeries = _dataSeries;
        if (dataSeries == null)
            return;

        try
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        }
        catch (Exception)
        {
        }

        TimeBeginPeriod(1);
        try
        {
            StreamDataCore(dataSeries, cancellationToken);
        }
        finally
        {
            TimeEndPeriod(1);
        }
    }

    private void StreamDataCore(UniformDataSeries<double, double> dataSeries, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        double nextSampleTimestamp = SampleIntervalMilliseconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (WaitForNextSample(stopwatch, nextSampleTimestamp, cancellationToken))
            {
                return;
            }

            double remainingMilliseconds = nextSampleTimestamp - stopwatch.Elapsed.TotalMilliseconds;
            int sampleCount = 1 + (int)Math.Floor((-remainingMilliseconds) / SampleIntervalMilliseconds);
            sampleCount = Math.Min(sampleCount, MaxSamplesPerBatch);
            nextSampleTimestamp += sampleCount * SampleIntervalMilliseconds;

            for (int i = 0; i < sampleCount; i++)
            {
                long sampleIndex = _sampleIndex++;
                double x = sampleIndex * SampleStep;
                double y = Math.Sin(x * 10) * Math.Exp(-x * 0.01);
                dataSeries.Append(y);
                dataSeries.Tag = Math.Round(y, 2);
            }
        }
    }

    private static bool WaitForNextSample(Stopwatch stopwatch, double targetTimestamp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            double remainingMilliseconds = targetTimestamp - stopwatch.Elapsed.TotalMilliseconds;
            if (remainingMilliseconds <= 0)
            {
                return false;
            }

            if (remainingMilliseconds >= SleepThresholdMilliseconds)
            {
                int sleepMilliseconds = Math.Max(1, (int)Math.Floor(remainingMilliseconds - 1.0));
                if (cancellationToken.WaitHandle.WaitOne(sleepMilliseconds))
                    return true;
            }
            else if (remainingMilliseconds >= YieldThresholdMilliseconds)
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
}
