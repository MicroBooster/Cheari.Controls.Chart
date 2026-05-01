using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Frozen;
using System.Collections.Generic;
using Cheari.Controls.Axes;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Rendering.Context;
using Cheari.Controls.Series;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Rendering.Gpu;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using Vortice.Wpf;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace Cheari.Controls.Rendering;

/// <summary>
/// D3D11 硬件渲染器，使用 Direct3D 11 进行高性能图形渲染。
/// 支持线图、面积图、柱状图、散点图和 OHLC 图表的硬件加速渲染。
/// 
/// <para><b>渲染管线架构：</b></para>
/// <code>
/// ┌─────────────────────────────────────────────────────────────────┐
/// │                    D3D11Renderer.Render()                       │
/// │                                                                 │
/// │  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
/// │  │ AxisRenderGroup│    │ AxisRenderGroup│    │     ...      │      │
/// │  │  (xRange,yRange)│    │  (xRange,yRange)│    │              │      │
/// │  │  ┌──────────┐ │    │  ┌──────────┐ │    │              │      │
/// │  │  │ IRenderCmd│ │    │  │ IRenderCmd│ │    │              │      │
/// │  │  │  CommandType  │ │    │  │  CommandType  │ │    │              │      │
/// │  │  └──────────┘ │    │  └──────────┘ │    │              │      │
/// │  └──────────────┘    └──────────────┘    └──────────────┘      │
/// │         │                    │                    │              │
/// │         ▼                    ▼                    ▼              │
/// │  ┌─────────────────────────────────────────────────────────┐   │
/// │  │            UpdateConstants (cbuffer b0)                  │   │
/// │  │  ViewportSize | XRange | YRange | RenderOptions         │   │
/// │  └─────────────────────────────────────────────────────────┘   │
/// │         │                                                      │
/// │         ▼                                                      │
/// │  ┌─────────────────────────────────────────────────────────┐   │
/// │  │          CommandType → Draw Method Dispatch              │   │
/// │  │  Line→DrawLineInstances  Area→DrawAreaFill              │   │
/// │  │  Bar→DrawBarInstances    Scatter→DrawMarkerInstances    │   │
/// │  │  GridLine→DrawGridLineInstances  Ohlc→Bodies+Wicks     │   │
/// │  └─────────────────────────────────────────────────────────┘   │
/// └─────────────────────────────────────────────────────────────────┘
/// </code>
/// 
/// <para><b>着色器管线：</b></para>
/// <list type="bullet">
///   <item>线图/网格线：LineVS + LinePS（实例化四边形 + 抗锯齿 feather）</item>
///   <item>柱状图：BarVS + BarPS（实例化四边形，无抗锯齿）</item>
///   <item>散点图：MarkerVS + MarkerPS（实例化四边形 + SDF 抗锯齿）</item>
///   <item>面积图：AreaVS + AreaPS（逐顶点三角形列表）</item>
/// </list>
/// 
/// <para><b>常量缓冲区布局 (cbuffer b0)：</b></para>
/// <para>所有着色器共享 <see cref="LineShaderConstants"/> 结构：
/// ViewportSize(float2) + XRange(float2) + YRange(float2) + RenderOptions(float2) = 32 字节</para>
/// </summary>
internal sealed class D3D11Renderer : IRenderer
{
    /// <summary>
    /// 获取是否需要 D3D11 支持。
    /// </summary>
    public bool RequiresD3D11 => true;

    public ChartRendererBackend Backend => ChartRendererBackend.D3D11;

    public System.Windows.Media.ImageSource? CurrentImageSource => null;

    private static readonly string s_diagnosticLogPath =
        Path.Combine(Path.GetTempPath(), "Cheari.Controls.Chart.d3d11.log");

    private ID3D11Device1? _device;
    private ID3D11DeviceContext1? _deviceContext;
    private ID3D11RenderTargetView? _renderTargetView;
    private ID3D11BlendState? _blendState;
    private ID3D11RasterizerState? _rasterizerState;
    private ID3D11Buffer? _constantBuffer;
    private readonly ID3D11Buffer[] _vertexBuffers = new ID3D11Buffer[2];
    private readonly uint[] _vertexStrides = new uint[2];
    private readonly uint[] _vertexOffsets = [0, 0];
    private LineShaderConstants _shaderConstants;

    private ID3D11VertexShader? _lineVertexShader;
    private ID3D11PixelShader? _linePixelShader;
    private ID3D11InputLayout? _lineInputLayout;
    private ID3D11Buffer? _lineInstanceBuffer;
    private int _lineInstanceCapacity;

    private ID3D11VertexShader? _markerVertexShader;
    private ID3D11PixelShader? _markerPixelShader;
    private ID3D11InputLayout? _markerInputLayout;
    private ID3D11Buffer? _markerInstanceBuffer;
    private int _markerInstanceCapacity;

    private ID3D11VertexShader? _barVertexShader;
    private ID3D11PixelShader? _barPixelShader;
    private ID3D11InputLayout? _barInputLayout;
    private ID3D11Buffer? _barInstanceBuffer;
    private int _barInstanceCapacity;

    private ID3D11VertexShader? _areaVertexShader;
    private ID3D11PixelShader? _areaPixelShader;
    private ID3D11InputLayout? _areaInputLayout;
    private ID3D11Buffer? _areaVertexBuffer;

    private ID3D11Buffer? _quadVertexBuffer;

    private GpuLineShader? _gpuLineShader;
    private GpuBarShader? _gpuBarShader;
    private GpuOhlcShader? _gpuOhlcShader;
    private GpuDownsampleShader? _gpuDownsampleShader;
    private GpuHybridRenderer? _gpuHybridRenderer;

    private int _targetWidth;
    private int _targetHeight;
    private nint _boundColorTexturePointer;
    private bool _isDisposed;

