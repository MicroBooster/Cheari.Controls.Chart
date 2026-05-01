using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Vortice.Wpf;

namespace Cheari.Controls;

/// <summary>
/// Chart 的绘图表面管理逻辑：FPS 计时器、渲染重试、连续刷新模式。
/// </summary>
public partial class Chart
{
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private int _fpsFrameCount;
    private double _smoothedFps;
    private DispatcherTimer? _fpsUpdateTimer;
    private int _consecutiveRenderFailures;
    private DispatcherTimer? _renderRetryTimer;
    private long _continuousRefreshUntilTimestamp;
    private volatile bool _continuousRefreshActive;
    private int _pendingUiDirtyRequest;
    private bool _invalidatePending;
    private readonly Action _markDirtyFromDispatcher;

    private void UpdateSurfaceRefreshMode()
    {
        RefreshContinuousRefreshMode();
        UpdateFpsTimerState();
    }

    private void RequestContinuousRefreshBurst()
    {
        if (_surface == null || ActualRendererBackend != ChartRendererBackend.D3D11)
            return;

        _continuousRefreshUntilTimestamp = Stopwatch.GetTimestamp() + s_continuousRefreshGraceTicks;
        RefreshContinuousRefreshMode();
    }

    private void RefreshContinuousRefreshMode()
    {
        if (_surface == null)
            return;

        bool shouldAlwaysRefresh = ShouldUseContinuousRefresh();
        _continuousRefreshActive = shouldAlwaysRefresh;
        if (_surface.AlwaysRefresh == shouldAlwaysRefresh)
            return;

        _surface.AlwaysRefresh = shouldAlwaysRefresh;
    }

    private bool ShouldUseContinuousRefresh()
    {
        if (_surface == null || ActualRendererBackend != ChartRendererBackend.D3D11)
            return false;

        if (IsStreaming)
            return true;

        return Stopwatch.GetTimestamp() <= Volatile.Read(ref _continuousRefreshUntilTimestamp);
    }

    private void RequestRedrawFromAnyThread()
    {
        if (!Dispatcher.CheckAccess() && _continuousRefreshActive)
        {
            _isDirty = true;
            Volatile.Write(ref _continuousRefreshUntilTimestamp, Stopwatch.GetTimestamp() + s_continuousRefreshGraceTicks);
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            MarkDirty();
            return;
        }

        if (Interlocked.Exchange(ref _pendingUiDirtyRequest, 1) != 0)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, _markDirtyFromDispatcher);
    }

    private void NotifyStreamingActivityFromAnyThread()
    {
        if (_surface == null || GetActualRendererBackendState() != ChartRendererBackend.D3D11)
        {
            RequestRedrawFromAnyThread();
            return;
        }

        Volatile.Write(ref _continuousRefreshUntilTimestamp, Stopwatch.GetTimestamp() + s_continuousRefreshGraceTicks);
        if (_continuousRefreshActive)
            return;

        RequestRedrawFromAnyThread();
    }

    private ChartRendererBackend GetActualRendererBackendState()
        => (ChartRendererBackend)Volatile.Read(ref _actualRendererBackendState);

    private void ScheduleRenderRetry()
    {
        if (_surface == null)
            return;

        if (_renderRetryTimer != null)
            return;

        int delayMilliseconds = Math.Min(1000, Math.Max(100, _consecutiveRenderFailures * 100));
        _renderRetryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(delayMilliseconds)
        };
        _renderRetryTimer.Tick += OnRenderRetryTimerTick;
        _renderRetryTimer.Start();
    }

    private void OnRenderRetryTimerTick(object? sender, EventArgs e)
    {
        StopRenderRetryTimer();
        MarkDirty();
    }

    private void StopRenderRetryTimer()
    {
        if (_renderRetryTimer == null)
            return;

        _renderRetryTimer.Stop();
        _renderRetryTimer.Tick -= OnRenderRetryTimerTick;
        _renderRetryTimer = null;
    }

    private void UpdateFpsTimerState()
    {
        if (!ShowFps || !IsLoaded)
        {
            StopFpsTimer();
            ResetFpsState();
            return;
        }

        _fpsStopwatch.Restart();
        _fpsFrameCount = 0;
        _smoothedFps = 0.0;
        CreateFpsTimerIfNeeded();
        _fpsUpdateTimer!.Start();
    }

    private void CreateFpsTimerIfNeeded()
    {
        if (_fpsUpdateTimer != null)
            return;

        _fpsUpdateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _fpsUpdateTimer.Tick += OnFpsUpdateTimerTick;
    }

    private void OnFpsUpdateTimerTick(object? sender, EventArgs e)
    {
        double elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
        if (elapsed <= 0)
            return;

        double sampleFps = _fpsFrameCount / elapsed;
        _smoothedFps = _smoothedFps <= 0.0
            ? sampleFps
            : (_smoothedFps * 0.65) + (sampleFps * 0.35);
        Fps = _smoothedFps;
        _fpsFrameCount = 0;
        _fpsStopwatch.Restart();
    }

    private void StopFpsTimer()
    {
        if (_fpsUpdateTimer == null)
            return;

        _fpsUpdateTimer.Stop();
    }

    private void ResetFpsState()
    {
        _fpsFrameCount = 0;
        _smoothedFps = 0.0;
        Fps = 0.0;
        _fpsStopwatch.Reset();
    }

    private void RecordRenderedFrame()
    {
        if (!ShowFps || !IsLoaded)
            return;

        if (!_fpsStopwatch.IsRunning)
            _fpsStopwatch.Start();

        _fpsFrameCount++;
    }

    private void SetRenderError(string? error)
    {
        RenderError = error ?? string.Empty;
        HasRenderError = !string.IsNullOrWhiteSpace(error);
    }
}
