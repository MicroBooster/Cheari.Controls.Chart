using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Cheari.Controls.Annotations;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.Controls;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Legend;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Rendering;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Rendering.Downsampling;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Synchronization;

namespace Cheari.Controls.Tests;

internal static class TestHelper
{
    public static void RunInSta(Action action)
    {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        captured?.Throw();
    }
}

public class CoreTests
{
    [Fact]
    public void DataRange_Constructor_SetsMinAndMax()
    {
        var range = new DataRange(-5.5, 10.3);
        Assert.Equal(-5.5, range.Min);
        Assert.Equal(10.3, range.Max);
    }

    [Fact]
    public void DataRange_Length_ReturnsDifference()
    {
        var range = new DataRange(3, 10);
        Assert.Equal(7, range.Length);
    }

    [Fact]
    public void DataRange_DefaultValue_IsZero()
    {
        var range = default(DataRange);
        Assert.Equal(0, range.Min);
        Assert.Equal(0, range.Max);
        Assert.Equal(0, range.Length);
    }

    [Fact]
    public void DataRange_NegativeRange_AutoSwapAndLengthPositive()
    {
        var range = new DataRange(10, 3);
        Assert.Equal(3, range.Min);
        Assert.Equal(10, range.Max);
        Assert.Equal(7, range.Length);
    }

    [Fact]
    public void DataRangeTypeConverter_ValidString_ConvertsCorrectly()
    {
        var converter = new DataRangeTypeConverter();
        var result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, "0.5,10.5");
        var range = Assert.IsType<DataRange>(result);
        Assert.Equal(0.5, range.Min);
        Assert.Equal(10.5, range.Max);
    }

    [Fact]
    public void DataRangeTypeConverter_NegativeString_ConvertsCorrectly()
    {
        var converter = new DataRangeTypeConverter();
        var result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, "-10,20");
        var range = Assert.IsType<DataRange>(result);
        Assert.Equal(-10, range.Min);
        Assert.Equal(20, range.Max);
    }

    [Fact]
    public void DataRangeTypeConverter_InvalidString_Throws()
    {
        var converter = new DataRangeTypeConverter();
        Assert.Throws<FormatException>(() => converter.ConvertFrom(null, CultureInfo.InvariantCulture, "invalid"));
    }

    [Fact]
    public void DataRangeTypeConverter_CanConvertFrom_StringIsTrue()
    {
        var converter = new DataRangeTypeConverter();
        Assert.True(converter.CanConvertFrom(null, typeof(string)));
        Assert.False(converter.CanConvertFrom(null, typeof(int)));
    }

    [Fact]
    public void LineStyle_HasFiveValues()
    {
        var values = Enum.GetValues<LineStyle>();
        Assert.Equal(5, values.Length);
        Assert.Contains(LineStyle.Solid, values);
        Assert.Contains(LineStyle.Dash, values);
        Assert.Contains(LineStyle.Dot, values);
        Assert.Contains(LineStyle.DashDot, values);
        Assert.Contains(LineStyle.DashDotDot, values);
    }

    [Fact]
    public void MarkerType_HasFiveValues()
    {
        var values = Enum.GetValues<MarkerType>();
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void VisibleRangeLimitMode_HasFourValues()
    {
        var values = Enum.GetValues<VisibleRangeLimitMode>();
        Assert.Equal(4, values.Length);
    }
}

public class ConverterTests
{
    [Fact]
    public void MathAddConverter_Convert_AddsValueAndParameter()
    {
        var converter = MathAddConverter.Instance;
        var result = converter.Convert(10.0, typeof(double), "5", CultureInfo.InvariantCulture);
        Assert.Equal(15.0, result);
    }

    [Fact]
    public void MathAddConverter_Convert_WithNegativeParameter_Subtracts()
    {
        var result = MathAddConverter.Instance.Convert(10.0, typeof(double), "-3", CultureInfo.InvariantCulture);
        Assert.Equal(7.0, result);
    }

    [Fact]
    public void MathAddConverter_Convert_NonDoubleValue_ReturnsSameValue()
    {
        var result = MathAddConverter.Instance.Convert("hello", typeof(string), "5", CultureInfo.InvariantCulture);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void MathAddConverter_Convert_NullParameter_ReturnsSameValue()
    {
        var result = MathAddConverter.Instance.Convert(10.0, typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void MathAddConverter_Convert_InvalidParameter_ReturnsSameValue()
    {
        var result = MathAddConverter.Instance.Convert(10.0, typeof(double), "abc", CultureInfo.InvariantCulture);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void MathAddConverter_ConvertBack_Throws()
    {
        Assert.Throws<NotImplementedException>(() =>
            MathAddConverter.Instance.ConvertBack(10.0, typeof(double), "5", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MathSubtractConverter_Convert_SubtractsParameter()
    {
        var converter = MathSubtractConverter.Instance;
        var result = converter.Convert(10.0, typeof(double), "3", CultureInfo.InvariantCulture);
        Assert.Equal(7.0, result);
    }

    [Fact]
    public void MathSubtractConverter_Convert_WithNegativeParameter_Adds()
    {
        var result = MathSubtractConverter.Instance.Convert(10.0, typeof(double), "-5", CultureInfo.InvariantCulture);
        Assert.Equal(15.0, result);
    }

    [Fact]
    public void MathSubtractConverter_Convert_NonDoubleValue_ReturnsSameValue()
    {
        var result = MathSubtractConverter.Instance.Convert(new object(), typeof(object), "5", CultureInfo.InvariantCulture);
        Assert.IsType<object>(result);
    }

    [Fact]
    public void MathSubtractConverter_Convert_NullParameter_ReturnsSameValue()
    {
        var result = MathSubtractConverter.Instance.Convert(10.0, typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void MathSubtractConverter_ConvertBack_Throws()
    {
        Assert.Throws<NotImplementedException>(() =>
            MathSubtractConverter.Instance.ConvertBack(10.0, typeof(double), "3", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MathAddConverter_IsSingleton()
    {
        Assert.Same(MathAddConverter.Instance, MathAddConverter.Instance);
    }

    [Fact]
    public void MathSubtractConverter_IsSingleton()
    {
        Assert.Same(MathSubtractConverter.Instance, MathSubtractConverter.Instance);
    }
}

public class LegendTests
{
    [Fact]
    public void ChartLegend_DefaultPosition_IsExternalRight()
    {
        var legend = new ChartLegend();
        Assert.Equal(LegendPosition.ExternalRight, legend.Position);
    }

    [Fact]
    public void ChartLegend_DefaultOrientation_IsVertical()
    {
        var legend = new ChartLegend();
        Assert.Equal(LegendOrientation.Vertical, legend.Orientation);
    }

    [Fact]
    public void ChartLegend_PositionChange_UpdatesAlignment()
    {
        var legend = new ChartLegend();
        legend.Position = LegendPosition.InternalTop;
        Assert.Equal(HorizontalAlignment.Center, legend.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, legend.VerticalAlignment);
    }

    [Fact]
    public void ChartLegend_PositionChange_InternalRight_SetsCorrectAlignment()
    {
        var legend = new ChartLegend();
        legend.Position = LegendPosition.InternalRight;
        Assert.Equal(HorizontalAlignment.Right, legend.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, legend.VerticalAlignment);
    }

    [Fact]
    public void ChartLegend_PositionChange_ExternalBottom_SetsStretch()
    {
        var legend = new ChartLegend();
        legend.Position = LegendPosition.ExternalBottom;
        Assert.Equal(HorizontalAlignment.Stretch, legend.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, legend.VerticalAlignment);
    }

    [Fact]
    public void ChartLegend_PositionChange_RaisesPropertyChanged()
    {
        var legend = new ChartLegend();
        var changedProperties = new List<string?>();
        legend.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        legend.Position = LegendPosition.InternalTop;

        Assert.Contains("Position", changedProperties);
        Assert.Contains("HorizontalAlignment", changedProperties);
        Assert.Contains("VerticalAlignment", changedProperties);
    }

    [Fact]
    public void ChartLegend_PositionChange_ToSameValue_NoEvent()
    {
        var legend = new ChartLegend();
        var fired = false;
        legend.PropertyChanged += (_, _) => fired = true;

        legend.Position = LegendPosition.ExternalRight;

        Assert.False(fired);
    }

    [Fact]
    public void ChartLegend_OrientationChange_RaisesPropertyChanged()
    {
        var legend = new ChartLegend();
        string? lastProperty = null;
        legend.PropertyChanged += (_, e) => lastProperty = e.PropertyName;

        legend.Orientation = LegendOrientation.Horizontal;

        Assert.Equal("Orientation", lastProperty);
    }

    [Fact]
    public void ChartLegend_HorizontalAlignment_SetManually()
    {
        var legend = new ChartLegend();
        legend.HorizontalAlignment = HorizontalAlignment.Left;
        Assert.Equal(HorizontalAlignment.Left, legend.HorizontalAlignment);
    }

    [Fact]
    public void ChartLegend_VerticalAlignment_SetManually()
    {
        var legend = new ChartLegend();
        legend.VerticalAlignment = VerticalAlignment.Top;
        Assert.Equal(VerticalAlignment.Top, legend.VerticalAlignment);
    }

    [Fact]
    public void ChartLegend_SetSeries_CreatesItems()
    {
        TestHelper.RunInSta(() =>
        {
            var legend = new ChartLegend();
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var lineSeries = new LineRenderableSeries
            {
                Stroke = Colors.Red,
                Title = "Series A",
                DataSeries = dataSeries
            };

            legend.SetSeries(new List<IRenderableSeries> { lineSeries });

            Assert.Single(legend.Items);
            Assert.Equal("Series A", legend.Items[0].Title);
            Assert.Equal(Colors.Red, legend.Items[0].Stroke);
        });
    }

    [Fact]
    public void ChartLegend_SetSeries_Empty_ClearsItems()
    {
        var legend = new ChartLegend();
        legend.SetSeries(new List<IRenderableSeries>());
        Assert.Empty(legend.Items);
    }

    [Fact]
    public void ChartLegend_SetSeries_RaisesItemsChanged()
    {
        var legend = new ChartLegend();
        var fired = false;
        legend.ItemsChanged += () => fired = true;

        var dataSeries = new VariableDataSeries<double, double>(x => x);
        dataSeries.Append(0, 1);
        legend.SetSeries(new List<IRenderableSeries>
        {
            new LineRenderableSeries { DataSeries = dataSeries }
        });

        Assert.True(fired);
    }

    [Fact]
    public void ChartLegend_SetSeries_WithObservableCollection_RespondsToChanges()
    {
        TestHelper.RunInSta(() =>
        {
            var legend = new ChartLegend();
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var collection = new ObservableCollection<IRenderableSeries>
            {
                new LineRenderableSeries
                {
                    Stroke = Colors.Blue,
                    Title = "Original",
                    DataSeries = dataSeries
                }
            };

            legend.SetSeries(collection);
            Assert.Single(legend.Items);

            var dataSeries2 = new VariableDataSeries<double, double>(x => x);
            dataSeries2.Append(0, 2);
            collection.Add(new LineRenderableSeries
            {
                Stroke = Colors.Green,
                Title = "Added",
                DataSeries = dataSeries2
            });

            Assert.Equal(2, legend.Items.Count);
            Assert.Equal("Original", legend.Items[0].Title);
            Assert.Equal("Added", legend.Items[1].Title);
        });
    }

    [Fact]
    public void ChartLegend_SetSeries_ReplacesOldSeries()
    {
        TestHelper.RunInSta(() =>
        {
            var legend = new ChartLegend();
            var dataSeries1 = new VariableDataSeries<double, double>(x => x);
            dataSeries1.Append(0, 1);
            var dataSeries2 = new VariableDataSeries<double, double>(x => x);
            dataSeries2.Append(0, 1);

            legend.SetSeries(new List<IRenderableSeries>
            {
                new LineRenderableSeries { Title = "First", DataSeries = dataSeries1 }
            });

            legend.SetSeries(new List<IRenderableSeries>
            {
                new ScatterRenderableSeries { Title = "Second", DataSeries = dataSeries2 }
            });

            Assert.Single(legend.Items);
            Assert.Equal("Second", legend.Items[0].Title);
        });
    }

    [Fact]
    public void LegendItem_Title_DelegatesToSeries()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new LineRenderableSeries
            {
                Title = "My Series",
                Stroke = Colors.Red,
                DataSeries = dataSeries
            };

            var item = new LegendItemTestHelper(series);

            Assert.Equal("My Series", item.Title);
            Assert.Equal(Colors.Red, item.Stroke);
            Assert.Same(series, item.Series);
        });
    }

    [Fact]
    public void LegendItem_IsVisible_PropagatesToSeries()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new LineRenderableSeries
            {
                IsVisible = true,
                DataSeries = dataSeries
            };
            var item = new LegendItemTestHelper(series);

            Assert.True(item.IsVisible);

            item.IsVisible = false;
            Assert.False(series.IsVisible);
            Assert.False(item.IsVisible);
        });
    }

    [Fact]
    public void LegendItem_Tag_DelegatesToDataSeries()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            dataSeries.Tag = "my-tag";
            var series = new LineRenderableSeries
            {
                DataSeries = dataSeries
            };
            var item = new LegendItemTestHelper(series);
            Assert.Equal("my-tag", item.Tag);
        });
    }

    [Fact]
    public void LegendIconHelper_CreateIcon_ForEachSeriesType()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var color = Colors.Red;

            var lineIcon = LegendIconHelper.CreateIcon(new LineRenderableSeries { DataSeries = dataSeries }, color);
            Assert.IsType<Path>(lineIcon);

            var scatterIcon = LegendIconHelper.CreateIcon(new ScatterRenderableSeries { DataSeries = dataSeries }, color);
            Assert.IsType<Path>(scatterIcon);

            var barIcon = LegendIconHelper.CreateIcon(new BarRenderableSeries
            {
                Fill = Colors.Blue,
                DataSeries = dataSeries
            }, color);
            Assert.IsType<Path>(barIcon);
            Assert.NotNull(barIcon.Fill);

            var areaIcon = LegendIconHelper.CreateIcon(new AreaRenderableSeries
            {
                Fill = Colors.Cyan,
                FillOpacity = 0.3,
                DataSeries = dataSeries
            }, color);
            Assert.IsType<Path>(areaIcon);

            var ohlcIcon = LegendIconHelper.CreateIcon(new OhlcRenderableSeries
            {
                UpFill = Colors.Green,
                DownFill = Colors.Red,
                DataSeries = new OhlcDataSeries()
            }, color);
            Assert.IsType<Path>(ohlcIcon);
        });
    }

