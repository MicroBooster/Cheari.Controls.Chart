using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Series;
using Vortice.Wpf;

namespace Cheari.Controls;

public partial class Chart
{
    private ObservableCollection<IChartModifier>? _chartOwnedModifiers;
    private DrawingSurfaceEventArgs? _lastSurfaceLoadArgs;

    private void OnRendererPreferenceChanged()
    {
        if (!_surfaceContentLoaded)
        {
            SetActualRendererBackend(ChartRendererBackend.Unknown);
            UpdateSurfaceVisualState();
            MarkDirty();
            return;
        }

        RecreateRendererForCurrentPreference();
        SignalViewportChanged();
    }

    private void RecreateRendererForCurrentPreference()
    {
        CleanupRenderer(keepLoop: false, clearVisual: false);

        if (_lastSurfaceLoadArgs != null)
            InitializeRendererForCurrentPreference(_lastSurfaceLoadArgs);
        else if (RendererPreference == ChartRendererPreference.SoftwareOnly)
            ActivateSoftwareRenderer();
    }

    private void InitializeRendererForCurrentPreference(DrawingSurfaceEventArgs args)
    {
        _surfaceContentLoaded = true;
        _lastSurfaceLoadArgs = args;
        CleanupRenderer(keepLoop: false, clearVisual: false);

        switch (RendererPreference)
        {
            case ChartRendererPreference.SoftwareOnly:
                ActivateSoftwareRenderer();
                break;
            case ChartRendererPreference.HardwareOnly:
                ActivateHardwareRenderer(args, allowFallback: false);
                break;
            default:
                ActivateHardwareRenderer(args, allowFallback: true);
                break;
        }

        EnsureRenderLoop();
        UpdateSurfaceVisualState();
    }

    private void ActivateHardwareRenderer(DrawingSurfaceEventArgs args, bool allowFallback)
    {
        _renderer = RendererFactory.CreateHardwareRenderer();
        _renderer.Initialize(args);

        if (!string.IsNullOrWhiteSpace(_renderer.LastError))
        {
            var error = _renderer.LastError;
            if (allowFallback)
            {
                ActivateSoftwareRenderer();
                SetRenderError(error);
            }
            else
            {
                CleanupRenderer(keepLoop: true, clearVisual: true);
                SetRenderError(error);
            }

            return;
        }

        SetActualRendererBackend(_renderer.Backend);
        SetRenderError(string.Empty);
    }

    private void ActivateSoftwareRenderer()
    {
        CleanupRenderer(keepLoop: true, clearVisual: false);
        _renderer = RendererFactory.CreateSoftwareRenderer();
        _renderer.Initialize(_lastSurfaceLoadArgs!);
        SetActualRendererBackend(_renderer.Backend);
    }

    private bool TryFallbackToSoftware(string? error)
    {
        if (RendererPreference != ChartRendererPreference.Auto || _renderer?.Backend != ChartRendererBackend.D3D11)
            return false;

        ActivateSoftwareRenderer();
        SetRenderError(error);
        UpdateSurfaceVisualState();
        PushSoftwareFrame();
        MarkDirty();
        return true;
    }

    private void EnsureRenderLoop()
    {
        _renderLoop?.Dispose();
        _renderLoop = new RenderLoop(RequestRedrawFromAnyThread);
        _renderLoop.Start();
    }

    private void CleanupRenderer(bool keepLoop, bool clearVisual)
    {
        _renderer?.Uninitialize();
        _renderer?.Dispose();
        _renderer = null;
        SetActualRendererBackend(ChartRendererBackend.Unknown);

        if (!keepLoop)
        {
            _renderLoop?.Dispose();
            _renderLoop = null;
        }

        if (clearVisual)
            ClearSoftwareFrame();

        UpdateSurfaceVisualState();
    }

    private void CleanupRenderingState()
    {
        StopRenderRetryTimer();
        StopFpsTimer();
        ResetFpsState();
        _invalidatePending = false;
        _continuousRefreshActive = false;
        _continuousRefreshUntilTimestamp = 0;
        _pendingUiDirtyRequest = 0;
        _surfaceContentLoaded = false;
        _lastSurfaceLoadArgs = null;
        _renderLoop?.Clear();
        CleanupRenderer(keepLoop: false, clearVisual: true);
    }

    private void SignalViewportChanged()
    {
        _renderLoop?.SignalViewportChanged();
        MarkDirty();
    }

    private void PushSoftwareFrame()
    {
        if (_softwareSurface != null)
            _softwareSurface.Source = _renderer?.CurrentImageSource;
    }

    private void ClearSoftwareFrame()
    {
        if (_softwareSurface != null)
            _softwareSurface.Source = null;
    }

