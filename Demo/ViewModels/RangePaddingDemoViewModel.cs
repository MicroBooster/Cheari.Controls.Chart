using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public enum DemoChartType
{
    Line,
    Scatter,
    Bar,
    Area,
    Ohlc
}

public class RangePaddingDemoViewModel : ChartDemoViewModelBase
{
    private double _xPaddingMin;
    private double _xPaddingMax;
    private double _yPaddingMin;
    private double _yPaddingMax;
    private bool _xAutoRange = true;
    private bool _yAutoRange = true;
    private DemoChartType _selectedChartType = DemoChartType.Line;
    private double _xLimitMin;
    private double _xLimitMax;
    private double _yLimitMin;
    private double _yLimitMax;
    private VisibleRangeLimitMode _xLimitMode;
    private VisibleRangeLimitMode _yLimitMode;

    public string Title { get; } = "坐标轴留白演示";

    public LinearAxis? XAxis { get; private set; }
    public LinearAxis? YAxis { get; private set; }

    public List<DemoChartTypeOption> ChartTypeOptions { get; } =
    [
        new("折线图", DemoChartType.Line),
        new("散点图", DemoChartType.Scatter),
        new("柱状图", DemoChartType.Bar),
        new("面积图", DemoChartType.Area),
        new("蜡烛图", DemoChartType.Ohlc),
    ];

    public DemoChartType SelectedChartType
    {
        get => _selectedChartType;
        set
        {
            if (SetProperty(ref _selectedChartType, value))
                BuildSeries();
        }
    }

    public double XPaddingMin
    {
        get => _xPaddingMin;
        set
        {
            if (SetProperty(ref _xPaddingMin, value) && XAxis != null)
            {
                XAxis.RangePaddingMin = _xPaddingMin;
                XAxis.RangePaddingMax = _xPaddingMax;
            }
        }
    }

    public double XPaddingMax
    {
        get => _xPaddingMax;
        set
        {
            if (SetProperty(ref _xPaddingMax, value) && XAxis != null)
            {
                XAxis.RangePaddingMin = _xPaddingMin;
                XAxis.RangePaddingMax = _xPaddingMax;
            }
        }
    }

    public double YPaddingMin
    {
        get => _yPaddingMin;
        set
        {
            if (SetProperty(ref _yPaddingMin, value) && YAxis != null)
            {
                YAxis.RangePaddingMin = _yPaddingMin;
                YAxis.RangePaddingMax = _yPaddingMax;
            }
        }
    }

    public double YPaddingMax
    {
        get => _yPaddingMax;
        set
        {
            if (SetProperty(ref _yPaddingMax, value) && YAxis != null)
            {
                YAxis.RangePaddingMin = _yPaddingMin;
                YAxis.RangePaddingMax = _yPaddingMax;
            }
        }
    }

    public bool XAutoRange
    {
        get => _xAutoRange;
        set
        {
            if (SetProperty(ref _xAutoRange, value) && XAxis != null)
                XAxis.AutoRange = value;
        }
    }

    public bool YAutoRange
    {
        get => _yAutoRange;
        set
        {
            if (SetProperty(ref _yAutoRange, value) && YAxis != null)
                YAxis.AutoRange = value;
        }
    }

    public List<LimitModeOption> LimitModeOptions { get; } =
    [
        new("不限制", VisibleRangeLimitMode.None),
        new("最小值", VisibleRangeLimitMode.MinOnly),
        new("最大值", VisibleRangeLimitMode.MaxOnly),
        new("全部", VisibleRangeLimitMode.MinAndMax),
    ];

    public double XLimitMin
    {
        get => _xLimitMin;
        set
        {
            if (SetProperty(ref _xLimitMin, value) && XAxis != null)
                XAxis.VisibleRangeLimit = new DataRange(value, XAxis.VisibleRangeLimit.Max);
        }
    }

    public double XLimitMax
    {
        get => _xLimitMax;
        set
        {
            if (SetProperty(ref _xLimitMax, value) && XAxis != null)
                XAxis.VisibleRangeLimit = new DataRange(XAxis.VisibleRangeLimit.Min, value);
        }
    }

    public double YLimitMin
    {
        get => _yLimitMin;
        set
        {
            if (SetProperty(ref _yLimitMin, value) && YAxis != null)
                YAxis.VisibleRangeLimit = new DataRange(value, YAxis.VisibleRangeLimit.Max);
        }
    }

    public double YLimitMax
    {
        get => _yLimitMax;
        set
        {
            if (SetProperty(ref _yLimitMax, value) && YAxis != null)
                YAxis.VisibleRangeLimit = new DataRange(YAxis.VisibleRangeLimit.Min, value);
        }
    }

    public VisibleRangeLimitMode XLimitMode
    {
        get => _xLimitMode;
        set
        {
            if (SetProperty(ref _xLimitMode, value) && XAxis != null)
                XAxis.VisibleRangeLimitMode = value;
        }
    }

    public VisibleRangeLimitMode YLimitMode
    {
        get => _yLimitMode;
        set
        {
            if (SetProperty(ref _yLimitMode, value) && YAxis != null)
                YAxis.VisibleRangeLimitMode = value;
        }
    }

