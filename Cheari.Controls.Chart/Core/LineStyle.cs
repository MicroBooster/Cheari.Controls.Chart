namespace Cheari.Controls.Core;

/// <summary>
/// 线条样式枚举，定义图表中线条的绘制样式。
/// </summary>
public enum LineStyle
{
    /// <summary>
    /// 实线，没有间断。
    /// </summary>
    Solid,

    /// <summary>
    /// 短划线样式。
    /// </summary>
    Dash,

    /// <summary>
    /// 点线样式。
    /// </summary>
    Dot,

    /// <summary>
    /// 点划线样式（短划+点）。
    /// </summary>
    DashDot,

    /// <summary>
    /// 双点划线样式（短划+点+点）。
    /// </summary>
    DashDotDot
}