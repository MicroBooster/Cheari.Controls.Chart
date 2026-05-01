using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;
using Vortice.Wpf;

namespace Cheari.Controls.Tests;

public class ChartRefactorTests
{
    [Fact]
    public void Chart_WhenModifiersMissing_InjectsDefaultPanAndZoom()
    {
        RunInSta(() =>
        {
            var chart = new Chart();
            InvokePrivate(chart, "UpdateModifiers");

            Assert.NotNull(chart.Modifiers);
            Assert.Collection(
                chart.Modifiers!,
                modifier => Assert.IsType<PanModifier>(modifier),
                modifier => Assert.IsType<ZoomModifier>(modifier));
        });
    }

    [Fact]
    public void Chart_ModifierLifecycle_IsSymmetricAcrossCollectionChangesAndUnload()
    {
        RunInSta(() =>
        {
            var first = new TrackingModifier();
            var second = new TrackingModifier();
            var replacement = new TrackingModifier();

            var chart = new Chart
            {
                Modifiers = new ObservableCollection<IChartModifier> { first, second },
                Template = new ControlTemplate(typeof(Chart))
            };

            InvokePrivate(chart, "UpdateModifiers");
            chart.ApplyTemplate();

            Assert.Equal(1, first.AttachCount);
            Assert.Equal(1, second.AttachCount);

            chart.Template = new ControlTemplate(typeof(Chart));
            chart.ApplyTemplate();

            Assert.Equal(1, first.AttachCount);
            Assert.Equal(1, second.AttachCount);

            chart.Modifiers!.Remove(first);
            Assert.Equal(1, first.DetachCount);

            chart.Modifiers[0] = replacement;
            Assert.Equal(1, second.DetachCount);
            Assert.Equal(1, replacement.AttachCount);

            InvokePrivate(chart, "OnUnloaded", chart, new RoutedEventArgs());

            Assert.Equal(1, replacement.DetachCount);
        });
    }

    [Fact]
    public void RenderLoop_KeepsLatestSnapshotPerSeries()
    {
        using var invalidated = new ManualResetEventSlim();
        int invalidationCount = 0;

        using var loop = new RenderLoop(() =>
        {
            Interlocked.Increment(ref invalidationCount);
            invalidated.Set();
        });

        loop.Start();

        var firstSeries = new VariableDataSeries<double, double>(x => x);
        firstSeries.Append(0, 1);
        var firstFrame = ((IDataFrameProvider)firstSeries).CreateFrame();

        firstSeries.Append(1, 2);
        var latestFirstFrame = ((IDataFrameProvider)firstSeries).CreateFrame();

        var secondSeries = new VariableDataSeries<double, double>(x => x);
        secondSeries.Append(10, 5);
        var secondFrame = ((IDataFrameProvider)secondSeries).CreateFrame();

        loop.SignalDataChanged(firstSeries, firstFrame);
        loop.SignalDataChanged(firstSeries, latestFirstFrame);
        loop.SignalDataChanged(secondSeries, secondFrame);

        Assert.True(invalidated.Wait(1000));
        Assert.True(invalidationCount >= 1);

        Assert.True(loop.TryGetLatestFrame(firstSeries, latestFirstFrame.Version, out var cachedFirst));
        Assert.Equal(latestFirstFrame.Version, cachedFirst!.Version);

        Assert.True(loop.TryGetLatestFrame(secondSeries, secondFrame.Version, out var cachedSecond));
        Assert.Equal(secondFrame.Version, cachedSecond!.Version);

        Assert.False(loop.TryGetLatestFrame(firstSeries, latestFirstFrame.Version + 1, out _));
    }

    [Fact]
    public void RenderLoop_CreatesSnapshotsForDirtySeriesOnBackgroundThread()
    {
        using var invalidated = new ManualResetEventSlim();

        using var loop = new RenderLoop(() => invalidated.Set());
        loop.Start();

        var dataSeries = new CountingDataSeries();
        dataSeries.Append(0, 1);
        dataSeries.Append(1, 2);

        int beforeFirstRefresh = dataSeries.CreateFrameCallCount;
        loop.SignalDataChanged(dataSeries);

        Assert.True(invalidated.Wait(1000));
        Assert.True(SpinWait.SpinUntil(
            () => loop.TryGetLatestFrame(dataSeries, dataSeries.Version, out _),
            1000));
        Assert.True(dataSeries.CreateFrameCallCount > beforeFirstRefresh);

        invalidated.Reset();
        dataSeries.Append(2, 3);
        int beforeSecondRefresh = dataSeries.CreateFrameCallCount;
        loop.SignalDataChanged(dataSeries);

        Assert.True(invalidated.Wait(1000));
        Assert.True(SpinWait.SpinUntil(
            () => loop.TryGetLatestFrame(dataSeries, dataSeries.Version, out var frame)
                && frame!.Version == dataSeries.Version,
            1000));
        Assert.True(dataSeries.CreateFrameCallCount > beforeSecondRefresh);
    }

