using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例图标辅助类，根据系列类型生成对应的矢量图标。
/// </summary>
public static class LegendIconHelper
{
    /// <summary>
    /// 根据图例项创建对应的矢量图标。
    /// </summary>
    /// <param name="item">图例项。</param>
    /// <returns>图标元素。</returns>
    public static FrameworkElement CreateIcon(ILegendItem item)
    {
        return CreateIcon(item.Series, item.Stroke);
    }

    /// <summary>
    /// 根据系列类型创建对应的矢量图标 Path 元素。
    /// </summary>
    public static Path CreateIcon(IRenderableSeries series, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var path = new Path
        {
            Width = 16,
            Height = 12,
            Stretch = Stretch.Uniform,
            Stroke = brush,
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var geometry = GetIconGeometry(series);
        geometry.Freeze();
        path.Data = geometry;

        if (series is AreaRenderableSeries areaSeries)
        {
            var fillBrush = new SolidColorBrush(areaSeries.Fill);
            fillBrush.Freeze();
            var fillPath = new Path
            {
                Width = 16,
                Height = 12,
                Stretch = Stretch.Uniform,
                Fill = fillBrush,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                SnapsToDevicePixels = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Data = geometry,
                Stroke = brush
            };
            return fillPath;
        }

        if (series is BarRenderableSeries barSeries)
        {
            var fillBrush = new SolidColorBrush(barSeries.Fill);
            fillBrush.Freeze();
            path.Fill = fillBrush;
        }

        return path;
    }

    private static Geometry GetIconGeometry(IRenderableSeries series)
    {
        return series switch
        {
            LineRenderableSeries => Geometry.Parse("M 1,9 L 5,5 L 9,7 L 13,2 L 15,4"),
            AreaRenderableSeries => Geometry.Parse("M 1,11 L 3,6 L 7,8 L 11,3 L 15,5 L 15,11 Z"),
            BarRenderableSeries => Geometry.Parse("M 1.5,10 h 2.5 v -4 h 2.5 v 4 h 2.5 v -7 h 2.5 v 7 h 2.5 v -3 h 2.5 Z"),
            ScatterRenderableSeries => Geometry.Parse("M 3,7 A 1.3,1.3 0 1,0 3.01,7 Z M 7.5,3 A 1.3,1.3 0 1,0 7.51,3 Z M 13,6.5 A 1.3,1.3 0 1,0 13.01,6.5 Z M 9.5,9.5 A 1.3,1.3 0 1,0 9.51,9.5 Z"),
            OhlcRenderableSeries => Geometry.Parse("M 1.5,10 v -7 M 1.5,3 h 3 M 1.5,10 h 3 M 5.5,8 v -4 M 5.5,4 h 3 M 5.5,8 h 3 M 9.5,11 v -8 M 9.5,3 h 3 M 9.5,11 h 3 M 13.5,7 v -3 M 13.5,4 h 3 M 13.5,7 h 3"),
            _ => Geometry.Parse("M 1,9 L 5,5 L 9,7 L 13,2 L 15,4")
        };
    }

    /// <summary>
    /// 更新图标的颜色。
    /// </summary>
    /// <param name="icon">图标元素。</param>
    /// <param name="color">新颜色。</param>
    public static void UpdateIconColor(FrameworkElement icon, Color color)
    {
        if (icon is Path path)
        {
            path.Stroke = new SolidColorBrush(color);
        }
    }
}
