# Cheari.Controls.Chart 架构文档

## 概述

Cheari.Controls.Chart 是一个基于 WPF 的高性能图表控件库，支持 DirectX 11 硬件加速渲染，提供流畅的数据可视化体验。

## 项目结构

```
Cheari.Controls.Chart/
├── Core/                    # 核心基础类型
│   ├── DataRange.cs         # 数据范围结构体
│   ├── LineStyle.cs         # 线条样式枚举
│   └── MarkerType.cs        # 标记点类型枚举
├── Axes/                    # 轴系统
│   ├── IAxis.cs             # 轴接口
│   ├── AxisBase.cs          # 轴基类
│   ├── LinearAxis.cs        # 线性轴
│   ├── LogAxis.cs           # 对数轴
│   ├── DateTimeAxis.cs      # 日期时间轴
│   ├── AxisPlacement.cs     # 轴位置枚举
│   ├── AxisScale.cs         # 轴刻度类型枚举
│   ├── TickInfo.cs          # 刻度信息结构体
│   ├── Controls/            # 轴控件
│   │   ├── AxisControl.cs
│   │   ├── AxisPanel.cs
│   │   ├── AxisItemsControl.cs
│   │   └── GridLinesControl.cs
│   └── CoordinateMappers/   # 坐标映射器
│       ├── ICoordinateMapper.cs
│       ├── LinearCoordinateMapper.cs
│       ├── LogCoordinateMapper.cs
│       └── DateTimeCoordinateMapper.cs
├── Data/                    # 数据系列
│   ├── IDataSeries.cs       # 数据系列接口
│   ├── VariableDataSeries.cs
│   ├── UniformDataSeries.cs
│   ├── OhlcDataSeries.cs
│   ├── RingBuffer.cs        # 环形缓冲区
│   ├── ReadOnlyListViews.cs
│   ├── DataSeriesChangeType.cs
│   └── DataSeriesChangeEventArgs.cs
├── Series/                  # 渲染系列
│   ├── IRenderableSeries.cs
│   ├── Types/               # 系列类型
│   │   ├── LineRenderableSeries.cs
│   │   ├── ScatterRenderableSeries.cs
│   │   ├── BarRenderableSeries.cs
│   │   ├── AreaRenderableSeries.cs
│   │   └── OhlcRenderableSeries.cs
│   └── Renderers/           # 系列渲染器
│       ├── LineSeriesRenderer.cs
│       ├── ScatterSeriesRenderer.cs
│       ├── BarSeriesRenderer.cs
│       ├── AreaSeriesRenderer.cs
│       └── OhlcSeriesRenderer.cs
├── Rendering/               # 渲染系统
│   ├── IRenderer.cs
│   ├── D3D11Renderer.cs     # DirectX 11 渲染器
│   ├── GdiRenderer.cs       # GDI+ 渲染器
│   ├── Commands/            # 渲染命令
│   │   ├── IRenderCommand.cs
│   │   ├── LineRenderOperation.cs
│   │   ├── ScatterRenderOperation.cs
│   │   ├── BarRenderOperation.cs
│   │   ├── AreaRenderOperation.cs
│   │   ├── OhlcRenderOperation.cs
│   │   └── GridLineRenderOperation.cs
│   ├── Downsampling/        # 降采样策略
│   │   ├── IDownsamplingStrategy.cs
│   │   ├── MinMaxDownsamplingStrategy.cs
│   │   └── UniformDownsampler.cs
│   └── Context/             # 渲染上下文
│       ├── IRenderContext.cs
│       └── ChartRenderContext.cs
├── Modifiers/               # 交互修饰器
│   ├── IChartModifier.cs
│   ├── PanModifier.cs       # 平移修饰器
│   └── ZoomModifier.cs      # 缩放修饰器
├── Chart.cs                 # 主图表控件
└── Themes/
    └── Generic.xaml         # 默认样式
```

## 核心组件说明

### 1. Core 层

提供基础数据类型和枚举定义，供整个图表库使用。

- **DataRange**: 表示数据范围，包含 Min、Max 和 Length 属性
- **LineStyle**: 线条样式（实线、虚线、点线等）
- **MarkerType**: 标记点类型（圆形、方形、菱形等）

### 2. Axes 层

负责坐标轴的管理和渲染。

- **IAxis**: 轴接口，定义轴的基本属性和方法
- **AxisBase**: 轴基类，提供通用实现
- **LinearAxis**: 线性轴，支持等间距刻度
- **LogAxis**: 对数轴，用于指数级数据
- **DateTimeAxis**: 日期时间轴，支持时间序列数据

#### CoordinateMappers

坐标映射器负责数据坐标与屏幕坐标之间的转换：

- **ICoordinateMapper**: 坐标映射接口
- **LinearCoordinateMapper**: 线性映射
- **LogCoordinateMapper**: 对数映射
- **DateTimeCoordinateMapper**: 日期时间映射

### 3. Data 层

