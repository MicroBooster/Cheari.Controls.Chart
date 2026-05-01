using System.Runtime.InteropServices;
using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Vortice.D3DCompiler;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuDownsampleShader : IDisposable
{
    private ID3D11ComputeShader? _computeShader;
    private ID3D11Buffer? _constantBuffer;
    private readonly GpuDataBuffer _inputBuffer = new();
    private readonly GpuDataBuffer _outputBuffer = new();
    private ID3D11Buffer _readbackBuffer = null!;
    private bool _isDisposed;

    // ═══════════════════════════════════════════════════════════════
    // GPU 降采样计算着色器 — MinMax 算法
    //
    // 将 N 个数据点降采样为 targetCount 个桶，每个桶输出 4 个点：
    //   [0] 桶起始点
    //   [1] 桶内最小值点（按 Y 值）
    //   [2] 桶内最大值点（按 Y 值）
    //   [3] 桶结束点
    // 这样保证降采样后的线条保留了原始数据的视觉极值特征。
    //
    // 输入:
    //   gInput (t0)  — 原始数据点 (StructuredBuffer<float2>)
    //   gOutput (u0) — 降采样结果 (RWStructuredBuffer<float2>)
    //
    // 线程组: [numthreads(64, 1, 1)]
    // ═══════════════════════════════════════════════════════════════

    private const string ComputeShaderSource = """
cbuffer DownsampleConstants : register(b0)
{
    int gPointCount;
    int gTargetCount;
    int gBucketSize;
    float gPadding;
};

StructuredBuffer<float2> gInput : register(t0);
RWStructuredBuffer<float2> gOutput : register(u0);

[numthreads(64, 1, 1)]
void CSMain(uint3 DTid : SV_DispatchThreadID)
{
    int bucketIndex = (int)DTid.x;
    if (bucketIndex >= gTargetCount)
        return;

    int bucketStart = bucketIndex * gBucketSize;
    int bucketEnd = min(bucketStart + gBucketSize, gPointCount);

    if (bucketStart >= gPointCount)
        return;

    float minY = 1e38f;
    float maxY = -1e38f;
    int minIdx = bucketStart;
    int maxIdx = bucketStart;

    for (int i = bucketStart; i < bucketEnd; i++)
    {
        float y = gInput[i].y;
        if (y < minY) { minY = y; minIdx = i; }
        if (y > maxY) { maxY = y; maxIdx = i; }
    }

    int outBase = bucketIndex * 4;
    gOutput[outBase + 0] = gInput[bucketStart];
    if (minIdx <= maxIdx)
    {
        gOutput[outBase + 1] = gInput[minIdx];
        gOutput[outBase + 2] = gInput[maxIdx];
    }
    else
    {
        gOutput[outBase + 1] = gInput[maxIdx];
        gOutput[outBase + 2] = gInput[minIdx];
    }
    gOutput[outBase + 3] = gInput[bucketEnd - 1];
}
""";

    /// <summary>
    /// GPU 降采样计算着色器常量结构，对应 HLSL cbuffer DownsampleConstants : register(b0)。
    /// 
    /// <para><b>内存布局（16 字节对齐）：</b></para>
    /// <list type="table">
    ///   <listheader><term>字段</term><description>偏移 | 大小 | HLSL 寄存器</description></listheader>
    ///   <item><term>PointCount</term><description>0  | 4 | gPointCount (r0.x)</description></item>
    ///   <item><term>TargetCount</term><description>4  | 4 | gTargetCount (r0.y)</description></item>
    ///   <item><term>BucketSize</term><description>8  | 4 | gBucketSize (r0.z)</description></item>
    ///   <item><term>Padding</term><description>12 | 4 | 对齐填充 (r0.w)</description></item>
    /// </list>
    /// <para>总大小: 16 字节（1 × 16）</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DownsampleConstants
    {
        public int PointCount;
        public int TargetCount;
        public int BucketSize;
        public float Padding;
    }

    public void Initialize(ID3D11Device1 device)
    {
        var csBytes = Compiler.Compile(ComputeShaderSource, "CSMain", "inline", "cs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);

        _computeShader = device.CreateComputeShader(csBytes.Span);

        _constantBuffer = device.CreateBuffer(
            (uint)Marshal.SizeOf<DownsampleConstants>(),
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        _inputBuffer.EnsureCapacity(device, 1024);
        _outputBuffer.EnsureCapacity(device, 4096);
    }

    public float[]? Downsample(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context,
        DataFrame frame,
        int targetCount)
    {
        if (_computeShader == null || frame.XValues == null || frame.YValues == null)
            return null;

        int pointCount = frame.Count;
        if (pointCount <= targetCount * 2)
            return null;

        int bucketSize = Math.Max(1, pointCount / targetCount);
        int inputFloatCount = pointCount * 2;
        int outputFloatCount = targetCount * 4 * 2;

        _inputBuffer.EnsureCapacity(device, inputFloatCount);
        _outputBuffer.EnsureCapacity(device, outputFloatCount);

        var interleaved = inputFloatCount <= 2048 ? stackalloc float[2048] : new float[inputFloatCount];
        for (int i = 0; i < pointCount; i++)
        {
            interleaved[i * 2] = frame.XValues[i];
            interleaved[i * 2 + 1] = frame.YValues[i];
        }
        _inputBuffer.Update(context, interleaved.Slice(0, inputFloatCount));

        var constants = new DownsampleConstants
        {
            PointCount = pointCount,
            TargetCount = targetCount,
            BucketSize = bucketSize,
            Padding = 0.0f
        };

        var mapped = context.Map(_constantBuffer!, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            Marshal.StructureToPtr(constants, mapped.DataPointer, false);
        }
        finally
        {
            context.Unmap(_constantBuffer!);
        }

        _inputBuffer.BindAsShaderResource(context, device, 0);
        _outputBuffer.BindAsShaderResource(context, device, 0);

        context.CSSetShader(_computeShader);
        context.CSSetConstantBuffer(0, _constantBuffer);
        context.Dispatch((uint)((targetCount + 63) / 64), 1, 1);

        context.CSSetShader(null);

        return ReadbackOutput(context, device, targetCount * 4);
    }

    private float[]? ReadbackOutput(ID3D11DeviceContext1 context, ID3D11Device1 device, int outputPointCount)
    {
        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _computeShader?.Dispose();
        _constantBuffer?.Dispose();
        _readbackBuffer?.Dispose();
        _inputBuffer.Dispose();
        _outputBuffer.Dispose();
        _isDisposed = true;
    }
}
