using Cheari.Controls.Axes;
using Cheari.Controls.Axes.Controls;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Series.Types;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Cheari.Controls.Tests;

public class DataSeriesTests
{
    [Fact]
    public void VariableDataSeries_WithDateTimeX_UsesInjectedProjectionAndCachesRange()
    {
        DateTime start = new(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
        var series = new VariableDataSeries<DateTime, double>(x => x.ToOADate());

        series.Append(start, 1.5);
        series.Append(start.AddHours(2), 3.5);

        Assert.Equal(start, series.GetTypedX(0));
        Assert.Equal(start.AddHours(2), series.GetTypedX(1));
        Assert.Equal(start.ToOADate(), series.GetX(0), 10);
        Assert.Equal(start.AddHours(2).ToOADate(), series.GetX(1), 10);

        var firstRange = series.XRange;
        var secondRange = series.XRange;

        Assert.Equal(firstRange.Min, secondRange.Min);
        Assert.Equal(firstRange.Max, secondRange.Max);
        Assert.Equal(start.ToOADate(), firstRange.Min, 10);
        Assert.Equal(start.AddHours(2).ToOADate(), firstRange.Max, 10);
    }

    [Fact]
    public void VariableDataSeries_WithStringX_PreservesTypedValuesAndUsesOrdinalProjection()
    {
        var mapping = new Dictionary<string, double>
        {
            ["A"] = 10,
            ["B"] = 30,
            ["C"] = 20
        };
        var series = new VariableDataSeries<string, double>(x => mapping[x]);

        series.Append("A", 1);
        series.Append("B", 2);
        series.Append("C", 3);

        Assert.Equal("B", series.GetTypedX(1));
        Assert.Equal("C", series.XValues[2]);
        Assert.Equal(10, series.XRange.Min);
        Assert.Equal(30, series.XRange.Max);
    }

    [Fact]
    public void UniformDataSeries_WithDateTimeSelector_ExposesVirtualXValuesAndCorrectRange()
    {
        DateTime start = new(2026, 4, 28, 8, 0, 0, DateTimeKind.Utc);
        var series = new UniformDataSeries<DateTime, double>(
            index => start.AddMinutes(index * 15),
            x => x.ToOADate());

        series.Append(new[] { 1.0, 2.0, 3.0 });

        Assert.Equal(start, series.GetTypedX(0));
        Assert.Equal(start.AddMinutes(30), series.GetTypedX(2));
        Assert.Equal(start.AddMinutes(15), series.XValues[1]);
        Assert.Equal(3, series.XValues.Count);
        Assert.Equal(start.ToOADate(), series.XRange.Min, 10);
        Assert.Equal(start.AddMinutes(30).ToOADate(), series.XRange.Max, 10);
    }

    [Fact]
    public void SingleAppend_RaisesCollectionAndRangeChanged_AndUpdatesHotCacheIncrementally()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        int rangeChangedCount = 0;
        NotifyCollectionChangedEventArgs? lastCollectionEvent = null;

        series.Append(1, 2);
        _ = series.XRange;
        _ = series.YRange;

        series.RangeChanged += (_, _) => rangeChangedCount++;
        series.CollectionChanged += (_, args) => lastCollectionEvent = args;

        series.Append(5, -3);

        Assert.Equal(1, rangeChangedCount);
        Assert.NotNull(lastCollectionEvent);
        Assert.Equal(NotifyCollectionChangedAction.Add, lastCollectionEvent!.Action);
        Assert.Equal(1, lastCollectionEvent.NewStartingIndex);
        Assert.Equal(1, series.XRange.Min);
        Assert.Equal(5, series.XRange.Max);
        Assert.Equal(-3, series.YRange.Min);
        Assert.Equal(2, series.YRange.Max);
    }

    [Fact]
    public void BulkAppend_UsesLazyRangeRecalculationAndSingleRangeChanged()
    {
        var series = new UniformDataSeries<double, double>(index => index * 2.0, x => x);
        int rangeChangedCount = 0;

        series.RangeChanged += (_, _) => rangeChangedCount++;
        series.Append(new[] { 10.0, 20.0, -5.0 });

        Assert.Equal(1, rangeChangedCount);
        Assert.Equal(0, series.XRange.Min);
        Assert.Equal(4, series.XRange.Max);
        Assert.Equal(-5, series.YRange.Min);
        Assert.Equal(20, series.YRange.Max);
    }

    [Fact]
    public void VariableDataSeries_BulkAppend_WithMismatchedLengths_Throws()
    {
        var series = new VariableDataSeries<double, double>(x => x);

        Assert.Throws<ArgumentException>(() => series.Append(new[] { 1.0, 2.0 }, new[] { 1.0 }));
    }

