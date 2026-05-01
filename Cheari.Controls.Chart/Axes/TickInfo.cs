namespace Cheari.Controls.Axes;

/// <summary>
/// 刻度信息结构体，表示坐标轴上一个刻度的位置和标签。
/// </summary>
public readonly struct TickInfo
{
    /// <summary>
    /// 获取刻度在坐标轴上的位置（数据坐标）。
    /// </summary>
    public double Position { get; init; }

    /// <summary>
    /// 获取刻度的显示标签文本。
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// 使用指定的位置和标签初始化 TickInfo 结构。
    /// </summary>
    /// <param name="position">刻度的位置（数据坐标）</param>
    /// <param name="label">刻度的显示标签</param>
    public TickInfo(double position, string label)
    {
        Position = position;
        Label = label;
    }
}