using Cheari.Controls.Axes;
using Cheari.Controls.Core;

namespace Cheari.Controls.Synchronization;

/// <summary>轴组接口，用于多个图表之间的坐标轴联动。</summary>
public interface IAxisGroup
{
    /// <summary>轴组ID。</summary>
    string Id { get; }
    /// <summary>注册图表到轴组。</summary>
    void Register(Chart chart);
    /// <summary>从轴组移除图表。</summary>
    void Unregister(Chart chart);
    /// <summary>通知轴组某个图表范围已变更。</summary>
    void NotifyRangeChanged(Chart source, string axisId, DataRange newRange);
}
