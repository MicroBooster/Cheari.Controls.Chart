using System.Windows.Media;
using Cheari.Controls.Series.Renderers;

namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// 柱状图渲染操作，包含一组GPU柱状图实例。
/// </summary>
internal sealed class BarRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.Bar;

    public List<GpuBarInstance> Instances { get; } = new();
}