    [Fact]
    public void LineSeriesRenderer_IgnoresOptionalMetadataProvider()
    {
        var seriesWithMetadata = new MetadataVariableDataSeries<double, double>(x => x);
        seriesWithMetadata.Append(0, 1);
        seriesWithMetadata.Append(1, 2);

        var seriesWithoutMetadata = new VariableDataSeries<double, double>(x => x);
        seriesWithoutMetadata.Append(0, 1);
        seriesWithoutMetadata.Append(1, 2);

        var contextWithMetadata = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 1),
            YRangeAccessor = () => new DataRange(1, 2),
            SeriesAccessor = () => new List<IRenderableSeries>
            {
                new LineRenderableSeries
                {
                    Stroke = Colors.Red,
                    StrokeThickness = 2,
                    DataSeries = seriesWithMetadata
                }
            }
        };
        var contextWithoutMetadata = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 1),
            YRangeAccessor = () => new DataRange(1, 2),
            SeriesAccessor = () => new List<IRenderableSeries>
            {
                new LineRenderableSeries
                {
                    Stroke = Colors.Red,
                    StrokeThickness = 2,
                    DataSeries = seriesWithoutMetadata
                }
            }
        };

        var withMetadata = new LineSeriesRenderer().Render(contextWithMetadata, 100, 50);
        var withoutMetadata = new LineSeriesRenderer().Render(contextWithoutMetadata, 100, 50);

        Assert.Single(withMetadata);
        Assert.Single(withoutMetadata);
        var withLine = Assert.IsType<LineRenderOperation>(withMetadata[0]);
        var withoutLine = Assert.IsType<LineRenderOperation>(withoutMetadata[0]);
        Assert.Equal(withoutLine.LineInstances.Count, withLine.LineInstances.Count);
        Assert.Equal(withoutLine.StrokeColor, withLine.StrokeColor);
        Assert.Equal(withoutLine.LineInstances[0].X1, withLine.LineInstances[0].X1);
        Assert.Equal(withoutLine.LineInstances[0].Y1, withLine.LineInstances[0].Y1);
        Assert.Equal(withoutLine.LineInstances[0].X2, withLine.LineInstances[0].X2);
        Assert.Equal(withoutLine.LineInstances[0].Y2, withLine.LineInstances[0].Y2);
    }

    [Fact]
    public void LineSeriesRenderer_Downsampling_PreservesSpikeWithinBucket()
    {
        var dataSeries = new VariableDataSeries<double, double>(x => x);
        dataSeries.Append(new[] { 0.0, 1.0, 2.0, 3.0, 4.0 }, new[] { 0.0, 0.0, 100.0, 0.0, 0.0 });

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 4),
            YRangeAccessor = () => new DataRange(0, 100),
            SeriesAccessor = () => new List<IRenderableSeries>
            {
                new LineRenderableSeries
                {
                    Stroke = Colors.Cyan,
                    StrokeThickness = 2,
                    DataSeries = dataSeries
                }
            }
        };

        var operations = new LineSeriesRenderer().Render(context, 2, 100);

        Assert.Single(operations);
        var lineOp = Assert.IsType<LineRenderOperation>(operations[0]);
        Assert.Contains(lineOp.LineInstances, segment => segment.Y1 == 100.0f || segment.Y2 == 100.0f);
    }

    [Fact]
    public void ChartRenderContext_DpiAwareConversion_ClampsAndRoundTrips()
    {
        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 100),
            YRangeAccessor = () => new DataRange(-50, 50),
            DpiScaleX = 2.0,
            DpiScaleY = 1.5
        };

        double dataX = context.ScreenToDataX(25, 200);
        double dataY = context.ScreenToDataY(20, 150);
        var screenPoint = context.DataToScreen(dataX, dataY, 200, 150);

        Assert.Equal(25, dataX, 10);
        Assert.Equal(30, dataY, 10);
        Assert.Equal(25, screenPoint.X, 10);
        Assert.Equal(20, screenPoint.Y, 10);
        Assert.Equal(0, context.ScreenToDataX(-10, 200), 10);
        Assert.Equal(100, context.ScreenToDataX(1000, 200), 10);
        Assert.Equal(-50, context.ScreenToDataY(1000, 150), 10);
        Assert.Equal(50, context.ScreenToDataY(-10, 150), 10);
    }

    [Fact]
    public void Chart_MarksDirty_WhenUnderlyingDataSeriesChanges()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>()
            };
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            var renderableSeries = new LineRenderableSeries { DataSeries = dataSeries };
            chart.Series.Add(renderableSeries);

            SetChartDirty(chart, false);
            dataSeries.Append(1, 1);

            Assert.True(GetChartDirty(chart));
        });
    }

    [Fact]
    public void Chart_BackgroundAppend_MarshalsDirtyUpdateBackToDispatcher()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>()
            };
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            chart.Series.Add(new LineRenderableSeries { DataSeries = dataSeries });

            SetChartDirty(chart, false);

            var worker = new Thread(() => dataSeries.Append(1, 1));
            worker.Start();
            worker.Join();

            Assert.False(GetChartDirty(chart));

            PumpDispatcher();

            Assert.True(GetChartDirty(chart));
        });
    }

    [Fact]
    public void Chart_ZoomExtents_UsesCachedRangesFromDataSeries()
    {
        RunInSta(() =>
        {
            var chart = new Chart();
            var first = new VariableDataSeries<double, double>(x => x);
            first.Append(-2, 10);
            first.Append(2, 20);

            var second = new UniformDataSeries<double, double>(index => 10 + index, x => x);
            second.Append(new[] { -5.0, 5.0 });

            chart.Series = new ObservableCollection<IRenderableSeries>
            {
                new LineRenderableSeries { DataSeries = first, IsVisible = true },
                new LineRenderableSeries { DataSeries = second, IsVisible = true }
            };

            chart.ZoomExtents();

            Assert.True(chart.XRange.Min <= -2, $"XRange.Min should be <= -2, got {chart.XRange.Min}");
            Assert.True(chart.XRange.Max >= 12, $"XRange.Max should be >= 12, got {chart.XRange.Max}");
            Assert.True(chart.YRange.Min <= -5, $"YRange.Min should be <= -5, got {chart.YRange.Min}");
            Assert.True(chart.YRange.Max >= 20, $"YRange.Max should be >= 20, got {chart.YRange.Max}");
        });
    }

    [Fact]
    public void Chart_ZoomExtents_SyncsUnboundAxesBackToDefaultRanges()
    {
        RunInSta(() =>
        {
            var chart = new Chart();
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            dataSeries.Append(new[] { -1.0, 0.5, 2.0, 4.0 });

            chart.Series = new ObservableCollection<IRenderableSeries>
            {
                new LineRenderableSeries
                {
                    DataSeries = dataSeries,
                    XAxisId = Chart.DefaultXAxisId,
                    YAxisId = Chart.DefaultYAxisId
                }
            };

            var topAxis = new LinearAxis
            {
                Id = "TopXAxis",
                Placement = AxisPlacement.Top,
                VisibleRange = new DataRange(100, 120),
                AutoRange = false
            };
            var rightAxis = new LinearAxis
            {
                Id = "RightYAxis",
                Placement = AxisPlacement.Right,
                VisibleRange = new DataRange(500, 700),
                AutoRange = false
            };

            chart.XAxes.Add(topAxis);
            chart.YAxes.Add(rightAxis);

            chart.XRange = new DataRange(10, 12);
            chart.YRange = new DataRange(20, 22);

            chart.ZoomExtents();

            Assert.Equal(chart.XRange.Min, topAxis.VisibleRange.Min, 10);
            Assert.Equal(chart.XRange.Max, topAxis.VisibleRange.Max, 10);
            Assert.Equal(chart.YRange.Min, rightAxis.VisibleRange.Min, 10);
            Assert.Equal(chart.YRange.Max, rightAxis.VisibleRange.Max, 10);
        });
    }

    [Fact]
    public void Chart_ZoomExtents_PreservesBoundSecondaryAxisAutoRange()
    {
        RunInSta(() =>
        {
            var primary = new UniformDataSeries<double, double>(index => index, x => x);
            primary.Append(new[] { -1.0, 1.0, 2.0 });

            var secondary = new UniformDataSeries<double, double>(index => index, x => x);
            secondary.Append(new[] { 250.0, 400.0, 800.0 });

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new LineRenderableSeries
                    {
                        DataSeries = primary,
                        XAxisId = Chart.DefaultXAxisId,
                        YAxisId = Chart.DefaultYAxisId
                    },
                    new LineRenderableSeries
                    {
                        DataSeries = secondary,
                        XAxisId = Chart.DefaultXAxisId,
                        YAxisId = "RightYAxis"
                    }
                }
            };

            var rightAxis = new LinearAxis
            {
                Id = "RightYAxis",
                Placement = AxisPlacement.Right,
                VisibleRange = new DataRange(-10, 10),
                AutoRange = false
            };

            chart.YAxes.Add(rightAxis);

            chart.ZoomExtents();

            var expectedRightRange = rightAxis.CalculateAutoRange([secondary]);
            Assert.Equal(expectedRightRange.Min, rightAxis.VisibleRange.Min, 10);
            Assert.Equal(expectedRightRange.Max, rightAxis.VisibleRange.Max, 10);
            Assert.NotEqual(chart.YRange.Min, rightAxis.VisibleRange.Min);
            Assert.NotEqual(chart.YRange.Max, rightAxis.VisibleRange.Max);
        });
    }

    [Fact]
    public void Chart_AutoRange_CoalescesDuplicateSeriesNotifications()
    {
        RunInSta(() =>
        {
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            var xAxis = new CountingLinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                AutoRange = true
            };
            var yAxis = new CountingLinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                AutoRange = true
            };

            var chart = new Chart
            {
                Series = [new LineRenderableSeries { DataSeries = dataSeries }],
                XAxes = [xAxis],
                YAxes = [yAxis]
            };

            dataSeries.Append(1.0);
            dataSeries.Append(2.0);
            dataSeries.Append(3.0);

            Assert.Equal(0, xAxis.AutoRangeCallCount);
            Assert.Equal(0, yAxis.AutoRangeCallCount);

            PumpDispatcher();

            Assert.Equal(1, xAxis.AutoRangeCallCount);
            Assert.Equal(1, yAxis.AutoRangeCallCount);
        });
    }

    [Fact]
    public void Chart_BackgroundAppend_MarshalsAutoRangeUpdateBackToDispatcher()
    {
        RunInSta(() =>
        {
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            var xAxis = new CountingLinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                AutoRange = true
            };
            var yAxis = new CountingLinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                AutoRange = true
            };

            var chart = new Chart
            {
                Series = [new LineRenderableSeries { DataSeries = dataSeries }],
                XAxes = [xAxis],
                YAxes = [yAxis]
            };

            var worker = new Thread(() =>
            {
                dataSeries.Append(1.0);
                dataSeries.Append(2.0);
                dataSeries.Append(3.0);
            });

            worker.Start();
            worker.Join();

            Assert.Equal(0, xAxis.AutoRangeCallCount);
            Assert.Equal(0, yAxis.AutoRangeCallCount);

            PumpDispatcher();

            Assert.Equal(1, xAxis.AutoRangeCallCount);
            Assert.Equal(1, yAxis.AutoRangeCallCount);
        });
    }

    [Fact]
    public void Chart_BackgroundAppend_WithFixedAxes_DoesNotQueueAutoRangeDispatcherWork()
    {
        RunInSta(() =>
        {
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            var xAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                AutoRange = false
            };
            var yAxis = new LinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                AutoRange = false
            };

            var chart = new Chart
            {
                Series = [new LineRenderableSeries { DataSeries = dataSeries }],
                XAxes = [xAxis],
                YAxes = [yAxis]
            };

            var worker = new Thread(() =>
            {
                dataSeries.Append(1.0);
                dataSeries.Append(2.0);
                dataSeries.Append(3.0);
            });

            worker.Start();
            worker.Join();

            Assert.Equal(0, GetPendingBackgroundAutoRangeRequest(chart));
        });
    }

    [Fact]
    public void OhlcDataSeries_Append_AndRangeCalculation()
    {
        var series = new OhlcDataSeries();
        series.Append(0, 100, 105, 98, 103);
        series.Append(1, 103, 108, 100, 101);

        Assert.Equal(2, series.Count);
        Assert.Equal(0, series.GetX(0));
        Assert.Equal(103, series.GetClose(0));
        Assert.Equal(101, series.GetClose(1));
        Assert.Equal(98, series.YRange.Min);
        Assert.Equal(108, series.YRange.Max);
        Assert.Equal(0, series.XRange.Min);
        Assert.Equal(1, series.XRange.Max);
    }

    [Fact]
    public void OhlcDataSeries_GetY_ReturnsClose()
    {
        var series = new OhlcDataSeries();
        series.Append(0, 50, 55, 48, 52);

        Assert.Equal(52, series.GetY(0));
    }

    [Fact]
    public void ScatterSeriesRenderer_ProducesMarkerInstances()
    {
        var dataSeries = new VariableDataSeries<double, double>(x => x);
        dataSeries.Append(0, 1);
        dataSeries.Append(1, 2);
        dataSeries.Append(2, 3);

        var scatterSeries = new ScatterRenderableSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 8,
            MarkerColor = Colors.Red,
            DataSeries = dataSeries
        };

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 2),
            YRangeAccessor = () => new DataRange(1, 3),
            SeriesAccessor = () => new List<IRenderableSeries> { scatterSeries }
        };

        var renderer = new ScatterSeriesRenderer();
        var commands = renderer.Render(scatterSeries, context, 100, 100);

        Assert.Single(commands);
        var op = Assert.IsType<ScatterRenderOperation>(commands[0]);
        Assert.True(op.Instances.Count > 0);
        Assert.Equal(MarkerType.Circle, op.MarkerType);
        Assert.Equal(8.0f, op.MarkerSize);
    }

    [Fact]
    public void BarSeriesRenderer_ProducesBarInstances()
    {
        var dataSeries = new VariableDataSeries<double, double>(x => x);
        dataSeries.Append(0, 10);
        dataSeries.Append(1, 20);
        dataSeries.Append(2, 15);

        var barSeries = new BarRenderableSeries
        {
            Fill = Colors.DodgerBlue,
            BarSpacing = 0.2,
            DataSeries = dataSeries
        };

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(-1, 3),
            YRangeAccessor = () => new DataRange(0, 25),
            SeriesAccessor = () => new List<IRenderableSeries> { barSeries }
        };

        var renderer = new BarSeriesRenderer();
        var commands = renderer.Render(barSeries, context, 100, 100);

        Assert.Single(commands);
        var op = Assert.IsType<BarRenderOperation>(commands[0]);
        Assert.Equal(3, op.Instances.Count);
    }

    [Fact]
    public void AreaSeriesRenderer_ProducesFillAndBorder()
    {
        var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
        dataSeries.Append(new[] { 1.0, 3.0, 2.0, 4.0 });

        var areaSeries = new AreaRenderableSeries
        {
            Stroke = Colors.Cyan,
            Fill = Colors.Cyan,
            FillOpacity = 0.3,
            BaselineY = 0,
            DataSeries = dataSeries
        };

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 3),
            YRangeAccessor = () => new DataRange(0, 5),
            SeriesAccessor = () => new List<IRenderableSeries> { areaSeries }
        };

        var renderer = new AreaSeriesRenderer();
        var commands = renderer.Render(areaSeries, context, 100, 100);

        Assert.True(commands.Count >= 1);
        var areaOp = commands.OfType<AreaRenderOperation>().FirstOrDefault();
        Assert.NotNull(areaOp);
        Assert.True(areaOp!.FillVertices.Count > 0);
    }

    [Fact]
    public void OhlcSeriesRenderer_ProducesBodiesAndWicks()
    {
        var dataSeries = new OhlcDataSeries();
        dataSeries.Append(0, 100, 105, 98, 103);
        dataSeries.Append(1, 103, 108, 100, 101);

        var ohlcSeries = new OhlcRenderableSeries
        {
            UpFill = Colors.Green,
            DownFill = Colors.Red,
            DataSeries = dataSeries
        };

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(-1, 2),
            YRangeAccessor = () => new DataRange(95, 110),
            SeriesAccessor = () => new List<IRenderableSeries> { ohlcSeries }
        };

        var renderer = new OhlcSeriesRenderer();
        var commands = renderer.Render(ohlcSeries, context, 100, 100);

        Assert.True(commands.Count >= 1);
        Assert.Contains(commands, c => c is BarRenderOperation);
        Assert.Contains(commands, c => c is LineRenderOperation);
    }

    [Fact]
    public void SeriesRendererDispatcher_DispatchesByType()
    {
        var lineData = new VariableDataSeries<double, double>(x => x);
        lineData.Append(0, 1);
        lineData.Append(1, 2);

        var scatterData = new VariableDataSeries<double, double>(x => x);
        scatterData.Append(0, 1);
        scatterData.Append(1, 2);

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 1),
            YRangeAccessor = () => new DataRange(1, 2),
            SeriesAccessor = () => new List<IRenderableSeries>
            {
                new LineRenderableSeries { Stroke = Colors.White, DataSeries = lineData },
                new ScatterRenderableSeries { MarkerType = MarkerType.Square, MarkerColor = Colors.Red, DataSeries = scatterData }
            }
        };

        var dispatcher = new SeriesRendererDispatcher();
        var commands = dispatcher.Render(context, 100, 100);

        Assert.True(commands.Count >= 2);
        Assert.Contains(commands, c => c is LineRenderOperation);
        Assert.Contains(commands, c => c is ScatterRenderOperation);
    }

    [Fact]
    public void LineRenderableSeries_DashArray_ReturnsCorrectPatterns()
    {
        Assert.Null(LineRenderableSeries.GetDashArrayForStyle(LineStyle.Solid));
        Assert.NotNull(LineRenderableSeries.GetDashArrayForStyle(LineStyle.Dash));
        Assert.NotNull(LineRenderableSeries.GetDashArrayForStyle(LineStyle.Dot));
        Assert.NotNull(LineRenderableSeries.GetDashArrayForStyle(LineStyle.DashDot));
        Assert.NotNull(LineRenderableSeries.GetDashArrayForStyle(LineStyle.DashDotDot));
    }

    [Fact]
    public void LogCoordinateMapper_MapsPositiveRangeCorrectly()
    {
        var mapper = LogCoordinateMapper.Instance;
        var range = new DataRange(1, 1000);
        double viewportSize = 600;

        double pos10 = mapper.DataToScreen(10, range, viewportSize);
        double pos100 = mapper.DataToScreen(100, range, viewportSize);
        double pos1000 = mapper.DataToScreen(1000, range, viewportSize);

        Assert.True(pos10 > 0);
        Assert.True(pos100 > pos10);
        Assert.Equal(viewportSize, pos1000, 5);

        double roundTrip = mapper.ScreenToData(pos100, range, viewportSize);
        Assert.Equal(100, roundTrip, 5);
    }

    [Fact]
    public void LogCoordinateMapper_HandlesZeroAndNegativeValues()
    {
        var mapper = LogCoordinateMapper.Instance;
        var range = new DataRange(1, 1000);

        double pos0 = mapper.DataToScreen(0, range, 600);
        Assert.Equal(0, pos0);

        double posNeg = mapper.DataToScreen(-5, range, 600);
        Assert.Equal(0, posNeg);
    }

    [Fact]
    public void LogCoordinateMapper_FallsBackToLinearForInvalidRange()
    {
        var mapper = LogCoordinateMapper.Instance;
        var invalidRange = new DataRange(-10, -1);

        double pos = mapper.DataToScreen(-5, invalidRange, 600);
        Assert.True(pos >= 0);

        double roundTrip = mapper.ScreenToData(pos, invalidRange, 600);
        Assert.Equal(-5, roundTrip, 5);
    }

    [Fact]
    public void DateTimeCoordinateMapper_RoundTripsCorrectly()
    {
        var mapper = DateTimeCoordinateMapper.Instance;
        var dt = new DateTime(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc);
        double doubleValue = DateTimeCoordinateMapper.DateTimeToDouble(dt);
        DateTime roundTrip = DateTimeCoordinateMapper.DoubleToDateTime(doubleValue);

        Assert.Equal(dt, roundTrip);
    }

    [Fact]
    public void DateTimeCoordinateMapper_DelegatesToLinearMapper()
    {
        var mapper = DateTimeCoordinateMapper.Instance;
        var range = new DataRange(0, 100);
        double viewportSize = 500;

        double pos = mapper.DataToScreen(50, range, viewportSize);
        Assert.Equal(250, pos, 5);

        double dataValue = mapper.ScreenToData(250, range, viewportSize);
        Assert.Equal(50, dataValue, 5);
    }

    [Fact]
    public void LinearAxis_CalculateAutoRange_ReturnsNiceRange()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis { Placement = AxisPlacement.Left };
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(1, 3);
            dataSeries.Append(2, 17);
            dataSeries.Append(3, 8);

            var autoRange = axis.CalculateAutoRange(new[] { dataSeries });

            Assert.True(autoRange.Min <= 3);
            Assert.True(autoRange.Max >= 17);
            Assert.True(autoRange.Min < autoRange.Max);
        });
    }

    [Fact]
    public void LinearAxis_GetMajorTicks_ReturnsTicksInRange()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 100)
            };

            var ticks = axis.GetMajorTicks(600);

            Assert.True(ticks.Length > 0);
            foreach (var tick in ticks)
            {
                Assert.True(tick.Position >= 0 && tick.Position <= 100);
                Assert.False(string.IsNullOrEmpty(tick.Label));
            }
        });
    }

    [Fact]
    public void LogAxis_CalculateAutoRange_ReturnsPowerOfTenRange()
    {
        RunInSta(() =>
        {
            var axis = new LogAxis { Placement = AxisPlacement.Left };
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(1, 5);
            dataSeries.Append(2, 50);
            dataSeries.Append(3, 500);

            var autoRange = axis.CalculateAutoRange(new[] { dataSeries });

            Assert.True(autoRange.Min <= 5);
            Assert.True(autoRange.Max >= 500);
        });
    }

    [Fact]
    public void LogAxis_GetMajorTicks_ReturnsLogarithmicTicks()
    {
        RunInSta(() =>
        {
            var axis = new LogAxis
            {
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(1, 1000)
            };

            var ticks = axis.GetMajorTicks(600);

            Assert.True(ticks.Length > 0);
            foreach (var tick in ticks)
            {
                Assert.True(tick.Position >= 1 && tick.Position <= 1000);
            }
        });
    }

    [Fact]
    public void AxisControl_Measure_LeftAxisWithInfiniteHeight_ReturnsFiniteDesiredSize()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 100),
                Title = "Value"
            };

            var control = new AxisControl
            {
                Axis = axis,
                FontSize = 12,
                Foreground = Brushes.White
            };

            control.Measure(new Size(100, double.PositiveInfinity));

            Assert.True(double.IsFinite(control.DesiredSize.Width));
            Assert.True(double.IsFinite(control.DesiredSize.Height));
            Assert.True(control.DesiredSize.Width > 0);
            Assert.Equal(0, control.DesiredSize.Height);
        });
    }

    [Fact]
    public void AxisControl_Measure_BottomAxisWithInfiniteWidth_ReturnsFiniteDesiredSize()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 100),
                Title = "Time"
            };

            var control = new AxisControl
            {
                Axis = axis,
                FontSize = 12,
                Foreground = Brushes.White
            };

            control.Measure(new Size(double.PositiveInfinity, 100));

            Assert.True(double.IsFinite(control.DesiredSize.Width));
            Assert.True(double.IsFinite(control.DesiredSize.Height));
            Assert.Equal(0, control.DesiredSize.Width);
            Assert.True(control.DesiredSize.Height > 0);
        });
    }

    [Fact]
    public void AxisPanel_Measure_WithInfiniteConstraint_DoesNotPropagateInfiniteDesiredSize()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 100),
                Title = "Value"
            };

            var control = new AxisControl
            {
                Axis = axis,
                FontSize = 12,
                Foreground = Brushes.White
            };

            var panel = new AxisPanel { AxisPlacement = AxisPlacement.Left };
            panel.Children.Add(control);

            panel.Measure(new Size(100, double.PositiveInfinity));

            Assert.True(double.IsFinite(panel.DesiredSize.Width));
            Assert.True(double.IsFinite(panel.DesiredSize.Height));
            Assert.True(panel.DesiredSize.Width > 0);
            Assert.Equal(0, panel.DesiredSize.Height);
        });
    }

    [Fact]
    public void AxisControl_Template_GeneratesAxisElements_ForAllPlacements()
    {
        RunInSta(() =>
        {
            foreach (var placement in new[] { AxisPlacement.Left, AxisPlacement.Right, AxisPlacement.Top, AxisPlacement.Bottom })
            {
                var axis = new LinearAxis
                {
                    Placement = placement,
                    VisibleRange = new DataRange(0, 100),
                    Title = $"Axis-{placement}"
                };

                var control = CreateAxisControl(axis, placement is AxisPlacement.Left or AxisPlacement.Right
                    ? new Size(80, 220)
                    : new Size(220, 80));

                var axisLine = GetTemplatePart<Line>(control, "PART_AxisLine");
                var tickHost = GetTemplatePart<Canvas>(control, "PART_TickHost");
                var title = GetTemplatePart<TextBlock>(control, "PART_Title");

                Assert.NotNull(axisLine);
                Assert.NotNull(tickHost);
                Assert.NotNull(title);
                Assert.Equal(axis.Title, title.Text);
                Assert.True(tickHost.Children.OfType<Line>().Any());
                Assert.True(tickHost.Children.OfType<TextBlock>().Any());

                bool isVertical = placement is AxisPlacement.Left or AxisPlacement.Right;
                Assert.Equal(isVertical, Math.Abs(axisLine.X1 - axisLine.X2) < 0.1);
            }
        });
    }

    [Fact]
    public void AxisControl_VisibleRangeChanged_RefreshesGeneratedLabels()
    {
        RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 100),
                Title = "Time"
            };

            var control = CreateAxisControl(axis, new Size(240, 80));
            var tickHost = GetTemplatePart<Canvas>(control, "PART_TickHost");
            var before = tickHost.Children.OfType<TextBlock>().Select(static text => text.Text).ToArray();

            axis.VisibleRange = new DataRange(0, 1);
            control.Measure(new Size(240, 80));
            control.Arrange(new Rect(0, 0, 240, 80));
            control.UpdateLayout();

            var after = tickHost.Children.OfType<TextBlock>().Select(static text => text.Text).ToArray();

            Assert.NotEqual(before, after);
        });
    }

    [Fact]
    public void AxisItemsControl_GeneratesAxisControlContainers()
    {
        RunInSta(() =>
        {
            var axes = new ObservableCollection<IAxis>
            {
                new LinearAxis
                {
                    Placement = AxisPlacement.Left,
                    VisibleRange = new DataRange(0, 10)
                },
                new LinearAxis
                {
                    Placement = AxisPlacement.Right,
                    VisibleRange = new DataRange(0, 20)
                }
            };

            var itemsControl = new AxisItemsControl
            {
                ItemsSource = axes
            };

            RunInWindow(itemsControl, new Size(240, 120), control =>
            {
                var firstContainer = control.ItemContainerGenerator.ContainerFromIndex(0);
                var secondContainer = control.ItemContainerGenerator.ContainerFromIndex(1);

                Assert.IsType<AxisControl>(firstContainer);
                Assert.IsType<AxisControl>(secondContainer);
                Assert.Same(axes[0], ((AxisControl)firstContainer!).Axis);
                Assert.Same(axes[1], ((AxisControl)secondContainer!).Axis);
            });
        });
    }

    [Fact]
    public void Chart_Template_SeparatesOuterAndPlotAreaChrome()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Template = CreateChartTemplate(),
                Background = Brushes.MidnightBlue,
                BorderBrush = Brushes.Goldenrod,
                BorderThickness = new Thickness(2),
                PlotAreaBackground = Brushes.Black,
                PlotAreaBorderBrush = Brushes.OrangeRed,
                PlotAreaBorderThickness = new Thickness(3)
            };

            RunInWindow(chart, new Size(220, 140), control =>
            {
                var rootBorder = GetTemplatePart<Border>(control, "PART_RootBorder");
                var plotAreaBorder = GetTemplatePart<Border>(control, "PART_PlotAreaBorder");

                Assert.Same(Brushes.MidnightBlue, rootBorder.Background);
                Assert.Same(Brushes.Goldenrod, rootBorder.BorderBrush);
                Assert.Equal(new Thickness(2), rootBorder.BorderThickness);
                Assert.Same(Brushes.Black, plotAreaBorder.Background);
                Assert.Same(Brushes.OrangeRed, plotAreaBorder.BorderBrush);
                Assert.Equal(new Thickness(3), plotAreaBorder.BorderThickness);
            });
        });
    }

    [Fact]
    public void GridLinesControl_UsesOnlyDefaultAxes()
    {
        RunInSta(() =>
        {
            var bottomColor = Colors.DeepSkyBlue;
            var leftColor = Colors.LimeGreen;
            var topColor = Colors.Magenta;
            var rightColor = Colors.Orange;

            var xAxes = new ObservableCollection<IAxis>
            {
                new LinearAxis
                {
                    Id = Chart.DefaultXAxisId,
                    Placement = AxisPlacement.Bottom,
                    VisibleRange = new DataRange(0, 10),
                    ShowMajorGridLines = true,
                    ShowMinorGridLines = false,
                    MajorTickInterval = 1,
                    MajorGridLineBrush = new SolidColorBrush(bottomColor)
                },
                new LinearAxis
                {
                    Id = "TopXAxis",
                    Placement = AxisPlacement.Top,
                    VisibleRange = new DataRange(0, 10),
                    ShowMajorGridLines = true,
                    ShowMinorGridLines = false,
                    MajorTickInterval = 1,
                    MajorGridLineBrush = new SolidColorBrush(topColor)
                }
            };

            var yAxes = new ObservableCollection<IAxis>
            {
                new LinearAxis
                {
                    Id = Chart.DefaultYAxisId,
                    Placement = AxisPlacement.Left,
                    VisibleRange = new DataRange(0, 10),
                    ShowMajorGridLines = true,
                    ShowMinorGridLines = false,
                    MajorTickInterval = 1,
                    MajorGridLineBrush = new SolidColorBrush(leftColor)
                },
                new LinearAxis
                {
                    Id = "RightYAxis",
                    Placement = AxisPlacement.Right,
                    VisibleRange = new DataRange(0, 10),
                    ShowMajorGridLines = true,
                    ShowMinorGridLines = false,
                    MajorTickInterval = 1,
                    MajorGridLineBrush = new SolidColorBrush(rightColor)
                }
            };

            var gridLines = new GridLinesControl
            {
                XAxes = xAxes,
                YAxes = yAxes
            };

            RunInWindow(gridLines, new Size(240, 140), control =>
            {
                var lines = control.Children.OfType<Line>().ToArray();

                Assert.NotEmpty(lines);
                Assert.Contains(lines, static line => Math.Abs(line.X1 - line.X2) < 0.1);
                Assert.Contains(lines, static line => Math.Abs(line.Y1 - line.Y2) < 0.1);
                Assert.DoesNotContain(lines, line => GetBrushColor(line.Stroke) == topColor);
                Assert.DoesNotContain(lines, line => GetBrushColor(line.Stroke) == rightColor);
                Assert.Contains(lines, line => GetBrushColor(line.Stroke) == bottomColor);
                Assert.Contains(lines, line => GetBrushColor(line.Stroke) == leftColor);
            });
        });
    }

    [Fact]
    public void GridLinesControl_MissingDefaultAxes_RendersNoLines()
    {
        RunInSta(() =>
        {
            var gridLines = new GridLinesControl
            {
                XAxes = new ObservableCollection<IAxis>
                {
                    new LinearAxis
                    {
                        Id = "CustomXAxis",
                        Placement = AxisPlacement.Bottom,
                        VisibleRange = new DataRange(0, 10),
                        ShowMajorGridLines = true,
                        MajorGridLineBrush = Brushes.Cyan
                    }
                },
                YAxes = new ObservableCollection<IAxis>
                {
                    new LinearAxis
                    {
                        Id = "CustomYAxis",
                        Placement = AxisPlacement.Left,
                        VisibleRange = new DataRange(0, 10),
                        ShowMajorGridLines = true,
                        MajorGridLineBrush = Brushes.LimeGreen
                    }
                }
            };

            RunInWindow(gridLines, new Size(240, 140), control =>
            {
                Assert.Empty(control.Children.OfType<Line>());
            });
        });
    }

    [Fact]
    public void GridLinesControl_DefaultAxisChanges_RefreshLines()
    {
        RunInSta(() =>
        {
            var defaultXAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 10),
                ShowMajorGridLines = true,
                ShowMinorGridLines = false,
                MajorTickInterval = 1,
                MajorGridLineBrush = Brushes.DeepSkyBlue
            };

            var gridLines = new GridLinesControl
            {
                XAxes = new ObservableCollection<IAxis> { defaultXAxis },
                YAxes = new ObservableCollection<IAxis>()
            };

            RunInWindow(gridLines, new Size(240, 120), control =>
            {
                var before = control.Children.OfType<Line>().Select(static line => line.X1).ToArray();

                defaultXAxis.MajorGridLineBrush = Brushes.Yellow;
                defaultXAxis.VisibleRange = new DataRange(0, 5);
                PumpDispatcher();
                control.UpdateLayout();

                var afterLines = control.Children.OfType<Line>().ToArray();
                var after = afterLines.Select(static line => line.X1).ToArray();

                Assert.False(before.SequenceEqual(after));
                Assert.NotEmpty(afterLines);
                Assert.All(afterLines, line => Assert.Equal(Colors.Yellow, GetBrushColor(line.Stroke)));
            });
        });
    }

    [Fact]
    public void Chart_PlotAreaHitTesting_ExcludesAxisRegions()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Template = CreateChartTemplate()
            };

            RunInWindow(chart, new Size(220, 140), control =>
            {
                var surface = GetTemplatePart<FrameworkElement>(control, "PART_Surface");
                var surfaceOrigin = surface.TransformToAncestor(control).Transform(new Point(0, 0));

                Assert.False(InvokeIsPointInPlotArea(control, new Point(Math.Max(0, surfaceOrigin.X - 5), surfaceOrigin.Y + 5)));
                Assert.False(InvokeIsPointInPlotArea(control, new Point(surfaceOrigin.X + 5, surfaceOrigin.Y + surface.ActualHeight + 5)));
                Assert.True(InvokeIsPointInPlotArea(control, new Point(surfaceOrigin.X + surface.ActualWidth / 2, surfaceOrigin.Y + surface.ActualHeight / 2)));
            });
        });
    }

    [Fact]
    public void Chart_AxisPresenters_OverlapPlotBorderByConfiguredThickness()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Template = CreateChartTemplate(),
                PlotAreaBorderThickness = new Thickness(1, 2, 3, 4)
            };

            RunInWindow(chart, new Size(240, 160), control =>
            {
                var topPresenter = GetTemplatePart<AxisItemsControl>(control, "PART_TopAxesPresenter");
                var leftPresenter = GetTemplatePart<AxisItemsControl>(control, "PART_LeftAxesPresenter");
                var rightPresenter = GetTemplatePart<AxisItemsControl>(control, "PART_RightAxesPresenter");
                var bottomPresenter = GetTemplatePart<AxisItemsControl>(control, "PART_BottomAxesPresenter");

                Assert.Same(Transform.Identity, topPresenter.RenderTransform);
                Assert.Same(Transform.Identity, leftPresenter.RenderTransform);
                Assert.Same(Transform.Identity, rightPresenter.RenderTransform);
                Assert.Same(Transform.Identity, bottomPresenter.RenderTransform);
            });
        });
    }

    [Fact]
    public void Chart_DefaultAxisTicks_StayAlignedWithGridLines_AfterRangeRelayout()
    {
        RunInSta(() =>
        {
            var chart = new Chart
            {
                Template = CreateChartTemplate(),
                PlotAreaBorderBrush = Brushes.OrangeRed,
                PlotAreaBorderThickness = new Thickness(3)
            };

            if (chart.XAxes.FirstOrDefault(static axis => axis.Id == Chart.DefaultXAxisId) is LinearAxis xAxis)
            {
                xAxis.ShowMajorGridLines = true;
                xAxis.ShowMinorGridLines = false;
            }

            if (chart.YAxes.FirstOrDefault(static axis => axis.Id == Chart.DefaultYAxisId) is LinearAxis yAxis)
            {
                yAxis.ShowMajorGridLines = true;
                yAxis.ShowMinorGridLines = false;
            }

            RunInWindow(chart, new Size(320, 220), control =>
            {
                AssertAxisAndGridAlignment(control);

                control.XRange = new DataRange(0, 1000);
                control.YRange = new DataRange(-2500, 2500);
                control.UpdateLayout();
                PumpDispatcher();

                AssertAxisAndGridAlignment(control);
            });
        });
    }

    [Fact]
    public void RenderableSeries_DefaultAxisIds_PointToDefaultAxes()
    {
        var line = new LineRenderableSeries();
        Assert.Equal("DefaultXAxis", line.XAxisId);
        Assert.Equal("DefaultYAxis", line.YAxisId);

        var scatter = new ScatterRenderableSeries();
        Assert.Equal("DefaultXAxis", scatter.XAxisId);
        Assert.Equal("DefaultYAxis", scatter.YAxisId);

        var bar = new BarRenderableSeries();
        Assert.Equal("DefaultXAxis", bar.XAxisId);
        Assert.Equal("DefaultYAxis", bar.YAxisId);
    }

    [Fact]
    public void AxisRenderGroup_StoresRangeAndCommands()
    {
        var group = new AxisRenderGroup
        {
            XRange = new DataRange(0, 10),
            YRange = new DataRange(-1, 1),
            XMapper = LinearCoordinateMapper.Instance,
            YMapper = LinearCoordinateMapper.Instance,
            Commands = new List<IRenderCommand>()
        };

        Assert.Equal(0, group.XRange.Min);
        Assert.Equal(10, group.XRange.Max);
        Assert.Equal(-1, group.YRange.Min);
        Assert.Equal(1, group.YRange.Max);
        Assert.Empty(group.Commands);
    }

    [Fact]
    public void SeriesRendererDispatcher_RenderGroups_GroupsByAxisId()
    {
        RunInSta(() =>
        {
        var data1 = new VariableDataSeries<double, double>(x => x);
        data1.Append(0, 1);
        data1.Append(1, 2);

        var data2 = new VariableDataSeries<double, double>(x => x);
        data2.Append(0, 100);
        data2.Append(1, 200);

        var series1 = new LineRenderableSeries
        {
            Stroke = Colors.Cyan,
            DataSeries = data1,
            YAxisId = "LeftAxis"
        };

        var series2 = new LineRenderableSeries
        {
            Stroke = Colors.Orange,
            DataSeries = data2,
            YAxisId = "RightAxis"
        };

        var leftAxis = new LinearAxis
        {
            Id = "LeftAxis",
            Placement = AxisPlacement.Left,
            VisibleRange = new DataRange(-1, 2)
        };

        var rightAxis = new LinearAxis
        {
            Id = "RightAxis",
            Placement = AxisPlacement.Right,
            VisibleRange = new DataRange(0, 300)
        };

        var defaultXAxis = new LinearAxis
        {
            Id = "DefaultXAxis",
            Placement = AxisPlacement.Bottom,
            VisibleRange = new DataRange(0, 1)
        };

        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 1),
            YRangeAccessor = () => new DataRange(-1, 2),
            SeriesAccessor = () => new List<IRenderableSeries> { series1, series2 },
            XAxesAccessor = () => new List<IAxis> { defaultXAxis },
            YAxesAccessor = () => new List<IAxis> { leftAxis, rightAxis }
        };

        var dispatcher = new SeriesRendererDispatcher();
        var groups = dispatcher.RenderGroups(context, 100, 100);

        Assert.Equal(2, groups.Count);
        });
    }

    [Fact]
    public void SeriesRendererDispatcher_RenderGroups_ExcludeGridLineCommands()
    {
        RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            dataSeries.Append(1, 2);

            var series = new LineRenderableSeries
            {
                Stroke = Colors.Cyan,
                DataSeries = dataSeries
            };

            var xAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(0, 1),
                ShowMajorGridLines = true
            };

            var yAxis = new LinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 2),
                ShowMajorGridLines = true
            };

            var context = new ChartRenderContext
            {
                XRangeAccessor = () => new DataRange(0, 1),
                YRangeAccessor = () => new DataRange(0, 2),
                SeriesAccessor = () => new List<IRenderableSeries> { series },
                XAxesAccessor = () => new List<IAxis> { xAxis },
                YAxesAccessor = () => new List<IAxis> { yAxis }
            };

            var groups = new SeriesRendererDispatcher().RenderGroups(context, 100, 100);

            Assert.Single(groups);
            Assert.DoesNotContain(groups[0].Commands, static command => command is GridLineRenderOperation);
        });
    }

    private static bool GetChartDirty(Chart chart)
    {
        var field = typeof(Chart).GetField("_isDirty", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)field!.GetValue(chart)!;
    }

    private static void SetChartDirty(Chart chart, bool value)
    {
        var field = typeof(Chart).GetField("_isDirty", BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(chart, value);
    }

    private static int GetPendingBackgroundAutoRangeRequest(Chart chart)
    {
        var field = typeof(Chart).GetField("_pendingBackgroundAutoRangeRequest", BindingFlags.Instance | BindingFlags.NonPublic);
        return (int)field!.GetValue(chart)!;
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

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static AxisControl CreateAxisControl(IAxis axis, Size size)
    {
        var control = new AxisControl
        {
            Axis = axis,
            FontSize = 12,
            Foreground = Brushes.White,
            Template = CreateAxisControlTemplate()
        };

        control.ApplyTemplate();
        control.Measure(size);
        control.Arrange(new Rect(new Point(0, 0), size));
        control.UpdateLayout();
        return control;
    }

    private static ControlTemplate CreateAxisControlTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.Name = "PART_Root";

        var titleHost = new FrameworkElementFactory(typeof(Border));
        titleHost.Name = "PART_TitleHost";

        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.Name = "PART_Title";
        titleHost.AppendChild(title);

        var contentHost = new FrameworkElementFactory(typeof(Canvas));
        contentHost.Name = "PART_ContentHost";

        var axisLine = new FrameworkElementFactory(typeof(Line));
        axisLine.Name = "PART_AxisLine";
        contentHost.AppendChild(axisLine);

        var tickHost = new FrameworkElementFactory(typeof(Canvas));
        tickHost.Name = "PART_TickHost";
        contentHost.AppendChild(tickHost);

        root.AppendChild(titleHost);
        root.AppendChild(contentHost);

        return new ControlTemplate(typeof(AxisControl))
        {
            VisualTree = root
        };
    }

    private static ControlTemplate CreateChartTemplate()
    {
        const string xaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:cc="clr-namespace:Cheari.Controls;assembly=Cheari.Controls.Chart"
                 xmlns:axesControls="clr-namespace:Cheari.Controls.Axes.Controls;assembly=Cheari.Controls.Chart"
                 xmlns:vortice="clr-namespace:Vortice.Wpf;assembly=Vortice.Wpf"
                 TargetType="{x:Type cc:Chart}">
    <ControlTemplate.Resources>
        <Style TargetType="{x:Type axesControls:AxisControl}">
            <Setter Property="Focusable" Value="False" />
            <Setter Property="IsHitTestVisible" Value="False" />
            <Setter Property="SnapsToDevicePixels" Value="True" />
            <Setter Property="UseLayoutRounding" Value="True" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type axesControls:AxisControl}">
                        <Grid x:Name="PART_Root" ClipToBounds="True">
                            <Border x:Name="PART_TitleHost" Background="Transparent">
                                <TextBlock x:Name="PART_Title"
                                           HorizontalAlignment="Center"
                                           VerticalAlignment="Center"
                                           TextAlignment="Center"
                                           TextTrimming="CharacterEllipsis" />
                            </Border>
                            <Canvas x:Name="PART_ContentHost" ClipToBounds="True">
                                <Line x:Name="PART_AxisLine" SnapsToDevicePixels="True" />
                                <Canvas x:Name="PART_TickHost" ClipToBounds="True" />
                            </Canvas>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </ControlTemplate.Resources>
    <Border x:Name="PART_RootBorder"
            Background="{TemplateBinding Background}"
            BorderBrush="{TemplateBinding BorderBrush}"
            BorderThickness="{TemplateBinding BorderThickness}">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <axesControls:AxisItemsControl x:Name="PART_TopAxesPresenter"
                                        Grid.Column="1"
                                        Grid.Row="0"
                                        Panel.ZIndex="1"
                                        ItemsSource="{Binding TopXAxes, RelativeSource={RelativeSource TemplatedParent}}">
                <axesControls:AxisItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <axesControls:AxisPanel AxisPlacement="Top" />
                    </ItemsPanelTemplate>
                </axesControls:AxisItemsControl.ItemsPanel>
            </axesControls:AxisItemsControl>

            <axesControls:AxisItemsControl x:Name="PART_LeftAxesPresenter"
                                        Grid.Column="0"
                                        Grid.Row="1"
                                        Panel.ZIndex="1"
                                        ItemsSource="{Binding LeftYAxes, RelativeSource={RelativeSource TemplatedParent}}">
                <axesControls:AxisItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <axesControls:AxisPanel AxisPlacement="Left" />
                    </ItemsPanelTemplate>
                </axesControls:AxisItemsControl.ItemsPanel>
            </axesControls:AxisItemsControl>

            <Grid Grid.Column="1"
                  Grid.Row="1"
                  ClipToBounds="True">
                <Border x:Name="PART_PlotAreaBorder"
                        Background="{TemplateBinding PlotAreaBackground}"
                        BorderBrush="{TemplateBinding PlotAreaBorderBrush}"
                        BorderThickness="{TemplateBinding PlotAreaBorderThickness}" />
                <Grid>
                    <axesControls:GridLinesControl x:Name="PART_GridLines"
                                                XAxes="{Binding XAxes, RelativeSource={RelativeSource TemplatedParent}}"
                                                YAxes="{Binding YAxes, RelativeSource={RelativeSource TemplatedParent}}" />
                    <vortice:DrawingSurface x:Name="PART_Surface"
                                            Stretch="Fill"
                                            HorizontalAlignment="Stretch"
                                            VerticalAlignment="Stretch" />
                </Grid>
            </Grid>

            <axesControls:AxisItemsControl x:Name="PART_RightAxesPresenter"
                                        Grid.Column="2"
                                        Grid.Row="1"
                                        Panel.ZIndex="1"
                                        ItemsSource="{Binding RightYAxes, RelativeSource={RelativeSource TemplatedParent}}">
                <axesControls:AxisItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <axesControls:AxisPanel AxisPlacement="Right" />
                    </ItemsPanelTemplate>
                </axesControls:AxisItemsControl.ItemsPanel>
            </axesControls:AxisItemsControl>

            <axesControls:AxisItemsControl x:Name="PART_BottomAxesPresenter"
                                        Grid.Column="1"
                                        Grid.Row="2"
                                        Panel.ZIndex="1"
                                        ItemsSource="{Binding BottomXAxes, RelativeSource={RelativeSource TemplatedParent}}">
                <axesControls:AxisItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <axesControls:AxisPanel AxisPlacement="Bottom" />
                    </ItemsPanelTemplate>
                </axesControls:AxisItemsControl.ItemsPanel>
            </axesControls:AxisItemsControl>
        </Grid>
    </Border>
</ControlTemplate>
""";

        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static T GetTemplatePart<T>(Control control, string name) where T : class
    {
        return (control.Template.FindName(name, control) as T)
            ?? throw new InvalidOperationException($"Template part '{name}' was not found.");
    }

    private static void RunInWindow<T>(T element, Size size, Action<T> action) where T : FrameworkElement
    {
        var window = new Window
        {
            Content = element,
            Width = size.Width,
            Height = size.Height,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };

        try
        {
            window.Show();
            element.ApplyTemplate();
            window.UpdateLayout();
            element.Measure(size);
            element.Arrange(new Rect(new Point(0, 0), size));
            element.UpdateLayout();
            PumpDispatcher();
            action(element);
        }
        finally
        {
            window.Close();
        }
    }

    private static bool InvokeIsPointInPlotArea(Chart chart, Point point)
    {
        var method = typeof(Chart).GetMethod("IsPointInPlotArea", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)method!.Invoke(chart, [point])!;
    }

    private static Color? GetBrushColor(Brush? brush)
        => (brush as SolidColorBrush)?.Color;

    private static void AssertTranslateTransform(Transform transform, double expectedX, double expectedY)
    {
        var translate = Assert.IsType<TranslateTransform>(transform);
        Assert.Equal(expectedX, translate.X, 10);
        Assert.Equal(expectedY, translate.Y, 10);
    }

    private static void AssertAxisAndGridAlignment(Chart chart)
    {
        var surface = GetTemplatePart<FrameworkElement>(chart, "PART_Surface");
        var gridLines = GetTemplatePart<GridLinesControl>(chart, "PART_GridLines");
        var leftAxes = GetTemplatePart<AxisItemsControl>(chart, "PART_LeftAxesPresenter");
        var bottomAxes = GetTemplatePart<AxisItemsControl>(chart, "PART_BottomAxesPresenter");

        chart.UpdateLayout();
        PumpDispatcher();

        Assert.Equal(surface.ActualWidth, gridLines.ActualWidth, 6);
        Assert.Equal(surface.ActualHeight, gridLines.ActualHeight, 6);

        var leftAxis = Assert.IsType<AxisControl>(leftAxes.ItemContainerGenerator.ContainerFromIndex(0));
        var bottomAxis = Assert.IsType<AxisControl>(bottomAxes.ItemContainerGenerator.ContainerFromIndex(0));
        leftAxis.ApplyTemplate();
        bottomAxis.ApplyTemplate();

        var leftContentHost = GetTemplatePart<FrameworkElement>(leftAxis, "PART_ContentHost");
        var bottomContentHost = GetTemplatePart<FrameworkElement>(bottomAxis, "PART_ContentHost");

        var horizontalGridPositions = gridLines.Children
            .OfType<Line>()
            .Where(static line => Math.Abs(line.Y1 - line.Y2) < 0.01)
            .Select(static line => line.Y1)
            .OrderBy(static position => position)
            .ToArray();
        var verticalGridPositions = gridLines.Children
            .OfType<Line>()
            .Where(static line => Math.Abs(line.X1 - line.X2) < 0.01)
            .Select(static line => line.X1)
            .OrderBy(static position => position)
            .ToArray();

        var leftTickPositions = GetTemplatePart<Canvas>(leftAxis, "PART_TickHost").Children
            .OfType<Line>()
            .Select(static line => line.Y1)
            .OrderBy(static position => position)
            .ToArray();

        var bottomTickPositions = GetTemplatePart<Canvas>(bottomAxis, "PART_TickHost").Children
            .OfType<Line>()
            .Select(static line => line.X1)
            .OrderBy(static position => position)
            .ToArray();

        Assert.Equal(horizontalGridPositions.Length, leftTickPositions.Length);
        Assert.Equal(verticalGridPositions.Length, bottomTickPositions.Length);

        for (int i = 0; i < horizontalGridPositions.Length; i++)
            Assert.True(Math.Abs(horizontalGridPositions[i] - leftTickPositions[i]) <= 1,
                $"Horizontal position mismatch at index {i}: grid={horizontalGridPositions[i]}, tick={leftTickPositions[i]}");

        for (int i = 0; i < verticalGridPositions.Length; i++)
            Assert.True(Math.Abs(verticalGridPositions[i] - bottomTickPositions[i]) <= 1,
                $"Vertical position mismatch at index {i}: grid={verticalGridPositions[i]}, tick={bottomTickPositions[i]}");
    }

    private sealed class MetadataVariableDataSeries<TX, TY> : VariableDataSeries<TX, TY>, IDataPointMetadataProvider
    {
        public MetadataVariableDataSeries(Func<TX, double> xToDouble)
            : base(xToDouble)
        {
        }

        public string? GetPointLabel(int index) => $"point-{index}";

        public Color? GetPointColor(int index) => Colors.Gold;
    }

    [Fact]
    public void Chart_ZoomExtents_BarYAxis_AdjustsToDataRange()
    {
        RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 156);
            dataSeries.Append(1, 89);
            dataSeries.Append(2, 234);
            dataSeries.Append(3, 178);
            dataSeries.Append(4, 198);
            dataSeries.Append(5, 45);

            var yAxis = new LinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 100),
                VisibleRangeLimitMode = VisibleRangeLimitMode.None,
                VisibleRangeLimit = new DataRange(double.MinValue, double.MaxValue),
                AutoRange = true
            };

            var xAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(-1, 6),
                AutoRange = true
            };

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new BarRenderableSeries
                    {
                        DataSeries = dataSeries,
                        BarSpacing = 0.2
                    }
                },
                XAxes = [xAxis],
                YAxes = [yAxis]
            };

            chart.ZoomExtents();

            Assert.True(chart.YRange.Max >= 200,
                $"YRange.Max should be >= 200 to cover data max 234, got {chart.YRange.Max}");
            Assert.True(chart.YRange.Min <= 0,
                $"YRange.Min should be <= 0 for bar baseline, got {chart.YRange.Min}");
            Assert.True(yAxis.VisibleRange.Max >= 200,
                $"yAxis.VisibleRange.Max should be >= 200, got {yAxis.VisibleRange.Max}");
            Assert.True(yAxis.VisibleRange.Min <= 0,
                $"yAxis.VisibleRange.Min should be <= 0, got {yAxis.VisibleRange.Min}");
        });
    }

    [Fact]
    public void Chart_ZoomExtents_BarYAxis_ReplacesManuallyZoomedRange()
    {
        RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 156);
            dataSeries.Append(1, 89);
            dataSeries.Append(2, 234);
            dataSeries.Append(3, 178);

            var yAxis = new LinearAxis
            {
                Id = Chart.DefaultYAxisId,
                Placement = AxisPlacement.Left,
                VisibleRange = new DataRange(0, 100),
                AutoRange = true
            };

            var xAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom,
                VisibleRange = new DataRange(-1, 4),
                AutoRange = true
            };

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new BarRenderableSeries
                    {
                        DataSeries = dataSeries,
                        BarSpacing = 0.2
                    }
                },
                XAxes = [xAxis],
                YAxes = [yAxis]
            };

            chart.YRange = new DataRange(30, 70);
            chart.XRange = new DataRange(1, 2);

            chart.ZoomExtents();

            Assert.True(chart.YRange.Max >= 200,
                $"After zoom-in then ZoomExtents, YRange.Max should be >= 200, got {chart.YRange.Max}");
            Assert.True(chart.YRange.Min <= 0,
                $"After zoom-in then ZoomExtents, YRange.Min should be <= 0, got {chart.YRange.Min}");
            Assert.True(yAxis.VisibleRange.Max >= 200,
                $"After zoom-in then ZoomExtents, yAxis.VisibleRange.Max should be >= 200, got {yAxis.VisibleRange.Max}");
            Assert.True(yAxis.VisibleRange.Min <= 0,
                $"After zoom-in then ZoomExtents, yAxis.VisibleRange.Min should be <= 0, got {yAxis.VisibleRange.Min}");
        });
    }

    private sealed class CountingLinearAxis : LinearAxis
    {
        public int AutoRangeCallCount { get; private set; }

        public override DataRange CalculateAutoRange(IEnumerable<IDataSeries> dataSeries)
        {
            AutoRangeCallCount++;
            return base.CalculateAutoRange(dataSeries);
        }
    }
}