    public RangePaddingDemoViewModel()
    {
        InitialXRange = new DataRange(0, 100);
        InitialYRange = new DataRange(-6, 6);
    }

    protected override void InitializeChart()
    {
        BuildAxes();
        BuildSeries();
    }

    private void BuildAxes()
    {
        XAxes.Clear();
        YAxes.Clear();

        XAxis = new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = InitialXRange,
            AutoRange = _xAutoRange,
            RangePaddingMin = _xPaddingMin,
            RangePaddingMax = _xPaddingMax,
            VisibleRangeLimit = new DataRange(_xLimitMin, _xLimitMax),
            VisibleRangeLimitMode = _xLimitMode,
            Title = "X 轴"
        };
        XAxes.Add(XAxis);

        YAxis = new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = InitialYRange,
            AutoRange = _yAutoRange,
            RangePaddingMin = _yPaddingMin,
            RangePaddingMax = _yPaddingMax,
            VisibleRangeLimit = new DataRange(_yLimitMin, _yLimitMax),
            VisibleRangeLimitMode = _yLimitMode,
            Title = "Y 轴"
        };
        YAxes.Add(YAxis);
    }

    private void BuildSeries()
    {
        Series.Clear();

        switch (_selectedChartType)
        {
            case DemoChartType.Line:
                BuildLineSeries();
                break;
            case DemoChartType.Scatter:
                BuildScatterSeries();
                break;
            case DemoChartType.Bar:
                BuildBarSeries();
                break;
            case DemoChartType.Area:
                BuildAreaSeries();
                break;
            case DemoChartType.Ohlc:
                BuildOhlcSeries();
                break;
        }
    }

    private void BuildLineSeries()
    {
        var data = CreateWaveformData();
        Series.Add(new LineRenderableSeries
        {
            Title = "复合波形",
            Stroke = Color.FromRgb(0, 188, 212),
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid,
            DataSeries = data
        });
    }

    private void BuildScatterSeries()
    {
        var data = CreateWaveformData();
        Series.Add(new ScatterRenderableSeries
        {
            Title = "复合波形",
            MarkerType = MarkerType.Diamond,
            MarkerSize = 6,
            MarkerColor = Color.FromRgb(233, 30, 99),
            Stroke = Color.FromRgb(233, 30, 99),
            DataSeries = data
        });
    }

    private void BuildBarSeries()
    {
        var data = CreateBarData();
        Series.Add(new BarRenderableSeries
        {
            Title = "柱状图",
            Stroke = Color.FromRgb(255, 193, 7),
            Fill = Color.FromRgb(255, 193, 7),
            BarSpacing = 0.3,
            DataSeries = data
        });
    }

    private void BuildAreaSeries()
    {
        var data = CreateWaveformData();
        Series.Add(new AreaRenderableSeries
        {
            Title = "复合波形",
            Stroke = Color.FromRgb(76, 175, 80),
            StrokeThickness = 2,
            Fill = Color.FromRgb(76, 175, 80),
            FillOpacity = 0.25,
            DataSeries = data
        });
    }

    private void BuildOhlcSeries()
    {
        var data = CreateOhlcData();
        Series.Add(new OhlcRenderableSeries
        {
            Title = "OHLC",
            UpFill = Color.FromRgb(0, 200, 83),
            DownFill = Color.FromRgb(255, 61, 61),
            UpStroke = Color.FromRgb(0, 200, 83),
            DownStroke = Color.FromRgb(255, 61, 61),
            DataSeries = data
        });
    }

    private static UniformDataSeries<double, double> CreateWaveformData()
    {
        var data = new UniformDataSeries<double, double>(index => index * 0.2, x => x);
        for (int i = 0; i < 500; i++)
        {
            double x = i * 0.2;
            double y = Math.Sin(x * 0.3) * 3 + Math.Cos(x * 0.07) * 1.5;
            data.Append(y);
        }
        return data;
    }

    private static UniformDataSeries<double, double> CreateBarData()
    {
        var data = new UniformDataSeries<double, double>(index => index, x => x);
        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
            data.Append(rng.NextDouble() * 8 - 4);
        return data;
    }

    private static OhlcDataSeries CreateOhlcData()
    {
        var data = new OhlcDataSeries();
        var rng = new Random(42);
        double close = 5.0;
        for (int i = 0; i < 100; i++)
        {
            double open = close;
            double change = (rng.NextDouble() - 0.5) * 2;
            close = open + change;
            double high = Math.Max(open, close) + rng.NextDouble() * 1.5;
            double low = Math.Min(open, close) - rng.NextDouble() * 1.5;
            data.Append(i * 0.5, open, high, low, close);
        }
        return data;
    }
}

public class DemoChartTypeOption(string label, DemoChartType value)
{
    public string Label { get; } = label;
    public DemoChartType Value { get; } = value;
}

public class LimitModeOption(string label, VisibleRangeLimitMode value)
{
    public string Label { get; } = label;
    public VisibleRangeLimitMode Value { get; } = value;
}
