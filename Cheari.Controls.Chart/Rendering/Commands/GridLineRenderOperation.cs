using Cheari.Controls.Series.Renderers;

namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// 网格线渲染操作，包含一组网格线实例。
/// </summary>
internal sealed class GridLineRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.GridLine;

    public IReadOnlyList<GpuLineInstance> Lines { get; init; } = Array.Empty<GpuLineInstance>();
}
