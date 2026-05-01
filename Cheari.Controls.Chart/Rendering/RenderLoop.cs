using System.Collections.Concurrent;
using System.Diagnostics;
using Cheari.Controls.Data;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 后台数据快照调度器。
/// 只负责缓存每个数据系列的最新快照并合并失效请求，不执行任何 UI 或渲染器调用。
/// 
/// <para><b>设计说明：</b></para>
/// <para>RenderLoop 不再进行帧率限制。渲染节奏完全由 DrawingSurface.AlwaysRefresh 驱动，
/// 确保与显示器刷新率同步。RenderLoop 仅负责：</para>
/// <list type="number">
///   <item>接收数据/视口变更信号</item>
///   <item>在后台线程创建 DataFrame 快照（避免阻塞 UI 线程）</item>
///   <item>通知 UI 线程需要重绘</item>
/// </list>
/// 
/// <para><b>线程安全说明：</b></para>
/// <para>使用 ConcurrentDictionary 存储快照，UI 线程的 TryGetLatestFrame 无需加锁，
/// 不会阻塞在后台线程的快照更新上。</para>
/// </summary>
internal sealed class RenderLoop : IDisposable
{
    private readonly Action _invalidateSurface;
    private readonly Thread _renderThread;
    private readonly AutoResetEvent _wakeSignal = new(false);
    private readonly ConcurrentDictionary<IDataSeries, DataFrame> _latestFrames = new();
    private readonly object _dirtySyncRoot = new();
    private readonly HashSet<IDataSeries> _dirtySeries = [];

    private volatile bool _running;
    private bool _threadStarted;
    private bool _dataChanged;
    private bool _viewportChanged;

    public RenderLoop(Action invalidateSurface)
    {
        ArgumentNullException.ThrowIfNull(invalidateSurface);

        _invalidateSurface = invalidateSurface;
        _renderThread = new Thread(RenderThreadProc)
        {
            Name = "ChartRenderLoop",
            IsBackground = true
        };
    }

    public void Start()
    {
        if (_threadStarted)
            return;

        _running = true;
        _threadStarted = true;
        _renderThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _wakeSignal.Set();

        if (_threadStarted && _renderThread.IsAlive)
            _renderThread.Join(1000);
    }

    public void SignalDataChanged(IDataSeries dataSeries, DataFrame newFrame)
    {
        ArgumentNullException.ThrowIfNull(dataSeries);
        ArgumentNullException.ThrowIfNull(newFrame);

        var oldFrame = _latestFrames.AddOrUpdate(
            dataSeries,
            _ => newFrame,
            (_, existing) => existing.Version > newFrame.Version ? existing : newFrame);

        if (!ReferenceEquals(oldFrame, newFrame))
            oldFrame?.Return();

        lock (_dirtySyncRoot)
        {
            _dataChanged = true;
        }

        _wakeSignal.Set();
    }

    public void SignalDataChanged(IDataSeries dataSeries)
    {
        ArgumentNullException.ThrowIfNull(dataSeries);

        lock (_dirtySyncRoot)
        {
            _dirtySeries.Add(dataSeries);
            _dataChanged = true;
        }

        _wakeSignal.Set();
    }

    public void SignalViewportChanged()
    {
        lock (_dirtySyncRoot)
        {
            _viewportChanged = true;
        }

        _wakeSignal.Set();
    }

    public bool TryGetLatestFrame(IDataSeries dataSeries, int minimumVersion, out DataFrame? frame)
    {
        ArgumentNullException.ThrowIfNull(dataSeries);

        if (_latestFrames.TryGetValue(dataSeries, out var latest) && latest.Version >= minimumVersion)
        {
            frame = latest;
            return true;
        }

        frame = null;
        return false;
    }

    public void RemoveSeries(IDataSeries dataSeries)
    {
        ArgumentNullException.ThrowIfNull(dataSeries);

        if (_latestFrames.TryRemove(dataSeries, out var frame))
            frame?.Return();

        lock (_dirtySyncRoot)
        {
            _dirtySeries.Remove(dataSeries);
        }
    }

    public void Clear()
    {
        foreach (var frame in _latestFrames.Values)
            frame?.Return();

        _latestFrames.Clear();

        lock (_dirtySyncRoot)
        {
            _dirtySeries.Clear();
            _dataChanged = false;
            _viewportChanged = false;
        }
    }

    private void RenderThreadProc()
    {
        while (_running)
        {
            try
            {
                _wakeSignal.WaitOne();
                if (!_running)
                    break;

                bool shouldInvalidate;
                List<IDataSeries>? seriesToSnapshot = null;
                lock (_dirtySyncRoot)
                {
                    shouldInvalidate = _dataChanged || _viewportChanged;
                    if (_dirtySeries.Count > 0)
                    {
                        seriesToSnapshot = _dirtySeries.ToList();
                        _dirtySeries.Clear();
                    }

                    _dataChanged = false;
                    _viewportChanged = false;
                }

                if (!shouldInvalidate)
                    continue;

                if (seriesToSnapshot != null)
                    RefreshPendingSnapshots(seriesToSnapshot);

                _invalidateSurface();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:O}] ChartRenderLoop error: {ex.GetType().Name}: {ex.Message}");

                lock (_dirtySyncRoot)
                {
                    _dirtySeries.Clear();
                    _dataChanged = false;
                    _viewportChanged = false;
                }
            }
        }
    }

    private void RefreshPendingSnapshots(IReadOnlyList<IDataSeries> seriesToSnapshot)
    {
        for (int i = 0; i < seriesToSnapshot.Count; i++)
        {
            var dataSeries = seriesToSnapshot[i];
            if (dataSeries is not IDataFrameProvider frameProvider)
                continue;

            try
            {
                var frame = frameProvider.CreateFrame();
                var oldFrame = _latestFrames.AddOrUpdate(
                    dataSeries,
                    _ => frame,
                    (_, existing) => existing.Version > frame.Version ? existing : frame);

                if (!ReferenceEquals(oldFrame, frame))
                {
                    frame.Return();
                }
                else if (oldFrame != null && _latestFrames.TryGetValue(dataSeries, out var current) && !ReferenceEquals(current, oldFrame))
                {
                    oldFrame.Return();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:O}] ChartRenderLoop snapshot error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _wakeSignal.Dispose();
    }
}