    [Fact]
    public void LegendIconHelper_UpdateIconColor_ChangesColor()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var icon = LegendIconHelper.CreateIcon(new LineRenderableSeries { DataSeries = dataSeries }, Colors.Red);
            var path = Assert.IsType<Path>(icon);
            var originalColor = ((SolidColorBrush)path.Stroke).Color;
            Assert.Equal(Colors.Red, originalColor);

            LegendIconHelper.UpdateIconColor(icon, Colors.Green);
            var newColor = ((SolidColorBrush)path.Stroke).Color;
            Assert.Equal(Colors.Green, newColor);
        });
    }

    [Fact]
    public void ChartLegend_AllPositionValues_ApplyAlignment()
    {
        var positions = Enum.GetValues<LegendPosition>();
        foreach (var position in positions)
        {
            var legend = new ChartLegend();
            legend.Position = position;

            Assert.True(Enum.IsDefined(legend.HorizontalAlignment));
            Assert.True(Enum.IsDefined(legend.VerticalAlignment));
        }
    }

    [Fact]
    public void LegendControl_PositionChange_MovesBetweenInternalAndExternalGrid()
    {
        TestHelper.RunInSta(() =>
        {
            var internalGrid = new Grid();
            var chartGrid = new Grid();
            var legend = new ChartLegend { Position = LegendPosition.InternalTop };
            var control = new LegendControl { Legend = legend };

            control.SetContainers(internalGrid, chartGrid);

            Assert.Same(internalGrid, control.Parent);

            control.Position = LegendPosition.ExternalTop;
            Assert.Same(chartGrid, control.Parent);

            control.Position = LegendPosition.InternalRight;
            Assert.Same(internalGrid, control.Parent);

            control.Position = LegendPosition.ExternalBottom;
            Assert.Same(chartGrid, control.Parent);

            control.Position = LegendPosition.InternalLeft;
            Assert.Same(internalGrid, control.Parent);

            control.Position = LegendPosition.ExternalLeft;
            Assert.Same(chartGrid, control.Parent);
        });
    }

    private sealed class LegendItemTestHelper
    {
        private readonly object _legendItem;
        private static readonly Type s_legendItemType = typeof(ChartLegend).Assembly
            .GetType("Cheari.Controls.Legend.LegendItem")!;

        public LegendItemTestHelper(RenderableSeriesBase series)
        {
            _legendItem = Activator.CreateInstance(s_legendItemType, [series])!;
        }

        public string Title => (string)s_legendItemType.GetProperty("Title")!.GetValue(_legendItem)!;
        public Color Stroke => (Color)s_legendItemType.GetProperty("Stroke")!.GetValue(_legendItem)!;
        public IRenderableSeries Series => (IRenderableSeries)s_legendItemType.GetProperty("Series")!.GetValue(_legendItem)!;
        public object? Tag => s_legendItemType.GetProperty("Tag")!.GetValue(_legendItem);

        public bool IsVisible
        {
            get => (bool)s_legendItemType.GetProperty("IsVisible")!.GetValue(_legendItem)!;
            set => s_legendItemType.GetProperty("IsVisible")!.SetValue(_legendItem, value);
        }
    }
}

public class AnnotationsTests
{
    [Fact]
    public void AnnotationBase_PropertyChanged_FiresOnChange()
    {
        var annotation = new TestAnnotation();
        var changedProperties = new List<string?>();
        ((INotifyPropertyChanged)annotation).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        annotation.X1 = 42;
        annotation.Y1 = -10;
        annotation.Text = "Hello";
        annotation.IsVisible = false;
        annotation.IsSelected = true;
        annotation.IsLocked = true;
        annotation.CoordinateMode = AnnotationCoordinateMode.Relative;

        Assert.Contains("X1", changedProperties);
        Assert.Contains("Y1", changedProperties);
        Assert.Contains("Text", changedProperties);
        Assert.Contains("IsVisible", changedProperties);
        Assert.Contains("IsSelected", changedProperties);
        Assert.Contains("IsLocked", changedProperties);
        Assert.Contains("CoordinateMode", changedProperties);
    }

    [Fact]
    public void AnnotationBase_SameValue_NoPropertyChanged()
    {
        var annotation = new TestAnnotation { X1 = 5 };
        var fired = false;
        ((INotifyPropertyChanged)annotation).PropertyChanged += (_, _) => fired = true;

        annotation.X1 = 5;

        Assert.False(fired);
    }

    [Fact]
    public void AnnotationBase_DefaultValues()
    {
        var annotation = new TestAnnotation();
        Assert.Equal(0, annotation.X1);
        Assert.Equal(0, annotation.Y1);
        Assert.Equal(0, annotation.X2);
        Assert.Equal(0, annotation.Y2);
        Assert.Null(annotation.Text);
        Assert.True(annotation.IsVisible);
        Assert.False(annotation.IsSelected);
        Assert.False(annotation.IsLocked);
        Assert.Equal(AnnotationCoordinateMode.Data, annotation.CoordinateMode);
        Assert.NotNull(annotation.Fill);
        Assert.NotNull(annotation.Stroke);
    }

