using System.Windows;
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
    private LegendPosition _selectedPosition = LegendPosition.InternalTop;
    private LegendOrientation _selectedOrientation = LegendOrientation.Vertical;
    private HorizontalAlignment _selectedHAlign = HorizontalAlignment.Center;
    private VerticalAlignment _selectedVAlign = VerticalAlignment.Center;

    public string Title { get; } = "图例演示";

    public ChartLegend LegendInstance { get; }

    public List<LegendPositionOption> PositionOptions { get; } =
    [
        new("绘图区·上方", LegendPosition.InternalTop),
        new("绘图区·下方", LegendPosition.InternalBottom),
        new("绘图区·左侧", LegendPosition.InternalLeft),
        new("绘图区·右侧", LegendPosition.InternalRight),
        new("图表外·上方", LegendPosition.ExternalTop),
        new("图表外·下方", LegendPosition.ExternalBottom),
        new("图表外·左侧", LegendPosition.ExternalLeft),
        new("图表外·右侧", LegendPosition.ExternalRight),
    ];

    public List<LegendOrientationOption> OrientationOptions { get; } =
    [
        new("垂直排列", LegendOrientation.Vertical),
        new("水平排列", LegendOrientation.Horizontal),
    ];

    public List<AlignmentOption> HAlignOptions { get; } =
    [
        new("左对齐", HorizontalAlignment.Left),
        new("居中", HorizontalAlignment.Center),
        new("右对齐", HorizontalAlignment.Right),
        new("拉伸", HorizontalAlignment.Stretch),
    ];

    public List<AlignmentOption> VAlignOptions { get; } =
    [
        new("上对齐", VerticalAlignment.Top),
        new("居中", VerticalAlignment.Center),
        new("下对齐", VerticalAlignment.Bottom),
        new("拉伸", VerticalAlignment.Stretch),
    ];

    public LegendPosition SelectedPosition
    {
        get => _selectedPosition;
        set
        {
            if (SetProperty(ref _selectedPosition, value))
            {
                LegendInstance.Position = value;
                _selectedHAlign = LegendInstance.HorizontalAlignment;
                _selectedVAlign = LegendInstance.VerticalAlignment;
                RaisePropertyChanged(nameof(SelectedHAlign));
                RaisePropertyChanged(nameof(SelectedVAlign));
            }
        }
    }

    public LegendOrientation SelectedOrientation
    {
        get => _selectedOrientation;
        set
        {
            if (SetProperty(ref _selectedOrientation, value))
                LegendInstance.Orientation = value;
        }
    }

    public HorizontalAlignment SelectedHAlign
    {
        get => _selectedHAlign;
        set
        {
            if (SetProperty(ref _selectedHAlign, value))
                LegendInstance.HorizontalAlignment = value;
        }
    }

    public VerticalAlignment SelectedVAlign
    {
        get => _selectedVAlign;
        set
        {
            if (SetProperty(ref _selectedVAlign, value))
                LegendInstance.VerticalAlignment = value;
        }
    }

    public LegendDemoViewModel()
    {
        LegendInstance = new ChartLegend
        {
            Position = _selectedPosition,
            Orientation = _selectedOrientation,
            HorizontalAlignment = _selectedHAlign,
            VerticalAlignment = _selectedVAlign
        };
        Legend = LegendInstance;
    }

    protected override void InitializeChart()
    {
        BuildAxes();
        BuildSeries();
        BuildModifiers();
        LegendInstance.SetSeries(Series);
    }

    private void BuildAxes()
    {
        XAxes.Clear();
        YAxes.Clear();

        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = new DataRange(0, 100),
            AutoRange = true,
            Title = "Time (s)"
        });

        YAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = new DataRange(-3, 8),
            AutoRange = true,
            Title = "Amplitude"
        });
    }

    private void BuildSeries()
    {
        var sineData = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var cosineData = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var scatterData = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var areaData = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var envelopeData = new UniformDataSeries<double, double>(index => index * 0.1, x => x);
        var barData = new UniformDataSeries<double, double>(index => index, x => x);

        var rng = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            double x = i * 0.1;
            sineData.Append(Math.Sin(x * 0.8) * 1.5 + 1.0);
            cosineData.Append(Math.Cos(x * 0.8) * 1.5 + 4.0);
            scatterData.Append(rng.NextDouble() * 2.5 + 5.0);
            areaData.Append(Math.Sin(x * 0.4) * 2.0 - 1.5);
            envelopeData.Append(Math.Exp(-x * 0.03) * Math.Sin(x * 0.6) * 3.0 - 1.0);

            if (i < 30)
                barData.Append(rng.NextDouble() * 3.0 + 6.0);
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Sine Wave",
            Stroke = Color.FromRgb(0, 188, 212),
            StrokeThickness = 1,
            LineStyle = LineStyle.Solid,
            DataSeries = sineData
        });

        Series.Add(new LineRenderableSeries
        {
            Title = "Cosine Wave",
            Stroke = Color.FromRgb(255, 152, 0),
            StrokeThickness = 2.5,
            LineStyle = LineStyle.Dash,
            StrokeDashArray = LineRenderableSeries.GetDashArrayForStyle(LineStyle.Dash),
            DataSeries = cosineData
        });

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Random Noise",
            MarkerType = MarkerType.Diamond,
            MarkerSize = 6,
            MarkerColor = Color.FromRgb(233, 30, 99),
            Stroke = Color.FromRgb(233, 30, 99),
            DataSeries = scatterData
        });

        Series.Add(new AreaRenderableSeries
        {
            Title = "Damped Sine",
            Stroke = Color.FromRgb(76, 175, 80),
            StrokeThickness = 2.0,
            Fill = Color.FromRgb(76, 175, 80),
            FillOpacity = 0.25,
            DataSeries = areaData
        });

        Series.Add(new LineRenderableSeries
        {
            Title = "Envelope Decay",
            Stroke = Color.FromRgb(156, 39, 176),
            StrokeThickness = 2.5,
            LineStyle = LineStyle.DashDot,
            StrokeDashArray = LineRenderableSeries.GetDashArrayForStyle(LineStyle.DashDot),
            DataSeries = envelopeData
        });

        Series.Add(new BarRenderableSeries
        {
            Title = "Bar Histogram",
            Stroke = Color.FromRgb(255, 193, 7),
            Fill = Color.FromRgb(255, 193, 7),
            BarSpacing = 0.3,
            DataSeries = barData
        });
    }

    private static void BuildModifiers()
    {
    }
}

public class LegendPositionOption
{
    public string Label { get; }
    public LegendPosition Value { get; }

    public LegendPositionOption(string label, LegendPosition value)
    {
        Label = label;
        Value = value;
    }
}

public class LegendOrientationOption
{
    public string Label { get; }
    public LegendOrientation Value { get; }

    public LegendOrientationOption(string label, LegendOrientation value)
    {
        Label = label;
        Value = value;
    }
}

public class AlignmentOption
{
    public string Label { get; }
    public Enum Value { get; }

    public AlignmentOption(string label, Enum value)
    {
        Label = label;
        Value = value;
    }
}
