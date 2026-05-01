using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

/// <summary>
    /// 数据系列接口，所有图表数据提供者必须实现此接口。
    /// 提供统一的数据访问方式，使渲染器无需了解具体数据类型即可访问数据。
    /// </summary>
    /// <remarks>
    /// <para>框架提供了以下内置实现：</para>
    /// <list type="bullet">
    ///   <item><see cref="IUniformDataSeries{TX,TY}"/> — 均匀 X 轴系列，X 坐标通过索引生成器计算</item>
    ///   <item><see cref="IVariableDataSeries{TX,TY}"/> — 变量 X 轴系列，X 坐标显式存储</item>
    /// </list>
    /// <para>数据系列实现了 <see cref="INotifyCollectionChanged"/>，当数据变更时通知 UI 线程。
    /// <see cref="Version"/> 属性用于渲染管线判断缓存是否有效，避免重复计算。</para>
    /// <para>启用 <see cref="FifoCapacity"/> 后，数据系列变为固定容量的滑动窗口，
    /// 旧数据自动丢弃，适用于实时流式数据场景。</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var series = new UniformDataSeries&lt;double, double&gt;(
    ///     xSelector: i => i * 0.01,
    ///     xToDouble: x => x,
    ///     yToDouble: y => y)
    /// {
    ///     FifoCapacity = 100
    /// };
    /// series.Append(Math.Sin(0));
    /// </code>
    /// </example>
public interface IDataSeries : INotifyCollectionChanged, INotifyPropertyChanged
{
    /// <summary>
    /// 获取数据点数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 获取或设置 FIFO 滑动窗口容量。
    /// 值为 0（默认）表示不启用 FIFO 模式，数据无限累积。
    /// 当设置为大于 0 的值时，数据达到容量后，新数据会覆盖最旧的数据。
    /// </summary>
    int FifoCapacity { get; set; }

    /// <summary>
    /// 获取数据版本号，每次数据变更时递增。
    /// 用于判断缓存是否有效。
    /// </summary>
    int Version { get; }

    /// <summary>
    /// 获取指定索引处的 X 坐标值（转换为双精度浮点数）。
    /// 用于渲染管线，无需了解原始数据类型。
    /// </summary>
    /// <param name="index">数据点索引</param>
    /// <returns>X 坐标的双精度值</returns>
    double GetX(int index);

    /// <summary>
    /// 获取指定索引处的 Y 坐标值（转换为双精度浮点数）。
    /// 用于渲染管线，无需了解原始数据类型。
    /// </summary>
    /// <param name="index">数据点索引</param>
    /// <returns>Y 坐标的双精度值</returns>
    double GetY(int index);

    /// <summary>
    /// 将 X 坐标值批量拷贝到目标 Span。
    /// </summary>
    /// <param name="destination">目标缓冲区</param>
    /// <returns>实际拷贝的元素数量</returns>
    int CopyXValues(Span<double> destination);

    /// <summary>
    /// 将 Y 坐标值批量拷贝到目标 Span。
    /// </summary>
    /// <param name="destination">目标缓冲区</param>
    /// <returns>实际拷贝的元素数量</returns>
    int CopyYValues(Span<double> destination);

    /// <summary>
    /// 获取数据系列的 X 轴范围。
    /// 实现方可以缓存该值，并在数据变更时失效。
    /// </summary>
    DataRange XRange { get; }

    /// <summary>
    /// 获取数据系列的 Y 轴范围。
    /// 实现方可以缓存该值，并在数据变更时失效。
    /// </summary>
    DataRange YRange { get; }

    /// <summary>
    /// 获取或设置附加信息，用于在 Legend 中显示曲线的额外数据。
    /// </summary>
    object? Tag { get; set; }

    /// <summary>
    /// 范围缓存可能发生变化时触发。
    /// </summary>
    event EventHandler? RangeChanged;

    /// <summary>
    /// 数据变更时触发，提供变更详情。
    /// </summary>
    event EventHandler<DataSeriesChangeEventArgs>? DataChanged;
}

internal interface IDataFrameProvider
{
    DataFrame CreateFrame();
}

/// <summary>
/// 泛型数据系列接口，提供强类型的数据访问。
/// </summary>
/// <typeparam name="TX">X 坐标类型</typeparam>
/// <typeparam name="TY">Y 坐标类型</typeparam>
public interface IDataSeries<TX, TY> : IDataSeries
{
    /// <summary>
    /// 获取指定索引处的 X 坐标值（原始类型）。
    /// </summary>
    /// <param name="index">数据点索引</param>
    /// <returns>X 坐标的原始类型值</returns>
    TX GetTypedX(int index);

    /// <summary>
    /// 获取指定索引处的 Y 坐标值（原始类型）。
    /// </summary>
    /// <param name="index">数据点索引</param>
    /// <returns>Y 坐标的原始类型值</returns>
    TY GetTypedY(int index);

    /// <summary>
    /// 获取 X 值只读视图。
    /// </summary>
    IReadOnlyList<TX> XValues { get; }

    /// <summary>
    /// 获取 Y 值只读视图。
    /// </summary>
    IReadOnlyList<TY> YValues { get; }
}

/// <summary>
/// 均匀 X 轴数据系列接口。
/// X 坐标通过索引生成器按需计算。
/// 适用于等间隔采样或基于索引推导 X 值的数据。
/// </summary>
/// <typeparam name="TX">X 坐标类型</typeparam>
/// <typeparam name="TY">Y 坐标类型</typeparam>
public interface IUniformDataSeries<TX, TY> : IDataSeries<TX, TY>
{
    /// <summary>
    /// 获取 X 坐标生成器。
    /// </summary>
    Func<int, TX> XSelector { get; }
}

/// <summary>
/// 变量 X 轴数据系列接口。
/// X 坐标显式存储，允许非均匀分布的数据点。
/// 适用于散点图、不规则间隔数据等场景。
/// </summary>
/// <typeparam name="TX">X 坐标类型</typeparam>
/// <typeparam name="TY">Y 坐标类型</typeparam>
public interface IVariableDataSeries<TX, TY> : IDataSeries<TX, TY>
{
}

/// <summary>
/// 可选的数据点元数据接口。
/// 为 tooltip、单点高亮等后续功能提供扩展点。
/// </summary>
public interface IDataPointMetadataProvider
{
    /// <summary>
    /// 获取指定索引处的数据点标签。
    /// </summary>
    string? GetPointLabel(int index);

    /// <summary>
    /// 获取指定索引处的数据点颜色。
    /// </summary>
    Color? GetPointColor(int index);
}
