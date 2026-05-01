namespace Cheari.Controls.Core;

/// <summary>
/// 可见范围限制模式，定义坐标轴 VisibleRange 的限制方式。
/// </summary>
public enum VisibleRangeLimitMode
{
    /// <summary>
    /// 不限制，VisibleRange 可以任意变化。
    /// </summary>
    None,

    /// <summary>
    /// 只限制最小值，VisibleRange.Min 不能小于 VisibleRangeLimit.Min。
    /// </summary>
    MinOnly,

    /// <summary>
    /// 只限制最大值，VisibleRange.Max 不能大于 VisibleRangeLimit.Max。
    /// </summary>
    MaxOnly,

    /// <summary>
    /// 同时限制最小值和最大值。
    /// </summary>
    MinAndMax
}
