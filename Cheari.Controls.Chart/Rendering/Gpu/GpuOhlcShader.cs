using System.Runtime.InteropServices;
using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuOhlcShader : IDisposable
{
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11Buffer? _constantBuffer;
    private readonly GpuDataBuffer _dataBuffer = new();
    private bool _isDisposed;

    // ═══════════════════════════════════════════════════════════════
    // GPU OHLC 着色器 — ByteAddressBuffer 实例化渲染，无抗锯齿
    //
    // 每根K线生成 12 个顶点（3 个四边形 × 4 顶点）：
    //   vertexIndex 0-3:  实体（开盘价-收盘价区域）
    //   vertexIndex 4-7:  上影线（最高价-实体上端）
    //   vertexIndex 8-11: 下影线（实体下端-最低价）
    //
    // 输入:
    //   gOhlcData (t0) — K线数据 (ByteAddressBuffer, 每根 5 × float = 20 字节)
    //                    布局: [X, Open, High, Low, Close]
    //   SV_VertexID   — 顶点索引 (0-11)
    //   SV_InstanceID — K线索引
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   COLOR0      — K线颜色（涨 → upColor, 跌 → downColor）
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
    float  gWickThickness;
    float4 gUpColor;
    float4 gDownColor;
};

ByteAddressBuffer gOhlcData : register(t0);

struct VSInput
{
    uint VertexID : SV_VertexID;
    uint InstanceID : SV_InstanceID;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

float2 DataToScreen(float2 data)
{
    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);
    float2 screen;
    screen.x = ((data.x - gXRange.x) / xLength) * gViewportSize.x;
    screen.y = gViewportSize.y - ((data.y - gYRange.x) / yLength) * gViewportSize.y;
    return screen;
}

float2 ScreenToNdc(float2 screen)
{
    float2 ndc;
    ndc.x = (screen.x / gViewportSize.x) * 2.0f - 1.0f;
    ndc.y = 1.0f - (screen.y / gViewportSize.y) * 2.0f;
    return ndc;
}

VSOutput main(VSInput input)
{
    int candleIndex = input.InstanceID;
    int vertexIndex = input.VertexID;

    uint offset = candleIndex * 20;
    float x = asfloat(gOhlcData.Load(offset));
    float openVal = asfloat(gOhlcData.Load(offset + 4));
    float highVal = asfloat(gOhlcData.Load(offset + 8));
    float lowVal = asfloat(gOhlcData.Load(offset + 12));
    float closeVal = asfloat(gOhlcData.Load(offset + 16));

    bool isUp = closeVal >= openVal;
    float4 color = isUp ? gUpColor : gDownColor;

    float bodyMin = min(openVal, closeVal);
    float bodyMax = max(openVal, closeVal);
    float barHalfWidth = gBarWidthData * 0.5f;
    float wickHalfWidth = gWickThickness * 0.5f;

    float2 pos;

    if (vertexIndex < 4)
    {
        float xMin = x - barHalfWidth;
        float xMax = x + barHalfWidth;
        float u = (vertexIndex == 0 || vertexIndex == 2) ? xMin : xMax;
        float v = (vertexIndex == 0 || vertexIndex == 1) ? bodyMax : bodyMin;
        pos = ScreenToNdc(DataToScreen(float2(u, v)));
    }
    else if (vertexIndex < 8)
    {
        int vi = vertexIndex - 4;
        float xMin = x - wickHalfWidth;
        float xMax = x + wickHalfWidth;
        float u = (vi == 0 || vi == 2) ? xMin : xMax;
        float v = (vi == 0 || vi == 1) ? highVal : bodyMax;
        pos = ScreenToNdc(DataToScreen(float2(u, v)));
    }
    else
    {
        int vi = vertexIndex - 8;
        float xMin = x - wickHalfWidth;
        float xMax = x + wickHalfWidth;
        float u = (vi == 0 || vi == 2) ? xMin : xMax;
        float v = (vi == 0 || vi == 1) ? bodyMin : lowVal;
        pos = ScreenToNdc(DataToScreen(float2(u, v)));
    }

    VSOutput output;
    output.Position = float4(pos, 0.0f, 1.0f);
    output.Color = color;
    return output;
}
""";

    private const string PixelShaderSource = """
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

    /// <summary>
    /// GPU OHLC 着色器常量结构，对应 HLSL cbuffer RenderConstants : register(b0)。
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
    ///   <item><term>WickThickness</term><description>40 | 4  | gWickThickness (r2.z)</description></item>
    ///   <item><term>_padding0</term><description>44 | 4  | 对齐填充 (r2.w)</description></item>
    ///   <item><term>UpColor</term><description>48 | 16 | gUpColor (r3)</description></item>
    ///   <item><term>DownColor</term><description>64 | 16 | gDownColor (r4)</description></item>
    /// </list>
    /// <para>总大小: 80 字节（5 × 16）</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct OhlcRenderConstants
    {
        public Vector2 ViewportSize;
        public Vector2 XRange;
        public Vector2 YRange;
        public Vector2 RenderOptions;
        public int PointCount;
        public float BarWidthData;
        public float WickThickness;
        private float _padding0;
        public Vector4 UpColor;
        public Vector4 DownColor;
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
            (uint)Marshal.SizeOf<OhlcRenderConstants>(),
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        _dataBuffer.EnsureCapacity(device, 1024, 20);
    }

    public void Render(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context,
        DataFrame frame,
        int width, int height,
        DataRange xRange, DataRange yRange,
        float barWidthData, float wickThickness,
        Vector4 upColor, Vector4 downColor)
    {
        if (_vertexShader == null || _pixelShader == null)
            return;

        if (frame.XValues == null || frame.OpenValues == null || frame.Count == 0)
            return;

        int count = frame.Count;
        int floatCount = count * 5;
        _dataBuffer.EnsureCapacity(device, floatCount, 20);

        var data = floatCount <= 5120 ? stackalloc float[5120] : new float[floatCount];
        for (int i = 0; i < count; i++)
        {
            data[i * 5] = frame.XValues[i];
            data[i * 5 + 1] = frame.OpenValues![i];
            data[i * 5 + 2] = frame.HighValues![i];
            data[i * 5 + 3] = frame.LowValues![i];
            data[i * 5 + 4] = frame.CloseValues![i];
        }
        _dataBuffer.Update(context, data.Slice(0, floatCount));

        UpdateConstants(context, width, height, xRange, yRange, count, barWidthData, wickThickness, upColor, downColor);

        _dataBuffer.BindAsShaderResource(context, device, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        context.IASetInputLayout(null);
        context.VSSetShader(_vertexShader);
        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetShader(_pixelShader);

        context.DrawInstanced(12, (uint)count, 0, 0);
    }

    private void UpdateConstants(
        ID3D11DeviceContext1 context,
        int width, int height,
        DataRange xRange, DataRange yRange,
        int pointCount, float barWidthData, float wickThickness,
        Vector4 upColor, Vector4 downColor)
    {
        if (_constantBuffer == null)
            return;

        var constants = new OhlcRenderConstants
        {
            ViewportSize = new Vector2(width, height),
            XRange = new Vector2((float)xRange.Min, (float)xRange.Max),
            YRange = new Vector2((float)yRange.Min, (float)yRange.Max),
            RenderOptions = Vector2.Zero,
            PointCount = pointCount,
            BarWidthData = barWidthData,
            WickThickness = wickThickness,
            UpColor = upColor,
            DownColor = downColor
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
