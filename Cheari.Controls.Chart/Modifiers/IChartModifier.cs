using Cheari.Controls.Rendering.Context;

namespace Cheari.Controls.Modifiers;

/// <summary>
/// 图表修饰器接口，定义了与用户交互的事件处理方法。
/// 修饰器可以实现平移、缩放、选择等交互功能。
/// </summary>
/// <remarks>
/// <para>修饰器通过 <see cref="SetContext"/> 接收 <see cref="IRenderContext"/>，
/// 可以访问视口信息、坐标轴和坐标转换功能。</para>
/// <para>框架提供了以下内置修饰器：</para>
/// <list type="bullet">
///   <item><see cref="PanModifier"/> — 鼠标拖拽平移</item>
///   <item><see cref="ZoomModifier"/> — 鼠标滚轮缩放</item>
/// </list>
/// <para>自定义修饰器只需实现本接口，然后添加到 <see cref="Chart.Modifiers"/> 集合即可。</para>
/// </remarks>
/// <example>
/// <code>
/// var chart = new Chart();
/// chart.Modifiers = new ObservableCollection&lt;IChartModifier&gt;
/// {
///     new PanModifier(),
///     new ZoomModifier { ZoomStep = 0.15 }
/// };
/// </code>
/// </example>
public interface IChartModifier
{
    /// <summary>
    /// 当修饰器附加到图表时调用。
    /// </summary>
    void OnAttached();

    /// <summary>
    /// 当修饰器从图表分离时调用。
    /// </summary>
    void OnDetached();

    /// <summary>
    /// 设置渲染上下文，用于访问图表的可视区域和坐标轴信息。
    /// </summary>
    /// <param name="context">渲染上下文</param>
    void SetContext(IRenderContext context);

    /// <summary>
    /// 处理鼠标按下事件。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e);

    /// <summary>
    /// 处理鼠标释放事件。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e);

    /// <summary>
    /// 处理鼠标移动事件。
    /// </summary>
    /// <param name="e">鼠标事件参数</param>
    void OnMouseMove(System.Windows.Input.MouseEventArgs e);

    /// <summary>
    /// 处理鼠标滚轮事件。
    /// </summary>
    /// <param name="e">鼠标滚轮事件参数</param>
    void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e);
}