    /// <summary>
    /// 获取最后一次渲染错误信息。
    /// </summary>
    public string? LastError { get; private set; }

    private static readonly QuadVertex[] s_quadVertices =
    [
        new() { Along = 0.0f, Side = -1.0f },
        new() { Along = 0.0f, Side = 1.0f },
        new() { Along = 1.0f, Side = -1.0f },
        new() { Along = 1.0f, Side = 1.0f }
    ];

    #region Shader Sources

    // ═══════════════════════════════════════════════════════════════
    // 线条着色器 — 实例化四边形渲染，支持抗锯齿 feather
    //
    // 输入语义:
    //   TEXCOORD0  — 四边形顶点坐标 (Along: 0/1, Side: -1/+1)
    //   TEXCOORD1  — 线段端点 (X1,Y1,X2,Y2) — 每实例
    //   COLOR0     — 线条颜色 (RGBA) — 每实例
    //   TEXCOORD2  — 线条粗细 — 每实例
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 传递颜色
    //   TEXCOORD0   — 局部坐标 (localAlong, localSide)
    //   TEXCOORD1   — 度量值 (halfLength, halfThickness, feather)
    //
    // 抗锯齿原理:
    //   feather 参数控制线条边缘的渐变宽度。像素着色器使用 smoothstep
    //   在 [0, feather] 范围内计算覆盖率，实现亚像素级平滑边缘。
    //   feather = 0 时退化为硬边渲染（无抗锯齿）。
    // ═══════════════════════════════════════════════════════════════

    private const string LineVertexShaderSource = """
cbuffer LineConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
};

struct VSInput
{
    float2 Quad : TEXCOORD0;
    float4 Segment : TEXCOORD1;
    float4 Color : COLOR0;
    float Thickness : TEXCOORD2;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
    float3 Metrics : TEXCOORD1;
};

VSOutput main(VSInput input)
{
    float2 startData = input.Segment.xy;
    float2 endData = input.Segment.zw;

    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float2 startScreen;
    startScreen.x = ((startData.x - gXRange.x) / xLength) * gViewportSize.x;
    startScreen.y = gViewportSize.y - ((startData.y - gYRange.x) / yLength) * gViewportSize.y;

    float2 endScreen;
    endScreen.x = ((endData.x - gXRange.x) / xLength) * gViewportSize.x;
    endScreen.y = gViewportSize.y - ((endData.y - gYRange.x) / yLength) * gViewportSize.y;

    float2 direction = endScreen - startScreen;
    float directionLength = max(length(direction), 1e-6f);
    direction /= directionLength;

    float2 normal = float2(-direction.y, direction.x);
    float feather = gRenderOptions.x;
    float halfThickness = input.Thickness * 0.5f;
    float expandedHalfThickness = halfThickness + feather;
    float halfLength = directionLength * 0.5f;
    float expandedHalfLength = halfLength + feather;
    float alongSign = input.Quad.x * 2.0f - 1.0f;
    float localAlong = alongSign * expandedHalfLength;
    float localSide = input.Quad.y * expandedHalfThickness;
    float2 center = (startScreen + endScreen) * 0.5f;
    float2 pixelPosition = center + direction * localAlong + normal * localSide;

    float2 ndc;
    ndc.x = (pixelPosition.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (pixelPosition.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = input.Color;
    output.Local = float2(localAlong, localSide);
    output.Metrics = float3(halfLength, halfThickness, feather);
    return output;
}
""";

    private const string LinePixelShaderSource = """
struct PSInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
    float3 Metrics : TEXCOORD1;
};

float4 main(PSInput input) : SV_Target
{
    float halfLength = input.Metrics.x;
    float halfThickness = input.Metrics.y;
    float feather = input.Metrics.z;
    float distanceToEdge = max(abs(input.Local.x) - halfLength, abs(input.Local.y) - halfThickness);

    float coverage;
    if (feather > 0.0f)
    {
        coverage = 1.0f - smoothstep(0.0f, feather, max(distanceToEdge, 0.0f));
    }
    else
    {
        coverage = distanceToEdge <= 0.0f ? 1.0f : 0.0f;
    }

    return float4(input.Color.rgb, input.Color.a * coverage);
}
""";

    // ═══════════════════════════════════════════════════════════════
    // 标记点着色器 — 实例化四边形渲染，SDF 抗锯齿
    //
    // 输入语义:
    //   TEXCOORD0  — 四边形顶点坐标 (Along: 0/1, Side: -1/+1)
    //   TEXCOORD1  — 标记中心坐标 (X,Y) — 每实例
    //   TEXCOORD2  — 标记大小 — 每实例
    //   COLOR0     — 标记颜色 (RGBA) — 每实例
    //   TEXCOORD3  — 标记类型 (0=圆,1=方,2=菱,3=上三角,4=下三角) — 每实例
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 传递颜色
    //   TEXCOORD0   — 局部坐标 (alongSign, sideSign) ∈ [-1,1]
    //   TEXCOORD1   — 标记类型
    //
    // 抗锯齿原理:
    //   像素着色器使用有符号距离场 (SDF) 计算标记形状的边缘距离，
    //   通过 smoothstep(-feather, feather, dist) 实现抗锯齿。
    // ═══════════════════════════════════════════════════════════════

    private const string MarkerVertexShaderSource = """
cbuffer LineConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
};

struct VSInput
{
    float2 Quad : TEXCOORD0;
    float2 Center : TEXCOORD1;
    float Size : TEXCOORD2;
    float4 Color : COLOR0;
    float MarkerType : TEXCOORD3;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
    float MarkerType : TEXCOORD1;
};

VSOutput main(VSInput input)
{
    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float2 centerScreen;
    centerScreen.x = ((input.Center.x - gXRange.x) / xLength) * gViewportSize.x;
    centerScreen.y = gViewportSize.y - ((input.Center.y - gYRange.x) / yLength) * gViewportSize.y;

    float halfSize = input.Size * 0.5f;
    float alongSign = input.Quad.x * 2.0f - 1.0f;
    float sideSign = input.Quad.y * 2.0f - 1.0f;
    float2 pixelPosition = centerScreen + float2(alongSign, sideSign) * halfSize;

    float2 ndc;
    ndc.x = (pixelPosition.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (pixelPosition.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = input.Color;
    output.Local = float2(alongSign, sideSign);
    output.MarkerType = input.MarkerType;
    return output;
}
""";

