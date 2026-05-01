namespace Cheari.Controls.Data;

/// <summary>
/// 数据系列变更类型枚举，定义数据系列发生变化的类型。
/// </summary>
public enum DataSeriesChangeType
{
    /// <summary>
    /// 追加新数据，在现有数据末尾添加。
    /// </summary>
    Append,

    /// <summary>
    /// 替换数据，用新数据替换指定范围的现有数据。
    /// </summary>
    Replace,

    /// <summary>
    /// 重置数据，清空所有现有数据。
    /// </summary>
    Reset
}