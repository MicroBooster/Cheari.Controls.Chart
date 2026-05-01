using System.Collections;

namespace Cheari.Controls.Data;

internal sealed class LiveArrayReadOnlyList<T> : IReadOnlyList<T>
{
    private readonly Func<T[]> _arrayAccessor;

    public LiveArrayReadOnlyList(Func<T[]> arrayAccessor)
    {
        ArgumentNullException.ThrowIfNull(arrayAccessor);
        _arrayAccessor = arrayAccessor;
    }

    public T this[int index] => _arrayAccessor()[index];

    public int Count => _arrayAccessor().Length;

    public IEnumerator<T> GetEnumerator()
    {
        var snapshot = _arrayAccessor();
        for (int i = 0; i < snapshot.Length; i++)
            yield return snapshot[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class RingBufferReadOnlyList<T> : IReadOnlyList<T>
{
    private readonly RingBuffer<T> _buffer;
    private T[]? _cachedArray;
    private int _cachedVersion = -1;

    public RingBufferReadOnlyList(RingBuffer<T> buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
    }

    public T this[int index]
    {
        get
        {
            EnsureCache();
            return _cachedArray![index];
        }
    }

    public int Count => _buffer.Count;

    private void EnsureCache()
    {
        int currentVersion = _buffer.Version;
        if (_cachedVersion != currentVersion)
        {
            _cachedArray = _buffer.ToArray();
            _cachedVersion = currentVersion;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        EnsureCache();
        for (int i = 0; i < _cachedArray!.Length; i++)
            yield return _cachedArray[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// 基于委托的生成式只读列表视图。
/// 元素按需生成，适用于计算密集但访问频率低的场景。
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
internal sealed class GeneratedReadOnlyList<T> : IReadOnlyList<T>
{
    private readonly Func<int> _countAccessor;
    private readonly Func<int, T> _itemAccessor;

    /// <summary>
    /// 初始化 GeneratedReadOnlyList 实例。
    /// </summary>
    /// <param name="countAccessor">数量访问器委托</param>
    /// <param name="itemAccessor">元素访问器委托</param>
    public GeneratedReadOnlyList(Func<int> countAccessor, Func<int, T> itemAccessor)
    {
        ArgumentNullException.ThrowIfNull(countAccessor);
        ArgumentNullException.ThrowIfNull(itemAccessor);
        _countAccessor = countAccessor;
        _itemAccessor = itemAccessor;
    }

    /// <summary>
    /// 获取指定索引处的元素。
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>指定索引处的元素</returns>
    public T this[int index] => _itemAccessor(index);

    /// <summary>
    /// 获取元素数量。
    /// </summary>
    public int Count => _countAccessor();

    /// <summary>
    /// 返回枚举器。
    /// </summary>
    /// <returns>枚举器</returns>
    public IEnumerator<T> GetEnumerator()
    {
        int count = _countAccessor();
        for (int i = 0; i < count; i++)
            yield return _itemAccessor(i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}