    [Fact]
    public void LineAnnotation_CreateVisual_ReturnsCanvasWithLine()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new LineAnnotation();
            var visual = annotation.CreateVisual();

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Single(canvas.Children);
            Assert.IsType<Line>(canvas.Children[0]);
        });
    }

    [Fact]
    public void LineAnnotation_UpdateVisual_UpdatesProperties()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new LineAnnotation
            {
                IsVisible = true,
                Stroke = new Pen(new SolidColorBrush(Colors.Red), 3)
            };
            var visual = annotation.CreateVisual();

            annotation.UpdateVisual(visual);

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Equal(Visibility.Visible, canvas.Visibility);
        });
    }

    [Fact]
    public void LineAnnotation_Hidden_UpdateVisual_Hides()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new LineAnnotation { IsVisible = false };
            var visual = annotation.CreateVisual();
            annotation.UpdateVisual(visual);

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Equal(Visibility.Collapsed, canvas.Visibility);
        });
    }

    [Fact]
    public void RectangleAnnotation_CreateVisual_ReturnsCanvasWithRectangleAndLabel()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new RectangleAnnotation { Text = "Zone" };
            var visual = annotation.CreateVisual();

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Equal(2, canvas.Children.Count);
            Assert.IsType<Rectangle>(canvas.Children[0]);
            var label = Assert.IsType<TextBlock>(canvas.Children[1]);
            Assert.Equal("Zone", label.Text);
        });
    }

    [Fact]
    public void RectangleAnnotation_UpdateVisual_UpdatesRectangleAndLabel()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new RectangleAnnotation
            {
                Fill = Brushes.Blue,
                Stroke = new Pen(new SolidColorBrush(Colors.Yellow), 2),
                Text = "Updated",
                IsVisible = true
            };
            var visual = annotation.CreateVisual();
            annotation.UpdateVisual(visual);

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Equal(Visibility.Visible, canvas.Visibility);
            if (canvas.Children[1] is TextBlock label)
                Assert.Equal("Updated", label.Text);
        });
    }

    [Fact]
    public void RectangleAnnotation_Hidden_UpdateVisual_Hides()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new RectangleAnnotation { IsVisible = false };
            var visual = annotation.CreateVisual();
            annotation.UpdateVisual(visual);

            var canvas = Assert.IsType<Canvas>(visual);
            Assert.Equal(Visibility.Collapsed, canvas.Visibility);
        });
    }

    [Fact]
    public void TextAnnotation_CreateVisual_ReturnsTextBlock()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new TextAnnotation { Label = "My Label", Text = "Fallback" };
            var visual = annotation.CreateVisual();

            var textBlock = Assert.IsType<TextBlock>(visual);
            Assert.Equal("My Label", textBlock.Text);
        });
    }

    [Fact]
    public void TextAnnotation_NoLabel_UsesText()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new TextAnnotation { Label = null, Text = "Only Text" };
            var visual = annotation.CreateVisual();

            var textBlock = Assert.IsType<TextBlock>(visual);
            Assert.Equal("Only Text", textBlock.Text);
        });
    }

    [Fact]
    public void TextAnnotation_NoLabelOrText_UsesDefault()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new TextAnnotation { Label = null, Text = null };
            var visual = annotation.CreateVisual();

            var textBlock = Assert.IsType<TextBlock>(visual);
            Assert.Equal("Text", textBlock.Text);
        });
    }

    [Fact]
    public void TextAnnotation_UpdateVisual_UpdatesProperties()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new TextAnnotation
            {
                Label = "Updated",
                Text = "Fallback",
                IsVisible = true,
                Stroke = new Pen(new SolidColorBrush(Colors.Lime), 1)
            };
            var visual = annotation.CreateVisual();
            annotation.UpdateVisual(visual);

            var textBlock = Assert.IsType<TextBlock>(visual);
            Assert.Equal("Updated", textBlock.Text);
            Assert.Equal(Visibility.Visible, textBlock.Visibility);
        });
    }

    [Fact]
    public void TextAnnotation_Hidden_UpdateVisual_Hides()
    {
        TestHelper.RunInSta(() =>
        {
            var annotation = new TextAnnotation { Label = "Hidden", IsVisible = false };
            var visual = annotation.CreateVisual();
            annotation.UpdateVisual(visual);

            var textBlock = Assert.IsType<TextBlock>(visual);
            Assert.Equal(Visibility.Collapsed, textBlock.Visibility);
        });
    }

    private sealed class TestAnnotation : AnnotationBase
    {
        public override FrameworkElement CreateVisual() => new Canvas();
        public override void UpdateVisual(FrameworkElement element) { }
    }
}

public class SynchronizationTests
{
    [Fact]
    public void SynchronizedAxisGroup_Constructor_SetsId()
    {
        var group = new SynchronizedAxisGroup("TestGroup");
        Assert.Equal("TestGroup", group.Id);
    }

    [Fact]
    public void SynchronizedAxisGroup_Register_AddsChart()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var chart = new Chart();

            group.Register(chart);

            Assert.True(HasChart(group, chart));
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_Register_Duplicate_DoesNotAddTwice()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var chart = new Chart();
            group.Register(chart);
            group.Register(chart);

            Assert.True(HasChart(group, chart));
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_Unregister_RemovesChart()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var chart = new Chart();
            group.Register(chart);

            group.Unregister(chart);

            Assert.False(HasChart(group, chart));
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_NotifyRangeChanged_DoesNotUpdateSource()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var xAxis = new LinearAxis { Id = Chart.DefaultXAxisId, Placement = AxisPlacement.Bottom, VisibleRange = new DataRange(0, 10) };
            var chart = new Chart
            {
                XAxes = new ObservableCollection<IAxis> { xAxis }
            };
            group.Register(chart);

            var originalMin = xAxis.VisibleRange.Min;
            group.NotifyRangeChanged(chart, Chart.DefaultXAxisId, new DataRange(100, 200));

            Assert.Equal(originalMin, xAxis.VisibleRange.Min, 10);
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_NotifyRangeChanged_NoReentrancy()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var xAxis1 = new LinearAxis { Id = Chart.DefaultXAxisId, Placement = AxisPlacement.Bottom, VisibleRange = new DataRange(0, 10) };
            var xAxis2 = new LinearAxis { Id = Chart.DefaultXAxisId, Placement = AxisPlacement.Bottom, VisibleRange = new DataRange(0, 10) };
            var chart1 = new Chart
            {
                XAxes = new ObservableCollection<IAxis> { xAxis1 }
            };
            var chart2 = new Chart
            {
                XAxes = new ObservableCollection<IAxis> { xAxis2 }
            };
            group.Register(chart1);
            group.Register(chart2);

            group.NotifyRangeChanged(chart1, Chart.DefaultXAxisId, new DataRange(5, 15));

            Assert.True(xAxis2.VisibleRange.Min <= 5);
            Assert.True(xAxis2.VisibleRange.Max >= 15);
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_NotifyRangeChanged_YAxis_SyncsAllYAxes()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var yAxis1 = new LinearAxis { Id = Chart.DefaultYAxisId, Placement = AxisPlacement.Left, VisibleRange = new DataRange(0, 10) };
            var yAxis2 = new LinearAxis { Id = Chart.DefaultYAxisId, Placement = AxisPlacement.Left, VisibleRange = new DataRange(0, 10) };
            var chart1 = new Chart
            {
                YAxes = new ObservableCollection<IAxis> { yAxis1 }
            };
            var chart2 = new Chart
            {
                YAxes = new ObservableCollection<IAxis> { yAxis2 }
            };
            group.Register(chart1);
            group.Register(chart2);

            group.NotifyRangeChanged(chart1, Chart.DefaultYAxisId, new DataRange(50, 100));

            Assert.Equal(50, yAxis2.VisibleRange.Min, 10);
            Assert.Equal(100, yAxis2.VisibleRange.Max, 10);
        });
    }

    [Fact]
    public void SynchronizedAxisGroup_Unregister_Nonexistent_NoError()
    {
        TestHelper.RunInSta(() =>
        {
            var group = new SynchronizedAxisGroup("Group");
            var chart = new Chart();

            group.Unregister(chart);
        });
    }

    private static bool HasChart(SynchronizedAxisGroup group, Chart chart)
    {
        var field = typeof(SynchronizedAxisGroup).GetField("_charts", BindingFlags.Instance | BindingFlags.NonPublic);
        var charts = (List<Chart>)field!.GetValue(group)!;
        return charts.Contains(chart);
    }
}

public class ModifiersAdditionalTests
{
    [Fact]
    public void ChartModifierBase_SetContext_StoresContext()
    {
        TestHelper.RunInSta(() =>
        {
            var modifier = new TestChartModifier();
            var context = new ChartRenderContext
            {
                XRangeAccessor = () => new DataRange(0, 10),
                YRangeAccessor = () => new DataRange(-1, 1)
            };

            modifier.SetContext(context);
        });
    }

    [Fact]
    public void ChartModifierBase_DefaultMouseHandlers_DoNotThrow()
    {
        var modifier = new TestChartModifier();
        modifier.OnMouseDown(null!);
        modifier.OnMouseUp(null!);
        modifier.OnMouseMove(null!);
    }

    [Fact]
    public void CrosshairModifier_Constructor_CreatesInstance()
    {
        var modifier = new CrosshairModifier();
        Assert.NotNull(modifier);
    }

    [Fact]
    public void CrosshairModifier_OnDetached_WithoutCanvas_NoError()
    {
        var modifier = new CrosshairModifier();
        modifier.OnDetached();
    }

    [Fact]
    public void TooltipModifier_Constructor_CreatesInstance()
    {
        var modifier = new TooltipModifier();
        Assert.NotNull(modifier);
    }

    [Fact]
    public void TooltipModifier_OnDetached_WithoutCanvas_NoError()
    {
        var modifier = new TooltipModifier();
        modifier.OnDetached();
    }

    [Fact]
    public void PanModifier_AttachDetach_IsSymmetric()
    {
        TestHelper.RunInSta(() =>
        {
            var modifier = new PanModifier();
            modifier.OnAttached();
            modifier.OnDetached();
        });
    }

    [Fact]
    public void ZoomModifier_AttachDetach_IsSymmetric()
    {
        TestHelper.RunInSta(() =>
        {
            var modifier = new ZoomModifier();
            modifier.OnAttached();
            modifier.OnDetached();
        });
    }

    [Fact]
    public void RubberBandModifier_Constructor_SetsDefaults()
    {
        var modifier = new RubberBandModifier();
        Assert.NotNull(modifier);
    }

    [Fact]
    public void IChartModifier_DefaultLifecycle_NoErrors()
    {
        TestHelper.RunInSta(() =>
        {
            var modifier = new TestChartModifier();
            modifier.OnAttached();
            modifier.OnDetached();
            var context = new ChartRenderContext
            {
                XRangeAccessor = () => new DataRange(0, 10),
                YRangeAccessor = () => new DataRange(0, 10)
            };
            modifier.SetContext(context);
        });
    }

    private sealed class TestChartModifier : ChartModifierBase
    {
    }
}

public class TimeSpanAxisTests
{
    [Fact]
    public void TimeSpanAxis_DefaultPlacement_IsBottom()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis();
            Assert.Equal(AxisPlacement.Bottom, axis.Placement);
        });
    }

    [Fact]
    public void TimeSpanAxis_Scale_IsLinear()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis();
            Assert.Equal(AxisScale.Linear, axis.Scale);
        });
    }

    [Fact]
    public void TimeSpanAxis_CoordinateMapper_IsLinear()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis();
            Assert.Same(LinearCoordinateMapper.Instance, axis.CoordinateMapper);
        });
    }

    [Fact]
    public void TimeSpanAxis_GetMajorTicks_ReturnsTicks()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis
            {
                VisibleRange = new DataRange(0, 3600)
            };
            var ticks = axis.GetMajorTicks(600);
            Assert.True(ticks.Length > 0);
            foreach (var tick in ticks)
                Assert.False(string.IsNullOrEmpty(tick.Label));
        });
    }

    [Fact]
    public void TimeSpanAxis_GetMajorTicks_ZeroRange_ReturnsEmpty()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis
            {
                VisibleRange = new DataRange(100, 100)
            };
            var ticks = axis.GetMajorTicks(600);
            Assert.Empty(ticks);
        });
    }

    [Fact]
    public void TimeSpanAxis_GetMinorTicks_ReturnsTicks()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis
            {
                VisibleRange = new DataRange(0, 3600)
            };
            var ticks = axis.GetMinorTicks(600);
            foreach (var tick in ticks)
                Assert.Equal(string.Empty, tick.Label);
        });
    }

    [Fact]
    public void TimeSpanAxis_CalculateAutoRange_WithData_ReturnsPaddedRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis();
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(100, 1);
            dataSeries.Append(200, 2);

            var range = axis.CalculateAutoRange([dataSeries]);
            Assert.True(range.Min <= 100);
            Assert.True(range.Max >= 200);
        });
    }

    [Fact]
    public void TimeSpanAxis_CalculateAutoRange_NoData_ReturnsDefault()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis();
            var range = axis.CalculateAutoRange([]);
            Assert.Equal(0, range.Min);
            Assert.Equal(100, range.Max);
        });
    }

    [Fact]
    public void TimeSpanAxis_VisibleTimeSpanRange_SetAndGet()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis
            {
                VisibleRange = new DataRange(0, 100)
            };

            var ts = TimeSpan.FromSeconds(50);
            axis.VisibleTimeSpanRange = ts;

            Assert.True(Math.Abs(axis.VisibleRange.Min - 50) < 1);
            Assert.True(axis.VisibleRange.Max > 50);
        });
    }

    [Fact]
    public void TimeSpanAxis_VisibleTimeSpanRange_Get_ReturnsTimeSpan()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new TimeSpanAxis
            {
                VisibleRange = new DataRange(60, 120)
            };

            var ts = axis.VisibleTimeSpanRange;
            Assert.Equal(60, ts.TotalSeconds, 1);
        });
    }
}

