using System.Windows;
using System.Windows.Media;

namespace Cheari.Controls.Annotations;

/// <summary>标注坐标模式。</summary>
public enum AnnotationCoordinateMode
{
    /// <summary>使用数据坐标系。</summary>
    Data,
    /// <summary>使用相对坐标系。</summary>
    Relative
}

/// <summary>标注接口，定义图表标注的契约。</summary>
public interface IAnnotation
{
    /// <summary>标注点1的X坐标。</summary>
    double X1 { get; set; }
    /// <summary>标注点1的Y坐标。</summary>
    double Y1 { get; set; }
    /// <summary>标注点2的X坐标。</summary>
    double X2 { get; set; }
    /// <summary>标注点2的Y坐标。</summary>
    double Y2 { get; set; }
    /// <summary>标注文本内容。</summary>
    string? Text { get; set; }
    /// <summary>标注填充画刷。</summary>
    Brush Fill { get; set; }
    /// <summary>标注描边画笔。</summary>
    Pen Stroke { get; set; }
    /// <summary>标注坐标模式。</summary>
    AnnotationCoordinateMode CoordinateMode { get; set; }
    /// <summary>标注是否被选中。</summary>
    bool IsSelected { get; set; }
    /// <summary>标注是否可见。</summary>
    bool IsVisible { get; set; }
    /// <summary>标注是否锁定。</summary>
    bool IsLocked { get; set; }

    /// <summary>创建标注的可视化元素。</summary>
    FrameworkElement CreateVisual();
    /// <summary>更新标注的可视化元素。</summary>
    void UpdateVisual(FrameworkElement element);
}
