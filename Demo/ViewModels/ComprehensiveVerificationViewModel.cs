using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;
using Demo.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public sealed class ComprehensiveVerificationViewModel : BindableBase
{
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;

    private UniformDataSeries<double, double>? _lineDataSeries;
    private VariableDataSeries<double, double>? _scatterDataSeries;
    private VariableDataSeries<double, double>? _barDataSeries;
    private UniformDataSeries<double, double>? _areaDataSeries;
    private OhlcDataSeries? _ohlcDataSeries;

    private double _linePhase;
    private double _areaPhase;
    private double _ohlcPrice = 100;
    private int _ohlcIndex;
    private int _scatterIndex;

    private bool _enableAntialiasing = true;
    public bool EnableAntialiasing
    {
        get => _enableAntialiasing;
        set => SetProperty(ref _enableAntialiasing, value);
    }

    public ObservableCollection<IAxis> LineXAxes { get; } = [];
    public ObservableCollection<IAxis> LineYAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> LineSeries { get; } = [];
    public ObservableCollection<IChartModifier> LineModifiers { get; } = [];

    public ObservableCollection<IAxis> ScatterXAxes { get; } = [];
    public ObservableCollection<IAxis> ScatterYAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> ScatterSeries { get; } = [];
    public ObservableCollection<IChartModifier> ScatterModifiers { get; } = [];

    public ObservableCollection<IAxis> BarXAxes { get; } = [];
    public ObservableCollection<IAxis> BarYAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> BarSeries { get; } = [];
    public ObservableCollection<IChartModifier> BarModifiers { get; } = [];

    public ObservableCollection<IAxis> AreaXAxes { get; } = [];
    public ObservableCollection<IAxis> AreaYAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> AreaSeries { get; } = [];
    public ObservableCollection<IChartModifier> AreaModifiers { get; } = [];

    public ObservableCollection<IAxis> OhlcXAxes { get; } = [];
    public ObservableCollection<IAxis> OhlcYAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> OhlcSeries { get; } = [];
    public ObservableCollection<IChartModifier> OhlcModifiers { get; } = [];

    public ComprehensiveVerificationViewModel()
    {
        SetupChartAxes(LineXAxes, LineYAxes, new DataRange(0, 2), new DataRange(-1.6, 1.6));
        SetupChartAxes(ScatterXAxes, ScatterYAxes, new DataRange(0, 10), new DataRange(-2, 2));
        SetupChartAxes(BarXAxes, BarYAxes, new DataRange(-1, 12), new DataRange(-50, 100));
        SetupChartAxes(AreaXAxes, AreaYAxes, new DataRange(0, 2), new DataRange(-1.2, 1.2));
        SetupChartAxes(OhlcXAxes, OhlcYAxes, new DataRange(-2, 62), new DataRange(70, 130));

        SetupModifiers(LineModifiers);
        SetupModifiers(ScatterModifiers);
        SetupModifiers(BarModifiers);
        SetupModifiers(AreaModifiers);
        SetupModifiers(OhlcModifiers);
    }

    public void Initialize()
    {

        _lineDataSeries = new UniformDataSeries<double, double>(
            xSelector: idx => idx * 0.01,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = 200
        };

        _scatterDataSeries = new VariableDataSeries<double, double>(
            xToDouble: x => x,
            yToDouble: y => y);

        _barDataSeries = new VariableDataSeries<double, double>(
            xToDouble: x => x,
            yToDouble: y => y);

        _areaDataSeries = new UniformDataSeries<double, double>(
            xSelector: idx => idx * 0.01,
            xToDouble: x => x,
            yToDouble: y => y)
        {
            FifoCapacity = 200
        };

        _ohlcDataSeries = new OhlcDataSeries { FifoCapacity = 200 };

        for (int i = 0; i < 30; i++)
        {
            AppendOhlc();
        }

        LineSeries.Add(new LineRenderableSeries
        {
            Title = "Damped Sin",
            Stroke = Colors.Cyan,
            StrokeThickness = 1,
            DataSeries = _lineDataSeries
        });

        ScatterSeries.Add(new ScatterRenderableSeries
        {
            Title = "Random Points",
            MarkerType = MarkerType.Circle,
            MarkerSize = 6,
            MarkerColor = Colors.Orange,
            DataSeries = _scatterDataSeries
        });

        BarSeries.Add(new BarRenderableSeries
        {
            Title = "Live Values",
            Fill = MediaColor.FromRgb(70, 130, 230),
            Stroke = Colors.White,
            StrokeThickness = 1,
            BarSpacing = 0.3,
            DataSeries = _barDataSeries
        });

        double[] barValues = [23, -30, 67, -15, 89, -45, 78, -20, 91, -35, 38, 72];
        for (int i = 0; i < barValues.Length; i++)
        {
            _barDataSeries.Append(i, barValues[i]);
        }

        AreaSeries.Add(new AreaRenderableSeries
        {
            Title = "Decay Cos",
            Stroke = Colors.LimeGreen,
            StrokeThickness = 1.5,
            Fill = Colors.LimeGreen,
            FillOpacity = 0.3,
            BaselineY = 0,
            DataSeries = _areaDataSeries
        });

        OhlcSeries.Add(new OhlcRenderableSeries
        {
            Title = "Live OHLC",
            UpFill = MediaColor.FromRgb(0, 200, 83),
            DownFill = MediaColor.FromRgb(255, 61, 61),
            UpStroke = MediaColor.FromRgb(0, 200, 83),
            DownStroke = MediaColor.FromRgb(255, 61, 61),
            StrokeThickness = 1,
            DataSeries = _ohlcDataSeries
        });

        StartStreaming();
    }

    public void Cleanup()
    {
        StopStreaming();
        _lineDataSeries = null;
        _scatterDataSeries = null;
        _barDataSeries = null;
        _areaDataSeries = null;
        _ohlcDataSeries = null;
    }

    private static void SetupChartAxes(
        ObservableCollection<IAxis> xAxes,
        ObservableCollection<IAxis> yAxes,
        DataRange xRange,
        DataRange yRange)
    {
        xAxes.Clear();
        yAxes.Clear();
        xAxes.Add(ChartConfigurationHelper.CreateDefaultXAxis(xRange));
        yAxes.Add(ChartConfigurationHelper.CreateDefaultYAxis(yRange));
    }

    private static void SetupModifiers(ObservableCollection<IChartModifier> modifiers)
    {
        modifiers.Clear();
        modifiers.Add(new PanModifier());
        modifiers.Add(new ZoomModifier());
    }

    private void StartStreaming()
    {
        StopStreaming();

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
        var cts = Interlocked.Exchange(ref _streamCts, null);
        var task = _streamTask;
        _streamTask = null;

        cts?.Cancel();
        if (task != null)
        {
            try { task.Wait(1000); }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { }
            catch (OperationCanceledException) { }
        }
        cts?.Dispose();
    }

    private void StreamData(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        double nextTick = 0;

        while (!ct.IsCancellationRequested)
        {
            double remaining = nextTick - stopwatch.Elapsed.TotalMilliseconds;
            if (remaining > 0)
            {
                if (remaining > 2)
                    ct.WaitHandle.WaitOne(Math.Max(1, (int)(remaining - 1)));
                else
                    Thread.Yield();
                continue;
            }

            nextTick += 33.0;

            if (remaining < -100)
                nextTick = stopwatch.Elapsed.TotalMilliseconds + 33.0;

            AppendLineData();
            AppendScatterData();
            AppendAreaData();
            AppendOhlc();
        }
    }

    private void AppendLineData()
    {
        var ds = _lineDataSeries;
        if (ds == null) return;

        _linePhase += 1.0;
        double x = _linePhase * 0.01;
        double y = Math.Sin(x * 10 + 0.5) * Math.Exp(-x * 0.01);
        ds.Append(y);
    }

    private void AppendScatterData()
    {
        var ds = _scatterDataSeries;
        if (ds == null) return;

        _scatterIndex++;
        if (_scatterIndex % 3 != 0)
            return;

        if (ds.Count > 500)
            return;

        double x = _scatterIndex * 0.03 + Math.Sin(_scatterIndex * 0.37) * 0.5;
        double y = Math.Sin(x * 2.7) + (Math.Sin(x * 17.3) * 0.15) + Math.Cos(x * 5.1) * 0.3;
        double xWrapped = x % 10.0;
        if (xWrapped < 0) xWrapped += 10.0;
        ds.Append(xWrapped, y);
    }

    private void AppendAreaData()
    {
        var ds = _areaDataSeries;
        if (ds == null) return;

        _areaPhase += 1.0;
        double x = _areaPhase * 0.01;
        double y = Math.Cos(x * 8) * Math.Exp(-x * 0.015);
        ds.Append(y);
    }

    private void AppendOhlc()
    {
        var ds = _ohlcDataSeries;
        if (ds == null) return;

        double open = _ohlcPrice;
        double change = (Random.Shared.NextDouble() - 0.48) * 5;
        double close = open + change;
        double high = Math.Max(open, close) + Random.Shared.NextDouble() * 2;
        double low = Math.Min(open, close) - Random.Shared.NextDouble() * 2;

        ds.Append(_ohlcIndex++, open, high, low, close);
        _ohlcPrice = close;
    }
}