提供数据存储和访问机制。

- **IDataSeries**: 数据系列接口，统一数据访问方式
- **VariableDataSeries**: 可变间隔数据系列，X和Y值都显式存储
- **UniformDataSeries**: 均匀间隔数据系列，X值通过索引计算
- **RingBuffer**: 环形缓冲区实现，支持O(1)分摊追加操作

### 4. Series 层

定义可渲染的数据系列类型。

- **IRenderableSeries**: 可渲染系列接口
- **LineRenderableSeries**: 折线图系列
- **ScatterRenderableSeries**: 散点图系列
- **BarRenderableSeries**: 柱状图系列
- **AreaRenderableSeries**: 面积图系列
- **OhlcRenderableSeries**: K线图系列

#### Renderers

系列渲染器负责将数据转换为渲染命令：

- **LineSeriesRenderer**: 折线渲染器
- **ScatterSeriesRenderer**: 散点渲染器
- **BarSeriesRenderer**: 柱状图渲染器
- **AreaSeriesRenderer**: 面积图渲染器
- **OhlcSeriesRenderer**: K线图渲染器

### 5. Rendering 层

提供渲染引擎和命令系统。

- **IRenderer**: 渲染器接口
- **D3D11Renderer**: DirectX 11 硬件加速渲染器
- **GdiRenderer**: GDI+ 软件渲染器（备用）

#### Commands

渲染命令模式，将渲染操作抽象为命令对象：

- **IRenderCommand**: 渲染命令接口
- **LineRenderOperation**: 线条渲染操作
- **ScatterRenderOperation**: 散点渲染操作
- **BarRenderOperation**: 柱状图渲染操作
- **AreaRenderOperation**: 面积图渲染操作
- **OhlcRenderOperation**: K线图渲染操作
- **GridLineRenderOperation**: 网格线渲染操作

#### Downsampling

降采样策略用于处理大数据量时的性能优化：

- **IDownsamplingStrategy**: 降采样策略接口
- **MinMaxDownsamplingStrategy**: 最小最大降采样（保留视觉特征）
- **UniformDownsampler**: 均匀降采样

#### Context

渲染上下文提供渲染所需的环境信息：

- **IRenderContext**: 渲染上下文接口
- **ChartRenderContext**: 图表渲染上下文实现

### 6. Modifiers 层

提供用户交互功能：

- **IChartModifier**: 修饰器接口
- **PanModifier**: 平移交互（拖拽平移）
- **ZoomModifier**: 缩放交互（鼠标滚轮缩放）

## 渲染流程

```
数据更新 → 系列渲染器 → 渲染命令 → 渲染引擎 → 屏幕输出
    │              │             │            │
    ▼              ▼             ▼            ▼
 DataSeries  RenderableSeries  RenderCommand  D3D11Renderer
```

1. **数据更新**: 数据系列接收到新数据
2. **系列渲染器**: 将数据转换为渲染命令（如线条、散点等）
3. **渲染命令**: 封装渲染参数和几何数据
4. **渲染引擎**: 执行渲染命令，输出到屏幕

## 坐标转换流程

```
数据坐标 → 坐标映射器 → 屏幕坐标
    │              │            │
    ▼              ▼            ▼
  DataRange  CoordinateMapper  Pixel Position
```

1. **数据坐标**: 原始数据值（如时间、数值）
2. **坐标映射器**: 根据轴类型进行坐标转换
3. **屏幕坐标**: 最终像素位置

## 关键技术特性

### 1. 硬件加速渲染

使用 DirectX 11 进行硬件加速，支持大规模数据的流畅渲染。

### 2. 数据虚拟化

使用环形缓冲区实现数据的高效管理，支持实时数据流场景。

### 3. 多级降采样

根据屏幕分辨率自动降采样，确保渲染性能。

### 4. 命令模式

渲染命令模式解耦数据处理与渲染执行，支持异步渲染。

### 5. 可扩展性

通过接口设计支持自定义轴类型、系列类型和渲染器。

## 命名空间结构

| 命名空间 | 说明 |
|---------|------|
| Cheari.Controls | 主控件（Chart） |
| Cheari.Controls.Core | 核心类型 |
| Cheari.Controls.Axes | 轴系统 |
| Cheari.Controls.Axes.Controls | 轴控件 |
| Cheari.Controls.Axes.CoordinateMappers | 坐标映射器 |
| Cheari.Controls.Data | 数据系列 |
| Cheari.Controls.Series | 渲染系列 |
| Cheari.Controls.Series.Types | 系列类型 |
| Cheari.Controls.Series.Renderers | 系列渲染器 |
| Cheari.Controls.Rendering | 渲染系统 |
| Cheari.Controls.Rendering.Commands | 渲染命令 |
| Cheari.Controls.Rendering.Downsampling | 降采样 |
| Cheari.Controls.Rendering.Context | 渲染上下文 |
| Cheari.Controls.Modifiers | 交互修饰器 |
