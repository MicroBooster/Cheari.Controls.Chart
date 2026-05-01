namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 日期时间刻度类型枚举，定义日期时间轴的刻度粒度。
/// </summary>
internal enum DateTimeTickKind
{
    /// <summary>
    /// 年度刻度。
    /// </summary>
    Year,

    /// <summary>
    /// 月度刻度。
    /// </summary>
    Month,

    /// <summary>
    /// 周刻度。
    /// </summary>
    Week,

    /// <summary>
    /// 日刻度。
    /// </summary>
    Day,

    /// <summary>
    /// 小时刻度。
    /// </summary>
    Hour,

    /// <summary>
    /// 分钟刻度。
    /// </summary>
    Minute,

    /// <summary>
    /// 秒刻度。
    /// </summary>
    Second,

    /// <summary>
    /// 毫秒刻度。
    /// </summary>
    Millisecond
}