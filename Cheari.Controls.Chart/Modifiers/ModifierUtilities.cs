using Cheari.Controls.Axes;

namespace Cheari.Controls.Modifiers;

/// <summary>
/// 修饰器共享工具方法，消除 PanModifier 和 ZoomModifier 之间的重复代码。
/// </summary>
internal static class ModifierUtilities
{
    /// <summary>
    /// 根据轴ID在轴列表中查找坐标轴。
    /// </summary>
    /// <param name="axes">坐标轴列表</param>
    /// <param name="axisId">轴ID</param>
    /// <returns>找到的坐标轴，未找到返回null</returns>
    public static IAxis? FindAxis(IReadOnlyList<IAxis> axes, string axisId)
    {
        for (int i = 0; i < axes.Count; i++)
        {
            if (axes[i].Id == axisId)
                return axes[i];
        }
        return null;
    }
}
