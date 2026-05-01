using System;
using Vortice.Direct3D11;

var blend = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha);
Console.WriteLine(blend.AlphaToCoverageEnable);
Console.WriteLine(blend.IndependentBlendEnable);
Console.WriteLine(blend.RenderTarget[0].BlendEnable);
Console.WriteLine(blend.RenderTarget[0].SourceBlend);
Console.WriteLine(blend.RenderTarget[0].DestinationBlend);
Console.WriteLine(blend.RenderTarget[0].SourceBlendAlpha);
Console.WriteLine(blend.RenderTarget[0].DestinationBlendAlpha);
Console.WriteLine(blend.RenderTarget[0].RenderTargetWriteMask);
blend.RenderTarget[0].SourceBlendAlpha = Blend.One;
blend.RenderTarget[0].DestinationBlendAlpha = Blend.InverseSourceAlpha;
blend.RenderTarget[0].BlendOperationAlpha = BlendOperation.Add;
blend.RenderTarget[0].RenderTargetWriteMask = ColorWriteEnable.All;
Console.WriteLine("ok");
