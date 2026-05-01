using System.Collections.Specialized;
using Cheari.Controls.Core;
using Cheari.Controls.Data;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 将内部快照适配为只读 IDataSeries，以复用现有下采样策略。
/// </summary>
internal sealed class SnapshotDataSeriesAdapter : IDataSeries
{
    private readonly DataFrame _frame;

    public SnapshotDataSeriesAdapter(DataFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
    }

    public int Count => _frame.Count;

    public int FifoCapacity
    {
        get => 0;
        set => throw new NotSupportedException("Snapshot data is read-only.");
    }

    public int Version => _frame.Version;

    public double GetX(int index) => _frame.XValues?[index] ?? 0;

    public double GetY(int index) => _frame.YValues?[index] ?? (_frame.CloseValues?[index] ?? 0);

    public int CopyXValues(Span<double> destination)
    {
        if (_frame.XValues == null)
            return 0;

        int count = Math.Min(_frame.Count, destination.Length);
        for (int i = 0; i < count; i++)
            destination[i] = _frame.XValues[i];

        return count;
    }

    public int CopyYValues(Span<double> destination)
    {
        var values = _frame.YValues ?? _frame.CloseValues;
        if (values == null)
            return 0;

        int count = Math.Min(_frame.Count, destination.Length);
        for (int i = 0; i < count; i++)
            destination[i] = values[i];

        return count;
    }

    public DataRange XRange => _frame.XRange;

    public DataRange YRange => _frame.YRange;

    public event EventHandler? RangeChanged
    {
        add { }
        remove { }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DataSeriesChangeEventArgs>? DataChanged
    {
        add { }
        remove { }
    }
}
