using System.Runtime.CompilerServices;

namespace Cheari.Controls.Data;

internal sealed class RingBuffer<T>
{
    private T[] _buffer;
    private int _head;
    private int _count;
    private int _version;
    private int _fixedCapacity;
    private bool _isFixedCapacity;
    private readonly object _lock = new object();

    private const int DefaultCapacity = 4;

    public ref struct ReadAccess
    {
        private readonly T[] _buffer;
        private readonly int _head;
        private readonly int _count;
        private readonly int _mask;

        internal ReadAccess(T[] buffer, int head, int count)
        {
            _buffer = buffer;
            _head = head;
            _count = count;
            _mask = buffer.Length - 1;
        }

        public int Count => _count;

        public T this[int index] => _buffer[(_head + index) & _mask];

        public ReadOnlySpan<T> FirstSpan => _buffer.AsSpan(_head, Math.Min(_count, _buffer.Length - _head));

        public ReadOnlySpan<T> SecondSpan => _count > (_buffer.Length - _head)
            ? _buffer.AsSpan(0, _count - (_buffer.Length - _head))
            : ReadOnlySpan<T>.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Span<T> destination)
        {
            int firstChunk = Math.Min(_count, _buffer.Length - _head);
            _buffer.AsSpan(_head, firstChunk).CopyTo(destination);
            if (_count > firstChunk)
                _buffer.AsSpan(0, _count - firstChunk).CopyTo(destination.Slice(firstChunk));
        }
    }

    public RingBuffer()
    {
        _buffer = Array.Empty<T>();
        _head = 0;
        _count = 0;
        _version = 0;
    }

    public RingBuffer(int capacity, int fixedCapacity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(fixedCapacity);
        _buffer = capacity > 0 ? new T[capacity] : Array.Empty<T>();
        _head = 0;
        _count = 0;
        _version = 0;
        _isFixedCapacity = fixedCapacity > 0;
        _fixedCapacity = _isFixedCapacity ? fixedCapacity : 0;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public bool IsFixedCapacity
    {
        get
        {
            lock (_lock)
            {
                return _isFixedCapacity;
            }
        }
    }

    public int FixedCapacityValue
    {
        get
        {
            lock (_lock)
            {
                return _fixedCapacity;
            }
        }
    }

    public int Version
    {
        get
        {
            lock (_lock)
            {
                return _version;
            }
        }
    }

    public T this[int index]
    {
        get
        {
            lock (_lock)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
                return _buffer[(_head + index) & (_buffer.Length - 1)];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        lock (_lock)
        {
            if (_isFixedCapacity)
            {
                if (_count == _fixedCapacity)
                {
                    int tail = (_head + _count) & (_buffer.Length - 1);
                    _buffer[tail] = item;
                    _head = (_head + 1) & (_buffer.Length - 1);
                }
                else
                {
                    EnsureCapacity(_count + 1);
                    int tail = (_head + _count) & (_buffer.Length - 1);
                    _buffer[tail] = item;
                    _count++;
                }
            }
            else
            {
                EnsureCapacity(_count + 1);
                int tail = (_head + _count) & (_buffer.Length - 1);
                _buffer[tail] = item;
                _count++;
            }
            _version++;
        }
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
            return;

        lock (_lock)
        {
            if (_isFixedCapacity)
            {
                int overflow = _count + items.Length - _fixedCapacity;
                if (overflow > 0)
                {
                    _head = (_head + overflow) & (_buffer.Length - 1);
                    _count = _fixedCapacity;
                }

                int mask = _buffer.Length - 1;
                int tail = (_head + _count) & mask;
                int remaining = items.Length;
                int srcIndex = 0;

                int firstChunk = Math.Min(remaining, _buffer.Length - tail);
                items.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(tail, firstChunk));
                remaining -= firstChunk;
                srcIndex += firstChunk;

                if (remaining > 0)
                {
                    items.Slice(srcIndex, remaining).CopyTo(_buffer.AsSpan(0, remaining));
                }

                _count += items.Length;
                if (_count > _fixedCapacity)
                    _count = _fixedCapacity;
            }
            else
            {
                EnsureCapacity(_count + items.Length);

                int mask = _buffer.Length - 1;
                int tail = (_head + _count) & mask;
                int remaining = items.Length;
                int srcIndex = 0;

                int firstChunk = Math.Min(remaining, _buffer.Length - tail);
                items.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(tail, firstChunk));
                remaining -= firstChunk;
                srcIndex += firstChunk;

                if (remaining > 0)
                {
                    items.Slice(srcIndex, remaining).CopyTo(_buffer.AsSpan(0, remaining));
                }

                _count += items.Length;
            }
            _version++;
        }
    }

    public void SetFixedCapacity(int fixedCapacity)
    {
        lock (_lock)
        {
            if (fixedCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(fixedCapacity), "Fixed capacity must be non-negative.");

            bool wasFixed = _isFixedCapacity;
            int oldFixedCapacity = _fixedCapacity;

            _isFixedCapacity = fixedCapacity > 0;
            _fixedCapacity = _isFixedCapacity ? fixedCapacity : 0;

            if (_isFixedCapacity)
            {
                if (_count > _fixedCapacity)
                {
                    int overflow = _count - _fixedCapacity;
                    _head = (_head + overflow) & (_buffer.Length - 1);
                    _count = _fixedCapacity;
                }

                if (_buffer.Length < _fixedCapacity)
                {
                    int newBufferSize = _fixedCapacity;
                    while ((newBufferSize & (newBufferSize - 1)) != 0)
                        newBufferSize++;

                    var newBuffer = new T[newBufferSize];
                    if (_count > 0)
                    {
                        int firstChunk = Math.Min(_count, _buffer.Length - _head);
                        Array.Copy(_buffer, _head, newBuffer, 0, firstChunk);

                        if (_count > firstChunk)
                        {
                            Array.Copy(_buffer, 0, newBuffer, firstChunk, _count - firstChunk);
                        }
                    }
                    _buffer = newBuffer;
                    _head = 0;
                }
            }
            else if (wasFixed && oldFixedCapacity > 0)
            {
            }

            _version++;
        }
    }

    public ReadAccess AcquireReadAccess()
    {
        lock (_lock)
        {
            return new ReadAccess(_buffer, _head, _count);
        }
    }

    public int CopyTo(Span<T> destination)
    {
        lock (_lock)
        {
            if (_count == 0)
                return 0;

            int firstChunk = Math.Min(_count, _buffer.Length - _head);
            _buffer.AsSpan(_head, firstChunk).CopyTo(destination);
            if (_count > firstChunk)
                _buffer.AsSpan(0, _count - firstChunk).CopyTo(destination.Slice(firstChunk));
            return _count;
        }
    }

    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0)
                return Array.Empty<T>();

            var result = new T[_count];
            CopyToUnsafe(result);
            return result;
        }
    }

    public void CopyTo(T[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length < _count)
            throw new ArgumentException("Destination array is too small.");

        lock (_lock)
        {
            CopyToUnsafe(destination);
        }
    }

    private void CopyToUnsafe(T[] destination)
    {
        int mask = _buffer.Length - 1;
        int firstChunk = Math.Min(_count, _buffer.Length - _head);
        Array.Copy(_buffer, _head, destination, 0, firstChunk);

        if (_count > firstChunk)
        {
            Array.Copy(_buffer, 0, destination, firstChunk, _count - firstChunk);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_count == 0)
                return;

            int mask = _buffer.Length - 1;
            for (int i = 0; i < _count; i++)
            {
                _buffer[(_head + i) & mask] = default!;
            }

            _head = 0;
            _count = 0;
            _version++;
        }
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer.Length >= required)
            return;

        int newCapacity = _buffer.Length == 0
            ? DefaultCapacity
            : _buffer.Length;

        while (newCapacity < required)
            newCapacity *= 2;

        var newBuffer = new T[newCapacity];

        if (_count > 0)
        {
            int firstChunk = Math.Min(_count, _buffer.Length - _head);
            Array.Copy(_buffer, _head, newBuffer, 0, firstChunk);

            if (_count > firstChunk)
            {
                Array.Copy(_buffer, 0, newBuffer, firstChunk, _count - firstChunk);
            }
        }

        _buffer = newBuffer;
        _head = 0;
    }
}