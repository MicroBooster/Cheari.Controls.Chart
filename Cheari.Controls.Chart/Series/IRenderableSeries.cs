using System.Windows.Media;
using Cheari.Controls.Data;
using Cheari.Controls.Core;

namespace Cheari.Controls.Series;

/// <summary>
/// 可渲染系列接口，定义了图表系列的基本属性。
/// </summary>
/// <remarks>
/// <para>每个可渲染系列绑定一个 <see cref="IDataSeries"/> 提供数据，
/// 并通过 <see cref="XAxisId"/> 和 <see cref="YAxisId"/> 关联到坐标轴。</para>
/// <para>框架提供了以下内置系列类型：</para>
/// <list type="bullet">
///   <item><c>LineRenderableSeries</c> — 折线图</item>
///   <item><c>AreaRenderableSeries</c> — 面积图</item>
///   <item><c>BarRenderableSeries</c> — 柱状图</item>
///   <item><c>ScatterRenderableSeries</c> — 散点图</item>
///   <item><c>OhlcRenderableSeries</c> — K线图</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var lineSeries = new LineRenderableSeries
/// {
///     Title = "Sin Wave",
///     Stroke = Colors.Cyan,
///     StrokeThickness = 1,
///     DataSeries = dataSeries,
///     XAxisId = Chart.DefaultXAxisId,
///     YAxisId = Chart.DefaultYAxisId
/// };
/// </code>
/// </example>
public interface IRenderableSeries
{
    /// <summary>
    /// 获取系列标题，用于图例显示。
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 获取系列是否可见。
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// 获取线条颜色。
    /// </summary>
    Color Stroke { get; }

    /// <summary>
    /// 获取线条宽度。
    /// </summary>
    double StrokeThickness { get; }

    /// <summary>
    /// 获取线条样式。
    /// </summary>
    LineStyle LineStyle { get; }

    /// <summary>
    /// 获取自定义虚线数组。
    /// </summary>
    double[]? StrokeDashArray { get; }

    /// <summary>
    /// 获取数据系列。
    /// </summary>
    IDataSeries DataSeries { get; }

    /// <summary>
    /// 获取关联的X轴ID。
    /// </summary>
    string XAxisId { get; }

    /// <summary>
    /// 获取关联的Y轴ID。
    /// </summary>
    string YAxisId { get; }
}