    private const string MarkerPixelShaderSource = """
struct PSInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
    float MarkerType : TEXCOORD1;
};

float sdCircle(float2 p) { return length(p) - 1.0f; }
float sdBox(float2 p) { float2 d = abs(p) - 1.0f; return length(max(d, 0.0f)) + min(max(d.x, d.y), 0.0f); }
float sdDiamond(float2 p) { float2 d = abs(p) * float2(1.0f, 1.4f); return (d.x + d.y - 1.0f); }
float sdTriangleUp(float2 p)
{
    float2 q = float2(abs(p.x), -p.y) + float2(0.0f, 0.5f);
    return max(q.x * 0.866f + q.y * 0.5f, -q.y) - 0.5f;
}
float sdTriangleDown(float2 p) { return sdTriangleUp(float2(p.x, -p.y)); }

float4 main(PSInput input) : SV_Target
{
    float2 p = input.Local;
    float dist;
    int mt = (int)input.MarkerType;

    if (mt == 0) dist = sdCircle(p);
    else if (mt == 1) dist = sdBox(p);
    else if (mt == 2) dist = sdDiamond(p);
    else if (mt == 3) dist = sdTriangleUp(p);
    else dist = sdTriangleDown(p);

    float feather = 0.8f;
    float coverage = 1.0f - smoothstep(-feather, feather, dist);

    return float4(input.Color.rgb, input.Color.a * coverage);
}
""";

    // ═══════════════════════════════════════════════════════════════
    // 柱状图着色器 — 实例化四边形渲染，无抗锯齿
    //
    // 输入语义:
    //   TEXCOORD0  — 四边形顶点坐标 (u: 0/1, v: 0/1)
    //   TEXCOORD1  — 柱体矩形 (XMin, YMin, XMax, YMax) — 每实例
    //   COLOR0     — 柱体颜色 (RGBA) — 每实例
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 传递颜色
    //   TEXCOORD0   — 局部 UV 坐标
    // ═══════════════════════════════════════════════════════════════

    private const string BarVertexShaderSource = """
cbuffer LineConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
};

struct VSInput
{
    float2 Quad : TEXCOORD0;
    float4 Rect : TEXCOORD1;
    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float2 minScreen;
    minScreen.x = ((input.Rect.x - gXRange.x) / xLength) * gViewportSize.x;
    minScreen.y = gViewportSize.y - ((input.Rect.w - gYRange.x) / yLength) * gViewportSize.y;

    float2 maxScreen;
    maxScreen.x = ((input.Rect.z - gXRange.x) / xLength) * gViewportSize.x;
    maxScreen.y = gViewportSize.y - ((input.Rect.y - gYRange.x) / yLength) * gViewportSize.y;

    float2 pixelPosition;
    pixelPosition.x = lerp(minScreen.x, maxScreen.x, input.Quad.x);
    pixelPosition.y = lerp(minScreen.y, maxScreen.y, input.Quad.y);

    float2 ndc;
    ndc.x = (pixelPosition.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (pixelPosition.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = input.Color;
    output.Local = input.Quad;
    return output;
}
""";

    private const string BarPixelShaderSource = """
struct PSInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Local : TEXCOORD0;
};

float4 main(PSInput input) : SV_Target
{
    return input.Color;
}
""";

    // ═══════════════════════════════════════════════════════════════
    // 面积图着色器 — 逐顶点三角形列表渲染，无抗锯齿
    //
    // 输入语义:
    //   TEXCOORD0  — 顶点位置 (X, Y)
    //   COLOR0     — 顶点颜色 (RGBA)
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 传递颜色
    // ═══════════════════════════════════════════════════════════════

    private const string AreaVertexShaderSource = """
cbuffer LineConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
};

struct VSInput
{
    float2 Pos : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

VSOutput main(VSInput input)
{
    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float2 screen;
    screen.x = ((input.Pos.x - gXRange.x) / xLength) * gViewportSize.x;
    screen.y = gViewportSize.y - ((input.Pos.y - gYRange.x) / yLength) * gViewportSize.y;

    float2 ndc;
    ndc.x = (screen.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (screen.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = input.Color;
    return output;
}
""";

    private const string AreaPixelShaderSource = """
struct PSInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

float4 main(PSInput input) : SV_Target
{
    return input.Color;
}
""";

    #endregion

    /// <summary>
    /// 初始化渲染器。
    /// </summary>
    /// <param name="args">绘图表面事件参数</param>
    public void Initialize(DrawingSurfaceEventArgs args)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(args);

        Uninitialize();

        _device = args.Device;
        _deviceContext = args.Context;
        try
        {
            InitializePipeline();
            LastError = null;
        }
        catch (Exception ex)
        {
            RecordFailure("InitializePipeline", ex);
            Uninitialize();
        }

