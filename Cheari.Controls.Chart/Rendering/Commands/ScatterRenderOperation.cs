using System.Windows.Media;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Core;

namespace Cheari.Controls.Rendering.Commands;

/// <summary>
/// GPU标记点实例结构，用于存储散点图标记点渲染所需的数据。
/// 
/// <para><b>内存布局（与 HLSL 实例输入对齐）：</b></para>
/// <list type="table">
///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 语义</description></listheader>
///   <item><term>X, Y</term><description>0  | 8  | TEXCOORD1 (float2 Center)</description></item>
///   <item><term>Size</term><description>8  | 4  | TEXCOORD2 (float Size)</description></item>
///   <item><term>R, G, B, A</term><description>12 | 16 | COLOR0 (float4 Color)</description></item>
///   <item><term>MarkerType</term><description>28 | 4  | TEXCOORD3 (float MarkerType)</description></item>
/// </list>
/// <para>总大小: 32 字节（2 × 16），无需 Padding</para>
/// </summary>
internal struct GpuMarkerInstance
{
    /// <summary>
    /// X坐标（数据坐标）。
    /// </summary>
    public float X;

    /// <summary>
    /// Y坐标（数据坐标）。
    /// </summary>
    public float Y;

    /// <summary>
    /// 标记点大小。
    /// </summary>
    public float Size;

    /// <summary>
    /// 红色通道颜色值（0-1）。
    /// </summary>
    public float R;

    /// <summary>
    /// 绿色通道颜色值（0-1）。
    /// </summary>
    public float G;

    /// <summary>
    /// 蓝色通道颜色值（0-1）。
    /// </summary>
    public float B;

    /// <summary>
    /// Alpha通道颜色值（0-1）。
    /// </summary>
    public float A;

    /// <summary>
    /// 标记点类型（枚举值）。
    /// </summary>
    public float MarkerType;
}

/// <summary>
/// 散点图渲染操作，包含一组标记点实例和渲染属性。
/// </summary>
internal sealed class ScatterRenderOperation : IRenderCommand
{
    public RenderCommandType CommandType => RenderCommandType.Scatter;

    public List<GpuMarkerInstance> Instances { get; } = new();

    /// <summary>
    /// 获取或设置标记点类型。
    /// </summary>
    public MarkerType MarkerType { get; set; }

    /// <summary>
    /// 获取或设置标记点大小。
    /// </summary>
    public float MarkerSize { get; set; } = 6.0f;

    /// <summary>
    /// 获取或设置标记点颜色。
    /// </summary>
    public Color MarkerColor { get; set; } = Colors.Cyan;
}
