using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

/// <summary>
/// GPU 数据缓冲区，用于将浮点数据上传到 GPU 并绑定为着色器资源。
/// </summary>
internal sealed class GpuDataBuffer : IDisposable
{
    private ID3D11Buffer? _buffer;
    private ID3D11ShaderResourceView? _shaderResourceView;
    private int _capacity;
    private int _committedCount;
    private bool _isDisposed;

    public int CommittedCount => _committedCount;

    public ID3D11Buffer? Buffer => _buffer;

    public void EnsureCapacity(ID3D11Device1 device, int floatCount, int structureByteStride = 8)
    {
        if (_buffer != null && _capacity >= floatCount)
            return;

        _shaderResourceView?.Dispose();
        _shaderResourceView = null;
        _buffer?.Dispose();

        _capacity = Math.Max(floatCount, 256);
        uint byteWidth = (uint)(_capacity * 4);

        _buffer = device.CreateBuffer(
            byteWidth,
            BindFlags.ShaderResource,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

        _committedCount = 0;
    }

    /// <summary>
    /// 将浮点数据上传到 GPU 缓冲区。
    /// 使用 Marshal.Copy 直接从托管数组拷贝到非托管内存，避免 Span.ToArray() 产生的额外堆分配。
    /// </summary>
    public void Update(ID3D11DeviceContext1 context, ReadOnlySpan<float> data)
    {
        if (_buffer == null || data.IsEmpty)
            return;

        var mapped = context.Map(_buffer, MapMode.WriteDiscard, MapFlags.None);
        try
        {
            int byteCount = data.Length * sizeof(float);
            float[]? rentedArray = null;
            float[] sourceArray;

            if (data.Length <= 2048)
            {
                rentedArray = System.Buffers.ArrayPool<float>.Shared.Rent(data.Length);
                data.CopyTo(rentedArray);
                sourceArray = rentedArray;
            }
            else
            {
                sourceArray = data.ToArray();
            }

            try
            {
                Marshal.Copy(sourceArray, 0, mapped.DataPointer, data.Length);
            }
            finally
            {
                if (rentedArray != null)
                    System.Buffers.ArrayPool<float>.Shared.Return(rentedArray);
            }

            _committedCount = data.Length;
        }
        finally
        {
            context.Unmap(_buffer);
        }
    }

    public void BindAsShaderResource(ID3D11DeviceContext1 context, ID3D11Device1 device, int slot)
    {
        if (_buffer == null)
            return;

        _shaderResourceView ??= device.CreateShaderResourceView(_buffer);

        context.PSSetShaderResource((uint)slot, _shaderResourceView);
        context.VSSetShaderResource((uint)slot, _shaderResourceView);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _shaderResourceView?.Dispose();
        _buffer?.Dispose();
        _buffer = null;
        _shaderResourceView = null;
        _capacity = 0;
        _committedCount = 0;
        _isDisposed = true;
    }
}
