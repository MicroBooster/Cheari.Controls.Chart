using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Cheari.Controls.Annotations;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.Controls;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Vortice.Wpf;

namespace Cheari.Controls;

/// <summary>
/// Cheari 图表控件，一个高性能的 WPF 图表控件，支持多种图表类型和交互功能。
/// 
/// 本文件包含核心依赖属性、构造函数、鼠标事件转发和公共方法。
/// 职责拆分到以下 partial class 文件：
/// - Chart.AxisManagement.cs   — 轴订阅/范围同步/AutoRange
/// - Chart.SeriesManagement.cs — 系列订阅/数据变更处理
/// - Chart.SurfaceManagement.cs — FPS计时器/渲染重试/连续刷新
/// - Chart.PlotAreaLayout.cs   — 视口度量/轴展示器偏移/角画刷
/// - Chart.RenderingLifecycle.cs — 渲染器初始化/切换/降级
/// </summary>
[ToolboxItem(true)]
[DesignTimeVisible(true)]
public partial class Chart : Control
{
    private static readonly IRenderableSeries[] s_emptySeries = Array.Empty<IRenderableSeries>();
    private static readonly IAxis[] s_emptyAxes = Array.Empty<IAxis>();
    private static readonly long s_continuousRefreshGraceTicks = Stopwatch.Frequency / 4;
    private static readonly DependencyPropertyDescriptor? s_axisPlacementDescriptor =
        DependencyPropertyDescriptor.FromProperty(AxisBase.PlacementProperty, typeof(AxisBase));
    private static readonly DependencyPropertyDescriptor? s_axisForegroundDescriptor =
        DependencyPropertyDescriptor.FromProperty(AxisBase.AxisForegroundProperty, typeof(AxisBase));
    private static readonly DependencyPropertyDescriptor? s_axisAutoRangeDescriptor =
        DependencyPropertyDescriptor.FromProperty(AxisBase.AutoRangeProperty, typeof(AxisBase));

    /// <summary>默认X轴ID。</summary>
    public const string DefaultXAxisId = "DefaultXAxis";
    /// <summary>默认Y轴ID。</summary>
    public const string DefaultYAxisId = "DefaultYAxis";

