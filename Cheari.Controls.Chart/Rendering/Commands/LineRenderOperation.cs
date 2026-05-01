using System.Windows.Media;
using Cheari.Controls.Series.Renderers;

namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// 线条渲染操作，包含一组GPU线条实例和渲染属性。
/// </summary>
internal sealed class LineRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.Line;

    public List<GpuLineInstance> LineInstances { get; } = new();

    /// <summary>
    /// 获取或设置线条颜色。
    /// </summary>
    public Color StrokeColor { get; set; } = Colors.White;

    /// <summary>
    /// 获取或设置线条宽度。
    /// </summary>
    public float StrokeThickness { get; set; } = 1.0f;
}