        _targetWidth = 0;
        _targetHeight = 0;
    }

    /// <summary>
    /// 反初始化渲染器，释放所有资源。
    /// </summary>
    public void Uninitialize()
    {
        if (_isDisposed)
            return;

        ReleasePipelineBindings();
        DisposeTargetResources();

        _blendState?.Dispose(); _blendState = null;
        _rasterizerState?.Dispose(); _rasterizerState = null;
        _constantBuffer?.Dispose(); _constantBuffer = null;
        _quadVertexBuffer?.Dispose(); _quadVertexBuffer = null;

        _lineVertexShader?.Dispose(); _lineVertexShader = null;
        _linePixelShader?.Dispose(); _linePixelShader = null;
        _lineInputLayout?.Dispose(); _lineInputLayout = null;
        _lineInstanceBuffer?.Dispose(); _lineInstanceBuffer = null;
        _lineInstanceCapacity = 0;

        _markerVertexShader?.Dispose(); _markerVertexShader = null;
        _markerPixelShader?.Dispose(); _markerPixelShader = null;
        _markerInputLayout?.Dispose(); _markerInputLayout = null;
        _markerInstanceBuffer?.Dispose(); _markerInstanceBuffer = null;
        _markerInstanceCapacity = 0;

        _barVertexShader?.Dispose(); _barVertexShader = null;
        _barPixelShader?.Dispose(); _barPixelShader = null;
        _barInputLayout?.Dispose(); _barInputLayout = null;
        _barInstanceBuffer?.Dispose(); _barInstanceBuffer = null;
        _barInstanceCapacity = 0;

        _areaVertexShader?.Dispose(); _areaVertexShader = null;
        _areaPixelShader?.Dispose(); _areaPixelShader = null;
        _areaInputLayout?.Dispose(); _areaInputLayout = null;
        _areaVertexBuffer?.Dispose(); _areaVertexBuffer = null;

        _gpuLineShader?.Dispose(); _gpuLineShader = null;
        _gpuBarShader?.Dispose(); _gpuBarShader = null;
        _gpuOhlcShader?.Dispose(); _gpuOhlcShader = null;
        _gpuDownsampleShader?.Dispose(); _gpuDownsampleShader = null;
        _gpuHybridRenderer?.Dispose(); _gpuHybridRenderer = null;

        _deviceContext = null;
        _device = null;
    }

    /// <summary>
    /// 使用单个渲染组渲染图表。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">渲染宽度</param>
    /// <param name="height">渲染高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="xRange">X轴数据范围</param>
    /// <param name="yRange">Y轴数据范围</param>
    /// <param name="commands">渲染命令列表</param>
    /// <returns>渲染是否成功</returns>
    public bool Render(
        DrawEventArgs args,
        int width,
        int height,
        MediaColor backgroundColor,
        bool enableAntialiasing,
        DataRange xRange,
        DataRange yRange,
        IReadOnlyList<IRenderCommand>? commands = null)
    {
        var group = new AxisRenderGroup
        {
            XRange = xRange,
            YRange = yRange,
            XMapper = LinearCoordinateMapper.Instance,
            YMapper = LinearCoordinateMapper.Instance,
            Commands = commands ?? Array.Empty<IRenderCommand>()
        };
        return Render(args, width, height, backgroundColor, enableAntialiasing, new AxisRenderGroup[] { group });
    }

    /// <summary>
    /// 使用多个渲染组渲染图表。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">渲染宽度</param>
    /// <param name="height">渲染高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="renderGroups">渲染组列表</param>
    /// <returns>渲染是否成功</returns>
    public bool Render(
        DrawEventArgs args,
        int width,
        int height,
        MediaColor backgroundColor,
        bool enableAntialiasing,
        IReadOnlyList<AxisRenderGroup> renderGroups)
    {
        if (_isDisposed)
        {
            LastError = "Renderer is disposed.";
            return false;
        }

        ArgumentNullException.ThrowIfNull(args);
        if (_deviceContext == null)
        {
            LastError ??= "Renderer is not initialized.";
            return false;
        }

        if (width <= 0 || height <= 0)
            return false;

        if (!EnsureTarget(args, width, height))
            return false;

        if (_renderTargetView == null || _blendState == null || _rasterizerState == null)
            return false;

        try
        {
            _ = backgroundColor;
            _deviceContext.OMSetRenderTargets(_renderTargetView, null);
            _deviceContext.ClearRenderTargetView(_renderTargetView, new Color4(0, 0, 0, 0));
            _deviceContext.RSSetViewport(new Viewport(0, 0, width, height, 0.0f, 1.0f));
            _deviceContext.RSSetState(_rasterizerState);
            _deviceContext.OMSetBlendState(_blendState, new Color4(0, 0, 0, 0), uint.MaxValue);

            for (int g = 0; g < renderGroups.Count; g++)
            {
                var group = renderGroups[g];
                UpdateConstants(width, height, enableAntialiasing, group.XRange, group.YRange);

                var commands = group.Commands;
                for (int i = 0; i < commands.Count; i++)
                {
                    var cmd = commands[i];
                    switch (cmd.CommandType)
                    {
                        case RenderCommandType.Area:
                            DrawAreaFill((AreaRenderOperation)cmd);
                            break;
                        case RenderCommandType.Bar:
                            DrawBarInstances((BarRenderOperation)cmd);
                            break;
                        case RenderCommandType.Scatter:
                            DrawMarkerInstances((ScatterRenderOperation)cmd);
                            break;
                        case RenderCommandType.GridLine:
                            DrawGridLineInstances((GridLineRenderOperation)cmd);
                            break;
                        case RenderCommandType.Line:
                            DrawLineInstances((LineRenderOperation)cmd);
                            break;
                        case RenderCommandType.Ohlc:
                            var ohlc = (OhlcRenderOperation)cmd;
                            DrawBarInstances(ohlc.Bodies);
                            DrawLineInstances(ohlc.Wicks);
                            break;
                    }
                }
            }

            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            RecordFailure("Render", ex);
            DisposeTargetResources();
            return false;
        }
    }

    #region Pipeline Initialization

    /// <summary>
    /// 初始化渲染管线。
    /// </summary>
    private void InitializePipeline()
    {
        if (_device == null)
            return;

        _quadVertexBuffer = _device.CreateBuffer(
            s_quadVertices,
            BindFlags.VertexBuffer,
            ResourceUsage.Immutable,
            CpuAccessFlags.None,
            ResourceOptionFlags.None,
            0,
            (uint)Marshal.SizeOf<QuadVertex>());

        _constantBuffer = _device.CreateBuffer(
            (uint)Marshal.SizeOf<LineShaderConstants>(),
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        var blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha);
        blendDescription.RenderTarget[0].SourceBlendAlpha = Blend.One;
        blendDescription.RenderTarget[0].DestinationBlendAlpha = Blend.InverseSourceAlpha;
        blendDescription.RenderTarget[0].BlendOperationAlpha = BlendOperation.Add;
        blendDescription.RenderTarget[0].RenderTargetWriteMask = ColorWriteEnable.All;
        _blendState = _device.CreateBlendState(blendDescription);

        var rasterizerDescription = new RasterizerDescription(CullMode.None, FillMode.Solid)
        {
            FrontCounterClockwise = false,
            DepthBias = 0,
            DepthBiasClamp = 0.0f,
            SlopeScaledDepthBias = 0.0f,
            DepthClipEnable = true,
            ScissorEnable = false,
            MultisampleEnable = false,
            AntialiasedLineEnable = false
        };
        _rasterizerState = _device.CreateRasterizerState(rasterizerDescription);

        InitializeLinePipeline();
        InitializeMarkerPipeline();
        InitializeBarPipeline();
        InitializeAreaPipeline();
        InitializeGpuPipelines();
    }

    /// <summary>
    /// 初始化线图渲染管线。
    /// </summary>
    private void InitializeLinePipeline()
    {
        if (_device == null) return;

        var vsBytes = CompileShader(LineVertexShaderSource, "main", "vs_5_0");
        var psBytes = CompileShader(LinePixelShaderSource, "main", "ps_5_0");
        var vsBytecode = vsBytes.ToArray();

        _lineVertexShader = _device.CreateVertexShader(vsBytecode);
        _linePixelShader = _device.CreatePixelShader(psBytes.Span);

        var inputElements = new[]
        {
            new InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("TEXCOORD", 1, Vortice.DXGI.Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("TEXCOORD", 2, Vortice.DXGI.Format.R32_Float, 32, 1, InputClassification.PerInstanceData, 1)
        };

        _lineInputLayout = CreateInputLayout(_device, vsBytecode, inputElements);
    }

    /// <summary>
    /// 初始化标记点渲染管线。
    /// </summary>
    private void InitializeMarkerPipeline()
    {
        if (_device == null) return;

        var vsBytes = CompileShader(MarkerVertexShaderSource, "main", "vs_5_0");
        var psBytes = CompileShader(MarkerPixelShaderSource, "main", "ps_5_0");
        var vsBytecode = vsBytes.ToArray();

        _markerVertexShader = _device.CreateVertexShader(vsBytecode);
        _markerPixelShader = _device.CreatePixelShader(psBytes.Span);

        var inputElements = new[]
        {
            new InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("TEXCOORD", 1, Vortice.DXGI.Format.R32G32_Float, 0, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("TEXCOORD", 2, Vortice.DXGI.Format.R32_Float, 8, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 12, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("TEXCOORD", 3, Vortice.DXGI.Format.R32_Float, 28, 1, InputClassification.PerInstanceData, 1)
        };

        _markerInputLayout = CreateInputLayout(_device, vsBytecode, inputElements);
    }

    /// <summary>
    /// 初始化柱状图渲染管线。
    /// </summary>
    private void InitializeBarPipeline()
    {
        if (_device == null) return;

        var vsBytes = CompileShader(BarVertexShaderSource, "main", "vs_5_0");
        var psBytes = CompileShader(BarPixelShaderSource, "main", "ps_5_0");
        var vsBytecode = vsBytes.ToArray();

        _barVertexShader = _device.CreateVertexShader(vsBytecode);
        _barPixelShader = _device.CreatePixelShader(psBytes.Span);

        var inputElements = new[]
        {
            new InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("TEXCOORD", 1, Vortice.DXGI.Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1)
        };

        _barInputLayout = CreateInputLayout(_device, vsBytecode, inputElements);
    }

    /// <summary>
    /// 初始化面积图渲染管线。
    /// </summary>
    private void InitializeAreaPipeline()
    {
        if (_device == null) return;

        var vsBytes = CompileShader(AreaVertexShaderSource, "main", "vs_5_0");
        var psBytes = CompileShader(AreaPixelShaderSource, "main", "ps_5_0");
        var vsBytecode = vsBytes.ToArray();

        _areaVertexShader = _device.CreateVertexShader(vsBytecode);
        _areaPixelShader = _device.CreatePixelShader(psBytes.Span);

        var inputElements = new[]
        {
            new InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 8, 0, InputClassification.PerVertexData, 0)
        };

        _areaInputLayout = CreateInputLayout(_device, vsBytecode, inputElements);
    }

    /// <summary>
    /// 创建输入布局。
    /// </summary>
    /// <param name="device">D3D11设备</param>
    /// <param name="vsBytecode">顶点着色器字节码</param>
    /// <param name="elements">输入元素描述</param>
    /// <returns>输入布局对象</returns>
    private static ID3D11InputLayout CreateInputLayout(ID3D11Device1 device, byte[] vsBytecode, InputElementDescription[] elements)
    {
        var handle = GCHandle.Alloc(vsBytecode, GCHandleType.Pinned);
        try
        {
            Compiler.GetInputSignatureBlob(handle.AddrOfPinnedObject(), (nuint)vsBytecode.Length, out Blob inputSignatureBlob);
            using (inputSignatureBlob)
            {
                return device.CreateInputLayout(elements, inputSignatureBlob);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private void InitializeGpuPipelines()
    {
        if (_device == null) return;

        try
        {
            _gpuLineShader = new GpuLineShader();
            _gpuLineShader.Initialize(_device);

            _gpuBarShader = new GpuBarShader();
            _gpuBarShader.Initialize(_device);

            _gpuOhlcShader = new GpuOhlcShader();
            _gpuOhlcShader.Initialize(_device);

            _gpuDownsampleShader = new GpuDownsampleShader();
            _gpuDownsampleShader.Initialize(_device);

            _gpuHybridRenderer = new GpuHybridRenderer();
            _gpuHybridRenderer.Initialize(_device);
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"[{DateTime.Now:O}] InitializeGpuPipelines failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}{Environment.NewLine}");
            _gpuLineShader?.Dispose(); _gpuLineShader = null;
            _gpuBarShader?.Dispose(); _gpuBarShader = null;
            _gpuOhlcShader?.Dispose(); _gpuOhlcShader = null;
            _gpuDownsampleShader?.Dispose(); _gpuDownsampleShader = null;
            _gpuHybridRenderer?.Dispose(); _gpuHybridRenderer = null;
        }
    }

    public bool HasGpuPipeline => _gpuLineShader != null;

    #endregion

    #region Draw Methods

    /// <summary>
    /// 绘制线实例。
    /// </summary>
    /// <param name="line">线渲染操作</param>
    private void DrawLineInstances(LineRenderOperation line)
    {
        if (_deviceContext == null || line.LineInstances.Count == 0
            || _lineVertexShader == null || _linePixelShader == null || _lineInputLayout == null)
            return;

        EnsureInstanceCapacity(ref _lineInstanceBuffer, ref _lineInstanceCapacity, line.LineInstances.Count, Marshal.SizeOf<GpuLineInstance>());
        if (_lineInstanceBuffer == null || _quadVertexBuffer == null)
            return;

        UploadInstanceData(_lineInstanceBuffer, line.LineInstances, Marshal.SizeOf<GpuLineInstance>());

        _deviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        _deviceContext.IASetInputLayout(_lineInputLayout);
        _deviceContext.VSSetShader(_lineVertexShader);
        _deviceContext.VSSetConstantBuffer(0, _constantBuffer);
        _deviceContext.PSSetShader(_linePixelShader);

        BindInstancedVertexBuffers(_quadVertexBuffer, _lineInstanceBuffer, (uint)Marshal.SizeOf<QuadVertex>(), (uint)Marshal.SizeOf<GpuLineInstance>());
        _deviceContext.DrawInstanced(4, (uint)line.LineInstances.Count, 0, 0);
    }

    /// <summary>
    /// 绘制网格线实例。
    /// </summary>
    /// <param name="grid">网格线渲染操作</param>
    private void DrawGridLineInstances(GridLineRenderOperation grid)
    {
        if (_deviceContext == null || grid.Lines.Count == 0
            || _lineVertexShader == null || _linePixelShader == null || _lineInputLayout == null)
            return;

        EnsureInstanceCapacity(ref _lineInstanceBuffer, ref _lineInstanceCapacity, grid.Lines.Count, Marshal.SizeOf<GpuLineInstance>());
        if (_lineInstanceBuffer == null || _quadVertexBuffer == null)
            return;

        UploadInstanceData<GpuLineInstance>(_lineInstanceBuffer, grid.Lines, Marshal.SizeOf<GpuLineInstance>());

        _deviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        _deviceContext.IASetInputLayout(_lineInputLayout);
        _deviceContext.VSSetShader(_lineVertexShader);
        _deviceContext.VSSetConstantBuffer(0, _constantBuffer);
        _deviceContext.PSSetShader(_linePixelShader);

        BindInstancedVertexBuffers(_quadVertexBuffer, _lineInstanceBuffer, (uint)Marshal.SizeOf<QuadVertex>(), (uint)Marshal.SizeOf<GpuLineInstance>());
        _deviceContext.DrawInstanced(4, (uint)grid.Lines.Count, 0, 0);
    }

    /// <summary>
    /// 绘制标记点实例。
    /// </summary>
    /// <param name="scatter">散点图渲染操作</param>
    private void DrawMarkerInstances(ScatterRenderOperation scatter)
    {
        if (_deviceContext == null || scatter.Instances.Count == 0
            || _markerVertexShader == null || _markerPixelShader == null || _markerInputLayout == null)
            return;

        EnsureInstanceCapacity(ref _markerInstanceBuffer, ref _markerInstanceCapacity, scatter.Instances.Count, Marshal.SizeOf<GpuMarkerInstance>());
        if (_markerInstanceBuffer == null || _quadVertexBuffer == null)
            return;

        UploadInstanceData(_markerInstanceBuffer, scatter.Instances, Marshal.SizeOf<GpuMarkerInstance>());

        _deviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        _deviceContext.IASetInputLayout(_markerInputLayout);
        _deviceContext.VSSetShader(_markerVertexShader);
        _deviceContext.VSSetConstantBuffer(0, _constantBuffer);
        _deviceContext.PSSetShader(_markerPixelShader);

        BindInstancedVertexBuffers(_quadVertexBuffer, _markerInstanceBuffer, (uint)Marshal.SizeOf<QuadVertex>(), (uint)Marshal.SizeOf<GpuMarkerInstance>());
        _deviceContext.DrawInstanced(4, (uint)scatter.Instances.Count, 0, 0);
    }

    /// <summary>
    /// 绘制柱状图实例。
    /// </summary>
    /// <param name="bar">柱状图渲染操作</param>
    private void DrawBarInstances(BarRenderOperation bar)
    {
        if (_deviceContext == null || bar.Instances.Count == 0
            || _barVertexShader == null || _barPixelShader == null || _barInputLayout == null)
            return;

        EnsureInstanceCapacity(ref _barInstanceBuffer, ref _barInstanceCapacity, bar.Instances.Count, Marshal.SizeOf<GpuBarInstance>());
        if (_barInstanceBuffer == null || _quadVertexBuffer == null)
            return;

        UploadInstanceData(_barInstanceBuffer, bar.Instances, Marshal.SizeOf<GpuBarInstance>());

        _deviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        _deviceContext.IASetInputLayout(_barInputLayout);
        _deviceContext.VSSetShader(_barVertexShader);
        _deviceContext.VSSetConstantBuffer(0, _constantBuffer);
        _deviceContext.PSSetShader(_barPixelShader);

        BindInstancedVertexBuffers(_quadVertexBuffer, _barInstanceBuffer, (uint)Marshal.SizeOf<QuadVertex>(), (uint)Marshal.SizeOf<GpuBarInstance>());
        _deviceContext.DrawInstanced(4, (uint)bar.Instances.Count, 0, 0);
    }

    /// <summary>
    /// 绘制面积填充。
    /// </summary>
    /// <param name="area">面积图渲染操作</param>
    private void DrawAreaFill(AreaRenderOperation area)
    {
        if (_deviceContext == null || area.FillVertices.Count == 0
            || _areaVertexShader == null || _areaPixelShader == null || _areaInputLayout == null)
            return;

        int vertexCount = area.FillVertices.Count;
        int vertexSize = Marshal.SizeOf<GpuAreaVertex>();
        uint requiredSize = (uint)(vertexSize * vertexCount);

        if (_areaVertexBuffer == null || _areaVertexBuffer.Description.ByteWidth < requiredSize)
        {
            _areaVertexBuffer?.Dispose();
            uint bufferSize = Math.Max(requiredSize, 256);
            _areaVertexBuffer = _device!.CreateBuffer(
                bufferSize,
                BindFlags.VertexBuffer,
                ResourceUsage.Dynamic,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0);
        }

        var mapped = _deviceContext.Map(_areaVertexBuffer, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            WriteSpanToMappedMemory(CollectionsMarshal.AsSpan(area.FillVertices), mapped.DataPointer);
        }
        finally
        {
            _deviceContext.Unmap(_areaVertexBuffer);
        }

        _deviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _deviceContext.IASetInputLayout(_areaInputLayout);
        _deviceContext.VSSetShader(_areaVertexShader);
        _deviceContext.VSSetConstantBuffer(0, _constantBuffer);
        _deviceContext.PSSetShader(_areaPixelShader);

        uint stride = (uint)vertexSize;
        uint offset = 0;
        _deviceContext.IASetVertexBuffers(0, 1, [_areaVertexBuffer], [stride], [offset]);
        _deviceContext.Draw((uint)vertexCount, 0);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 确保实例缓冲区有足够的容量。
    /// </summary>
    /// <param name="buffer">缓冲区引用</param>
    /// <param name="capacity">容量引用</param>
    /// <param name="requiredCount">所需实例数量</param>
    /// <param name="instanceStride">实例步长</param>
    private void EnsureInstanceCapacity(ref ID3D11Buffer? buffer, ref int capacity, int requiredCount, int instanceStride)
    {
        if (_device == null || requiredCount <= capacity)
            return;

        buffer?.Dispose();
        capacity = Math.Max(requiredCount, Math.Max(256, capacity * 2));
        buffer = _device.CreateBuffer(
            (uint)(instanceStride * capacity),
            BindFlags.VertexBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);
    }

    /// <summary>
    /// 上传实例数据到缓冲区。
    /// </summary>
    /// <typeparam name="T">实例类型</typeparam>
    /// <param name="buffer">目标缓冲区</param>
    /// <param name="instances">实例列表</param>
    /// <param name="stride">步长</param>
    private void UploadInstanceData<T>(ID3D11Buffer buffer, IReadOnlyList<T> instances, int stride) where T : unmanaged
    {
        if (_deviceContext == null) return;

        var mapped = _deviceContext.Map(buffer, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            if (instances is List<T> list)
            {
                WriteSpanToMappedMemory(CollectionsMarshal.AsSpan(list), mapped.DataPointer);
            }
            else
            {
                for (int i = 0; i < instances.Count; i++)
                    Marshal.StructureToPtr(instances[i], IntPtr.Add(mapped.DataPointer, i * stride), false);
            }
        }
        finally
        {
            _deviceContext.Unmap(buffer);
        }
    }

    /// <summary>
    /// 绑定实例化顶点缓冲区。
    /// </summary>
    /// <param name="quadBuffer">四边形顶点缓冲区</param>
    /// <param name="instanceBuffer">实例缓冲区</param>
    /// <param name="quadStride">四边形步长</param>
    /// <param name="instanceStride">实例步长</param>
    private void BindInstancedVertexBuffers(ID3D11Buffer quadBuffer, ID3D11Buffer instanceBuffer, uint quadStride, uint instanceStride)
    {
        if (_deviceContext == null) return;

        _vertexBuffers[0] = quadBuffer;
        _vertexBuffers[1] = instanceBuffer;
        _vertexStrides[0] = quadStride;
        _vertexStrides[1] = instanceStride;
        _deviceContext.IASetVertexBuffers(0, 2, _vertexBuffers, _vertexStrides, _vertexOffsets);
    }

    /// <summary>
    /// 更新着色器常量。
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private void UpdateConstants(int width, int height, bool enableAntialiasing, DataRange xRange, DataRange yRange)
    {
        if (_deviceContext == null || _constantBuffer == null)
            return;

        _shaderConstants.ViewportSize = new Vector2(width, height);
        _shaderConstants.XRange = new Vector2((float)xRange.Min, (float)xRange.Max);
        _shaderConstants.YRange = new Vector2((float)yRange.Min, (float)yRange.Max);
        _shaderConstants.RenderOptions = new Vector2(enableAntialiasing ? 1.0f : 0.0f, 0.0f);

        var mapped = _deviceContext.Map(_constantBuffer, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            WriteValueToMappedMemory(_shaderConstants, mapped.DataPointer);
        }
        finally
        {
            _deviceContext.Unmap(_constantBuffer);
        }
    }

    /// <summary>
    /// 将连续实例数据批量写入映射后的 GPU 内存，避免逐元素 Marshal 开销。
    /// </summary>
    private static unsafe void WriteSpanToMappedMemory<T>(ReadOnlySpan<T> values, IntPtr destination) where T : unmanaged
    {
        if (values.IsEmpty)
            return;

        var target = new Span<T>((void*)destination, values.Length);
        values.CopyTo(target);
    }

    private static unsafe void WriteValueToMappedMemory<T>(T value, IntPtr destination) where T : unmanaged
    {
        *(T*)destination = value;
    }

    #endregion

    #region Infrastructure

    /// <summary>
    /// 确保渲染目标有效。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>是否成功</returns>
    private bool EnsureTarget(DrawEventArgs args, int width, int height)
    {
        var colorTexture = args.Surface.ColorTexture;
        if (colorTexture == null)
        {
            DisposeTargetResources();
            return false;
        }

        if (_renderTargetView == null
            || _targetWidth != width
            || _targetHeight != height
            || _boundColorTexturePointer != colorTexture.NativePointer)
        {
            RecreateTarget(colorTexture, width, height);
        }

        return _renderTargetView != null;
    }

    /// <summary>
    /// 重新创建渲染目标。
    /// </summary>
    /// <param name="colorTexture">颜色纹理</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    private void RecreateTarget(ID3D11Texture2D colorTexture, int width, int height)
    {
        if (_device == null)
            return;

        DisposeTargetResources();

        _renderTargetView = _device.CreateRenderTargetView(colorTexture);
        _targetWidth = width;
        _targetHeight = height;
        _boundColorTexturePointer = colorTexture.NativePointer;
    }

    /// <summary>
    /// 编译着色器。
    /// </summary>
    /// <param name="source">着色器源码</param>
    /// <param name="entryPoint">入口点</param>
    /// <param name="target">目标着色器版本</param>
    /// <returns>编译后的字节码</returns>
    private static ReadOnlyMemory<byte> CompileShader(string source, string entryPoint, string target)
    {
        try
        {
            return Compiler.Compile(
                source,
                entryPoint,
                "inline",
                target,
                ShaderFlags.OptimizationLevel3,
                EffectFlags.None);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to compile shader '{entryPoint}' ({target}). {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// 将 MediaColor 转换为 Color4。
    /// </summary>
    /// <param name="color">MediaColor</param>
    /// <returns>Color4</returns>
    private static Color4 ToColor4(MediaColor color)
        => new(color.ScR, color.ScG, color.ScB, color.ScA);

    /// <summary>
    /// 释放管线绑定。
    /// </summary>
    private void ReleasePipelineBindings()
    {
        if (_deviceContext == null)
            return;

        try
        {
            _deviceContext.ClearState();
            _deviceContext.Flush();
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"[{DateTime.Now:O}] ReleasePipelineBindings: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
    }

    /// <summary>
    /// 记录失败信息。
    /// </summary>
    /// <param name="stage">失败阶段</param>
    /// <param name="ex">异常</param>
    private void RecordFailure(string stage, Exception ex)
    {
        LastError = $"{stage}: {ex.GetType().Name}: {ex.Message}";
        string message = $"[{DateTime.Now:O}] {LastError}{Environment.NewLine}{ex}{Environment.NewLine}";
        WriteDiagnostic(message);
    }

    /// <summary>
    /// 写入诊断日志。
    /// </summary>
    /// <param name="message">日志消息</param>
    private static void WriteDiagnostic(string message)
    {
        Debug.WriteLine(message);
        try
        {
            File.AppendAllText(s_diagnosticLogPath, message);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 释放目标资源。
    /// </summary>
    private void DisposeTargetResources()
    {
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        _targetWidth = 0;
        _targetHeight = 0;
        _boundColorTexturePointer = 0;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        Uninitialize();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 如果已释放则抛出异常。
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    #endregion
}

/// <summary>
/// 四边形顶点结构。
/// </summary>
internal struct QuadVertex
{
    /// <summary>
    /// 沿长度方向的坐标（0-1）。
    /// </summary>
    public float Along;

    /// <summary>
    /// 沿宽度方向的坐标（-1到1）。
    /// </summary>
    public float Side;
}

/// <summary>
/// 线着色器常量结构，对应 HLSL cbuffer LineConstants : register(b0)。
/// 
/// <para><b>HLSL cbuffer 与 C# 结构体对齐规则：</b></para>
/// <para>HLSL cbuffer 按 16 字节（4 × float）对齐打包。
/// C# 端使用 [StructLayout(LayoutKind.Sequential)] 确保字段按声明顺序排列，
/// 每个 Vector2 占 8 字节，连续两个 Vector2 恰好填满一个 16 字节寄存器槽。</para>
/// 
/// <list type="table">
///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 寄存器</description></listheader>
///   <item><term>ViewportSize</term><description>0  | 8  | gViewportSize (cb0.r0.xy)</description></item>
///   <item><term>XRange</term><description>8  | 8  | gXRange (cb0.r0.zw)</description></item>
///   <item><term>YRange</term><description>16 | 8  | gYRange (cb0.r1.xy)</description></item>
///   <item><term>RenderOptions</term><description>24 | 8  | gRenderOptions (cb0.r1.zw)</description></item>
/// </list>
/// <para>总大小: 32 字节（2 × 16），无需 Padding</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LineShaderConstants
{
    /// <summary>
    /// 视口大小。
    /// </summary>
    public Vector2 ViewportSize;

    /// <summary>
    /// X轴数据范围。
    /// </summary>
    public Vector2 XRange;

    /// <summary>
    /// Y轴数据范围。
    /// </summary>
    public Vector2 YRange;

    /// <summary>
    /// 渲染选项。
    /// </summary>
    public Vector2 RenderOptions;
}
