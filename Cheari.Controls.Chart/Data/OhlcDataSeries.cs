using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Cheari.Controls.Core;

namespace Cheari.Controls.Data;

/// <summary>
/// OHLC（开高低收）数据系列接口，提供K线图所需的四值数据访问。
/// </summary>
public interface IOhlcDataSeries : IDataSeries
{
    /// <summary>获取指定索引处的开盘价。</summary>
    double GetOpen(int index);
    /// <summary>获取指定索引处的最高价。</summary>
    double GetHigh(int index);
    /// <summary>获取指定索引处的最低价。</summary>
    double GetLow(int index);
    /// <summary>获取指定索引处的收盘价。</summary>
    double GetClose(int index);

    /// <summary>将开盘价复制到目标跨度。</summary>
    int CopyOpenValues(Span<double> destination);
    /// <summary>将最高价复制到目标跨度。</summary>
    int CopyHighValues(Span<double> destination);
    /// <summary>将最低价复制到目标跨度。</summary>
    int CopyLowValues(Span<double> destination);
    /// <summary>将收盘价复制到目标跨度。</summary>
    int CopyCloseValues(Span<double> destination);
}

/// <summary>
/// OHLC 数据系列实现，用于K线图的数据存储。
/// </summary>
public class OhlcDataSeries : DataSeriesBase, IOhlcDataSeries
{
    private readonly RingBuffer<double> _xBuffer = new();
    private readonly RingBuffer<double> _openBuffer = new();
    private readonly RingBuffer<double> _highBuffer = new();
    private readonly RingBuffer<double> _lowBuffer = new();
    private readonly RingBuffer<double> _closeBuffer = new();
    private DataRange _xRange;
    private DataRange _yRange;
    private bool _isXRangeDirty = true;
    private bool _isYRangeDirty = true;

    /// <inheritdoc/>
    public override int Count => _xBuffer.Count;

    /// <inheritdoc/>
    public override int Version => _xBuffer.Version;

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

    /// <inheritdoc/>
    public override double GetX(int index) => _xBuffer[index];

    /// <inheritdoc/>
    public override double GetY(int index) => _closeBuffer[index];

    /// <inheritdoc/>
    public override int CopyXValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _xBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public override int CopyYValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _closeBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public int CopyOpenValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _openBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public int CopyHighValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _highBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public int CopyLowValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _lowBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public int CopyCloseValues(Span<double> destination)
    {
        lock (_lock)
        {
            return _closeBuffer.CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    internal override DataFrame CreateFrameCore()
    {
        lock (_lock)
        {
            int count = _xBuffer.Count;
            if (count == 0)
                return DataFrame.Empty;

            var xAccess = _xBuffer.AcquireReadAccess();
            var openAccess = _openBuffer.AcquireReadAccess();
            var highAccess = _highBuffer.AcquireReadAccess();
            var lowAccess = _lowBuffer.AcquireReadAccess();
            var closeAccess = _closeBuffer.AcquireReadAccess();

            var xArr = new float[count];
            var openArr = new float[count];
            var highArr = new float[count];
            var lowArr = new float[count];
            var closeArr = new float[count];

            for (int i = 0; i < count; i++)
            {
                xArr[i] = (float)xAccess[i];
                openArr[i] = (float)openAccess[i];
                highArr[i] = (float)highAccess[i];
                lowArr[i] = (float)lowAccess[i];
                closeArr[i] = (float)closeAccess[i];
            }

            return new DataFrame
            {
                Count = count,
                Version = _xBuffer.Version,
                XRange = XRange,
                YRange = YRange,
                Type = DataFrameType.Ohlc,
                XValues = xArr,
                OpenValues = openArr,
                HighValues = highArr,
                LowValues = lowArr,
                CloseValues = closeArr
            };
        }
    }

    /// <inheritdoc/>
    public double GetOpen(int index) => _openBuffer[index];

    /// <inheritdoc/>
    public double GetHigh(int index) => _highBuffer[index];

    /// <inheritdoc/>
    public double GetLow(int index) => _lowBuffer[index];

    /// <inheritdoc/>
    public double GetClose(int index) => _closeBuffer[index];

    /// <summary>追加一条K线数据。</summary>
    public void Append(double x, double open, double high, double low, double close)
    {
        int appendedIndex;
        bool overflow = false;

        lock (_lock)
        {
            appendedIndex = _xBuffer.Count;

            if (_fifoCapacity > 0 && appendedIndex >= _fifoCapacity)
            {
                overflow = true;
                _headIndex++;
            }

            _xBuffer.Add(x);
            _openBuffer.Add(open);
            _highBuffer.Add(high);
            _lowBuffer.Add(low);
            _closeBuffer.Add(close);
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
            UpdateRangeForAppend(x, open, high, low, close);
            OnRangeChanged();
            OnDataChanged(DataSeriesChangeEventArgs.Append(appendedIndex, 1));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, close, appendedIndex));
        }
    }