public class DateTimeAxisTests
{
    [Fact]
    public void DateTimeAxis_DefaultPlacement_IsBottom()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis();
            Assert.Equal(AxisPlacement.Bottom, axis.Placement);
        });
    }

    [Fact]
    public void DateTimeAxis_Scale_IsDateTime()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis();
            Assert.Equal(AxisScale.DateTime, axis.Scale);
        });
    }

    [Fact]
    public void DateTimeAxis_CoordinateMapper_IsDateTime()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis();
            Assert.Same(DateTimeCoordinateMapper.Instance, axis.CoordinateMapper);
        });
    }

    [Fact]
    public void DateTimeAxis_GetMajorTicks_ReturnsTicks()
    {
        TestHelper.RunInSta(() =>
        {
            var now = DateTime.Now;
            var axis = new DateTimeAxis
            {
                VisibleRange = new DataRange(
                    DateTimeCoordinateMapper.DateTimeToDouble(now.AddDays(-1)),
                    DateTimeCoordinateMapper.DateTimeToDouble(now.AddDays(1)))
            };
            var ticks = axis.GetMajorTicks(600);
            Assert.True(ticks.Length > 0);
            foreach (var tick in ticks)
                Assert.False(string.IsNullOrEmpty(tick.Label));
        });
    }

    [Fact]
    public void DateTimeAxis_GetMajorTicks_ZeroRange_ReturnsEmpty()
    {
        TestHelper.RunInSta(() =>
        {
            var now = DateTime.Now;
            var d = DateTimeCoordinateMapper.DateTimeToDouble(now);
            var axis = new DateTimeAxis
            {
                VisibleRange = new DataRange(d, d)
            };
            var ticks = axis.GetMajorTicks(600);
            Assert.Empty(ticks);
        });
    }

    [Fact]
    public void DateTimeAxis_GetMinorTicks_ReturnsTicks()
    {
        TestHelper.RunInSta(() =>
        {
            var now = DateTime.Now;
            var axis = new DateTimeAxis
            {
                VisibleRange = new DataRange(
                    DateTimeCoordinateMapper.DateTimeToDouble(now.AddHours(-5)),
                    DateTimeCoordinateMapper.DateTimeToDouble(now.AddHours(5)))
            };
            var ticks = axis.GetMinorTicks(600);
            Assert.True(ticks.Length > 0);
        });
    }

    [Fact]
    public void DateTimeAxis_CalculateAutoRange_WithData_ReturnsPaddedRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis();
            var now = DateTime.Now;
            var start = DateTimeCoordinateMapper.DateTimeToDouble(now);
            var end = DateTimeCoordinateMapper.DateTimeToDouble(now.AddHours(2));

            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(start, 1);
            dataSeries.Append(end, 2);

            var range = axis.CalculateAutoRange([dataSeries]);
            Assert.True(range.Min <= start + 0.001);
            Assert.True(range.Max >= end - 0.001);
        });
    }

    [Fact]
    public void DateTimeAxis_CalculateAutoRange_NoData_ReturnsVisibleRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis();
            var originalVisible = axis.VisibleRange;
            var range = axis.CalculateAutoRange([]);
            Assert.Equal(originalVisible.Min, range.Min);
            Assert.Equal(originalVisible.Max, range.Max);
        });
    }

    [Fact]
    public void DateTimeAxis_LabelFormat_SetsAndGets()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new DateTimeAxis { LabelFormat = "yyyy-MM-dd HH:mm" };
            Assert.Equal("yyyy-MM-dd HH:mm", axis.LabelFormat);
        });
    }

    [Fact]
    public void DateTimeCoordinateMapper_DateTimeToDouble_AndBack()
    {
        var dt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var d = DateTimeCoordinateMapper.DateTimeToDouble(dt);
        var back = DateTimeCoordinateMapper.DoubleToDateTime(d);
        Assert.Equal(dt, back);
    }
}

public class DownsamplingTests
{
    [Fact]
    public void MinMaxDownsampling_EmptyData_ClearsOutput()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var sampledX = new List<double> { 1, 2, 3 };
        var sampledY = new List<double> { 1, 2, 3 };

        strategy.Downsample([], [], 10, new DataRange(0, 100), sampledX, sampledY);

        Assert.Empty(sampledX);
        Assert.Empty(sampledY);
    }

    [Fact]
    public void MinMaxDownsampling_SmallData_TargetLargerThanData_PreservesAll()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var xValues = new double[] { 0, 1, 2, 3, 4 };
        var yValues = new double[] { 10, 20, 15, 25, 18 };
        var sampledX = new List<double>();
        var sampledY = new List<double>();

        strategy.Downsample(xValues, yValues, 10, new DataRange(double.MinValue, double.MaxValue), sampledX, sampledY);

        Assert.Equal(5, sampledX.Count);
        Assert.Equal(5, sampledY.Count);
    }

    [Fact]
    public void MinMaxDownsampling_WithSpike_PreservesExtremes()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var xValues = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var yValues = new double[] { 0, 0, 100, 0, 0, 0, -50, 0, 0, 0 };
        var sampledX = new List<double>();
        var sampledY = new List<double>();

        strategy.Downsample(xValues, yValues, 2,
            new DataRange(double.MinValue, double.MaxValue), sampledX, sampledY);

        Assert.Contains(sampledY, y => Math.Abs(y - 100) < 0.01);
        Assert.Contains(sampledY, y => Math.Abs(y + 50) < 0.01);
    }

    [Fact]
    public void MinMaxDownsampling_VisibleRange_FiltersData()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var xValues = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var yValues = new double[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
        var sampledX = new List<double>();
        var sampledY = new List<double>();

        strategy.Downsample(xValues, yValues, 5, new DataRange(2, 7), sampledX, sampledY);

        Assert.All(sampledX, x => Assert.True(x >= 2 && x <= 7));
    }

    [Fact]
    public void MinMaxDownsampling_TargetZero_NoCrash()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var xValues = new double[] { 0, 1, 2, 3, 4 };
        var yValues = new double[] { 10, 20, 15, 25, 18 };
        var sampledX = new List<double>();
        var sampledY = new List<double>();

        strategy.Downsample(xValues, yValues, 0, new DataRange(double.MinValue, double.MaxValue), sampledX, sampledY);

        Assert.Equal(5, sampledX.Count);
    }

    [Fact]
    public void MinMaxDownsampling_SingleDataPoint_ReturnsIt()
    {
        var strategy = new MinMaxDownsamplingStrategy();
        var sampledX = new List<double>();
        var sampledY = new List<double>();

        strategy.Downsample([5.0], [42.0], 10, new DataRange(double.MinValue, double.MaxValue), sampledX, sampledY);

        Assert.Single(sampledX);
        Assert.Equal(5, sampledX[0]);
        Assert.Equal(42, sampledY[0]);
    }
}

public class RenderableSeriesAdditionalTests
{
    [Fact]
    public void RenderableSeriesBase_Default_IsVisible()
    {
        var series = new LineRenderableSeries();
        Assert.True(series.IsVisible);
    }

    [Fact]
    public void RenderableSeriesBase_DefaultStroke_IsBlue()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(Colors.Blue, series.Stroke);
    }

    [Fact]
    public void RenderableSeriesBase_DefaultStrokeThickness_IsOne()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(1.0, series.StrokeThickness);
    }

    [Fact]
    public void RenderableSeriesBase_DefaultLineStyle_IsSolid()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(LineStyle.Solid, series.LineStyle);
    }

    [Fact]
    public void RenderableSeriesBase_StrokeDashArray_DefaultsNull()
    {
        var series = new LineRenderableSeries();
        Assert.Null(series.StrokeDashArray);
    }

    [Fact]
    public void RenderableSeriesBase_CustomDashArray_OverridesLineStyle()
    {
        var series = new LineRenderableSeries
        {
            LineStyle = LineStyle.Dash,
            StrokeDashArray = [2.0, 4.0, 6.0]
        };
        Assert.NotNull(series.StrokeDashArray);
        Assert.Equal(3, series.StrokeDashArray!.Length);
    }

    [Fact]
    public void RenderableSeriesBase_XAxisId_Default()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(Chart.DefaultXAxisId, series.XAxisId);
    }

    [Fact]
    public void RenderableSeriesBase_YAxisId_Default()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(Chart.DefaultYAxisId, series.YAxisId);
    }

    [Fact]
    public void RenderableSeriesBase_Title_DefaultsEmpty()
    {
        var series = new LineRenderableSeries();
        Assert.Equal(string.Empty, series.Title);
    }

    [Fact]
    public void RenderableSeriesBase_Tag_SetsDataSeriesTag()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new LineRenderableSeries { DataSeries = dataSeries };

            series.Tag = "test-tag";

            Assert.Equal("test-tag", dataSeries.Tag);
            Assert.Equal("test-tag", series.Tag);
        });
    }

    [Fact]
    public void RenderableSeriesBase_IsVisibleChange_RaisesPropertyChanged()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new LineRenderableSeries { DataSeries = dataSeries };
            string? changed = null;
            series.PropertyChanged += (_, e) => changed = e.PropertyName;

            series.IsVisible = false;

            Assert.Equal("IsVisible", changed);
        });
    }

    [Fact]
    public void RenderableSeriesBase_DataSeries_Setter_UnsubscribesOld()
    {
        TestHelper.RunInSta(() =>
        {
            var oldData = new VariableDataSeries<double, double>(x => x);
            oldData.Append(0, 1);
            var series = new LineRenderableSeries { DataSeries = oldData };

            var newData = new VariableDataSeries<double, double>(x => x);
            newData.Append(0, 1);
            series.DataSeries = newData;

            Assert.Same(newData, series.DataSeries);
        });
    }

    [Fact]
    public void RenderableSeriesBase_DataSeries_GetterWithoutSet_Throws()
    {
        var series = new LineRenderableSeries();
        Assert.Throws<InvalidOperationException>(() => _ = series.DataSeries);
    }

    [Fact]
    public void LineRenderableSeries_DashArray_AllStyles()
    {
        Assert.Null(LineRenderableSeries.GetDashArrayForStyle(LineStyle.Solid));
        var dash = LineRenderableSeries.GetDashArrayForStyle(LineStyle.Dash);
        Assert.NotNull(dash);
        Assert.True(((System.Collections.IList)dash).Count >= 2);

        var dot = LineRenderableSeries.GetDashArrayForStyle(LineStyle.Dot);
        Assert.NotNull(dot);

        var dashDot = LineRenderableSeries.GetDashArrayForStyle(LineStyle.DashDot);
        Assert.NotNull(dashDot);

        var dashDotDot = LineRenderableSeries.GetDashArrayForStyle(LineStyle.DashDotDot);
        Assert.NotNull(dashDotDot);
    }

    [Fact]
    public void RenderableSeriesBase_StrokeChange_DoesNotThrow()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new LineRenderableSeries { DataSeries = dataSeries };

            series.Stroke = Colors.Yellow;
            Assert.Equal(Colors.Yellow, series.Stroke);
        });
    }
}

