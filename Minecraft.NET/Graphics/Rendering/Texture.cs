using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class Texture : IDisposable
{
    private readonly D3D11Context _d3d;
    public ComPtr<ID3D11Texture2D> Tex;
    public ComPtr<ID3D11ShaderResourceView> SRV;
    public ComPtr<ID3D11SamplerState> Sampler;

    public Texture(D3D11Context d3d, string path)
    {
        _d3d = d3d;
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Texture2DDesc desc = new Texture2DDesc
        {
            Width = (uint)image.Width,
            Height = (uint)image.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Silk.NET.DXGI.Format.FormatR8G8B8A8Unorm,
            SampleDesc = new Silk.NET.DXGI.SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource
        };

        fixed (byte* ptr = image.Data)
        {
            SubresourceData subresource = new SubresourceData(ptr, (uint)image.Width * 4, (uint)(image.Width * image.Height * 4));
            fixed (ComPtr<ID3D11Texture2D>* texPtr = &Tex)
                _d3d.Device.CreateTexture2D(&desc, &subresource, (ID3D11Texture2D**)texPtr);
        }

        ShaderResourceViewDesc srvDesc = new ShaderResourceViewDesc
        {
            Format = desc.Format,
            ViewDimension = D3DSrvDimension.D3D101SrvDimensionTexture2D
        };
        srvDesc.Texture2D.MipLevels = 1;

        fixed (ComPtr<ID3D11ShaderResourceView>* srvPtr = &SRV)
            _d3d.Device.CreateShaderResourceView((ID3D11Resource*)Tex.Handle, &srvDesc, (ID3D11ShaderResourceView**)srvPtr);

        SamplerDesc sampDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipPoint,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue
        };

        fixed (ComPtr<ID3D11SamplerState>* sampPtr = &Sampler)
            _d3d.Device.CreateSamplerState(&sampDesc, (ID3D11SamplerState**)sampPtr);
    }

    public void Bind(uint slot = 0)
    {
        var srv = SRV.Handle;
        _d3d.Context.PSSetShaderResources(slot, 1, &srv);

        var samp = Sampler.Handle;
        _d3d.Context.PSSetSamplers(slot, 1, &samp);
    }

    public void Dispose()
    {
        Sampler.Dispose();
        SRV.Dispose();
        Tex.Dispose();
    }
}