    /// <summary>批量追加K线数据。</summary>
    public void AppendRange(ReadOnlySpan<double> xValues, ReadOnlySpan<double> openValues,
        ReadOnlySpan<double> highValues, ReadOnlySpan<double> lowValues, ReadOnlySpan<double> closeValues)
    {
        if (xValues.Length != openValues.Length || xValues.Length != highValues.Length
            || xValues.Length != lowValues.Length || xValues.Length != closeValues.Length)
            throw new ArgumentException("All arrays must have the same length.");

        if (xValues.Length == 0)
            return;

        lock (_lock)
        {
            int previousCount = _xBuffer.Count;

            if (_fifoCapacity > 0 && previousCount + xValues.Length > _fifoCapacity)
            {
                int overflowCount = previousCount + xValues.Length - _fifoCapacity;
                _headIndex += overflowCount;
            }

            _xBuffer.AddRange(xValues);
            _openBuffer.AddRange(openValues);
            _highBuffer.AddRange(highValues);
            _lowBuffer.AddRange(lowValues);
            _closeBuffer.AddRange(closeValues);
        }

        InvalidateRangeCaches();
        OnRangeChanged();
        OnDataChanged(DataSeriesChangeEventArgs.Reset());
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <inheritdoc/>
    protected override void OnFifoCapacityChanged()
    {
        _xBuffer.SetFixedCapacity(_fifoCapacity);
        _openBuffer.SetFixedCapacity(_fifoCapacity);
        _highBuffer.SetFixedCapacity(_fifoCapacity);
        _lowBuffer.SetFixedCapacity(_fifoCapacity);
        _closeBuffer.SetFixedCapacity(_fifoCapacity);
        
        if (_fifoCapacity > 0)
        {
            int currentCount = _xBuffer.Count;
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

    private void UpdateRangeForAppend(double x, double open, double high, double low, double close)
    {
        if (Count == 1)
        {
            _xRange = new DataRange(x, x);
            _yRange = new DataRange(low, high);
            _isXRangeDirty = false;
            _isYRangeDirty = false;
            return;
        }

        if (!_isXRangeDirty)
            _xRange = new DataRange(Math.Min(_xRange.Min, x), Math.Max(_xRange.Max, x));

        if (!_isYRangeDirty)
            _yRange = new DataRange(Math.Min(_yRange.Min, low), Math.Max(_yRange.Max, high));
    }

    private void EnsureXRange()
    {
        if (!_isXRangeDirty)
            return;

        lock (_lock)
        {
            int count = _xBuffer.Count;
            if (count == 0)
            {
                _xRange = new DataRange(0, 1);
                _isXRangeDirty = false;
                return;
            }

            var access = _xBuffer.AcquireReadAccess();
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < access.Count; i++)
            {
                double v = access[i];
                if (v < min) min = v;
                if (v > max) max = v;
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
            int count = _lowBuffer.Count;
            if (count == 0)
            {
                _yRange = new DataRange(-1, 1);
                _isYRangeDirty = false;
                return;
            }

            var lowAccess = _lowBuffer.AcquireReadAccess();
            var highAccess = _highBuffer.AcquireReadAccess();
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < lowAccess.Count; i++)
            {
                double lo = lowAccess[i];
                double hi = highAccess[i];
                if (lo < min) min = lo;
                if (hi > max) max = hi;
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
}
