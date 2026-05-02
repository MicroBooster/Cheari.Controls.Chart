using System.ComponentModel;
using System.Windows.Media;
using Cheari.Controls.Series;

namespace Cheari.Controls.Legend;

/// <summary>
/// 图例项接口，定义单个图例项的属性。
/// </summary>
public interface ILegendItem : INotifyPropertyChanged
{
    /// <summary>
    /// 获取图例项的标题。
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 获取图例项的线条颜色。
    /// </summary>
    Color Stroke { get; }

    /// <summary>
    /// 获取图例项的填充颜色。对于面积图和柱状图，这是主要颜色。
    /// </summary>
    Color Fill { get; }

    /// <summary>
    /// 获取关联的系列。
    /// </summary>
    IRenderableSeries Series { get; }

    /// <summary>
    /// 获取或设置图例项是否可见。
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// 获取关联数据系列的附加信息，用于在 Legend 中显示曲线的额外数据。
    /// </summary>
    object? Tag { get; }
}