    static Chart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Chart), new FrameworkPropertyMetadata(typeof(Chart)));
        BackgroundProperty.OverrideMetadata(typeof(Chart), new FrameworkPropertyMetadata(Brushes.Transparent));
    }

    /// <summary>初始化 Chart 实例。</summary>
    public Chart()
    {
        _renderContext.XRangeAccessor = GetCurrentXRange;
        _renderContext.YRangeAccessor = GetCurrentYRange;
        _renderContext.SeriesAccessor = GetCurrentSeriesList;
        _renderContext.XAxesAccessor = GetCurrentXAxesList;
        _renderContext.YAxesAccessor = GetCurrentYAxesList;
        _renderContext.OnRangeChanged = ApplyRenderContextRangeChange;
        _renderContext.OnAxisRangeChanged = ApplyRenderContextAxisRangeChange;
        _markDirtyFromDispatcher = () =>
        {
            Interlocked.Exchange(ref _pendingUiDirtyRequest, 0);
            MarkDirty();
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        XAxes = [];
        YAxes = [];
    }

    #region 依赖属性

    /// <summary>标识 <see cref="Series"/> 依赖属性。</summary>
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(nameof(Series), typeof(ObservableCollection<IRenderableSeries>), typeof(Chart),
            new PropertyMetadata(null, OnSeriesChanged));

    /// <summary>获取或设置图表系列集合。</summary>
    public ObservableCollection<IRenderableSeries>? Series
    {
        get => (ObservableCollection<IRenderableSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>标识 <see cref="XRange"/> 依赖属性。</summary>
    public static readonly DependencyProperty XRangeProperty =
        DependencyProperty.Register(nameof(XRange), typeof(DataRange), typeof(Chart),
            new FrameworkPropertyMetadata(new DataRange(0, 100), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    /// <summary>获取或设置X轴可见数据范围。</summary>
    public DataRange XRange
    {
        get => (DataRange)GetValue(XRangeProperty);
        set => SetValue(XRangeProperty, value);
    }

    /// <summary>标识 <see cref="YRange"/> 依赖属性。</summary>
    public static readonly DependencyProperty YRangeProperty =
        DependencyProperty.Register(nameof(YRange), typeof(DataRange), typeof(Chart),
            new FrameworkPropertyMetadata(new DataRange(-1, 1), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    /// <summary>获取或设置Y轴可见数据范围。</summary>
    public DataRange YRange
    {
        get => (DataRange)GetValue(YRangeProperty);
        set => SetValue(YRangeProperty, value);
    }

    /// <summary>标识 <see cref="XAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty XAxesProperty =
        DependencyProperty.Register(nameof(XAxes), typeof(ObservableCollection<IAxis>), typeof(Chart),
            new PropertyMetadata(null, OnAxesChanged));

    /// <summary>获取或设置X轴集合。</summary>
    public ObservableCollection<IAxis> XAxes
    {
        get => (ObservableCollection<IAxis>)GetValue(XAxesProperty);
        set => SetValue(XAxesProperty, value);
    }

    /// <summary>标识 <see cref="YAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty YAxesProperty =
        DependencyProperty.Register(nameof(YAxes), typeof(ObservableCollection<IAxis>), typeof(Chart),
            new PropertyMetadata(null, OnAxesChanged));

    /// <summary>获取或设置Y轴集合。</summary>
    public ObservableCollection<IAxis> YAxes
    {
        get => (ObservableCollection<IAxis>)GetValue(YAxesProperty);
        set => SetValue(YAxesProperty, value);
    }

    /// <summary>标识 <see cref="BottomXAxes"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey BottomXAxesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(BottomXAxes), typeof(IEnumerable<IAxis>), typeof(Chart),
            new PropertyMetadata(Enumerable.Empty<IAxis>()));
    /// <summary>标识 <see cref="BottomXAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty BottomXAxesProperty = BottomXAxesPropertyKey.DependencyProperty;
    /// <summary>获取底部X轴集合。</summary>
    public IEnumerable<IAxis> BottomXAxes { get => (IEnumerable<IAxis>)GetValue(BottomXAxesProperty); private set => SetValue(BottomXAxesPropertyKey, value); }

    /// <summary>标识 <see cref="TopXAxes"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey TopXAxesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TopXAxes), typeof(IEnumerable<IAxis>), typeof(Chart),
            new PropertyMetadata(Enumerable.Empty<IAxis>()));
    /// <summary>标识 <see cref="TopXAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty TopXAxesProperty = TopXAxesPropertyKey.DependencyProperty;
    /// <summary>获取顶部X轴集合。</summary>
    public IEnumerable<IAxis> TopXAxes { get => (IEnumerable<IAxis>)GetValue(TopXAxesProperty); private set => SetValue(TopXAxesPropertyKey, value); }

    /// <summary>标识 <see cref="LeftYAxes"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey LeftYAxesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(LeftYAxes), typeof(IEnumerable<IAxis>), typeof(Chart),
            new PropertyMetadata(Enumerable.Empty<IAxis>()));
    /// <summary>标识 <see cref="LeftYAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty LeftYAxesProperty = LeftYAxesPropertyKey.DependencyProperty;
    /// <summary>获取左侧Y轴集合。</summary>
    public IEnumerable<IAxis> LeftYAxes { get => (IEnumerable<IAxis>)GetValue(LeftYAxesProperty); private set => SetValue(LeftYAxesPropertyKey, value); }

    /// <summary>标识 <see cref="RightYAxes"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey RightYAxesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(RightYAxes), typeof(IEnumerable<IAxis>), typeof(Chart),
            new PropertyMetadata(Enumerable.Empty<IAxis>()));
    /// <summary>标识 <see cref="RightYAxes"/> 依赖属性。</summary>
    public static readonly DependencyProperty RightYAxesProperty = RightYAxesPropertyKey.DependencyProperty;
    /// <summary>获取右侧Y轴集合。</summary>
    public IEnumerable<IAxis> RightYAxes { get => (IEnumerable<IAxis>)GetValue(RightYAxesProperty); private set => SetValue(RightYAxesPropertyKey, value); }

    /// <summary>标识 <see cref="TopLeftCornerBrush"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey TopLeftCornerBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TopLeftCornerBrush), typeof(Brush), typeof(Chart),
            new PropertyMetadata(Brushes.Transparent));
    /// <summary>标识 <see cref="TopLeftCornerBrush"/> 依赖属性。</summary>
    public static readonly DependencyProperty TopLeftCornerBrushProperty = TopLeftCornerBrushPropertyKey.DependencyProperty;
    /// <summary>获取左上角画刷。</summary>
    public Brush TopLeftCornerBrush { get => (Brush)GetValue(TopLeftCornerBrushProperty); private set => SetValue(TopLeftCornerBrushPropertyKey, value); }

    /// <summary>标识 <see cref="TopRightCornerBrush"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey TopRightCornerBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TopRightCornerBrush), typeof(Brush), typeof(Chart),
            new PropertyMetadata(Brushes.Transparent));
    /// <summary>标识 <see cref="TopRightCornerBrush"/> 依赖属性。</summary>
    public static readonly DependencyProperty TopRightCornerBrushProperty = TopRightCornerBrushPropertyKey.DependencyProperty;
    /// <summary>获取右上角画刷。</summary>
    public Brush TopRightCornerBrush { get => (Brush)GetValue(TopRightCornerBrushProperty); private set => SetValue(TopRightCornerBrushPropertyKey, value); }

    /// <summary>标识 <see cref="BottomLeftCornerBrush"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey BottomLeftCornerBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(BottomLeftCornerBrush), typeof(Brush), typeof(Chart),
            new PropertyMetadata(Brushes.Transparent));
    /// <summary>标识 <see cref="BottomLeftCornerBrush"/> 依赖属性。</summary>
    public static readonly DependencyProperty BottomLeftCornerBrushProperty = BottomLeftCornerBrushPropertyKey.DependencyProperty;
    /// <summary>获取左下角画刷。</summary>
    public Brush BottomLeftCornerBrush { get => (Brush)GetValue(BottomLeftCornerBrushProperty); private set => SetValue(BottomLeftCornerBrushPropertyKey, value); }

    /// <summary>标识 <see cref="BottomRightCornerBrush"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey BottomRightCornerBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(BottomRightCornerBrush), typeof(Brush), typeof(Chart),
            new PropertyMetadata(Brushes.Transparent));
    /// <summary>标识 <see cref="BottomRightCornerBrush"/> 依赖属性。</summary>
    public static readonly DependencyProperty BottomRightCornerBrushProperty = BottomRightCornerBrushPropertyKey.DependencyProperty;
    /// <summary>获取右下角画刷。</summary>
    public Brush BottomRightCornerBrush { get => (Brush)GetValue(BottomRightCornerBrushProperty); private set => SetValue(BottomRightCornerBrushPropertyKey, value); }

    /// <summary>标识 <see cref="Modifiers"/> 依赖属性。</summary>
    public static readonly DependencyProperty ModifiersProperty =
        DependencyProperty.Register(nameof(Modifiers), typeof(ObservableCollection<IChartModifier>), typeof(Chart),
            new PropertyMetadata(null, OnModifiersChanged));

    /// <summary>获取或设置图表修饰器集合。</summary>
    public ObservableCollection<IChartModifier>? Modifiers
    {
        get => (ObservableCollection<IChartModifier>?)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    /// <summary>标识 <see cref="Legend"/> 依赖属性。</summary>
    public static readonly DependencyProperty LegendProperty =
        DependencyProperty.Register(nameof(Legend), typeof(ILegend), typeof(Chart),
            new PropertyMetadata(null, OnLegendChanged));

    /// <summary>获取或设置图表图例。</summary>
    public ILegend? Legend
    {
        get => (ILegend?)GetValue(LegendProperty);
        set => SetValue(LegendProperty, value);
    }

    /// <summary>标识 <see cref="RendererPreference"/> 依赖属性。</summary>
    public static readonly DependencyProperty RendererPreferenceProperty =
        DependencyProperty.Register(nameof(RendererPreference), typeof(ChartRendererPreference), typeof(Chart),
            new PropertyMetadata(ChartRendererPreference.Auto, OnRendererPreferenceChanged));

    /// <summary>获取或设置渲染器偏好。</summary>
    public ChartRendererPreference RendererPreference
    {
        get => (ChartRendererPreference)GetValue(RendererPreferenceProperty);
        set => SetValue(RendererPreferenceProperty, value);
    }

    /// <summary>标识 <see cref="ActualRendererBackend"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey ActualRendererBackendPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ActualRendererBackend), typeof(ChartRendererBackend), typeof(Chart),
            new PropertyMetadata(ChartRendererBackend.Unknown));
    /// <summary>标识 <see cref="ActualRendererBackend"/> 依赖属性。</summary>
    public static readonly DependencyProperty ActualRendererBackendProperty = ActualRendererBackendPropertyKey.DependencyProperty;
    /// <summary>获取实际渲染后端类型。</summary>
    public ChartRendererBackend ActualRendererBackend { get => (ChartRendererBackend)GetValue(ActualRendererBackendProperty); private set => SetValue(ActualRendererBackendPropertyKey, value); }

    /// <summary>标识 <see cref="PlotAreaBackground"/> 依赖属性。</summary>
    public static readonly DependencyProperty PlotAreaBackgroundProperty =
        DependencyProperty.Register(nameof(PlotAreaBackground), typeof(Brush), typeof(Chart),
            new PropertyMetadata(Brushes.Black));
    /// <summary>获取或设置绘图区背景画刷。</summary>
    public Brush PlotAreaBackground { get => (Brush)GetValue(PlotAreaBackgroundProperty); set => SetValue(PlotAreaBackgroundProperty, value); }

    /// <summary>标识 <see cref="PlotAreaBorderBrush"/> 依赖属性。</summary>
    public static readonly DependencyProperty PlotAreaBorderBrushProperty =
        DependencyProperty.Register(nameof(PlotAreaBorderBrush), typeof(Brush), typeof(Chart),
            new PropertyMetadata(null));
    /// <summary>获取或设置绘图区边框画刷。</summary>
    public Brush? PlotAreaBorderBrush { get => (Brush?)GetValue(PlotAreaBorderBrushProperty); set => SetValue(PlotAreaBorderBrushProperty, value); }

    /// <summary>标识 <see cref="PlotAreaBorderThickness"/> 依赖属性。</summary>
    public static readonly DependencyProperty PlotAreaBorderThicknessProperty =
        DependencyProperty.Register(nameof(PlotAreaBorderThickness), typeof(Thickness), typeof(Chart),
            new PropertyMetadata(new Thickness(0), OnPlotAreaBorderThicknessChanged));
    /// <summary>获取或设置绘图区边框厚度。</summary>
    public Thickness PlotAreaBorderThickness { get => (Thickness)GetValue(PlotAreaBorderThicknessProperty); set => SetValue(PlotAreaBorderThicknessProperty, value); }

    /// <summary>标识 <see cref="Fps"/> 依赖属性。</summary>
    public static readonly DependencyProperty FpsProperty =
        DependencyProperty.Register(nameof(Fps), typeof(double), typeof(Chart),
            new PropertyMetadata(0.0));
    /// <summary>获取当前帧率。</summary>
    public double Fps { get => (double)GetValue(FpsProperty); private set => SetValue(FpsProperty, value); }

    /// <summary>标识 <see cref="ShowFps"/> 依赖属性。</summary>
    public static readonly DependencyProperty ShowFpsProperty =
        DependencyProperty.Register(nameof(ShowFps), typeof(bool), typeof(Chart),
            new PropertyMetadata(false, OnShowFpsChanged));
    /// <summary>获取或设置是否显示帧率。</summary>
    public bool ShowFps { get => (bool)GetValue(ShowFpsProperty); set => SetValue(ShowFpsProperty, value); }

    /// <summary>标识 <see cref="EnableAntialiasing"/> 依赖属性。</summary>
    public static readonly DependencyProperty EnableAntialiasingProperty =
        DependencyProperty.Register(nameof(EnableAntialiasing), typeof(bool), typeof(Chart),
            new PropertyMetadata(true, OnEnableAntialiasingChanged));
    /// <summary>获取或设置是否启用抗锯齿。</summary>
    public bool EnableAntialiasing { get => (bool)GetValue(EnableAntialiasingProperty); set => SetValue(EnableAntialiasingProperty, value); }

    /// <summary>标识 <see cref="RenderError"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey RenderErrorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(RenderError), typeof(string), typeof(Chart),
            new PropertyMetadata(string.Empty));
    /// <summary>标识 <see cref="RenderError"/> 依赖属性。</summary>
    public static readonly DependencyProperty RenderErrorProperty = RenderErrorPropertyKey.DependencyProperty;
    /// <summary>获取渲染错误信息。</summary>
    public string RenderError { get => (string)GetValue(RenderErrorProperty); private set => SetValue(RenderErrorPropertyKey, value); }

    /// <summary>标识 <see cref="HasRenderError"/> 只读依赖属性。</summary>
    private static readonly DependencyPropertyKey HasRenderErrorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasRenderError), typeof(bool), typeof(Chart),
            new PropertyMetadata(false));
    /// <summary>标识 <see cref="HasRenderError"/> 依赖属性。</summary>
    public static readonly DependencyProperty HasRenderErrorProperty = HasRenderErrorPropertyKey.DependencyProperty;
    /// <summary>获取是否存在渲染错误。</summary>
    public bool HasRenderError { get => (bool)GetValue(HasRenderErrorProperty); private set => SetValue(HasRenderErrorPropertyKey, value); }

    private static void OnModifiersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            chart.OnModifiersCollectionChanged(
                e.OldValue as ObservableCollection<IChartModifier>,
                e.NewValue as ObservableCollection<IChartModifier>);
            chart.MarkDirty();
        }
    }

    private static void OnLegendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            chart.UpdateLegendSeries();
        }
    }

    private static void OnRendererPreferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
            chart.OnRendererPreferenceChanged();
    }

    private static void OnShowFpsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
        {
            chart.UpdateSurfaceRefreshMode();
            chart.MarkDirty();
        }
    }

    private static void OnEnableAntialiasingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
            chart.MarkDirty();
    }

    private static void OnPlotAreaBorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
            chart.UpdateAxisPresenterOverlap();
    }

    /// <summary>标识 <see cref="IsStreaming"/> 依赖属性。</summary>
    public static readonly DependencyProperty IsStreamingProperty =
        DependencyProperty.Register(nameof(IsStreaming), typeof(bool), typeof(Chart),
            new PropertyMetadata(false, OnIsStreamingChanged));

    /// <summary>
    /// 获取或设置是否处于流数据模式。
    /// 当 IsStreaming=true 时，AlwaysRefresh 始终保持开启，渲染节奏由显示器刷新率驱动，
    /// 消除宽限期切换导致的帧率波动。适用于实时数据推送场景。
    /// </summary>
    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    private static void OnIsStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Chart chart)
            chart.UpdateSurfaceRefreshMode();
    }

    #endregion

    #region 私有字段

    private DrawingSurface? _surface;
    private Image? _softwareSurface;
    private GridLinesControl? _gridLines;
    private IRenderer? _renderer;
    private readonly ChartRenderContext _renderContext = new();
    private readonly SeriesRendererDispatcher _seriesRenderer = new();
    private RenderLoop? _renderLoop;
    private bool _isMouseDown;
    private Point _lastMousePosition = new Point();
    private volatile bool _isDirty = true;
    private int _actualRendererBackendState = (int)ChartRendererBackend.Unknown;
    private bool _modifiersManagedByChart;
    private bool _surfaceContentLoaded;
    private readonly HashSet<IChartModifier> _attachedModifiers = new();

    internal IChartRendererFactory RendererFactory { get; set; } = new DefaultChartRendererFactory();

    #endregion

    #region 方法

    private void MarkDirty()
    {
        _isDirty = true;
        var surface = _surface;
        if (surface == null)
            return;

        bool wasAlwaysRefreshing = surface.AlwaysRefresh;
        RequestContinuousRefreshBurst();

        if (_invalidatePending)
            return;

        if (surface.AlwaysRefresh && wasAlwaysRefreshing)
            return;

        _invalidatePending = true;
        surface.Invalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureDefaultAxes();
        EnsureDefaultModifiers();
        if (Modifiers != null)
            AttachModifiers(Modifiers);
        RefreshModifierContexts();
        UpdateFpsTimerState();
        UpdateRenderContext();
        MarkDirty();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachAllModifiers();
        CleanupRenderingState();
    }

    private void OnSurfaceLoadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        InitializeRendererForCurrentPreference(e);
        SignalViewportChanged();
    }

    private void OnSurfaceUnloadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        CleanupRenderingState();
    }

    private void OnSurfaceDraw(object? sender, DrawEventArgs e)
    {
        if (_surface == null || _renderer == null)
            return;

        if (!_isDirty)
        {
            RefreshContinuousRefreshMode();
            return;
        }

        try
        {
            _invalidatePending = false;

            if (!_isDirty)
                return;

            int width = Math.Max(0, _surface.TextureWidth);
            int height = Math.Max(0, _surface.TextureHeight);
            if (width <= 0 || height <= 0)
                return;

            UpdateRenderContext();
            var frames = CollectFramesCore(GetCurrentSeriesList());

            var renderGroups = _seriesRenderer.RenderGroups(frames, _renderContext, width, height);
            if (_renderer.Render(e, width, height, Colors.Transparent, EnableAntialiasing, renderGroups))
            {
                _consecutiveRenderFailures = 0;
                StopRenderRetryTimer();
                SetRenderError(_renderer.LastError);
                PushSoftwareFrame();
                RecordRenderedFrame();
                _isDirty = false;
                return;
            }

            _consecutiveRenderFailures++;
            if (TryFallbackToSoftware(_renderer.LastError ?? "Rendering failed."))
                return;

            SetRenderError(_renderer.LastError ?? "Rendering failed.");
            _isDirty = false;
            ScheduleRenderRetry();
        }
        catch (Exception ex)
        {
            _consecutiveRenderFailures++;
            if (TryFallbackToSoftware($"Render exception: {ex.GetType().Name}: {ex.Message}"))
                return;

            SetRenderError($"Render exception: {ex.GetType().Name}: {ex.Message}");
            _isDirty = false;
            ScheduleRenderRetry();
        }
        finally
        {
            RefreshContinuousRefreshMode();
        }
    }

    /// <summary>应用控件模板。</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_surface != null)
        {
            CleanupRenderingState();
            _surface.SizeChanged -= OnSurfaceSizeChanged;
            _surface.LoadContent -= OnSurfaceLoadContent;
            _surface.Draw -= OnSurfaceDraw;
            _surface.UnloadContent -= OnSurfaceUnloadContent;
        }

        if (_gridLines != null)
            _gridLines.SizeChanged -= OnGridLinesSizeChanged;

        _surface = GetTemplateChild("PART_Surface") as DrawingSurface;
        _softwareSurface = GetTemplateChild("PART_SoftwareSurface") as Image;
        _gridLines = GetTemplateChild("PART_GridLines") as GridLinesControl;
        _topAxesPresenter = GetTemplateChild("PART_TopAxesPresenter") as FrameworkElement;
        _leftAxesPresenter = GetTemplateChild("PART_LeftAxesPresenter") as FrameworkElement;
        _rightAxesPresenter = GetTemplateChild("PART_RightAxesPresenter") as FrameworkElement;
        _bottomAxesPresenter = GetTemplateChild("PART_BottomAxesPresenter") as FrameworkElement;
        InvalidatePlotAreaMetrics();
        InvalidateAxisPlacementCaches();

        if (_surface != null)
        {
            UpdateSurfaceRefreshMode();
            _surface.SizeChanged += OnSurfaceSizeChanged;
            _surface.LoadContent += OnSurfaceLoadContent;
            _surface.Draw += OnSurfaceDraw;
            _surface.UnloadContent += OnSurfaceUnloadContent;
        }

        if (_gridLines != null)
            _gridLines.SizeChanged += OnGridLinesSizeChanged;

        UpdateAxisPresenterOverlap();
        UpdateModifiers();
        UpdateRenderContext();
        UpdateSurfaceVisualState();
        SignalViewportChanged();
    }

    /// <summary>处理渲染尺寸变更。</summary>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidatePlotAreaMetrics();
        SignalViewportChanged();
    }

    /// <summary>处理鼠标按下事件。</summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        bool isInPlotArea = IsPointInPlotArea(e.GetPosition(this));

        if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed && isInPlotArea)
        {
            ZoomExtents();
            e.Handled = true;
            return;
        }

        _isMouseDown = isInPlotArea;
        _lastMousePosition = e.GetPosition(this);
        ForwardMouseEvent(e);
    }

    /// <summary>处理鼠标释放事件。</summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _isMouseDown = false;
        ForwardMouseEvent(e);
    }

    /// <summary>处理鼠标移动事件。</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isMouseDown)
        {
            Point currentPos = e.GetPosition(this);
            _lastMousePosition = currentPos;
            ForwardMouseEvent(e);
        }
    }

    /// <summary>处理鼠标滚轮事件。</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ForwardMouseEvent(e);
    }

    private void ForwardMouseEvent(MouseEventArgs e)
    {
        if (Modifiers == null)
            return;

        bool isInPlotArea = IsPointInPlotArea(e.GetPosition(this));

        if (e is MouseButtonEventArgs buttonArgs)
        {
            if (buttonArgs.RoutedEvent == Mouse.MouseDownEvent)
            {
                if (!isInPlotArea)
                    return;

                foreach (var modifier in Modifiers)
                    modifier.OnMouseDown(buttonArgs);
            }
            else if (buttonArgs.RoutedEvent == Mouse.MouseUpEvent)
            {
                foreach (var modifier in Modifiers)
                    modifier.OnMouseUp(buttonArgs);
            }
        }
        else if (e.RoutedEvent == Mouse.MouseMoveEvent)
        {
            if (!isInPlotArea && !_isMouseDown)
                return;

            foreach (var modifier in Modifiers)
                modifier.OnMouseMove(e);
        }
        else if (e is MouseWheelEventArgs wheelArgs && e.RoutedEvent == Mouse.MouseWheelEvent)
        {
            if (!isInPlotArea)
                return;

            foreach (var modifier in Modifiers)
                modifier.OnMouseWheel(wheelArgs);
        }
    }

    private bool IsPointInPlotArea(Point chartPoint)
    {
        if (_surface == null)
            return true;

        var surfacePoint = TranslatePoint(chartPoint, _surface);
        return surfacePoint.X >= 0
            && surfacePoint.Y >= 0
            && surfacePoint.X <= _surface.ActualWidth
            && surfacePoint.Y <= _surface.ActualHeight;
    }

    private DataRange GetCurrentXRange() => XRange;
    private DataRange GetCurrentYRange() => YRange;
    private IList<IRenderableSeries> GetCurrentSeriesList() => Series is not null ? Series : s_emptySeries;
    private IList<IAxis> GetCurrentXAxesList() => XAxes is not null ? XAxes : s_emptyAxes;
    private IList<IAxis> GetCurrentYAxesList() => YAxes is not null ? YAxes : s_emptyAxes;

    private void ApplyRenderContextRangeChange(DataRange xRange, DataRange yRange)
    {
        XRange = xRange;
        YRange = yRange;
        SignalViewportChanged();
    }

    /// <summary>自动缩放所有轴到数据范围。</summary>
    public void ZoomExtents()
    {
        if (Series == null || Series.Count == 0)
            return;

        var visibleSeries = Series
            .Where(s => s.IsVisible && s.DataSeries != null && s.DataSeries.Count > 0)
            .ToArray();

        if (visibleSeries.Length == 0)
            return;

        var touchedXAxisIds = new HashSet<string>(StringComparer.Ordinal);
        var touchedYAxisIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var xGroup in visibleSeries.GroupBy(s => s.XAxisId))
        {
            var xAxis = FindAxisOrDefault(XAxes, xGroup.Key);
            var xDataSeries = xGroup.Select(s => s.DataSeries);
            var autoXRange = xAxis.CalculateAutoRange(xDataSeries);
            touchedXAxisIds.Add(xGroup.Key);
            xAxis.VisibleRange = autoXRange;

            if (xGroup.Key == DefaultXAxisId)
                XRange = autoXRange;
        }

        foreach (var yGroup in visibleSeries.GroupBy(s => s.YAxisId))
        {
            var yAxis = FindAxisOrDefault(YAxes, yGroup.Key);
            var yDataSeries = yGroup.Select(s => s.DataSeries);
            var autoYRange = yAxis.CalculateAutoRange(yDataSeries);
            touchedYAxisIds.Add(yGroup.Key);
            yAxis.VisibleRange = autoYRange;

            if (yGroup.Key == DefaultYAxisId)
                YRange = autoYRange;
        }

        SyncUntouchedAxesToDefaultRange(XAxes, touchedXAxisIds, DefaultXAxisId);
        SyncUntouchedAxesToDefaultRange(YAxes, touchedYAxisIds, DefaultYAxisId);
    }

    private void UpdateModifiers()
    {
        EnsureDefaultModifiers();
        RefreshModifierContexts();
    }

    private void UpdateLegendSeries()
    {
        var legend = Legend;
        var series = Series;
        if (legend != null && series != null)
            legend.SetSeries(series);
    }

    #endregion
}
