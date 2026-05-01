namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// OHLC（K线）渲染操作，包含K线实体（Bodies）和影线（Wicks）。
/// </summary>
internal sealed class OhlcRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.Ohlc;

    public BarRenderOperation Bodies { get; } = new();

    /// <summary>
    /// 获取影线（最高价-最低价线）渲染操作。
    /// </summary>
    public LineRenderOperation Wicks { get; } = new();
}
