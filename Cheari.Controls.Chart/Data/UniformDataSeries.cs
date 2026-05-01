using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

/// <summary>
/// 均匀X轴数据系列，X坐标通过索引选择器生成，无需显式存储。
/// </summary>
public class UniformDataSeries<TX, TY> : DataSeriesBase, IUniformDataSeries<TX, TY>
{
    private readonly Func<TX, double> _xToDouble;
    private readonly Func<TY, double> _yToDouble;
    private DataRange _xRange;
    private DataRange _yRange;
    private bool _isXRangeDirty = true;
    private bool _isYRangeDirty = true;
    private RingBuffer<TY> _ringBuffer;
    private GeneratedReadOnlyList<TX> _xValuesView;
    private RingBufferReadOnlyList<TY> _yValuesView;

    /// <summary>初始化均匀数据系列。</summary>
    /// <param name="xSelector">X坐标选择器，根据索引生成X值。</param>
    /// <param name="xToDouble">X值到double的转换函数。</param>
    /// <param name="yToDouble">Y值到double的转换函数，为null时使用Convert.ToDouble。</param>
    public UniformDataSeries(Func<int, TX> xSelector, Func<TX, double> xToDouble, Func<TY, double>? yToDouble = null)
    {
        ArgumentNullException.ThrowIfNull(xSelector);
        ArgumentNullException.ThrowIfNull(xToDouble);

        XSelector = xSelector;
        _xToDouble = xToDouble;
        _yToDouble = yToDouble ?? (y => Convert.ToDouble(y));
        _ringBuffer = new RingBuffer<TY>();
        _xValuesView = new GeneratedReadOnlyList<TX>(() => Count, GetTypedX);
        _yValuesView = new RingBufferReadOnlyList<TY>(_ringBuffer);
    }

    /// <summary>获取X坐标选择器。</summary>
    public Func<int, TX> XSelector { get; }

    /// <inheritdoc/>
    public override int Count => _ringBuffer.Count;

    /// <inheritdoc/>
    public override int Version => _ringBuffer.Version;

    /// <summary>获取X值只读列表视图。</summary>
    public IReadOnlyList<TX> XValues => _xValuesView;

    /// <summary>获取Y值只读列表视图。</summary>
    public IReadOnlyList<TY> YValues => _yValuesView;

    /// <inheritdoc/>
    public override DataRange XRange
    {
        get
        {
            EnsureXRange();
            return _xRange;
        }
    }

    /// <inheritdoc/>
    public override DataRange YRange
    {
        get
        {
            EnsureYRange();
            return _yRange;
        }
    }

    /// <summary>获取指定索引处的强类型X值。</summary>
    public TX GetTypedX(int index) => XSelector(index);

    /// <summary>获取指定索引处的强类型Y值。</summary>
    public TY GetTypedY(int index) => _ringBuffer[index];

    /// <inheritdoc/>
    public override double GetX(int index) => _xToDouble(GetTypedX(index));

    /// <inheritdoc/>
    public override double GetY(int index) => _yToDouble(_ringBuffer[index]);

    /// <inheritdoc/>
    public override int CopyXValues(Span<double> destination)
    {
        lock (_lock)
        {
            int count = Math.Min(Count, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = GetX(i);
            return count;
        }
    }

    /// <inheritdoc/>
    public override int CopyYValues(Span<double> destination)
    {
        lock (_lock)
        {
            var access = _ringBuffer.AcquireReadAccess();
            int count = Math.Min(access.Count, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = _yToDouble(access[i]);
            return count;
        }
    }

    internal override DataFrame CreateFrameCore()
    {
        lock (_lock)
        {
            int count = _ringBuffer.Count;
            if (count == 0)
                return DataFrame.Empty;

            var yAccess = _ringBuffer.AcquireReadAccess();

            var xArr = System.Buffers.ArrayPool<float>.Shared.Rent(count);
            var yArr = System.Buffers.ArrayPool<float>.Shared.Rent(count);

            for (int i = 0; i < count; i++)
            {
                xArr[i] = (float)_xToDouble(XSelector(i));
                yArr[i] = (float)_yToDouble(yAccess[i]);
            }

            return new DataFrame
            {
                Count = count,
                Version = _ringBuffer.Version,
                XRange = XRange,
                YRange = YRange,
                Type = DataFrameType.Uniform,
                XValues = xArr,
                YValues = yArr,
                RentedXLength = count,
                RentedYLength = count
            };
        }
    }

    /// <summary>追加单个Y值。</summary>
    public void Append(TY y)
    {
        int appendedIndex;
        int previousCount;
        bool overflow = false;

        lock (_lock)
        {
            previousCount = _ringBuffer.Count;
            appendedIndex = previousCount;

            if (_fifoCapacity > 0 && previousCount >= _fifoCapacity)
            {
                overflow = true;
            }

            _ringBuffer.Add(y);
            
            if (_ringBuffer.IsFixedCapacity && previousCount >= _fifoCapacity)
            {
                _headIndex++;
            }
        }

        if (overflow)
        {
            InvalidateRangeCaches();
            OnRangeChanged();
            OnDataChanged(DataSeriesChangeEventArgs.Reset());
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }
        else
        {
            UpdateRangeForAppend(y, appendedIndex);
            OnRangeChanged();
            OnDataChanged(DataSeriesChangeEventArgs.Append(appendedIndex, 1));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, y, appendedIndex));
        }
    }

