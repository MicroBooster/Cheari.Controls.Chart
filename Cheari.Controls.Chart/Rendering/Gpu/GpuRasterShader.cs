using System.Runtime.InteropServices;
using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuRasterShader : IDisposable
{
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11Buffer? _constantBuffer;
    private readonly GpuDataBuffer _dataBuffer = new();
    private ID3D11Texture2D _renderTargetTexture = null!;
    private ID3D11RenderTargetView _renderTargetView = null!;
    private ID3D11ShaderResourceView _renderTargetSrv = null!;
    private bool _isDisposed;

    // ═══════════════════════════════════════════════════════════════
    // GPU 光栅化着色器 — 全屏四边形像素级渲染
    //
    // 顶点着色器生成覆盖整个视口的四边形（4 个顶点），
    // 像素着色器对每个像素进行线性插值查找数据点，
    // 在曲线下方区域填充半透明颜色。
    //
    // 输入:
    //   gDataPoints (t0) — 数据点缓冲区 (StructuredBuffer<float2>)
    //   SV_VertexID      — 顶点索引 (0-3)
    //
    // 输出语义:
    //   SV_Position — 裁剪空间位置
    //   TEXCOORD0   — UV 坐标 [0,1] × [0,1]
    // ═══════════════════════════════════════════════════════════════

    private const string VertexShaderSource = """
cbuffer RasterConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    int    gPointCount;
    float  gPadding1;
    float  gPadding2;
    float  gPadding3;
};

struct VSInput
{
    uint VertexID : SV_VertexID;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    float2 positions[4] =
    {
        float2(-1.0f, 1.0f),
        float2(1.0f, 1.0f),
        float2(-1.0f, -1.0f),
        float2(1.0f, -1.0f)
    };

    float2 uvs[4] =
    {
        float2(0.0f, 0.0f),
        float2(1.0f, 0.0f),
        float2(0.0f, 1.0f),
        float2(1.0f, 1.0f)
    };

    output.Position = float4(positions[input.VertexID], 0.0f, 1.0f);
    output.TexCoord = uvs[input.VertexID];
    return output;
}
""";

    private const string PixelShaderSource = """
cbuffer RasterConstants : register(b0)
{
    float2 gViewportSize;
    float2 gXRange;
    float2 gYRange;
    int    gPointCount;
    float  gPadding1;
    float  gPadding2;
    float  gPadding3;
};

StructuredBuffer<float2> gDataPoints : register(t0);

float4 main(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    float xLength = max(gXRange.y - gXRange.x, 1e-6f);
    float yLength = max(gYRange.y - gYRange.x, 1e-6f);

    float dataX = lerp(gXRange.x, gXRange.y, uv.x);
    float dataY = lerp(gYRange.y, gYRange.x, uv.y);

    int leftIndex = (int)(uv.x * (gPointCount - 1));
    int rightIndex = min(leftIndex + 1, gPointCount - 1);

    float leftX = gDataPoints[leftIndex].x;
    float rightX = gDataPoints[rightIndex].x;
    float leftY = gDataPoints[leftIndex].y;
    float rightY = gDataPoints[rightIndex].y;

    float xSpan = max(rightX - leftX, 1e-6f);
    float t = clamp((dataX - leftX) / xSpan, 0.0f, 1.0f);
    float curveY = lerp(leftY, rightY, t);

    if (dataY <= curveY)
        return float4(0.2f, 0.4f, 0.8f, 0.3f);

    return float4(0.0f, 0.0f, 0.0f, 0.0f);
}
""";

    /// <summary>
    /// GPU 光栅化着色器常量结构，对应 HLSL cbuffer RasterConstants : register(b0)。
    /// 
    /// <para><b>内存布局（16 字节对齐）：</b></para>
    /// <list type="table">
    ///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 寄存器</description></listheader>
    ///   <item><term>ViewportSize</term><description>0  | 8  | gViewportSize (r0.xy)</description></item>
    ///   <item><term>XRange</term><description>8  | 8  | gXRange (r0.zw)</description></item>
    ///   <item><term>YRange</term><description>16 | 8  | gYRange (r1.xy)</description></item>
    ///   <item><term>PointCount</term><description>24 | 4  | gPointCount (r1.z)</description></item>
    ///   <item><term>Padding1-5</term><description>28 | 20 | 对齐填充 (r1.w ~ r5)</description></item>
    /// </list>
    /// <para>总大小: 48 字节（3 × 16）</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RasterConstants
    {
        public Vector2 ViewportSize;
        public Vector2 XRange;
        public Vector2 YRange;
        public int PointCount;
        public float Padding1;
        public float Padding2;
        public float Padding3;
        public float Padding4;
        public float Padding5;
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
            (uint)Marshal.SizeOf<RasterConstants>(),
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
        int width, int height,
        DataRange xRange, DataRange yRange)
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

        UpdateConstants(context, width, height, xRange, yRange, pointCount);

        _dataBuffer.BindAsShaderResource(context, device, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        context.IASetInputLayout(null);
        context.VSSetShader(_vertexShader);
        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetShader(_pixelShader);

        context.Draw(4, 0);
    }

    private void UpdateConstants(
        ID3D11DeviceContext1 context,
        int width, int height,
        DataRange xRange, DataRange yRange,
        int pointCount)
    {
        if (_constantBuffer == null)
            return;

        var constants = new RasterConstants
        {
            ViewportSize = new Vector2(width, height),
            XRange = new Vector2((float)xRange.Min, (float)xRange.Max),
            YRange = new Vector2((float)yRange.Min, (float)yRange.Max),
            PointCount = pointCount
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
        _renderTargetView?.Dispose();
        _renderTargetSrv?.Dispose();
        _renderTargetTexture?.Dispose();
        _dataBuffer.Dispose();
        _isDisposed = true;
    }
}
