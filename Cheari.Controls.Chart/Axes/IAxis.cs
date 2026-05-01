using System.Windows.Media;
using Cheari.Controls.Data;
using Cheari.Controls.Core;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Axes;

/// <summary>
/// 坐标轴接口，定义坐标轴的基本属性和行为。
/// </summary>
/// <remarks>
/// <para>IAxis 是所有坐标轴类型的公共抽象。框架提供了以下内置实现：</para>
/// <list type="bullet">
///   <item><see cref="LinearAxis"/> — 线性刻度轴，适用于大多数数值数据</item>
///   <item><see cref="DateTimeAxis"/> — 日期时间轴，自动生成时间刻度标签</item>
///   <item><see cref="TimeSpanAxis"/> — 时间间隔轴，适用于持续时间数据</item>
/// </list>
/// <para>坐标轴通过 <see cref="CoordinateMapper"/> 支持非线性映射（如对数轴），
/// 并通过 <see cref="VisibleRangeLimit"/> 和 <see cref="VisibleRangeLimitMode"/> 
/// 限制用户的平移/缩放范围。</para>
/// </remarks>
/// <example>
/// <code>
/// var xAxis = new LinearAxis
/// {
///     Id = Chart.DefaultXAxisId,
///     VisibleRange = new DataRange(0, 100),
///     AutoRange = AutoRange.Always,
///     ShowMajorGridLines = true
/// };
/// </code>
/// </example>
public interface IAxis
{
    /// <summary>
    /// 获取轴的唯一标识符。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 获取或设置轴的位置（左、右、上、下）。
    /// </summary>
    AxisPlacement Placement { get; set; }

    /// <summary>
    /// 获取轴的刻度类型（线性、对数、日期时间）。
    /// </summary>
    AxisScale Scale { get; }

    /// <summary>
    /// 获取或设置轴的可见范围。
    /// </summary>
    DataRange VisibleRange { get; set; }

    /// <summary>
    /// 获取或设置可见范围的限制边界。
    /// 与 <see cref="VisibleRangeLimitMode"/> 配合使用，
    /// 限制 <see cref="VisibleRange"/> 不能超出此范围。
    /// </summary>
    DataRange VisibleRangeLimit { get; set; }

    /// <summary>
    /// 获取或设置可见范围的限制模式。
    /// 决定 <see cref="VisibleRangeLimit"/> 的哪些边界生效。
    /// </summary>
    VisibleRangeLimitMode VisibleRangeLimitMode { get; set; }

    /// <summary>
    /// 获取坐标映射器，用于数据坐标和屏幕坐标之间的转换。
    /// </summary>
    ICoordinateMapper CoordinateMapper { get; }

    /// <summary>
    /// 获取或设置轴的标题。
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// 获取或设置轴是否可见。
    /// </summary>
    bool IsAxisVisible { get; set; }

    /// <summary>
    /// 获取或设置轴的前景色（用于刻度线、刻度标签等）。
    /// </summary>
    Brush AxisForeground { get; set; }

    /// <summary>
    /// 获取或设置是否自动调整范围以适应数据。
    /// </summary>
    bool AutoRange { get; set; }

    /// <summary>
    /// 获取或设置主刻度计算模式。
    /// </summary>
    TickCalculationMode TickCalculationMode { get; set; }

    /// <summary>
    /// 获取或设置主刻度间隔。
    /// 仅在 TickCalculationMode 为 FixedInterval 时生效。
    /// </summary>
    double MajorTickInterval { get; set; }

    /// <summary>
    /// 获取或设置固定主刻度数量。
    /// 仅在 TickCalculationMode 为 FixedCount 时生效，
    /// 刻度间隔 = VisibleRange.Length / (MajorTickCount - 1)，
    /// 首尾刻度始终对齐到范围边界。
    /// </summary>
    int MajorTickCount { get; set; }

    /// <summary>
    /// 获取或设置是否将可见范围对齐到主刻度。
    /// 启用后，VisibleRange 的 Min/Max 会自动调整到最近的刻度值，
    /// 使第一个和最后一个主刻度始终对齐到轴的两端。
    /// 仅在 TickCalculationMode 为 Auto 或 FixedInterval 时生效。
    /// </summary>
    bool AlignRangeToTicks { get; set; }

    /// <summary>
    /// 获取或设置刻度标签的格式化字符串。
    /// </summary>
    string? LabelFormat { get; set; }

    /// <summary>
    /// 获取或设置是否显示主网格线。
    /// </summary>
    bool ShowMajorGridLines { get; set; }

    /// <summary>
    /// 获取或设置是否显示次网格线。
    /// </summary>
    bool ShowMinorGridLines { get; set; }

    /// <summary>
    /// 获取或设置主网格线的颜色。
    /// </summary>
    Color MajorGridLineColor { get; set; }

    /// <summary>
    /// 获取或设置主网格线的画刷。
    /// </summary>
    Brush? MajorGridLineBrush { get; set; }

    /// <summary>
    /// 获取或设置主网格线的粗细。
    /// </summary>
    double MajorGridLineThickness { get; set; }

    /// <summary>
    /// 获取或设置主网格线的虚线数组。
    /// </summary>
    DoubleCollection? MajorGridLineDashArray { get; set; }

    /// <summary>
    /// 获取或设置次网格线的画刷。
    /// </summary>
    Brush? MinorGridLineBrush { get; set; }

    /// <summary>
    /// 获取或设置次网格线的粗细。
    /// </summary>
    double MinorGridLineThickness { get; set; }

    /// <summary>
    /// 获取或设置次网格线的虚线数组。
    /// </summary>
    DoubleCollection? MinorGridLineDashArray { get; set; }

    /// <summary>
    /// 当可见范围改变时触发的事件。
    /// </summary>
    event EventHandler<EventArgs>? VisibleRangeChanged;

    /// <summary>
    /// 获取主刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    TickInfo[] GetMajorTicks(double viewportSize);

    /// <summary>
    /// 获取次刻度信息数组。
    /// </summary>
    /// <param name="viewportSize">视口大小</param>
    /// <returns>刻度信息数组</returns>
    TickInfo[] GetMinorTicks(double viewportSize);

    /// <summary>
    /// 根据数据系列计算自动范围。
    /// </summary>
    /// <param name="dataSeries">数据系列集合</param>
    /// <returns>计算得到的自动范围</returns>
    DataRange CalculateAutoRange(IEnumerable<IDataSeries> dataSeries);
}