    /// <summary>批量追加Y值。</summary>
    public void Append(IEnumerable<TY> yValues)
    {
        ArgumentNullException.ThrowIfNull(yValues);
        var items = yValues as List<TY> ?? yValues.ToList();
        if (items.Count == 0)
            return;

        bool overflow;
        int appendedIndex;
        int newCount;

        lock (_lock)
        {
            int previousCount = _ringBuffer.Count;
            appendedIndex = previousCount;

            overflow = _fifoCapacity > 0 && previousCount + items.Count > _fifoCapacity;

            if (overflow)
            {
                int overflowCount = previousCount + items.Count - _fifoCapacity;
                _headIndex += overflowCount;
            }

            _ringBuffer.AddRange(CollectionsMarshal.AsSpan(items));
            newCount = _ringBuffer.Count;
        }

        if (overflow)
        {
            InvalidateRangeCaches();
            OnRangeChanged();
            OnDataChanged(DataSeriesChangeEventArgs.Reset());
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                UpdateRangeForAppend(items[i], appendedIndex + i);
            }

            OnRangeChanged();
            OnDataChanged(DataSeriesChangeEventArgs.Append(appendedIndex, items.Count));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, items, appendedIndex));
        }
    }

    /// <inheritdoc/>
    protected override void OnFifoCapacityChanged()
    {
        _ringBuffer.SetFixedCapacity(_fifoCapacity);
        
        if (_fifoCapacity > 0)
        {
            int currentCount = _ringBuffer.Count;
            if (currentCount > 0)
            {
                int startIndex = currentCount > _fifoCapacity ? currentCount - _fifoCapacity : 0;
                _headIndex += startIndex;
            }
        }
        else
        {
            _headIndex = 0;
        }

        InvalidateRangeCaches();
        OnRangeChanged();
        OnDataChanged(DataSeriesChangeEventArgs.Reset());
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }

    private void UpdateRangeForAppend(TY appendedValue, int appendedIndex)
    {
        if (Count == 1)
        {
            double x = GetX(0);
            double y = _yToDouble(appendedValue);
            _xRange = NormalizeRange(x, x);
            _yRange = NormalizeRange(y, y);
            _isXRangeDirty = false;
            _isYRangeDirty = false;
            return;
        }

        if (!_isXRangeDirty)
        {
            double appendedX = GetX(appendedIndex);
            _xRange = new DataRange(
                Math.Min(_xRange.Min, appendedX),
                Math.Max(_xRange.Max, appendedX));
        }

        if (!_isYRangeDirty)
        {
            double value = _yToDouble(appendedValue);
            _yRange = new DataRange(
                Math.Min(_yRange.Min, value),
                Math.Max(_yRange.Max, value));
        }
    }

    private void EnsureXRange()
    {
        if (!_isXRangeDirty)
            return;

        lock (_lock)
        {
            if (!_isXRangeDirty)
                return;

            int count = _ringBuffer.Count;
            if (count == 0)
            {
                _xRange = new DataRange(0, 1);
            }
            else
            {
                _xRange = NormalizeRange(GetX(0), GetX(count - 1));
            }
            _isXRangeDirty = false;
        }
    }

    private void EnsureYRange()
    {
        if (!_isYRangeDirty)
            return;

        lock (_lock)
        {
            int count = _ringBuffer.Count;
            if (count == 0)
            {
                _yRange = new DataRange(-1, 1);
                _isYRangeDirty = false;
                return;
            }

            var access = _ringBuffer.AcquireReadAccess();
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < access.Count; i++)
            {
                double value = _yToDouble(access[i]);
                if (value < min) min = value;
                if (value > max) max = value;
            }

            _yRange = new DataRange(min, max);
            _isYRangeDirty = false;
        }
    }

    private void InvalidateRangeCaches()
    {
        _isXRangeDirty = true;
        _isYRangeDirty = true;
    }

    private static DataRange NormalizeRange(double a, double b)
        => a <= b ? new DataRange(a, b) : new DataRange(b, a);
}