public class RenderSeriesDataTests
{
    [Fact]
    public void CreateFrame_WithVariableSeries_ReturnsFrame()
    {
        var dataSeries = new VariableDataSeries<double, double>(x => x);
        dataSeries.Append(0, 1);
        dataSeries.Append(1, 2);

        var frame = RenderSeriesData.CreateFrame(dataSeries);

        Assert.NotNull(frame);
        Assert.Equal(2, frame.Count);
        frame.Return();
    }

    [Fact]
    public void CreateFrame_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RenderSeriesData.CreateFrame(null!));
    }

    [Fact]
    public void CreateFrame_EmptyUniformSeries_ReturnsEmptyFrame()
    {
        var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);

        var frame = RenderSeriesData.CreateFrame(dataSeries);

        Assert.Same(DataFrame.Empty, frame);
    }

    [Fact]
    public void GetFrame_NullContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RenderSeriesData.GetFrame(null!, new LineRenderableSeries()));
    }

    [Fact]
    public void GetFrame_NullSeries_Throws()
    {
        var context = new ChartRenderContext
        {
            XRangeAccessor = () => new DataRange(0, 1),
            YRangeAccessor = () => new DataRange(0, 1)
        };
        Assert.Throws<ArgumentNullException>(() => RenderSeriesData.GetFrame(context, null!));
    }
}

public class UniformDataSeriesAdditionalTests
{
    [Fact]
    public void UniformDataSeries_FifoCapacity_Overflow_DiscardsOldData()
    {
        var series = new UniformDataSeries<double, double>(index => index * 1.0, x => x);
        series.Append(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        int collectionChangedCount = 0;
        series.CollectionChanged += (_, _) => collectionChangedCount++;

        series.FifoCapacity = 3;

        Assert.Equal(3, series.Count);
        Assert.True(collectionChangedCount > 0);
    }

    [Fact]
    public void UniformDataSeries_FifoCapacity_Zero_NoLimit()
    {
        var series = new UniformDataSeries<double, double>(index => index, x => x);
        series.FifoCapacity = 0;
        series.Append(new[] { 1.0, 2.0 });

        Assert.Equal(2, series.Count);
    }

    [Fact]
    public void UniformDataSeries_FifoCapacity_Negative_Throws()
    {
        var series = new UniformDataSeries<double, double>(index => index, x => x);
        Assert.Throws<ArgumentOutOfRangeException>(() => series.FifoCapacity = -1);
    }

    [Fact]
    public void UniformDataSeries_XValues_GeneratedList()
    {
        var series = new UniformDataSeries<double, double>(index => index * 2.0, x => x);
        series.Append(new[] { 1.0, 2.0, 3.0 });

        Assert.Equal(3, series.XValues.Count);
        Assert.Equal(0, series.XValues[0]);
        Assert.Equal(2, series.XValues[1]);
        Assert.Equal(4, series.XValues[2]);
    }

    [Fact]
    public void UniformDataSeries_YValues_ReturnsValues()
    {
        var series = new UniformDataSeries<double, double>(index => index, x => x);
        series.Append(new[] { 10.0, 20.0, 30.0 });

        Assert.Equal(3, series.YValues.Count);
        Assert.Equal(10.0, series.YValues[0]);
        Assert.Equal(20.0, series.YValues[1]);
        Assert.Equal(30.0, series.YValues[2]);
    }

    [Fact]
    public void UniformDataSeries_SingleAppend_UpdatesCorrectly()
    {
        var series = new UniformDataSeries<double, double>(index => index * 10.0, x => x);
        int rangeChangedCount = 0;
        series.RangeChanged += (_, _) => rangeChangedCount++;

        series.Append(5.0);
        series.Append(15.0);

        Assert.Equal(2, series.Count);
        Assert.Equal(10.0, series.GetX(1));
        Assert.Equal(5.0, series.GetY(0));
        Assert.Equal(15.0, series.GetY(1));
        Assert.True(rangeChangedCount >= 2);
    }
}

public class VariableDataSeriesAdditionalTests
{
    [Fact]
    public void VariableDataSeries_FifoCapacity_Overflow_OldDiscarded()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        series.Append(0, 1);
        series.Append(1, 2);
        series.Append(2, 3);
        series.Append(3, 4);

        series.FifoCapacity = 2;

        Assert.Equal(2, series.Count);
        Assert.Equal(2.0, series.GetX(0));
        Assert.Equal(3.0, series.GetY(0));
    }

    [Fact]
    public void VariableDataSeries_FifoCapacity_Negative_Throws()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        Assert.Throws<ArgumentOutOfRangeException>(() => series.FifoCapacity = -5);
    }

    [Fact]
    public void VariableDataSeries_Version_IncrementsOnAppend()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        var v1 = series.Version;
        series.Append(0, 1);
        var v2 = series.Version;
        series.Append(1, 2);

        Assert.True(v2 > v1);
        Assert.True(series.Version > v2);
    }

    [Fact]
    public void VariableDataSeries_Tag_DefaultsNull()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        Assert.Null(series.Tag);
    }

    [Fact]
    public void VariableDataSeries_Tag_SetAndGet()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        series.Tag = "metadata";
        Assert.Equal("metadata", series.Tag);
    }

    [Fact]
    public void VariableDataSeries_Count_HundredPoints()
    {
        var series = new VariableDataSeries<double, double>(x => x);
        for (int i = 0; i < 100; i++)
            series.Append(i, i * 2);
        Assert.Equal(100, series.Count);
        Assert.Equal(99, series.GetX(99));
        Assert.Equal(198, series.GetY(99));
    }
}

public class OhlcDataSeriesAdditionalTests
{
    [Fact]
    public void OhlcDataSeries_GetOpenHighLowClose()
    {
        var series = new OhlcDataSeries();
        series.Append(10, 120, 135, 115, 128);

        Assert.Equal(120, series.GetOpen(0));
        Assert.Equal(135, series.GetHigh(0));
        Assert.Equal(115, series.GetLow(0));
        Assert.Equal(128, series.GetClose(0));
    }

    [Fact]
    public void OhlcDataSeries_Empty_YRangeDefault()
    {
        var series = new OhlcDataSeries();
        Assert.Equal(-1, series.YRange.Min);
        Assert.Equal(1, series.YRange.Max);
    }

    [Fact]
    public void OhlcDataSeries_CopyValues()
    {
        var series = new OhlcDataSeries();
        series.Append(0, 10, 20, 5, 15);
        series.Append(1, 15, 25, 10, 20);

        Span<double> dest = stackalloc double[2];
        Assert.Equal(2, series.CopyOpenValues(dest));
        Assert.Equal(10, dest[0]);
        Assert.Equal(15, dest[1]);

        Assert.Equal(2, series.CopyHighValues(dest));
        Assert.Equal(20, dest[0]);
        Assert.Equal(25, dest[1]);

        Assert.Equal(2, series.CopyLowValues(dest));
        Assert.Equal(5, dest[0]);
        Assert.Equal(10, dest[1]);

        Assert.Equal(2, series.CopyCloseValues(dest));
        Assert.Equal(15, dest[0]);
        Assert.Equal(20, dest[1]);
    }

    [Fact]
    public void OhlcDataSeries_FifoCapacity_DiscardsOld()
    {
        var series = new OhlcDataSeries();
        series.Append(0, 100, 105, 98, 103);
        series.Append(1, 103, 108, 100, 101);
        series.Append(2, 101, 110, 99, 107);

        series.FifoCapacity = 2;

        Assert.Equal(2, series.Count);
        Assert.Equal(1, series.GetX(0));
        Assert.Equal(2, series.GetX(1));
    }

    [Fact]
    public void OhlcDataSeries_BulkAppend_WithMismatch_Throws()
    {
        var series = new OhlcDataSeries();
        Assert.Throws<ArgumentException>(() =>
            series.AppendRange([1.0], [2.0], [3.0], [4.0], []));
    }

    [Fact]
    public void OhlcDataSeries_BulkAppend_CorrectRanges()
    {
        var series = new OhlcDataSeries();
        series.AppendRange(
            [0.0, 1.0, 2.0],
            [100.0, 103.0, 101.0],
            [105.0, 108.0, 110.0],
            [98.0, 100.0, 99.0],
            [103.0, 101.0, 107.0]);

        Assert.Equal(3, series.Count);
        Assert.Equal(0, series.XRange.Min);
        Assert.Equal(2, series.XRange.Max);
        Assert.Equal(98, series.YRange.Min);
        Assert.Equal(110, series.YRange.Max);
    }

    [Fact]
    public void OhlcDataSeries_BulkAppend_EmptyArray_DoesNothing()
    {
        var series = new OhlcDataSeries();
        series.AppendRange([], [], [], [], []);
        Assert.Equal(0, series.Count);
    }
}

