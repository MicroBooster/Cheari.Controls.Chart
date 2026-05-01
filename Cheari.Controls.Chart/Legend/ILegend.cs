using System.Windows;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例接口，定义图例的基本属性和行为。
/// </summary>
public interface ILegend
{
    /// <summary>
    /// 获取或设置图例的位置。
    /// </summary>
    LegendPosition Position { get; set; }

    /// <summary>
    /// 获取或设置图例的排列方向。
    /// </summary>
    LegendOrientation Orientation { get; set; }

    /// <summary>
    /// 获取或设置图例的水平对齐方式。
    /// </summary>
    HorizontalAlignment HorizontalAlignment { get; set; }

    /// <summary>
    /// 获取或设置图例的垂直对齐方式。
    /// </summary>
    VerticalAlignment VerticalAlignment { get; set; }

    /// <summary>
    /// 获取图例项的只读列表。
    /// </summary>
    IReadOnlyList<ILegendItem> Items { get; }

    /// <summary>
    /// 当图例项集合发生变化时触发。
    /// </summary>
    event Action? ItemsChanged;

    /// <summary>
    /// 设置要显示的系列集合。
    /// </summary>
    /// <param name="series">系列集合。</param>
    void SetSeries(IReadOnlyList<IRenderableSeries> series);
}
