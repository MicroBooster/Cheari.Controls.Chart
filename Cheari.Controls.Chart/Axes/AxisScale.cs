namespace Cheari.Controls.Axes;

/// <summary>
/// 轴刻度类型枚举，定义坐标轴的数值刻度方式。
/// </summary>
public enum AxisScale
{
    /// <summary>
    /// 线性刻度，等间距分布。
    /// </summary>
    Linear,

    /// <summary>
    /// 对数刻度，以10为底的对数分布。
    /// </summary>
    Logarithmic,

    /// <summary>
    /// 日期时间刻度，适用于时间序列数据。
    /// </summary>
    DateTime
}