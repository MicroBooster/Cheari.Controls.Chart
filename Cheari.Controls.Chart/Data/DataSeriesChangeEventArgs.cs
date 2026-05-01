namespace Cheari.Controls.Data;

/// <summary>
/// 数据系列变更事件参数，提供细粒度的数据变更信息。
/// </summary>
public class DataSeriesChangeEventArgs : EventArgs
{
    /// <summary>
    /// 获取变更类型。
    /// </summary>
    public DataSeriesChangeType ChangeType { get; init; }

    /// <summary>
    /// 获取变更开始的索引位置。
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// 获取变更涉及的数据点数量。
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 创建追加类型的变更事件参数。
    /// </summary>
    /// <param name="startIndex">追加的起始索引</param>
    /// <param name="count">追加的数量</param>
    /// <returns>变更事件参数</returns>
    public static DataSeriesChangeEventArgs Append(int startIndex, int count)
        => new() { ChangeType = DataSeriesChangeType.Append, StartIndex = startIndex, Count = count };

    /// <summary>
    /// 创建重置类型的变更事件参数。
    /// </summary>
    /// <returns>变更事件参数</returns>
    public static DataSeriesChangeEventArgs Reset()
        => new() { ChangeType = DataSeriesChangeType.Reset, StartIndex = 0, Count = 0 };
}