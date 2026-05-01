using System.Collections.ObjectModel;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series;
using Demo.Helpers;

namespace Demo.ViewModels;

public class ChartDemoViewModelBase : BindableBase
{
    private string _description = "";
    private bool _enableAntialiasing = true;
    private ChartRendererPreference _rendererPreference = ChartRendererPreference.Auto;

    public ObservableCollection<IAxis> XAxes { get; } = [];
    public ObservableCollection<IAxis> YAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> Series { get; } = [];
    public ObservableCollection<IChartModifier> Modifiers { get; } = [new PanModifier(), new ZoomModifier()];

    public DataRange InitialXRange { get; protected set; } = new DataRange(0, 10);
    public DataRange InitialYRange { get; protected set; } = new DataRange(-1.5, 1.5);

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool EnableAntialiasing
    {
        get => _enableAntialiasing;
        set => SetProperty(ref _enableAntialiasing, value);
    }

    public ChartRendererPreference RendererPreference
    {
        get => _rendererPreference;
        set => SetProperty(ref _rendererPreference, value);
    }

    /// <summary>
    /// 由导航页在进入激活状态时调用，用于准备图表数据和实时资源。
    /// </summary>
    public void Initialize()
    {
        Series.Clear();

        InitializeChart();
    }

    /// <summary>
    /// 由导航页在离开或卸载时调用，用于停止派生类型持有的实时资源。
    /// </summary>
    public virtual void Cleanup()
    {
    }

    protected virtual void InitializeChart()
    {
    }

    protected void ResetDefaultAxes(DataRange? xRange = null, DataRange? yRange = null)
    {
        XAxes.Clear();
        YAxes.Clear();

        InitialXRange = xRange ?? new DataRange(0, 10);
        InitialYRange = yRange ?? new DataRange(-1.5, 1.5);

        XAxes.Add(ChartConfigurationHelper.CreateDefaultXAxis(InitialXRange));
        YAxes.Add(ChartConfigurationHelper.CreateDefaultYAxis(InitialYRange));
    }
}
