using System.Windows.Media;
using Cheari.Controls.Series.Renderers;

namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// 面积渲染操作，包含填充顶点和可选的边框线条。
/// </summary>
internal sealed class AreaRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.Area;

    public List<GpuAreaVertex> FillVertices { get; } = new();

    /// <summary>
    /// 获取或设置边框线条操作。
    /// </summary>
    public LineRenderOperation? BorderLine { get; set; }
}
