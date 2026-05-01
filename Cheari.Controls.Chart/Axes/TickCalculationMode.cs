namespace Cheari.Controls.Axes;

/// <summary>
/// 主刻度计算模式，决定主刻度间隔的计算方式。
/// </summary>
public enum TickCalculationMode
{
    /// <summary>
    /// 自动计算模式。根据轴的像素长度自动计算刻度间隔，
    /// 约每60像素一个刻度，间隔取美观数（1, 2, 5 的倍数）。
    /// </summary>
    Auto,

    /// <summary>
    /// 固定间隔模式。使用 MajorTickInterval 属性指定的间隔。
    /// </summary>
    FixedInterval,

    /// <summary>
    /// 固定数量模式。使用 MajorTickCount 属性指定的刻度数量，
    /// 刻度间隔 = VisibleRange.Length / (MajorTickCount - 1)，
    /// 首尾刻度始终对齐到范围边界。
    /// </summary>
    FixedCount
}