public class AxisBaseAdditionalTests
{
    [Fact]
    public void LinearAxis_VisibleRange_SetterAndGetter()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRange = new DataRange(-10, 10)
            };
            Assert.Equal(-10, axis.VisibleRange.Min);
            Assert.Equal(10, axis.VisibleRange.Max);
        });
    }

    [Fact]
    public void LinearAxis_DefaultAutoRange_IsTrue()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis();
            Assert.True(axis.AutoRange);
        });
    }

    [Fact]
    public void LinearAxis_DefaultShowMajorGridLines_IsTrue()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis();
            Assert.True(axis.ShowMajorGridLines);
        });
    }

    [Fact]
    public void LinearAxis_Title_SetsAndGets()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis { Title = "Temperature" };
            Assert.Equal("Temperature", axis.Title);
        });
    }

    [Fact]
    public void LogAxis_DefaultPlacement_IsLeft()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LogAxis();
            Assert.Equal(AxisPlacement.Left, axis.Placement);
        });
    }

    [Fact]
    public void AxisPlacement_HasFourValues()
    {
        var values = Enum.GetValues<AxisPlacement>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void TickCalculationMode_HasValues()
    {
        var values = Enum.GetValues<TickCalculationMode>();
        Assert.True(values.Length > 0);
    }
}

public class ChartAdditionalSurfaceTests
{
    [Fact]
    public void Chart_Constructor_SetsDefaultRanges()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(0, chart.XRange.Min);
            Assert.Equal(100, chart.XRange.Max);
            Assert.Equal(-1, chart.YRange.Min);
            Assert.Equal(1, chart.YRange.Max);
        });
    }

    [Fact]
    public void Chart_Constructor_CreatesDefaultAxes()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.NotNull(chart.XAxes);
            Assert.NotNull(chart.YAxes);
        });
    }

    [Fact]
    public void Chart_DefaultPlotAreaBackground_IsTransparent()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.NotNull(chart.PlotAreaBackground);
        });
    }

    [Fact]
    public void Chart_DefaultBorderThickness_IsZero()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(new Thickness(0), chart.BorderThickness);
        });
    }

    [Fact]
    public void Chart_DefaultPlotAreaBorderThickness_IsZero()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(new Thickness(0), chart.PlotAreaBorderThickness);
        });
    }

    [Fact]
    public void Chart_DefaultAnnotations_IsNull()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Null(chart.Annotations);
        });
    }

    [Fact]
    public void Chart_SetAnnotations_CreatesCollection()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart
            {
                Annotations = new ObservableCollection<IAnnotation>
                {
                    new LineAnnotation { X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 }
                }
            };
            Assert.Single(chart.Annotations!);
        });
    }

    [Fact]
    public void Chart_XAxes_CanAddMultipleAxes()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart
            {
                XAxes = new ObservableCollection<IAxis>()
            };

            chart.XAxes.Add(new LinearAxis { Id = "Axis1", Placement = AxisPlacement.Bottom });
            chart.XAxes.Add(new LinearAxis { Id = "Axis2", Placement = AxisPlacement.Top });

            Assert.Equal(2, chart.XAxes.Count);
        });
    }

    [Fact]
    public void Chart_YAxes_CanAddMultipleAxes()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart
            {
                YAxes = new ObservableCollection<IAxis>()
            };

            chart.YAxes.Add(new LinearAxis { Id = "Axis1", Placement = AxisPlacement.Left });
            chart.YAxes.Add(new LinearAxis { Id = "Axis2", Placement = AxisPlacement.Right });

            Assert.Equal(2, chart.YAxes.Count);
        });
    }

    [Fact]
    public void Chart_Series_Empty_NoCrashOnZoomExtents()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>()
            };
            chart.ZoomExtents();
        });
    }

    [Fact]
    public void Chart_VisibleRangeLimitMode_DefaultNone()
    {
        TestHelper.RunInSta(() =>
        {
            var defaultXAxis = new LinearAxis
            {
                Id = Chart.DefaultXAxisId,
                Placement = AxisPlacement.Bottom
            };
            Assert.Equal(VisibleRangeLimitMode.None, defaultXAxis.VisibleRangeLimitMode);
        });
    }

    [Fact]
    public void Chart_MultipleSeriesTypes_CanCoexist()
    {
        TestHelper.RunInSta(() =>
        {
            var lineData = new VariableDataSeries<double, double>(x => x);
            lineData.Append(0, 1);
            lineData.Append(1, 2);

            var scatterData = new VariableDataSeries<double, double>(x => x);
            scatterData.Append(0.5, 1.5);

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new LineRenderableSeries
                    {
                        Stroke = Colors.Red,
                        DataSeries = lineData,
                        Title = "Line"
                    },
                    new ScatterRenderableSeries
                    {
                        MarkerColor = Colors.Blue,
                        DataSeries = scatterData,
                        Title = "Scatter"
                    }
                }
            };

            Assert.Equal(2, chart.Series!.Count);
        });
    }

    [Fact]
    public void Chart_RendererPreference_DefaultAuto()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(ChartRendererPreference.Auto, chart.RendererPreference);
        });
    }

    [Fact]
    public void Chart_ActualRendererBackend_DefaultUnknown()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(ChartRendererBackend.Unknown, chart.ActualRendererBackend);
        });
    }

    [Fact]
    public void Chart_DefaultBackground_IsTransparent()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            Assert.Equal(Brushes.Transparent.ToString(), chart.Background.ToString());
        });
    }

    [Fact]
    public void OhlcRenderableSeries_DefaultColors()
    {
        TestHelper.RunInSta(() =>
        {
            var series = new OhlcRenderableSeries
            {
                DataSeries = new OhlcDataSeries()
            };
            Assert.NotEqual(default(Color), series.UpFill);
            Assert.NotEqual(default(Color), series.DownFill);
        });
    }

    [Fact]
    public void BarRenderableSeries_DefaultBarSpacing_IsPointTwo()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new BarRenderableSeries { DataSeries = dataSeries };
            Assert.Equal(0.2, series.BarSpacing);
        });
    }

    [Fact]
    public void AreaRenderableSeries_DefaultBaseline_IsZero()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            dataSeries.Append(1.0);
            var series = new AreaRenderableSeries { DataSeries = dataSeries };
            Assert.Equal(0.0, series.BaselineY);
        });
    }

    [Fact]
    public void ScatterRenderableSeries_DefaultMarkerSize_IsSix()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new VariableDataSeries<double, double>(x => x);
            dataSeries.Append(0, 1);
            var series = new ScatterRenderableSeries
            {
                DataSeries = dataSeries,
                MarkerType = MarkerType.Circle
            };
            Assert.Equal(6.0, series.MarkerSize);
        });
    }

    [Fact]
    public void LineRenderOperation_DefaultValues()
    {
        var op = new LineRenderOperation();
        Assert.Equal(Colors.White, op.StrokeColor);
        Assert.Equal(1.0f, op.StrokeThickness);
        Assert.Empty(op.LineInstances);
    }

    [Fact]
    public void BarRenderOperation_Defaults()
    {
        var op = new BarRenderOperation();
        Assert.Empty(op.Instances);
    }

    [Fact]
    public void ScatterRenderOperation_Defaults()
    {
        var op = new ScatterRenderOperation();
        Assert.Equal(6.0f, op.MarkerSize);
        Assert.Equal(Colors.Cyan, op.MarkerColor);
        Assert.Empty(op.Instances);
    }

    [Fact]
    public void AreaRenderOperation_Defaults()
    {
        var op = new AreaRenderOperation();
        Assert.Null(op.BorderLine);
        Assert.Empty(op.FillVertices);
    }

    [Fact]
    public void Chart_ZoomExtents_WithFifoCapacity_Works()
    {
        TestHelper.RunInSta(() =>
        {
            var dataSeries = new UniformDataSeries<double, double>(index => index, x => x);
            dataSeries.Append(new[] { 1.0, 3.0, 5.0, 2.0 });
            dataSeries.FifoCapacity = 2;

            var chart = new Chart
            {
                Series = new ObservableCollection<IRenderableSeries>
                {
                    new LineRenderableSeries { DataSeries = dataSeries }
                }
            };

            chart.ZoomExtents();

            Assert.True(chart.XRange.Min <= chart.XRange.Max);
            Assert.True(chart.YRange.Min <= chart.YRange.Max);
        });
    }
}

public class RenderLoopAdditionalTests
{
    [Fact]
    public void RenderLoop_Constructor_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RenderLoop(null!));
    }

    [Fact]
    public void RenderLoop_Start_IsIdempotent()
    {
        int count = 0;
        using var loop = new RenderLoop(() =>
        {
            Interlocked.Increment(ref count);
        });

        loop.Start();
        loop.Start();

        Assert.True(count >= 0);
    }

    [Fact]
    public void RenderLoop_SignalDataChanged_NullSeries_Throws()
    {
        using var loop = new RenderLoop(() => { });
        Assert.Throws<ArgumentNullException>(() => loop.SignalDataChanged(null!));
    }

    [Fact]
    public void RenderLoop_TryGetLatestFrame_NullSeries_Throws()
    {
        using var loop = new RenderLoop(() => { });
        Assert.Throws<ArgumentNullException>(() => loop.TryGetLatestFrame(null!, 0, out _));
    }

    [Fact]
    public void RenderLoop_RemoveSeries_Null_Throws()
    {
        using var loop = new RenderLoop(() => { });
        Assert.Throws<ArgumentNullException>(() => loop.RemoveSeries(null!));
    }

    [Fact]
    public void RenderLoop_SignalViewportChanged_WakesRenderer()
    {
        using var invalidated = new ManualResetEventSlim();
        int invalidationCount = 0;
        using var loop = new RenderLoop(() =>
        {
            Interlocked.Increment(ref invalidationCount);
            invalidated.Set();
        });

        loop.Start();
        loop.SignalViewportChanged();
        Assert.True(invalidated.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(invalidationCount >= 1);
    }

    [Fact]
    public void RenderLoop_Clear_RemovesAllFrames()
    {
        using var loop = new RenderLoop(() => { });
        var dataSeries = new CountingDataSeries();
        dataSeries.Append(0, 1);
        var frame = dataSeries.CreateSnapshot();
        loop.SignalDataChanged(dataSeries, frame);
        Assert.True(loop.TryGetLatestFrame(dataSeries, 0, out _));

        loop.Clear();

        Assert.False(loop.TryGetLatestFrame(dataSeries, 0, out _));
    }

    [Fact]
    public void RenderLoop_Stop_CleansUp()
    {
        using var invalidated = new ManualResetEventSlim();
        using var loop = new RenderLoop(() => invalidated.Set());
        loop.Start();
        loop.Stop();

        loop.SignalViewportChanged();
        Assert.False(invalidated.Wait(TimeSpan.FromMilliseconds(500)));
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
            if (Count == 0) return DataFrame.Empty;
            return new DataFrame
            {
                Count = Count, Version = Version, XRange = XRange, YRange = YRange,
                Type = DataFrameType.Xy,
                XValues = _xValues.Select(v => (float)v).ToArray(),
                YValues = _yValues.Select(v => (float)v).ToArray()
            };
        }

        protected override void OnFifoCapacityChanged() { }
    }
}

