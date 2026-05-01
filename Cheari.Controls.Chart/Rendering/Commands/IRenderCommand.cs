namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// 渲染命令类型枚举，标识不同类型的渲染操作。
/// 用于替代 D3D11Renderer 中的 if-else 类型判断链。
/// </summary>
internal enum RenderCommandType
{
    Line,
    Area,
    Bar,
    Scatter,
    Ohlc,
    GridLine
}

/// <summary>
/// 渲染命令接口，作为渲染器与图表类型之间的抽象层。
/// 每种图表类型产生自己的 RenderCommand，渲染器按 <see cref="CommandType"/> 分发处理。
/// </summary>
internal interface IRenderCommand
{
    /// <summary>
    /// 获取渲染命令的类型，用于渲染器分发。
    /// </summary>
    RenderCommandType CommandType { get; }
}
