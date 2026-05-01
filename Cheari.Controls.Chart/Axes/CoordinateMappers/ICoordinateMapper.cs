using Cheari.Controls.Core;

namespace Cheari.Controls.Axes.CoordinateMappers;

/// <summary>
/// 坐标映射策略接口，定义数据坐标与屏幕坐标之间的转换逻辑。
/// </summary>
public interface ICoordinateMapper
{
    /// <summary>
    /// 将数据坐标转换为屏幕坐标。
    /// </summary>
    /// <param name="dataValue">数据坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>屏幕坐标值</returns>
    double DataToScreen(double dataValue, DataRange dataRange, double viewportSize);

    /// <summary>
    /// 将屏幕坐标转换为数据坐标。
    /// </summary>
    /// <param name="screenValue">屏幕坐标值</param>
    /// <param name="dataRange">数据范围</param>
    /// <param name="viewportSize">视口尺寸</param>
    /// <returns>数据坐标值</returns>
    double ScreenToData(double screenValue, DataRange dataRange, double viewportSize);
}
