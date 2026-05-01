using System.Buffers;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

internal enum DataFrameType
{
    Xy,
    Ohlc,
    Uniform
}

/// <summary>
/// 数据帧，表示数据系列在某一时刻的快照。
/// 数组通过 ArrayPool 租用，使用完毕后必须调用 Return() 归还。
/// </summary>
internal sealed class DataFrame
{
    public int Count { get; init; }
    public int Version { get; init; }
    public DataRange XRange { get; init; }
    public DataRange YRange { get; init; }
    public DataFrameType Type { get; init; }

    public float[]? XValues { get; init; }
    public float[]? YValues { get; init; }
    public float[]? OpenValues { get; init; }
    public float[]? HighValues { get; init; }
    public float[]? LowValues { get; init; }
    public float[]? CloseValues { get; init; }

    internal int RentedXLength { get; init; }
    internal int RentedYLength { get; init; }
    internal int RentedOhlcLength { get; init; }

    private bool _returned;

    public static DataFrame Empty { get; } = new()
    {
        Count = 0,
        Version = 0,
        XRange = new DataRange(0, 1),
        YRange = new DataRange(-1, 1),
        Type = DataFrameType.Xy
    };

    /// <summary>
    /// 归还租用的数组到 ArrayPool。
    /// 调用后不应再访问此 DataFrame 的任何数组属性。
    /// </summary>
    public void Return()
    {
        if (_returned)
            return;

        _returned = true;

        if (XValues != null && RentedXLength > 0)
            ArrayPool<float>.Shared.Return(XValues);
        if (YValues != null && RentedYLength > 0)
            ArrayPool<float>.Shared.Return(YValues);
        if (OpenValues != null && RentedOhlcLength > 0)
            ArrayPool<float>.Shared.Return(OpenValues);
        if (HighValues != null && RentedOhlcLength > 0)
            ArrayPool<float>.Shared.Return(HighValues);
        if (LowValues != null && RentedOhlcLength > 0)
            ArrayPool<float>.Shared.Return(LowValues);
        if (CloseValues != null && RentedOhlcLength > 0)
            ArrayPool<float>.Shared.Return(CloseValues);
    }
}
