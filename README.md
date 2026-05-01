# Cheari.Controls.Chart

`Cheari.Controls.Chart` 是一个面向 WPF 的高性能图表控件，保留 `D3D11 + GDI` 双渲染架构，并支持在运行时根据环境切换后端。

## 当前能力

- 支持 `Line`、`Scatter`、`Bar`、`Area`、`OHLC`
- 支持 `LinearAxis`、`LogAxis`、`DateTimeAxis` 与多轴布局
- 默认交互为 `PanModifier + ZoomModifier`
- 内置后台快照调度，优先复用 `RenderLoop` 缓存，减少每帧同步抓拍
- 支持三种渲染策略：
  - `Auto`：优先 D3D11，失败后回退到 GDI
  - `HardwareOnly`：只允许 D3D11，失败时保留错误状态
  - `SoftwareOnly`：直接使用 GDI 软件渲染

## 快速开始

推荐使用 ViewModel 绑定方式创建 `Series`、`Modifiers` 和坐标轴集合。

```xaml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:cheari="http://schemas.cheari.com/controls">
    <cheari:Chart
        Series="{Binding Series}"
        XAxes="{Binding XAxes}"
        YAxes="{Binding YAxes}"
        Modifiers="{Binding Modifiers}"
        RendererPreference="{Binding RendererPreference}" />
</Window>
```

```csharp
using System.Collections.ObjectModel;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;
using System.Windows.Media;

var xAxis = new LinearAxis
{
    Id = Chart.DefaultXAxisId,
    Placement = AxisPlacement.Bottom,
    VisibleRange = new DataRange(0, 10),
    AutoRange = true
};

var yAxis = new LinearAxis
{
    Id = Chart.DefaultYAxisId,
    Placement = AxisPlacement.Left,
    VisibleRange = new DataRange(-1.5, 1.5),
    AutoRange = true
};

var dataSeries = new UniformDataSeries<double, double>(
    index => index * 0.05,
    x => x);

for (int i = 0; i < 200; i++)
{
    double x = i * 0.05;
    dataSeries.Append(Math.Sin(x));
}

var viewModel = new
{
    XAxes = new ObservableCollection<IAxis> { xAxis },
    YAxes = new ObservableCollection<IAxis> { yAxis },
    Series = new ObservableCollection<IRenderableSeries>
    {
        new LineRenderableSeries
        {
            Title = "Signal",
            Stroke = Colors.Cyan,
            StrokeThickness = 1.5,
            DataSeries = dataSeries
        }
    },
    Modifiers = new ObservableCollection<IChartModifier>
    {
        new PanModifier(),
        new ZoomModifier()
    },
    RendererPreference = ChartRendererPreference.Auto
};
```

## 运行时诊断

`Chart` 公开了两个与渲染后端相关的状态：

- `RendererPreference`：显式指定后端策略
- `ActualRendererBackend`：查看当前实际使用的是 `D3D11` 还是 `Gdi`

`HardwareOnly` 模式下如果初始化失败，`RenderError` 会保留错误信息，且不会自动回退。

## Demo

`Demo` 项目包含以下后端相关场景：

- `自动后端`：默认 `Auto` 行为
- `强制软件`：固定走 `SoftwareOnly`
- `硬件诊断`：固定走 `HardwareOnly`，观察失败时的错误展示

## 公开扩展面

建议直接依赖以下类型：

- `Chart`
- 坐标轴类型：`LinearAxis`、`LogAxis`、`DateTimeAxis`
- 数据系列类型：`VariableDataSeries`、`UniformDataSeries`、`OhlcDataSeries`
- 可渲染系列类型：`LineRenderableSeries`、`ScatterRenderableSeries`、`BarRenderableSeries`、`AreaRenderableSeries`、`OhlcRenderableSeries`
- `IChartModifier`
- `IRenderContext`

渲染命令、GPU 管线、后端实现和快照调度器都属于内部实现细节。
