namespace Cheari.Controls.Rendering;

internal interface IChartRendererFactory
{
    IRenderer CreateHardwareRenderer();

    IRenderer CreateSoftwareRenderer();
}

internal sealed class DefaultChartRendererFactory : IChartRendererFactory
{
    public IRenderer CreateHardwareRenderer() => new D3D11Renderer();

    public IRenderer CreateSoftwareRenderer() => new GdiRenderer();
}
