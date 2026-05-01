namespace Cheari.Controls;

/// <summary>
/// 指定图表渲染后端的选择策略。
/// </summary>
public enum ChartRendererPreference
{
    /// <summary>
    /// 优先使用 D3D11，失败后自动回退到 GDI 软件渲染。
    /// </summary>
    Auto,

    /// <summary>
    /// 仅允许使用 D3D11 硬件渲染。
    /// </summary>
    HardwareOnly,

    /// <summary>
    /// 始终使用 GDI 软件渲染。
    /// </summary>
    SoftwareOnly
}
