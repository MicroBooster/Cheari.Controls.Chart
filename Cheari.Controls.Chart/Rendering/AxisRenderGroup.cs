using Cheari.Controls.Core;
using Cheari.Controls.Axes;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Axes.CoordinateMappers;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 坐标轴渲染组，将共享同一对坐标轴的数据系列组合在一起进行渲染。
/// </summary>
internal sealed class AxisRenderGroup
{
    /// <summary>
    /// 获取X轴数据范围。
    /// </summary>
    public DataRange XRange { get; init; }

    /// <summary>
    /// 获取Y轴数据范围。
    /// </summary>
    public DataRange YRange { get; init; }

    /// <summary>
    /// 获取X轴坐标映射器。
    /// </summary>
    public ICoordinateMapper XMapper { get; init; } = LinearCoordinateMapper.Instance;

    /// <summary>
    /// 获取Y轴坐标映射器。
    /// </summary>
    public ICoordinateMapper YMapper { get; init; } = LinearCoordinateMapper.Instance;

    /// <summary>
    /// 获取渲染命令列表。
    /// </summary>
    public IReadOnlyList<IRenderCommand> Commands { get; init; } = Array.Empty<IRenderCommand>();

    /// <summary>
    /// 获取X轴引用，用于GPU渲染时实时验证XRange一致性。
    /// </summary>
    public IAxis? XAxis { get; init; }

    /// <summary>
    /// 获取Y轴引用，用于GPU渲染时实时验证YRange一致性。
    /// </summary>
    public IAxis? YAxis { get; init; }
}