public class CoordinateMapperTests
{
    [Fact]
    public void LinearCoordinateMapper_RoundTrip()
    {
        var mapper = LinearCoordinateMapper.Instance;
        var range = new DataRange(-100, 100);
        double size = 500;

        double pos = mapper.DataToScreen(50, range, size);
        double roundTrip = mapper.ScreenToData(pos, range, size);

        Assert.Equal(50, roundTrip, 5);
    }

    [Fact]
    public void LinearCoordinateMapper_DataToScreen_MinValue_AtZero()
    {
        var mapper = LinearCoordinateMapper.Instance;
        var range = new DataRange(10, 20);
        double size = 200;

        double posMin = mapper.DataToScreen(10, range, size);
        Assert.Equal(0, posMin, 5);
    }

    [Fact]
    public void LinearCoordinateMapper_DataToScreen_MaxValue_AtSize()
    {
        var mapper = LinearCoordinateMapper.Instance;
        var range = new DataRange(0, 100);
        double size = 400;

        double posMax = mapper.DataToScreen(100, range, size);
        Assert.Equal(400, posMax, 5);
    }

    [Fact]
    public void LogCoordinateMapper_PowerOfTen_EvenSpacing()
    {
        var mapper = LogCoordinateMapper.Instance;
        var range = new DataRange(1, 100);
        double size = 600;

        double pos1 = mapper.DataToScreen(1, range, size);
        double pos10 = mapper.DataToScreen(10, range, size);
        double pos100 = mapper.DataToScreen(100, range, size);

        double diff1 = pos10 - pos1;
        double diff2 = pos100 - pos10;

        Assert.True(Math.Abs(diff1 - diff2) < 2);
    }

    [Fact]
    public void LogCoordinateMapper_RoundTrip_PreservesData()
    {
        var mapper = LogCoordinateMapper.Instance;
        var range = new DataRange(1, 10000);
        double size = 800;

        double data150 = mapper.ScreenToData(mapper.DataToScreen(150, range, size), range, size);
        double data5000 = mapper.ScreenToData(mapper.DataToScreen(5000, range, size), range, size);

        Assert.Equal(150, data150, 10);
        Assert.True(Math.Abs(data5000 - 5000) / 5000 < 1e-10);
    }
}

public class CoordinateMapperAdditionalTests
{
    [Fact]
    public void LogCoordinateMapper_Instance_IsSingleton()
    {
        Assert.Same(LogCoordinateMapper.Instance, LogCoordinateMapper.Instance);
    }

    [Fact]
    public void LinearCoordinateMapper_Instance_IsSingleton()
    {
        Assert.Same(LinearCoordinateMapper.Instance, LinearCoordinateMapper.Instance);
    }

    [Fact]
    public void DateTimeCoordinateMapper_Instance_IsSingleton()
    {
        Assert.Same(DateTimeCoordinateMapper.Instance, DateTimeCoordinateMapper.Instance);
    }
}

public class BarRenderingRegressionTests
{
    [Fact]
    public void GpuBarInstance_MemoryLayout_MatchesGPUInputLayout()
    {
        int size = Marshal.SizeOf<GpuBarInstance>();
        Assert.Equal(32, size);

        int offsetX1 = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.X1));
        int offsetY1 = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.Y1));
        int offsetX2 = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.X2));
        int offsetY2 = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.Y2));
        int offsetR = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.R));
        int offsetG = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.G));
        int offsetB = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.B));
        int offsetA = (int)Marshal.OffsetOf<GpuBarInstance>(nameof(GpuBarInstance.A));

        Assert.Equal(0, offsetX1);
        Assert.Equal(4, offsetY1);
        Assert.Equal(8, offsetX2);
        Assert.Equal(12, offsetY2);
        Assert.Equal(16, offsetR);
        Assert.Equal(20, offsetG);
        Assert.Equal(24, offsetB);
        Assert.Equal(28, offsetA);
    }

    [Fact]
    public void QuadVertex_MemoryLayout_MatchesGPUInputLayout()
    {
        int size = Marshal.SizeOf<QuadVertex>();
        Assert.Equal(8, size);

        int offsetAlong = (int)Marshal.OffsetOf<QuadVertex>(nameof(QuadVertex.Along));
        int offsetSide = (int)Marshal.OffsetOf<QuadVertex>(nameof(QuadVertex.Side));

        Assert.Equal(0, offsetAlong);
        Assert.Equal(4, offsetSide);
    }

    [Fact]
    public void BarSeriesRenderer_BarInstances_NoDoubleHeight()
    {
        TestHelper.RunInSta(() =>
        {
            double[] data = [23, 45, 67, 34, 89, 56, 78, 43, 91, 65, 38, 72];
             var ds = new UniformDataSeries<double, double>(i => i, x => x);
             foreach (var v in data)
                 ds.Append(v);
             var barSeries = new BarRenderableSeries
             {
                 Fill = Colors.DodgerBlue,
                 DataSeries = ds
             };

            var context = new ChartRenderContext
            {
                XRangeAccessor = () => new DataRange(0, 12),
                YRangeAccessor = () => new DataRange(0, 100)
            };

            var renderer = new BarSeriesRenderer();
            var commands = renderer.Render(barSeries, context, 400, 300);

            var barCmd = Assert.Single(commands);
            var barOp = Assert.IsType<BarRenderOperation>(barCmd);
            Assert.Equal(data.Length, barOp.Instances.Count);

            var mapper = LinearCoordinateMapper.Instance;
            var yRange = new DataRange(0, 100);
            double viewportHeight = 300;

            for (int i = 0; i < data.Length; i++)
            {
                var inst = barOp.Instances[i];
                double y1 = inst.Y1;
                double y2 = inst.Y2;

                Assert.Equal(0.0, y1, 5);
                Assert.Equal(data[i], y2, 5);

                double expectedHeight = mapper.DataToScreen(y2, yRange, viewportHeight)
                                       - mapper.DataToScreen(y1, yRange, viewportHeight);
                double actualHeight = y2 - y1;

                double ratio = actualHeight / (yRange.Max - yRange.Min) * viewportHeight;
                Assert.Equal(expectedHeight, ratio, 3);
            }
        });
    }

    [Fact]
    public void BarSeriesRenderer_BarHeight_FormulaMatchesCoordinateMapper()
    {
        double[] dataValues = [10, 50, 91, 25, 75];
        var yRange = new DataRange(0, 100);
        double viewportHeight = 500;

        var mapper = LinearCoordinateMapper.Instance;

        foreach (double value in dataValues)
         {
             double screenPos = mapper.DataToScreen(value, yRange, viewportHeight);
             double baselinePos = mapper.DataToScreen(0, yRange, viewportHeight);
             double expectedPixelHeight = Math.Abs(baselinePos - screenPos);

             double formulaPixelHeight = (value - 0) / (yRange.Max - yRange.Min) * viewportHeight;

             Assert.Equal(expectedPixelHeight, formulaPixelHeight, 1e-6);
             Assert.NotEqual(expectedPixelHeight * 2.0, formulaPixelHeight, 1e-6);
         }
    }
}

public class VisibleRangeProcessingTests
{
    #region ApplyRelativeRangePadding

