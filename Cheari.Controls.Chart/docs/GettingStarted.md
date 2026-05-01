# Cheari.Controls.Chart 入门指南

本文档基于当前真实 API，使用 ViewModel 绑定方式创建 `Series`、`Modifiers` 和轴集合。不要再使用旧版本里那种把泛型数据系列直接内联进 XAML 的写法。

## 1. 最小可运行模型

```csharp
using System.Collections.ObjectModel;
using System.Windows.Media;
using Cheari.Controls;
using Cheari.Controls.Axes;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Modifiers;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Types;

public sealed class ChartPageViewModel
{
    public ObservableCollection<IAxis> XAxes { get; } = [];
    public ObservableCollection<IAxis> YAxes { get; } = [];
    public ObservableCollection<IRenderableSeries> Series { get; } = [];
    public ObservableCollection<IChartModifier> Modifiers { get; } =
    [
        new PanModifier(),
        new ZoomModifier()
    ];

    public ChartRendererPreference RendererPreference { get; set; } = ChartRendererPreference.Auto;

    public ChartPageViewModel()
    {
        XAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultXAxisId,
            Placement = AxisPlacement.Bottom,
            VisibleRange = new DataRange(0, 10),
            AutoRange = true,
            Title = "Time"
        });

        YAxes.Add(new LinearAxis
        {
            Id = Chart.DefaultYAxisId,
            Placement = AxisPlacement.Left,
            VisibleRange = new DataRange(-1.5, 1.5),
            AutoRange = true,
            Title = "Value"
        });

        var dataSeries = new UniformDataSeries<double, double>(
            index => index * 0.05,
            x => x);

        for (int i = 0; i < 200; i++)
        {
            double x = i * 0.05;
            dataSeries.Append(Math.Sin(x));
        }

        Series.Add(new LineRenderableSeries
        {
            Title = "Signal",
            Stroke = Colors.Cyan,
            StrokeThickness = 1.5,
            DataSeries = dataSeries
        });
    }
}
```

## 2. 绑定到 Chart

```xaml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:cheari="http://schemas.cheari.com/controls">
    <Grid>
        <cheari:Chart
            Series="{Binding Series}"
            XAxes="{Binding XAxes}"
            YAxes="{Binding YAxes}"
            Modifiers="{Binding Modifiers}"
            RendererPreference="{Binding RendererPreference}"
            PlotAreaBackground="#05070B"
            Background="#1A2238"
            EnableAntialiasing="True" />
    </Grid>
</Window>
```

`Chart.Series`、`Chart.XAxes`、`Chart.YAxes`、`Chart.Modifiers` 都保持为可绑定集合型 DP，适合标准 MVVM。

## 3. 渲染后端策略

### Auto

```csharp
RendererPreference = ChartRendererPreference.Auto;
```

- 优先尝试 `D3D11`
- 初始化失败时自动切换到 `Gdi`
- `ActualRendererBackend` 可用于确认最终落到哪个后端

### HardwareOnly

```csharp
RendererPreference = ChartRendererPreference.HardwareOnly;
```

- 禁止自动回退
- 如果 D3D11 初始化失败，`RenderError` 会保留错误信息
- `ActualRendererBackend` 会保持 `Unknown`

### SoftwareOnly

```csharp
RendererPreference = ChartRendererPreference.SoftwareOnly;
```

- 跳过 D3D 初始化
- 直接使用软件渲染链
- 适合远程桌面、虚拟机或兼容性回归验证

## 4. 默认交互

如果没有显式绑定 `Modifiers`，`Chart` 会自动注入：

- `PanModifier`
- `ZoomModifier`

显式提供集合时，控件会按集合生命周期对称调用 `OnAttached()` / `OnDetached()`。

## 5. 常见数据系列

### `UniformDataSeries`

适合等间隔采样，例如传感器或实时波形。

```csharp
var dataSeries = new UniformDataSeries<double, double>(
    index => index * 0.02,
    x => x);

dataSeries.Append(1.0);
dataSeries.Append(0.8);
dataSeries.Append(0.3);
```

### `VariableDataSeries`

适合非均匀横坐标。

```csharp
var dataSeries = new VariableDataSeries<double, double>(x => x);
dataSeries.Append(0.0, 2.0);
dataSeries.Append(1.7, 3.5);
dataSeries.Append(4.2, -1.0);
```

### `OhlcDataSeries`

适合 K 线或金融时间序列。

```csharp
var dataSeries = new OhlcDataSeries();
dataSeries.Append(0, 100, 110, 95, 105);
dataSeries.Append(1, 105, 112, 101, 102);
```

## 6. 运行时状态

`Chart` 提供以下常用状态：

- `XRange` / `YRange`：当前默认轴可见范围
- `Fps`：启用 `ShowFps` 后的实时帧率
- `ActualRendererBackend`：当前后端
- `RenderError` / `HasRenderError`：错误诊断信息

## 7. Demo 对照

`Demo` 项目包含以下建议优先查看的页面：

- `自动后端`
- `强制软件`
- `硬件诊断`
- `多轴`
- `FIFO演示`
- `蜡烛图`

这些页面与当前公开 API 保持一致，可直接作为集成参考。