    [Fact]
    public void Chart_CollectFrames_ReusesFreshSnapshots_AndCreatesOnlyOneFallbackSnapshot()
    {
        RunInSta(() =>
        {
            var dataSeries = new CountingDataSeries();
            dataSeries.Append(0, 1);
            dataSeries.Append(1, 2);

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new LineRenderableSeries { DataSeries = dataSeries }
                }
            };

            chart.EnsureRenderLoopForTesting();
            chart.CacheSnapshotForTesting(dataSeries, dataSeries.CreateSnapshot());

            int beforeReuse = dataSeries.CreateFrameCallCount;
            var firstFrames = chart.CollectFramesForCurrentRender();

            Assert.Single(firstFrames);
            Assert.Equal(beforeReuse, dataSeries.CreateFrameCallCount);

            dataSeries.Append(2, 3);

            int beforeFallback = dataSeries.CreateFrameCallCount;
            var secondFrames = chart.CollectFramesForCurrentRender();

            Assert.Single(secondFrames);
            Assert.Equal(beforeFallback + 1, dataSeries.CreateFrameCallCount);
            Assert.Equal(dataSeries.Version, secondFrames[0].frame.Version);

            int beforeRefreshedReuse = dataSeries.CreateFrameCallCount;
            _ = chart.CollectFramesForCurrentRender();

            Assert.Equal(beforeRefreshedReuse, dataSeries.CreateFrameCallCount);
        });
    }

    [Fact]
    public void Chart_RendererPreference_AutoFallsBackToSoftwareWhenHardwareInitializationFails()
    {
        RunInSta(() =>
        {
            var hardware = new FakeRenderer(ChartRendererBackend.D3D11, requiresD3D11: true, initializeError: "D3D unavailable");
            var software = new FakeRenderer(ChartRendererBackend.Gdi, requiresD3D11: false);
            var chart = new Chart
            {
                RendererPreference = ChartRendererPreference.Auto,
                RendererFactory = new FakeRendererFactory(hardware, software)
            };

            chart.InitializeRendererForTesting();

            Assert.Equal(1, hardware.InitializeCount);
            Assert.Equal(1, software.InitializeCount);
            Assert.Equal(ChartRendererBackend.Gdi, chart.ActualRendererBackend);
            Assert.True(chart.HasRenderError);
            Assert.Equal("D3D unavailable", chart.RenderError);
        });
    }

    [Fact]
    public void Chart_RendererPreference_HardwareOnlyDoesNotFallbackOnInitializationFailure()
    {
        RunInSta(() =>
        {
            var hardware = new FakeRenderer(ChartRendererBackend.D3D11, requiresD3D11: true, initializeError: "D3D unavailable");
            var software = new FakeRenderer(ChartRendererBackend.Gdi, requiresD3D11: false);
            var chart = new Chart
            {
                RendererPreference = ChartRendererPreference.HardwareOnly,
                RendererFactory = new FakeRendererFactory(hardware, software)
            };

            chart.InitializeRendererForTesting();

            Assert.Equal(1, hardware.InitializeCount);
            Assert.Equal(0, software.InitializeCount);
            Assert.Equal(ChartRendererBackend.Unknown, chart.ActualRendererBackend);
            Assert.True(chart.HasRenderError);
            Assert.Equal("D3D unavailable", chart.RenderError);
        });
    }

    [Fact]
    public void Chart_RendererPreference_SoftwareOnlySkipsHardwareInitialization()
    {
        RunInSta(() =>
        {
            var hardware = new FakeRenderer(ChartRendererBackend.D3D11, requiresD3D11: true);
            var software = new FakeRenderer(ChartRendererBackend.Gdi, requiresD3D11: false);
            var chart = new Chart
            {
                RendererPreference = ChartRendererPreference.SoftwareOnly,
                RendererFactory = new FakeRendererFactory(hardware, software)
            };

            chart.InitializeRendererForTesting();

            Assert.Equal(0, hardware.InitializeCount);
            Assert.Equal(1, software.InitializeCount);
            Assert.Equal(ChartRendererBackend.Gdi, chart.ActualRendererBackend);
            Assert.False(chart.HasRenderError);
        });
    }

    private static void RunInSta(Action action)
    {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        captured?.Throw();
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] arguments)
    {
        var method = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == methodName && method.GetParameters().Length == arguments.Length);

        return method.Invoke(target, arguments);
    }

    private sealed class TrackingModifier : IChartModifier
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        public void OnAttached() => AttachCount++;

        public void OnDetached() => DetachCount++;

        public void SetContext(Cheari.Controls.Rendering.Context.IRenderContext context)
        {
        }

        public void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        public void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        public void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
        }

        public void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
        }
    }

    private sealed class CountingDataSeries : DataSeriesBase
    {
        private readonly List<double> _xValues = new();
        private readonly List<double> _yValues = new();
        private int _version;

        public int CreateFrameCallCount { get; private set; }

        public override int Count => _xValues.Count;

        public override int Version => _version;

        public override DataRange XRange => Count == 0 ? new DataRange(0, 1) : new DataRange(_xValues[0], _xValues[^1]);

        public override DataRange YRange => Count == 0 ? new DataRange(-1, 1) : new DataRange(_yValues.Min(), _yValues.Max());

        public void Append(double x, double y)
        {
            _xValues.Add(x);
            _yValues.Add(y);
            _version++;
        }

        public DataFrame CreateSnapshot() => ((IDataFrameProvider)this).CreateFrame();

        public override double GetX(int index) => _xValues[index];

        public override double GetY(int index) => _yValues[index];

        public override int CopyXValues(Span<double> destination)
        {
            int count = Math.Min(destination.Length, _xValues.Count);
            for (int i = 0; i < count; i++)
                destination[i] = _xValues[i];

            return count;
        }

        public override int CopyYValues(Span<double> destination)
        {
            int count = Math.Min(destination.Length, _yValues.Count);
            for (int i = 0; i < count; i++)
                destination[i] = _yValues[i];

            return count;
        }

        internal override DataFrame CreateFrameCore()
        {
            CreateFrameCallCount++;

            if (Count == 0)
                return DataFrame.Empty;

            return new DataFrame
            {
                Count = Count,
                Version = Version,
                XRange = XRange,
                YRange = YRange,
                Type = DataFrameType.Xy,
                XValues = _xValues.Select(value => (float)value).ToArray(),
                YValues = _yValues.Select(value => (float)value).ToArray()
            };
        }

        protected override void OnFifoCapacityChanged()
        {
        }
    }

    private sealed class FakeRendererFactory : IChartRendererFactory
    {
        private readonly IRenderer _hardwareRenderer;
        private readonly IRenderer _softwareRenderer;

        public FakeRendererFactory(IRenderer hardwareRenderer, IRenderer softwareRenderer)
        {
            _hardwareRenderer = hardwareRenderer;
            _softwareRenderer = softwareRenderer;
        }

        public IRenderer CreateHardwareRenderer() => _hardwareRenderer;

        public IRenderer CreateSoftwareRenderer() => _softwareRenderer;
    }

    private sealed class FakeRenderer : IRenderer
    {
        private readonly string? _initializeError;

        public FakeRenderer(ChartRendererBackend backend, bool requiresD3D11, string? initializeError = null)
        {
            Backend = backend;
            RequiresD3D11 = requiresD3D11;
            _initializeError = initializeError;
        }

        public bool RequiresD3D11 { get; }

        public ChartRendererBackend Backend { get; }

        public ImageSource? CurrentImageSource => null;

        public string? LastError { get; private set; }

        public int InitializeCount { get; private set; }

        public void Initialize(DrawingSurfaceEventArgs args)
        {
            InitializeCount++;
            LastError = _initializeError;
        }

        public void Uninitialize()
        {
        }

        public bool Render(
            DrawEventArgs args,
            int width,
            int height,
            Color backgroundColor,
            bool enableAntialiasing,
            DataRange xRange,
            DataRange yRange,
            IReadOnlyList<IRenderCommand>? commands = null)
        {
            return true;
        }

        public bool Render(
            DrawEventArgs args,
            int width,
            int height,
            Color backgroundColor,
            bool enableAntialiasing,
            IReadOnlyList<AxisRenderGroup> renderGroups)
        {
            return true;
        }

        public void Dispose()
        {
        }
    }
}
