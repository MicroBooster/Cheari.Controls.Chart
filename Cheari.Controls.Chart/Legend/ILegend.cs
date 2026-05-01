using System.Windows.Media;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例行，表示图例中的单条记录，绑定到一个渲染系列。
/// </summary>
public interface ILegendItem
{
    /// <summary>获取系列标题。</summary>
    string Title { get; }
    /// <summary>获取线条颜色。</summary>
    Color Stroke { get; }
    /// <summary>获取关联的渲染系列。</summary>
    IRenderableSeries Series { get; }
    /// <summary>获取或设置系列是否可见。</summary>
    bool IsVisible { get; set; }
}

/// <summary>
/// 图例位置枚举。
/// </summary>
public enum LegendPosition
{
    /// <summary>左上角。</summary>
    TopLeft,
    /// <summary>右上角。</summary>
    TopRight,
    /// <summary>左下角。</summary>
    BottomLeft,
    /// <summary>右下角。</summary>
    BottomRight,
    /// <summary>顶部居中。</summary>
    TopCenter,
    /// <summary>底部居中。</summary>
    BottomCenter
}

/// <summary>
/// 图例接口，定义图表图例的契约。
/// </summary>
public interface ILegend
{
    /// <summary>获取或设置图例位置。</summary>
    LegendPosition Position { get; set; }
    /// <summary>获取图例行列表。</summary>
    IReadOnlyList<ILegendItem> Items { get; }
    /// <summary>设置图例关联的系列列表。</summary>
    void SetSeries(IReadOnlyList<IRenderableSeries> series);
    /// <summary>图例行集合变更时触发。</summary>
    event Action? ItemsChanged;
}
