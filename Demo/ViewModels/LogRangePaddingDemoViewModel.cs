using System.Windows;
using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Demo.ViewModels;

public enum LogDemoChartType
{
    Line,
    Scatter,
    Bar,
    Area
}

public class LogRangePaddingDemoViewModel : ChartDemoViewModelBase
{
    private Thickness _PlotAreaMargin;
    private bool _xAutoRange = true;
    private bool _yAutoRange = true;
    private LogDemoChartType _selectedChartType = LogDemoChartType.Line;
    private double _xLimitMin;
    private double _xLimitMax;
    private double _yLimitMin;
    private double _yLimitMax;
    private VisibleRangeLimitMode _xLimitMode;
    private VisibleRangeLimitMode _yLimitMode;
    private double _logBase = 10.0;

    public LogAxis? XAxis { get; private set; }
    public LogAxis? YAxis { get; private set; }

    public List<LogChartTypeOption> ChartTypeOptions { get; } =
    [
        new("折线图", LogDemoChartType.Line),
        new("散点图", LogDemoChartType.Scatter),
        new("柱状图", LogDemoChartType.Bar),
        new("面积图", LogDemoChartType.Area),
    ];

    public List<LimitModeOption> LimitModeOptions { get; } =
    [
        new("不限制", VisibleRangeLimitMode.None),
        new("最小值", VisibleRangeLimitMode.MinOnly),
        new("最大值", VisibleRangeLimitMode.MaxOnly),
        new("全部", VisibleRangeLimitMode.MinAndMax),
    ];

    public Thickness PlotAreaMargin
    {
        get => _PlotAreaMargin;
        set => SetProperty(ref _PlotAreaMargin, value);
    }

    public double PlotAreaMarginLeft
    {
        get => _PlotAreaMargin.Left;
        set
        {
            var newPadding = new Thickness(value, _PlotAreaMargin.Top,
                _PlotAreaMargin.Right, _PlotAreaMargin.Bottom);
            if (SetProperty(ref _PlotAreaMargin, newPadding))
            {
                RaisePropertyChanged(nameof(PlotAreaMarginLeft));
                RaisePropertyChanged(nameof(PlotAreaMargin));
            }
        }
    }

    public double PlotAreaMarginTop
    {
        get => _PlotAreaMargin.Top;
        set
        {
            var newPadding = new Thickness(_PlotAreaMargin.Left, value,
                _PlotAreaMargin.Right, _PlotAreaMargin.Bottom);
            if (SetProperty(ref _PlotAreaMargin, newPadding))
            {
                RaisePropertyChanged(nameof(PlotAreaMarginTop));
                RaisePropertyChanged(nameof(PlotAreaMargin));
            }
        }
    }

    public double PlotAreaMarginRight
    {
        get => _PlotAreaMargin.Right;
        set
        {
            var newPadding = new Thickness(_PlotAreaMargin.Left, _PlotAreaMargin.Top,
                value, _PlotAreaMargin.Bottom);
            if (SetProperty(ref _PlotAreaMargin, newPadding))
            {
                RaisePropertyChanged(nameof(PlotAreaMarginRight));
                RaisePropertyChanged(nameof(PlotAreaMargin));
            }
        }
    }

    public double PlotAreaMarginBottom
    {
        get => _PlotAreaMargin.Bottom;
        set
        {
            var newPadding = new Thickness(_PlotAreaMargin.Left, _PlotAreaMargin.Top,
                _PlotAreaMargin.Right, value);
            if (SetProperty(ref _PlotAreaMargin, newPadding))
            {
                RaisePropertyChanged(nameof(PlotAreaMarginBottom));
                RaisePropertyChanged(nameof(PlotAreaMargin));
            }
        }
    }

    public LogDemoChartType SelectedChartType
    {
        get => _selectedChartType;
        set
        {
            if (SetProperty(ref _selectedChartType, value))
                BuildSeries();
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

    public double LogBase
    {
        get => _logBase;
        set
        {
            if (SetProperty(ref _logBase, value))
            {
                if (YAxis != null) YAxis.Base = value;
                if (XAxis != null) XAxis.Base = value;
            }
        }
    }

    public LogRangePaddingDemoViewModel()
    {
        InitialXRange = new DataRange(1, 50);
        InitialYRange = new DataRange(1, 10000);
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

        XAxis = new LogAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = InitialXRange,
            AutoRange = _xAutoRange,
            VisibleRangeLimit = new DataRange(_xLimitMin, _xLimitMax),
            VisibleRangeLimitMode = _xLimitMode,
            Base = _logBase,
            Title = "X 轴（Log）"
        };
        XAxes.Add(XAxis);

        YAxis = new LogAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = InitialYRange,
            AutoRange = _yAutoRange,
            VisibleRangeLimit = new DataRange(_yLimitMin, _yLimitMax),
            VisibleRangeLimitMode = _yLimitMode,
            Base = _logBase,
            Title = "Y 轴（Log）"
        };
        YAxes.Add(YAxis);

        Description = "对数轴留白测试 | 验证 Chart.PlotAreaMargin 视口留白功能，Y 轴为对数刻度";
    }

    private void BuildSeries()
    {
        Series.Clear();

        switch (_selectedChartType)
        {
            case LogDemoChartType.Line:
                BuildLineSeries();
                break;
            case LogDemoChartType.Scatter:
                BuildScatterSeries();
                break;
            case LogDemoChartType.Bar:
                BuildBarSeries();
                break;
            case LogDemoChartType.Area:
                BuildAreaSeries();
                break;
        }
    }

    private void BuildLineSeries()
    {
        var data = CreateExpGrowthData();
        Series.Add(new LineRenderableSeries
        {
            Title = "指数增长",
            Stroke = Color.FromRgb(0, 188, 212),
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid,
            DataSeries = data
        });
    }

    private void BuildScatterSeries()
    {
        var data = CreateExpGrowthData();
        Series.Add(new ScatterRenderableSeries
        {
            Title = "指数增长",
            MarkerType = MarkerType.Diamond,
            MarkerSize = 6,
            MarkerColor = Color.FromRgb(233, 30, 99),
            Stroke = Color.FromRgb(233, 30, 99),
            DataSeries = data
        });
    }

    private void BuildBarSeries()
    {
        var data = CreateExpGrowthData();
        Series.Add(new BarRenderableSeries
        {
            Title = "指数增长",
            Stroke = Color.FromRgb(255, 193, 7),
            Fill = Color.FromRgb(255, 193, 7),
            BarSpacing = 0.3,
            DataSeries = data
        });
    }

    private void BuildAreaSeries()
    {
        var data = CreateExpGrowthData();
        Series.Add(new AreaRenderableSeries
        {
            Title = "指数增长",
            Stroke = Color.FromRgb(76, 175, 80),
            StrokeThickness = 2,
            Fill = Color.FromRgb(76, 175, 80),
            FillOpacity = 0.25,
            DataSeries = data
        });
    }

    private static UniformDataSeries<double, double> CreateExpGrowthData()
    {
        var data = new UniformDataSeries<double, double>(index => index + 1, x => x);
        for (int i = 0; i < 50; i++)
        {
            double y = Math.Pow(1.25, i);
            data.Append(y);
        }
        return data;
    }
}

public class LogChartTypeOption(string label, LogDemoChartType value)
{
    public string Label { get; } = label;
    public LogDemoChartType Value { get; } = value;
}
