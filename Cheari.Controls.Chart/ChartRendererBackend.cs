namespace Cheari.Controls;

/// <summary>
/// 表示图表当前实际使用的渲染后端。
/// </summary>
public enum ChartRendererBackend
{
    /// <summary>
    /// 当前尚未建立有效的渲染后端。
    /// </summary>
    Unknown,

    /// <summary>
    /// 当前正在使用 D3D11 硬件渲染。
    /// </summary>
    D3D11,

    /// <summary>
    /// 当前正在使用 GDI 软件渲染。
    /// </summary>
    Gdi
}
