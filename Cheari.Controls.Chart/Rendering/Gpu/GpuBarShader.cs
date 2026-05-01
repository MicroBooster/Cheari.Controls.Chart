using System.Runtime.InteropServices;
using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuBarShader : IDisposable
{
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11Buffer? _constantBuffer;
    private readonly GpuDataBuffer _dataBuffer = new();
    private bool _isDisposed;

    // ═══════════════════════════════════════════════════════════════
    // GPU 柱状图着色器 — StructuredBuffer 实例化渲染，无抗锯齿
    //
    // 输入:
    //   gBarData (t0) — 柱体数据缓冲区 (float4 × pointCount)
    //                   每个元素: (X, Y, reserved, reserved)
    //   SV_VertexID   — 四边形内顶点索引 (0-3)
    //   SV_InstanceID — 柱体索引
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — 柱体颜色（Y >= baseline → fillColor, 否则 strokeColor）
    //   TEXCOORD0   — 局部 UV 坐标
    // ═══════════════════════════════════════════════════════════════

    private const string VertexShaderSource = """
cbuffer RenderConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    float2 gRenderOptions;
    int    gPointCount;
    float  gBarWidthData;
    float  gBaselineY;
    float4 gFillColor;
    float4 gStrokeColor;
};

StructuredBuffer<float4> gBarData : register(t0);

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
};

VSOutput main(VSInput input)
{
    int barIndex = input.InstanceID;
    int vertexInQuad = input.VertexID;

    float4 bar = gBarData[barIndex];
    float x = bar.x;
    float y = bar.y;
    float barHalfWidth = gBarWidthData * 0.5f;

    float xMin = x - barHalfWidth;
    float xMax = x + barHalfWidth;
    float yMin = min(y, gBaselineY);
    float yMax = max(y, gBaselineY);

    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float2 minScreen;
    minScreen.x = ((xMin - gXRange.x) / xLength) * gViewportSize.x;
    minScreen.y = gViewportSize.y - ((yMax - gYRange.x) / yLength) * gViewportSize.y;

    float2 maxScreen;
    maxScreen.x = ((xMax - gXRange.x) / xLength) * gViewportSize.x;
    maxScreen.y = gViewportSize.y - ((yMin - gYRange.x) / yLength) * gViewportSize.y;

    float u = (vertexInQuad == 0 || vertexInQuad == 2) ? 0.0f : 1.0f;
    float v = (vertexInQuad == 0 || vertexInQuad == 1) ? 0.0f : 1.0f;

    float2 pixelPosition;
    pixelPosition.x = lerp(minScreen.x, maxScreen.x, u);
    pixelPosition.y = lerp(minScreen.y, maxScreen.y, v);

    float2 ndc;
    ndc.x = (pixelPosition.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (pixelPosition.y / gViewportSize.y) * 2.0f;

    VSOutput output;
    output.Position = float4(ndc, 0.0f, 1.0f);
    output.Color = y >= gBaselineY ? gFillColor : gStrokeColor;
    output.Local = float2(u, v);
    return output;
}
""";

    private const string PixelShaderSource = """
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

    /// <summary>
    /// GPU 柱状图着色器常量结构，对应 HLSL cbuffer RenderConstants : register(b0)。
    /// 
    /// <para><b>内存布局（16 字节对齐）：</b></para>
    /// <list type="table">
    ///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 寄存器</description></listheader>
    ///   <item><term>ViewportSize</term><description>0  | 8  | gViewportSize (r0.xy)</description></item>
    ///   <item><term>XRange</term><description>8  | 8  | gXRange (r0.zw)</description></item>
    ///   <item><term>YRange</term><description>16 | 8  | gYRange (r1.xy)</description></item>
    ///   <item><term>RenderOptions</term><description>24 | 8  | gRenderOptions (r1.zw)</description></item>
    ///   <item><term>PointCount</term><description>32 | 4  | gPointCount (r2.x)</description></item>
    ///   <item><term>BarWidthData</term><description>36 | 4  | gBarWidthData (r2.y)</description></item>
    ///   <item><term>BaselineY</term><description>40 | 4  | gBaselineY (r2.z)</description></item>
    ///   <item><term>_padding0</term><description>44 | 4  | 对齐填充 (r2.w)</description></item>
    ///   <item><term>FillColor</term><description>48 | 16 | gFillColor (r3)</description></item>
    ///   <item><term>StrokeColor</term><description>64 | 16 | gStrokeColor (r4)</description></item>
    /// </list>
    /// <para>总大小: 80 字节（5 × 16）</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BarRenderConstants
    {
        public Vector2 ViewportSize;
        public Vector2 XRange;
        public Vector2 YRange;
        public Vector2 RenderOptions;
        public int PointCount;
        public float BarWidthData;
        public float BaselineY;
        private float _padding0;
        public Vector4 FillColor;
        public Vector4 StrokeColor;
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
            (uint)Marshal.SizeOf<BarRenderConstants>(),
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        _dataBuffer.EnsureCapacity(device, 1024, 16);
    }

    public void Render(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context,
        DataFrame frame,
        int width, int height,
        DataRange xRange, DataRange yRange,
        float barWidthData, float baselineY,
        Vector4 fillColor, Vector4 strokeColor)
    {
        if (_vertexShader == null || _pixelShader == null)
            return;

        if (frame.XValues == null || frame.YValues == null || frame.Count == 0)
            return;

        int count = frame.Count;
        int floatCount = count * 4;
        _dataBuffer.EnsureCapacity(device, floatCount, 16);

        var data = floatCount <= 4096 ? stackalloc float[4096] : new float[floatCount];
        for (int i = 0; i < count; i++)
        {
            data[i * 4] = frame.XValues[i];
            data[i * 4 + 1] = frame.YValues[i];
            data[i * 4 + 2] = 0.0f;
            data[i * 4 + 3] = 0.0f;
        }
        _dataBuffer.Update(context, data.Slice(0, floatCount));

        UpdateConstants(context, width, height, xRange, yRange, count, barWidthData, baselineY, fillColor, strokeColor);

        _dataBuffer.BindAsShaderResource(context, device, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        context.IASetInputLayout(null);
        context.VSSetShader(_vertexShader);
        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetShader(_pixelShader);

        context.DrawInstanced(4, (uint)count, 0, 0);
    }

    private void UpdateConstants(
        ID3D11DeviceContext1 context,
        int width, int height,
        DataRange xRange, DataRange yRange,
        int pointCount, float barWidthData, float baselineY,
        Vector4 fillColor, Vector4 strokeColor)
    {
        if (_constantBuffer == null)
            return;

        var constants = new BarRenderConstants
        {
            ViewportSize = new Vector2(width, height),
            XRange = new Vector2((float)xRange.Min, (float)xRange.Max),
            YRange = new Vector2((float)yRange.Min, (float)yRange.Max),
            RenderOptions = Vector2.Zero,
            PointCount = pointCount,
            BarWidthData = barWidthData,
            BaselineY = baselineY,
            FillColor = fillColor,
            StrokeColor = strokeColor
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
        _dataBuffer.Dispose();
        _isDisposed = true;
    }
}
