using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

/// <summary>
/// 数据系列抽象基类，提供 FIFO 容量管理、范围变更通知和线程安全锁。
/// </summary>
public abstract class DataSeriesBase : IDataSeries, IDataFrameProvider
{
    /// <summary>线程安全锁对象。</summary>
    protected readonly object _lock = new object();
    /// <summary>FIFO 滑动窗口容量，0 表示无限制。</summary>
    protected int _fifoCapacity;
    /// <summary>FIFO 模式下的数据头偏移索引。</summary>
    protected int _headIndex;
    /// <summary>存储 Tag 属性的字段。</summary>
    private object? _tag;

    /// <summary>当数据范围发生变化时触发。</summary>
    public event EventHandler? RangeChanged;
    /// <summary>当数据内容发生变更时触发。</summary>
    public event EventHandler<DataSeriesChangeEventArgs>? DataChanged;
    /// <summary>当集合发生变更时触发。</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    /// <summary>当属性值发生变化时触发。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>获取数据点数量。</summary>
    public abstract int Count { get; }
    /// <summary>获取数据版本号，每次数据变更递增。</summary>
    public abstract int Version { get; }

    /// <summary>
    /// 获取或设置 FIFO 滑动窗口容量。
    /// 当数据点数超过此容量时，最旧的数据点将被自动丢弃。
    /// 设为 0 表示无限制。
    /// </summary>
    public int FifoCapacity
    {
        get
        {
            lock (_lock)
            {
                return _fifoCapacity;
            }
        }
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "FifoCapacity must be non-negative.");

            lock (_lock)
            {
                if (_fifoCapacity == value)
                    return;

                _fifoCapacity = value;
                OnFifoCapacityChanged();
            }
        }
    }

    /// <summary>获取指定索引处的X值。</summary>
    public abstract double GetX(int index);
    /// <summary>获取指定索引处的Y值。</summary>
    public abstract double GetY(int index);
    /// <summary>将X值复制到目标跨度。</summary>
    public abstract int CopyXValues(Span<double> destination);
    /// <summary>将Y值复制到目标跨度。</summary>
    public abstract int CopyYValues(Span<double> destination);
    /// <summary>获取X轴数据范围。</summary>
    public abstract DataRange XRange { get; }
    /// <summary>获取Y轴数据范围。</summary>
    public abstract DataRange YRange { get; }

    /// <summary>
    /// 获取或设置附加信息，用于在 Legend 中显示曲线的额外数据。
    /// </summary>
    public object? Tag
    {
        get => _tag;
        set => SetProperty(ref _tag, value);
    }

    /// <summary>
    /// 属性变更辅助方法，触发 PropertyChanged 事件。
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 触发 PropertyChanged 事件。
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    DataFrame IDataFrameProvider.CreateFrame() => CreateFrameCore();

    /// <summary>当 FIFO 容量变更时调用，子类需实现容量调整逻辑。</summary>
    protected abstract void OnFifoCapacityChanged();
    /// <summary>创建数据帧快照。</summary>
    internal abstract DataFrame CreateFrameCore();

    /// <summary>触发 RangeChanged 事件。</summary>
    protected virtual void OnRangeChanged()
    {
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>触发 DataChanged 事件。</summary>
    protected virtual void OnDataChanged(DataSeriesChangeEventArgs args)
    {
        DataChanged?.Invoke(this, args);
    }

    /// <summary>触发 CollectionChanged 事件。</summary>
    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        CollectionChanged?.Invoke(this, args);
    }

    /// <summary>在锁内执行指定操作。</summary>
    protected void InvokeInLock(Action action)
    {
        lock (_lock)
        {
            action();
        }
    }
}
