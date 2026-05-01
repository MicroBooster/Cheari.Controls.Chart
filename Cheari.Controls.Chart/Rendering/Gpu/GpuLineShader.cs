using System.Runtime.InteropServices;
using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuLineShader : IDisposable
{
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11Buffer? _constantBuffer;
    private ID3D11Buffer _quadVertexBuffer = null!;
    private readonly GpuDataBuffer _dataBuffer = new();
    private bool _isDisposed;

    // ═══════════════════════════════════════════════════════════════
    // GPU 线着色器 — StructuredBuffer 实例化渲染，支持抗锯齿 feather
    //
    // 与 D3D11Renderer 中的 LineVertexShader 不同，此着色器使用
    // StructuredBuffer<float2> 传递数据点，由 SV_VertexID 和
    // SV_InstanceID 计算四边形顶点位置，无需实例缓冲区。
    //
    // 输入:
    //   gDataPoints (t0) — 数据点缓冲区 (float2 × pointCount)
    //   SV_VertexID      — 四边形内顶点索引 (0-3)
    //   SV_InstanceID    — 线段索引
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 线条颜色（来自 cbuffer）
    //   TEXCOORD0   — 局部坐标 (localAlong, localSide)
    //   TEXCOORD1   — 度量值 (halfLength, halfThickness, feather)
    //
    // 抗锯齿: 同 LinePixelShader，使用 feather + smoothstep
    // ═══════════════════════════════════════════════════════════════

    private const string VertexShaderSource = """
cbuffer RenderConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
    int    gPointCount;
    float  gThickness;
    float4 gColor;
};

StructuredBuffer<float2> gDataPoints : register(t0);

struct VSInput
{
    uint VertexID : SV_VertexID;
    uint InstanceID : SV_InstanceID;
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
    int segmentIndex = input.InstanceID;
    int vertexInQuad = input.VertexID;

    float2 startData = gDataPoints[segmentIndex];
    float2 endData = gDataPoints[segmentIndex + 1];

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
    float halfThickness = gThickness * 0.5f;
    float expandedHalfThickness = halfThickness + feather;
    float halfLength = directionLength * 0.5f;
    float expandedHalfLength = halfLength + feather;

    float alongSign = (vertexInQuad == 0 || vertexInQuad == 1) ? -1.0f : 1.0f;
    float sideSign = (vertexInQuad == 0 || vertexInQuad == 2) ? -1.0f : 1.0f;

    float localAlong = alongSign * expandedHalfLength;
    float localSide = sideSign * expandedHalfThickness;
    float2 center = (startScreen + endScreen) * 0.5f;
    float2 pixelPosition = center + direction * localAlong + normal * localSide;

    float2 ndc;
    ndc.x = (pixelPosition.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (pixelPosition.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = gColor;
    output.Local = float2(localAlong, localSide);
    output.Metrics = float3(halfLength, halfThickness, feather);
    return output;
}
""";

    private const string PixelShaderSource = """
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

    /// <summary>
    /// GPU 线着色器常量结构，对应 HLSL cbuffer RenderConstants : register(b0)。
    /// 
    /// <para><b>内存布局（16 字节对齐）：</b></para>
    /// <list type="table">
    ///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 寄存器</description></listheader>
    ///   <item><term>ViewportSize</term><description>0  | 8  | gViewportSize (r0.xy)</description></item>
    ///   <item><term>XRange</term><description>8  | 8  | gXRange (r0.zw)</description></item>
    ///   <item><term>YRange</term><description>16 | 8  | gYRange (r1.xy)</description></item>
    ///   <item><term>RenderOptions</term><description>24 | 8  | gRenderOptions (r1.zw)</description></item>
    ///   <item><term>PointCount</term><description>32 | 4  | gPointCount (r2.x)</description></item>
    ///   <item><term>Thickness</term><description>36 | 4  | gThickness (r2.y)</description></item>
    ///   <item><term>Color</term><description>48 | 16 | gColor (r3)</description></item>
    ///   <item><term>_padding0, _padding1</term><description>40 | 8  | 对齐填充 (r2.zw)</description></item>
    /// </list>
    /// <para>总大小: 64 字节（4 × 16）</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RenderConstants
    {
        public Vector2 ViewportSize;
        public Vector2 XRange;
        public Vector2 YRange;
        public Vector2 RenderOptions;
        public int PointCount;
        public float Thickness;
        public Vector4 Color;
        private float _padding0;
        private float _padding1;
    }

    public void Initialize(ID3D11Device1 device)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        var vsBytes = Compiler.Compile(VertexShaderSource, "main", "inline", "vs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
        var psBytes = Compiler.Compile(PixelShaderSource, "main", "inline", "ps_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);

        _vertexShader = device.CreateVertexShader(vsBytes.ToArray());
        _pixelShader = device.CreatePixelShader(psBytes.Span);

        _constantBuffer = device.CreateBuffer(
            (uint)Marshal.SizeOf<RenderConstants>(),
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        _dataBuffer.EnsureCapacity(device, 1024);
    }

    public void Render(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context,
        DataFrame frame,
        int width,
        int height,
        bool enableAntialiasing,
        float thickness,
        Vector4 color)
    {
        if (_vertexShader == null || _pixelShader == null)
            return;

        if (frame.XValues == null || frame.YValues == null || frame.Count < 2)
            return;

        int pointCount = frame.Count;
        int floatCount = pointCount * 2;
        _dataBuffer.EnsureCapacity(device, floatCount);

        var interleaved = floatCount <= 2048 ? stackalloc float[2048] : new float[floatCount];
        for (int i = 0; i < pointCount; i++)
        {
            interleaved[i * 2] = frame.XValues[i];
            interleaved[i * 2 + 1] = frame.YValues[i];
        }
        _dataBuffer.Update(context, interleaved.Slice(0, floatCount));

        UpdateConstants(context, width, height, enableAntialiasing, frame.XRange, frame.YRange, pointCount, thickness, color);

        _dataBuffer.BindAsShaderResource(context, device, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        context.IASetInputLayout(null);
        context.VSSetShader(_vertexShader);
        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetShader(_pixelShader);

        context.DrawInstanced(4, (uint)(pointCount - 1), 0, 0);
    }

    private void UpdateConstants(
        ID3D11DeviceContext1 context,
        int width, int height,
        bool enableAntialiasing,
        DataRange xRange, DataRange yRange,
        int pointCount, float thickness, Vector4 color)
    {
        if (_constantBuffer == null)
            return;

        var constants = new RenderConstants
        {
            ViewportSize = new Vector2(width, height),
            XRange = new Vector2((float)xRange.Min, (float)xRange.Max),
            YRange = new Vector2((float)yRange.Min, (float)yRange.Max),
            RenderOptions = new Vector2(enableAntialiasing ? 1.0f : 0.0f, 0.0f),
            PointCount = pointCount,
            Thickness = thickness,
            Color = color
        };

        var mapped = context.Map(_constantBuffer, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            Marshal.StructureToPtr(constants, mapped.DataPointer, false);
        }
        finally
        {
            context.Unmap(_constantBuffer);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _vertexShader?.Dispose();
        _pixelShader?.Dispose();
        _constantBuffer?.Dispose();
        _quadVertexBuffer?.Dispose();
        _dataBuffer.Dispose();
        _isDisposed = true;
    }
}
