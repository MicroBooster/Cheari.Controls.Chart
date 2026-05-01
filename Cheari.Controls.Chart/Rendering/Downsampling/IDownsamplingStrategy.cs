using Cheari.Controls.Data;
using Cheari.Controls.Core;

namespace Cheari.Controls.Rendering.Downsampling;

/// <summary>
/// 降采样策略接口，定义数据降采样的方法。
/// </summary>
public interface IDownsamplingStrategy
{
    /// <summary>对数据系列进行降采样。</summary>
    void Downsample(IDataSeries dataSeries, int targetCount, DataRange visibleXRange, List<double> sampledX, List<double> sampledY);

    /// <summary>对原始数据跨度进行降采样。</summary>
    void Downsample(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, int targetCount, DataRange visibleXRange, List<double> sampledX, List<double> sampledY);
}
