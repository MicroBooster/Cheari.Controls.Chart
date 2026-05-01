using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

/// <summary>
/// 变量X轴数据系列，X和Y坐标均显式存储。
/// </summary>
public class VariableDataSeries<TX, TY> : DataSeriesBase, IVariableDataSeries<TX, TY>
{
    private readonly RingBuffer<TX> _xRingBuffer = new();
    private readonly RingBuffer<TY> _yRingBuffer = new();
    private readonly RingBufferReadOnlyList<TX> _xValuesView;
    private readonly RingBufferReadOnlyList<TY> _yValuesView;
    private readonly Func<TX, double> _xToDouble;
    private readonly Func<TY, double> _yToDouble;
    private DataRange _xRange;
    private DataRange _yRange;
    private bool _isXRangeDirty = true;
    private bool _isYRangeDirty = true;

    /// <summary>初始化变量数据系列。</summary>
    /// <param name="xToDouble">X值到double的转换函数。</param>
    /// <param name="yToDouble">Y值到double的转换函数，为null时使用Convert.ToDouble。</param>
    public VariableDataSeries(Func<TX, double> xToDouble, Func<TY, double>? yToDouble = null)
    {
        ArgumentNullException.ThrowIfNull(xToDouble);
        _xToDouble = xToDouble;
        _yToDouble = yToDouble ?? (y => Convert.ToDouble(y));
        _xValuesView = new RingBufferReadOnlyList<TX>(_xRingBuffer);
        _yValuesView = new RingBufferReadOnlyList<TY>(_yRingBuffer);
    }

    /// <inheritdoc/>
    public override int Count => _xRingBuffer.Count;

    /// <inheritdoc/>
    public override int Version => _xRingBuffer.Version;

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
    public TX GetTypedX(int index) => _xRingBuffer[index];

    /// <summary>获取指定索引处的强类型Y值。</summary>
    public TY GetTypedY(int index) => _yRingBuffer[index];

    /// <inheritdoc/>
    public override double GetX(int index) => _xToDouble(_xRingBuffer[index]);

    /// <inheritdoc/>
    public override double GetY(int index) => _yToDouble(_yRingBuffer[index]);

    /// <inheritdoc/>
    public override int CopyXValues(Span<double> destination)
    {
        lock (_lock)
        {
            var access = _xRingBuffer.AcquireReadAccess();
            int count = Math.Min(access.Count, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = _xToDouble(access[i]);
            return count;
        }
    }

    /// <inheritdoc/>
    public override int CopyYValues(Span<double> destination)
    {
        lock (_lock)
        {
            var access = _yRingBuffer.AcquireReadAccess();
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
            int count = _xRingBuffer.Count;
            if (count == 0)
                return DataFrame.Empty;

            var xAccess = _xRingBuffer.AcquireReadAccess();
            var yAccess = _yRingBuffer.AcquireReadAccess();

            var xArr = System.Buffers.ArrayPool<float>.Shared.Rent(count);
            var yArr = System.Buffers.ArrayPool<float>.Shared.Rent(count);

            for (int i = 0; i < count; i++)
            {
                xArr[i] = (float)_xToDouble(xAccess[i]);
                yArr[i] = (float)_yToDouble(yAccess[i]);
            }

            return new DataFrame
            {
                Count = count,
                Version = _xRingBuffer.Version,
                XRange = XRange,
                YRange = YRange,
                Type = DataFrameType.Xy,
                XValues = xArr,
                YValues = yArr,
                RentedXLength = count,
                RentedYLength = count
            };
        }
    }

    /// <summary>追加单个数据点。</summary>
    public void Append(TX x, TY y)
    {
        int appendedIndex;
        bool overflow;

        DataSeriesChangeEventArgs? dataChangeArgs;
        NotifyCollectionChangedEventArgs? collectionChangeArgs;

        lock (_lock)
        {
            appendedIndex = _xRingBuffer.Count;

            overflow = _fifoCapacity > 0 && appendedIndex >= _fifoCapacity;

            if (overflow)
                _headIndex++;

            _xRingBuffer.Add(x);
            _yRingBuffer.Add(y);

            if (overflow)
            {
                InvalidateRangeCaches();
                dataChangeArgs = DataSeriesChangeEventArgs.Reset();
                collectionChangeArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            }
            else
            {
                UpdateRangeForAppend(x, y);
                dataChangeArgs = DataSeriesChangeEventArgs.Append(appendedIndex, 1);
                collectionChangeArgs = new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    new KeyValuePair<TX, TY>(x, y), appendedIndex);
            }
        }

        OnRangeChanged();
        OnDataChanged(dataChangeArgs);
        OnCollectionChanged(collectionChangeArgs);
    }