    private void UpdateSurfaceVisualState()
    {
        if (_surface != null)
            _surface.Opacity = ActualRendererBackend == ChartRendererBackend.Gdi ? 0.0 : 1.0;

        if (_softwareSurface != null)
            _softwareSurface.Visibility = ActualRendererBackend == ChartRendererBackend.Gdi ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetActualRendererBackend(ChartRendererBackend backend)
    {
        Volatile.Write(ref _actualRendererBackendState, (int)backend);
        ActualRendererBackend = backend;
        UpdateSurfaceVisualState();
        UpdateSurfaceRefreshMode();
    }

    private void OnModifiersCollectionChanged(
        ObservableCollection<IChartModifier>? oldCollection,
        ObservableCollection<IChartModifier>? newCollection)
    {
        if (oldCollection != null)
        {
            oldCollection.CollectionChanged -= OnModifierCollectionChanged;
            DetachModifiers(oldCollection);
        }

        if (newCollection == null || newCollection.Count == 0)
        {
            EnsureDefaultModifiers();
            return;
        }

        _modifiersManagedByChart = ReferenceEquals(newCollection, _chartOwnedModifiers);
        newCollection.CollectionChanged += OnModifierCollectionChanged;
        AttachModifiers(newCollection);
        RefreshModifierContexts();
    }

    private void OnModifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DetachAllModifiers();

            if (Modifiers == null || Modifiers.Count == 0)
            {
                EnsureDefaultModifiers();
                return;
            }

            AttachModifiers(Modifiers);
            RefreshModifierContexts();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (IChartModifier modifier in e.OldItems)
                DetachModifier(modifier);
        }

        if (e.NewItems != null)
        {
            foreach (IChartModifier modifier in e.NewItems)
                AttachModifier(modifier);
        }

        if (Modifiers == null || Modifiers.Count == 0)
        {
            EnsureDefaultModifiers();
            return;
        }

        RefreshModifierContexts();
    }

    private void EnsureDefaultModifiers()
    {
        if (Modifiers is { Count: > 0 })
            return;

        _chartOwnedModifiers = new ObservableCollection<IChartModifier>
        {
            new PanModifier(),
            new ZoomModifier()
        };

        Modifiers = _chartOwnedModifiers;
    }

    private void AttachModifiers(IEnumerable<IChartModifier> modifiers)
    {
        foreach (var modifier in modifiers)
            AttachModifier(modifier);
    }

    private void DetachModifiers(IEnumerable<IChartModifier> modifiers)
    {
        foreach (var modifier in modifiers)
            DetachModifier(modifier);
    }

    private void DetachAllModifiers()
    {
        foreach (var modifier in _attachedModifiers.ToArray())
            DetachModifier(modifier);
    }

    private void AttachModifier(IChartModifier modifier)
    {
        if (!_attachedModifiers.Add(modifier))
            return;

        modifier.OnAttached();
        modifier.SetContext(_renderContext);

        if (modifier is ChartModifierBase baseModifier)
            baseModifier.OverlayCanvas = _modifierOverlay;
    }

    private void DetachModifier(IChartModifier modifier)
    {
        if (!_attachedModifiers.Remove(modifier))
            return;

        if (modifier is ChartModifierBase baseModifier)
            baseModifier.OverlayCanvas = null;

        modifier.OnDetached();
    }

    private void RefreshModifierContexts()
    {
        if (Modifiers == null)
            return;

        foreach (var modifier in Modifiers)
            modifier.SetContext(_renderContext);
    }

    internal void EnsureRenderLoopForTesting()
    {
        EnsureRenderLoop();
    }

    internal void CacheSnapshotForTesting(IDataSeries dataSeries, DataFrame frame)
    {
        EnsureRenderLoop();
        _renderLoop!.SignalDataChanged(dataSeries, frame);
    }

    internal void InitializeRendererForTesting()
    {
        _surfaceContentLoaded = true;
        _lastSurfaceLoadArgs = null;
        CleanupRenderer(keepLoop: false, clearVisual: false);

        switch (RendererPreference)
        {
            case ChartRendererPreference.SoftwareOnly:
                ActivateSoftwareRenderer();
                break;
            case ChartRendererPreference.HardwareOnly:
                ActivateTestingHardwareRenderer(allowFallback: false);
                break;
            default:
                ActivateTestingHardwareRenderer(allowFallback: true);
                break;
        }

        EnsureRenderLoop();
        UpdateSurfaceVisualState();
    }

    internal IReadOnlyList<(IRenderableSeries series, DataFrame frame)> CollectFramesForCurrentRender()
    {
        UpdateRenderContext();
        return CollectFramesCore(GetCurrentSeriesList());
    }

    private List<(IRenderableSeries series, DataFrame frame)> CollectFramesCore(IList<IRenderableSeries> seriesList)
    {
        var frames = new List<(IRenderableSeries series, DataFrame frame)>(seriesList.Count);
        var renderLoop = _renderLoop;

        for (int i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            if (!series.IsVisible)
                continue;

            var dataSeries = series.DataSeries ?? throw new InvalidOperationException(
                $"Renderable series '{series.GetType().Name}' requires a non-null DataSeries.");

            DataFrame frame;
            if (renderLoop != null && renderLoop.TryGetLatestFrame(dataSeries, dataSeries.Version, out var cachedFrame) && cachedFrame != null)
            {
                frame = cachedFrame;
            }
            else
            {
                frame = RenderSeriesData.CreateFrame(dataSeries);
                renderLoop?.SignalDataChanged(dataSeries, frame);
            }

            if (frame.Count > 0)
                frames.Add((series, frame));
        }

        return frames;
    }

    private void ActivateTestingHardwareRenderer(bool allowFallback)
    {
        _renderer = RendererFactory.CreateHardwareRenderer();
        _renderer.Initialize(null!);

        if (!string.IsNullOrWhiteSpace(_renderer.LastError))
        {
            var error = _renderer.LastError;
            if (allowFallback)
            {
                ActivateSoftwareRenderer();
                SetRenderError(error);
            }
            else
            {
                CleanupRenderer(keepLoop: true, clearVisual: true);
                SetRenderError(error);
            }

            return;
        }

        SetActualRendererBackend(_renderer.Backend);
        SetRenderError(string.Empty);
    }
}