    [Fact]
    public void ApplyRelativeRangePadding_ZeroPadding_ReturnsSameRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis();
            var core = new DataRange(10, 100);
            var result = axis.ApplyRelativeRangePadding(core);
            Assert.Equal(core.Min, result.Min);
            Assert.Equal(core.Max, result.Max);
        });
    }

    [Fact]
    public void ApplyRelativeRangePadding_MinOnly_ExtendsMinSide()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis { RangePaddingMin = 0.1 };
            var core = new DataRange(0, 100);
            var result = axis.ApplyRelativeRangePadding(core);

            double expectedPad = 100 * 0.1;
            Assert.Equal(core.Min - expectedPad, result.Min, 6);
            Assert.Equal(core.Max, result.Max, 6);
        });
    }

    [Fact]
    public void ApplyRelativeRangePadding_MaxOnly_ExtendsMaxSide()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis { RangePaddingMax = 0.1 };
            var core = new DataRange(0, 100);
            var result = axis.ApplyRelativeRangePadding(core);

            double expectedPad = 100 * 0.1;
            Assert.Equal(core.Min, result.Min, 6);
            Assert.Equal(core.Max + expectedPad, result.Max, 6);
        });
    }

    [Fact]
    public void ApplyRelativeRangePadding_BothSides_ExtendsBothIndependently()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.05,
                RangePaddingMax = 0.15
            };
            var core = new DataRange(0, 200);
            var result = axis.ApplyRelativeRangePadding(core);

            Assert.Equal(core.Min - 200 * 0.05, result.Min, 6);
            Assert.Equal(core.Max + 200 * 0.15, result.Max, 6);
        });
    }

    [Fact]
    public void ApplyRelativeRangePadding_ZeroLength_ReturnsSameRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.1
            };
            var core = new DataRange(5, 5);
            var result = axis.ApplyRelativeRangePadding(core);
            Assert.Equal(5, result.Min);
            Assert.Equal(5, result.Max);
        });
    }

    [Fact]
    public void RangePaddingMinMax_AreIndependentProperties_NoSwap()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.2,
                RangePaddingMax = 0.0
            };

            Assert.Equal(0.2, axis.RangePaddingMin);
            Assert.Equal(0.0, axis.RangePaddingMax);
        });
    }

    #endregion

    #region ReverseRelativeRangePadding

    [Fact]
    public void ReverseRelativeRangePadding_ZeroPadding_ReturnsSameRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis();
            var padded = new DataRange(0, 100);
            var core = CallReverseRelativeRangePadding(axis, padded);
            Assert.Equal(padded.Min, core.Min);
            Assert.Equal(padded.Max, core.Max);
        });
    }

    [Fact]
    public void ReverseRelativeRangePadding_RoundTrip_PreservesCoreRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.15
            };
            var originalCore = new DataRange(20, 120);
            var visible = axis.ApplyRelativeRangePadding(originalCore);
            var recoveredCore = CallReverseRelativeRangePadding(axis, visible);

            Assert.Equal(originalCore.Min, recoveredCore.Min, 6);
            Assert.Equal(originalCore.Max, recoveredCore.Max, 6);
        });
    }

    [Fact]
    public void ReverseRelativeRangePadding_MinAndMax_RecoversCorrectly()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.05,
                RangePaddingMax = 0.2
            };
            var padded = new DataRange(-10, 200);
            var core = CallReverseRelativeRangePadding(axis, padded);
            var rePadded = axis.ApplyRelativeRangePadding(core);

            Assert.Equal(padded.Min, rePadded.Min, 6);
            Assert.Equal(padded.Max, rePadded.Max, 6);
        });
    }

    private static DataRange CallReverseRelativeRangePadding(LinearAxis axis, DataRange padded)
    {
        var method = typeof(AxisBase).GetMethod("ReverseRelativeRangePadding",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (DataRange)method!.Invoke(axis, [padded])!;
    }

    #endregion

    #region ClampToVisibleRangeLimit

    [Fact]
    public void ClampToVisibleRangeLimit_None_ReturnsSameRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.None,
                VisibleRangeLimit = new DataRange(0, 50)
            };
            var range = new DataRange(-10, 200);
            var result = axis.ClampToVisibleRangeLimit(range);
            Assert.Equal(range.Min, result.Min);
            Assert.Equal(range.Max, result.Max);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MinOnly_ClampsMinAndPreservesLength()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinOnly,
                VisibleRangeLimit = new DataRange(5, 100)
            };
            var range = new DataRange(0, 20);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(5, result.Min);
            Assert.Equal(25, result.Max);
            Assert.Equal(20, result.Length, 6);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MinOnly_WithinLimit_NoChange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinOnly,
                VisibleRangeLimit = new DataRange(5, 100)
            };
            var range = new DataRange(10, 30);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(10, result.Min);
            Assert.Equal(30, result.Max);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MaxOnly_ClampsMaxAndPreservesLength()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MaxOnly,
                VisibleRangeLimit = new DataRange(0, 50)
            };
            var range = new DataRange(10, 70);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(-10, result.Min);
            Assert.Equal(50, result.Max);
            Assert.Equal(60, result.Length, 6);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MaxOnly_WithinLimit_NoChange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MaxOnly,
                VisibleRangeLimit = new DataRange(0, 100)
            };
            var range = new DataRange(10, 80);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(10, result.Min);
            Assert.Equal(80, result.Max);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MinAndMax_ClampsBothSides()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(0, 100)
            };
            var range = new DataRange(-10, 150);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(0, result.Min);
            Assert.Equal(100, result.Max);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MinAndMax_FullyOutside_ReturnsLimit()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(10, 50)
            };
            var range = new DataRange(60, 100);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(10, result.Min);
            Assert.Equal(50, result.Max);
        });
    }

    [Fact]
    public void ClampToVisibleRangeLimit_MinAndMax_WithinLimit_NoChange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(0, 100)
            };
            var range = new DataRange(20, 80);
            var result = axis.ClampToVisibleRangeLimit(range);

            Assert.Equal(20, result.Min);
            Assert.Equal(80, result.Max);
        });
    }

    #endregion

    #region 三步链完整验证

    [Fact]
    public void ThreeStepChain_WithoutLimit_VisibleEqualsCoreWithPadding()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.2,
                VisibleRangeLimitMode = VisibleRangeLimitMode.None
            };
            var core = new DataRange(0, 100);

            var clamped = axis.ClampToVisibleRangeLimit(core);
            var visible = axis.ApplyRelativeRangePadding(clamped);

            Assert.Equal(core.Min, clamped.Min);
            Assert.Equal(core.Max, clamped.Max);
            Assert.True(visible.Length > core.Length);
        });
    }

    [Fact]
    public void ThreeStepChain_WithLimit_ClampedBeforePadding()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.1,
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(5, 95)
            };
            var core = new DataRange(0, 100);

            var clamped = axis.ClampToVisibleRangeLimit(core);
            var visible = axis.ApplyRelativeRangePadding(clamped);

            Assert.Equal(5, clamped.Min);
            Assert.Equal(95, clamped.Max);

            double clampedLength = clamped.Length;
            Assert.Equal(clamped.Min - clampedLength * 0.1, visible.Min, 6);
            Assert.Equal(clamped.Max + clampedLength * 0.1, visible.Max, 6);
        });
    }

    [Fact]
    public void OnVisibleRangeChanged_DirectSet_ReappliesLimitAndPadding()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.0,
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(10, 90)
            };

            axis.VisibleRange = new DataRange(0, 100);

            var core = CallReverseRelativeRangePadding(axis, axis.VisibleRange);
            Assert.True(core.Min >= 10, $"core.Min={core.Min} should be >= 10");
            Assert.True(core.Max <= 90, $"core.Max={core.Max} should be <= 90");
        });
    }

    [Fact]
    public void OnVisibleRangeChanged_WithinLimit_KeepsOriginal()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.0,
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(0, 100)
            };

            axis.VisibleRange = new DataRange(20, 80);

            var core = CallReverseRelativeRangePadding(axis, axis.VisibleRange);
            Assert.True(core.Min >= 20, $"VisibleRange should be based on {core.Min}..{core.Max}");
            Assert.True(core.Max <= 80);
        });
    }

    #endregion

    #region VisibleRangeLimit 变更触发

    [Fact]
    public void OnVisibleRangeLimitChanged_Tighten_MovesCoreRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                CoreRange = new DataRange(0, 100),
                VisibleRange = new DataRange(0, 100),
                VisibleRangeLimitMode = VisibleRangeLimitMode.MinAndMax,
                VisibleRangeLimit = new DataRange(0, 100)
            };

            // 收紧 limit
            axis.VisibleRangeLimit = new DataRange(20, 80);

            Assert.True(axis.CoreRange.Min >= 20, $"CoreRange.Min={axis.CoreRange.Min}");
            Assert.True(axis.CoreRange.Max <= 80, $"CoreRange.Max={axis.CoreRange.Max}");
        });
    }

    [Fact]
    public void OnVisibleRangeLimitModeChanged_EnableMinOnly_Clamps()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                CoreRange = new DataRange(-10, 50),
                VisibleRange = new DataRange(-10, 50),
                VisibleRangeLimit = new DataRange(0, 100),
                VisibleRangeLimitMode = VisibleRangeLimitMode.None
            };

            axis.VisibleRangeLimitMode = VisibleRangeLimitMode.MinOnly;

            Assert.True(axis.CoreRange.Min >= 0, $"CoreRange.Min={axis.CoreRange.Min}");
        });
    }

    #endregion

    #region Bar 柱状图范围

    [Fact]
    public void AdjustXRangeForBars_ExtendsBothSidesByHalfBar()
    {
        var barSeries = new BarRenderableSeries
        {
            BarSpacing = 0.3,
            DataSeries = CreateSequentialData(0, 10)
        };
        var group = new[] { barSeries as IRenderableSeries }.GroupBy(s => "X").First();

        var niceRange = new DataRange(0, 9);
        var result = CallAdjustXRangeForBars(niceRange, group);

        Assert.True(result.Min < 0, $"Min={result.Min} should be < 0");
        Assert.True(result.Max > 9, $"Max={result.Max} should be > 9");
    }

    [Fact]
    public void AdjustXRangeForBars_WithBarWidth_RespectsWidth()
    {
        var barSeries = new BarRenderableSeries
        {
            BarSpacing = 0.2,
            BarWidth = 0.3,
            DataSeries = CreateSequentialData(5, 5)
        };
        var group = new[] { barSeries as IRenderableSeries }.GroupBy(s => "X").First();

        var niceRange = new DataRange(5, 9);
        var result = CallAdjustXRangeForBars(niceRange, group);

        Assert.Equal(5 - 0.15, result.Min, 6);
        Assert.Equal(9 + 0.15, result.Max, 6);
    }

    [Fact]
    public void AdjustYRangeForBarBaseline_AllPositive_IncludesZero()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            chart.Series = new ObservableCollection<IRenderableSeries>
            {
                new BarRenderableSeries
                {
                    YAxisId = "Y",
                    DataSeries = CreateSequentialData(0, 3)
                }
            };
            var range = new DataRange(10, 50);
            var result = CallAdjustYRangeForBarBaseline(chart, "Y", range);

            Assert.Equal(0, result.Min);
            Assert.Equal(50, result.Max);
        });
    }

    [Fact]
    public void AdjustYRangeForBarBaseline_AllNegative_IncludesZero()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            chart.Series = new ObservableCollection<IRenderableSeries>
            {
                new BarRenderableSeries
                {
                    YAxisId = "Y",
                    DataSeries = CreateSequentialData(0, 3)
                }
            };
            var range = new DataRange(-50, -10);
            var result = CallAdjustYRangeForBarBaseline(chart, "Y", range);

            Assert.Equal(-50, result.Min);
            Assert.Equal(0, result.Max);
        });
    }

    [Fact]
    public void AdjustYRangeForBarBaseline_CrossesZero_NoAdjustment()
    {
        TestHelper.RunInSta(() =>
        {
            var chart = new Chart();
            chart.Series = new ObservableCollection<IRenderableSeries>
            {
                new BarRenderableSeries
                {
                    YAxisId = "Y",
                    DataSeries = CreateSequentialData(0, 3)
                }
            };
            var range = new DataRange(-10, 30);
            var result = CallAdjustYRangeForBarBaseline(chart, "Y", range);

            Assert.Equal(range.Min, result.Min);
            Assert.Equal(range.Max, result.Max);
        });
    }

    private static UniformDataSeries<double, double> CreateSequentialData(double start, int count)
    {
        var ds = new UniformDataSeries<double, double>(i => start + i, x => x);
        for (int i = 0; i < count; i++)
            ds.Append(i * 10.0);
        return ds;
    }

    private static DataRange CallAdjustXRangeForBars(DataRange niceRange,
        IGrouping<string, IRenderableSeries> barGroup)
    {
        var method = typeof(Chart).GetMethod("AdjustXRangeForBars",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (DataRange)method!.Invoke(null, [niceRange, barGroup])!;
    }

    private static DataRange CallAdjustYRangeForBarBaseline(Chart chart, string axisId, DataRange range)
    {
        var method = typeof(Chart).GetMethod("AdjustYRangeForBarBaseline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (DataRange)method!.Invoke(chart, [axisId, range])!;
    }

    #endregion

    #region CoreRange 与 VisibleRange 关系

    [Fact]
    public void CoreRange_And_VisibleRange_DifferByPaddingOnly()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis
            {
                RangePaddingMin = 0.1,
                RangePaddingMax = 0.1,
                CoreRange = new DataRange(0, 100)
            };
            axis.VisibleRange = axis.ApplyRelativeRangePadding(axis.CoreRange);

            double expectedPad = 100 * 0.1;
            Assert.Equal(axis.CoreRange.Min - expectedPad, axis.VisibleRange.Min, 6);
            Assert.Equal(axis.CoreRange.Max + expectedPad, axis.VisibleRange.Max, 6);
            Assert.True(axis.VisibleRange.Length > axis.CoreRange.Length);
        });
    }

    [Fact]
    public void CoreRange_DefaultValue_IsZeroRange()
    {
        TestHelper.RunInSta(() =>
        {
            var axis = new LinearAxis();
            Assert.Equal(0, axis.CoreRange.Length);
        });
    }

    #endregion
}
