using System.Windows.Controls;
using Cheari.Controls.Rendering.Context;

namespace Cheari.Controls.Modifiers;

/// <summary>修饰器基类，提供默认空实现和叠加画布访问。</summary>
public abstract class ChartModifierBase : IChartModifier
{
    private protected IRenderContext? Context { get; private set; }

    internal Canvas? OverlayCanvas { get; set; }

    /// <inheritdoc />
    public virtual void OnAttached() { }
    /// <inheritdoc />
    public virtual void OnDetached() { }
    /// <inheritdoc />
    public virtual void SetContext(IRenderContext context)
    {
        Context = context;
    }
    /// <inheritdoc />
    public virtual void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e) { }
    /// <inheritdoc />
    public virtual void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e) { }
    /// <inheritdoc />
    public virtual void OnMouseMove(System.Windows.Input.MouseEventArgs e) { }
    /// <inheritdoc />
    public virtual void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e) { }
}