    /// <summary>批量追加数据点。</summary>
    public void Append(IEnumerable<TX> xValues, IEnumerable<TY> yValues)
    {
        ArgumentNullException.ThrowIfNull(xValues);
        ArgumentNullException.ThrowIfNull(yValues);

        var xItems = xValues as List<TX> ?? xValues.ToList();
        var yItems = yValues as List<TY> ?? yValues.ToList();

        if (xItems.Count != yItems.Count)
            throw new ArgumentException("XValues and YValues must contain the same number of items.");

        if (xItems.Count == 0)
            return;

        lock (_lock)
        {
            int previousCount = _xRingBuffer.Count;

            if (_fifoCapacity > 0 && previousCount + xItems.Count > _fifoCapacity)
            {
                int overflowCount = previousCount + xItems.Count - _fifoCapacity;
                _headIndex += overflowCount;
            }

            _xRingBuffer.AddRange(CollectionsMarshal.AsSpan(xItems));
            _yRingBuffer.AddRange(CollectionsMarshal.AsSpan(yItems));

            InvalidateRangeCaches();
        }

        OnRangeChanged();
        OnDataChanged(DataSeriesChangeEventArgs.Reset());
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }

    /// <inheritdoc/>
    protected override void OnFifoCapacityChanged()
    {
        _xRingBuffer.SetFixedCapacity(_fifoCapacity);
        _yRingBuffer.SetFixedCapacity(_fifoCapacity);
        
        if (_fifoCapacity > 0)
        {
            int currentCount = _xRingBuffer.Count;
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

    private void UpdateRangeForAppend(TX appendedX, TY appendedY)
    {
        if (Count == 1)
        {
            double xVal = _xToDouble(appendedX);
            double yVal = _yToDouble(appendedY);
            _xRange = NormalizeRange(xVal, xVal);
            _yRange = NormalizeRange(yVal, yVal);
            _isXRangeDirty = false;
            _isYRangeDirty = false;
            return;
        }

        if (!_isXRangeDirty)
        {
            double value = _xToDouble(appendedX);
            _xRange = new DataRange(
                Math.Min(_xRange.Min, value),
                Math.Max(_xRange.Max, value));
        }

        if (!_isYRangeDirty)
        {
            double value = _yToDouble(appendedY);
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
            int count = _xRingBuffer.Count;
            if (count == 0)
            {
                _xRange = new DataRange(0, 1);
                _isXRangeDirty = false;
                return;
            }

            var access = _xRingBuffer.AcquireReadAccess();
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < access.Count; i++)
            {
                double value = _xToDouble(access[i]);
                if (value < min) min = value;
                if (value > max) max = value;
            }

            _xRange = new DataRange(min, max);
            _isXRangeDirty = false;
        }
    }

    private void EnsureYRange()
    {
        if (!_isYRangeDirty)
            return;

        lock (_lock)
        {
            int count = _yRingBuffer.Count;
            if (count == 0)
            {
                _yRange = new DataRange(-1, 1);
                _isYRangeDirty = false;
                return;
            }

            var access = _yRingBuffer.AcquireReadAccess();
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
