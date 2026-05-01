using System.Numerics;
using Cheari.Controls.Core;
using Cheari.Controls.Data;
using Cheari.Controls.Rendering.Strategy;
using Vortice.Direct3D11;

namespace Cheari.Controls.Rendering.Gpu;

internal sealed class GpuHybridRenderer : IDisposable
{
    private readonly GpuLineShader _lineShader = new();
    private readonly GpuRasterShader _rasterShader = new();
    private bool _isInitialized;
    private bool _isDisposed;

    public void Initialize(ID3D11Device1 device)
    {
        if (_isInitialized) return;

        _lineShader.Initialize(device);
        _rasterShader.Initialize(device);
        _isInitialized = true;
    }

    public void Render(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context,
        DataFrame frame,
        int width, int height,
        DataRange xRange, DataRange yRange,
        RenderStrategy strategy,
        bool enableAntialiasing,
        float thickness,
        Vector4 lineColor)
    {
        if (!_isInitialized || frame.Count < 2)
            return;

        switch (strategy)
        {
            case RenderStrategy.Vector:
                _lineShader.Render(device, context, frame, width, height, enableAntialiasing, thickness, lineColor);
                break;

            case RenderStrategy.Raster:
                _rasterShader.Render(device, context, frame, width, height, xRange, yRange);
                break;

            case RenderStrategy.Hybrid:
                _rasterShader.Render(device, context, frame, width, height, xRange, yRange);
                _lineShader.Render(device, context, frame, width, height, enableAntialiasing, thickness, lineColor);
                break;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _lineShader.Dispose();
        _rasterShader.Dispose();
        _isDisposed = true;
    }
}
