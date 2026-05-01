using System.Windows.Media;
using Vortice.Wpf;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 渲染器接口，定义图表渲染的标准契约。
/// </summary>
internal interface IRenderer : IDisposable
{
    /// <summary>
    /// 获取渲染器是否需要 D3D11 支持。
    /// </summary>
    bool RequiresD3D11 { get; }

    /// <summary>
    /// 获取当前渲染器对应的后端类型。
    /// </summary>
    ChartRendererBackend Backend { get; }

    /// <summary>
    /// 获取当前可见的软件帧源。硬件渲染器返回 null。
    /// </summary>
    ImageSource? CurrentImageSource { get; }

    /// <summary>
    /// 获取最后一次初始化或渲染错误。
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// 初始化渲染器。
    /// </summary>
    /// <param name="args">绘图表面事件参数</param>
    void Initialize(DrawingSurfaceEventArgs args);

    /// <summary>
    /// 反初始化渲染器，释放资源。
    /// </summary>
    void Uninitialize();

    /// <summary>
    /// 渲染图表。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">视口宽度</param>
    /// <param name="height">视口高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="xRange">X轴数据范围</param>
    /// <param name="yRange">Y轴数据范围</param>
    /// <param name="commands">渲染命令列表</param>
    /// <returns>是否渲染成功</returns>
    bool Render(
        DrawEventArgs args,
        int width,
        int height,
        Color backgroundColor,
        bool enableAntialiasing,
        DataRange xRange,
        DataRange yRange,
        IReadOnlyList<IRenderCommand>? commands = null);

    /// <summary>
    /// 渲染图表（多轴版本）。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">视口宽度</param>
    /// <param name="height">视口高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="renderGroups">渲染组列表，每个组对应一对坐标轴</param>
    /// <returns>是否渲染成功</returns>
    bool Render(
        DrawEventArgs args,
        int width,
        int height,
        Color backgroundColor,
        bool enableAntialiasing,
        IReadOnlyList<AxisRenderGroup> renderGroups);
}
