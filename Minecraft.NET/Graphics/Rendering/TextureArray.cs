using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class TextureArray : IDisposable
{
    private readonly D3D11Context _d3d;
    public ComPtr<ID3D11Texture2D> Texture;
    public ComPtr<ID3D11ShaderResourceView> SRV;
    public ComPtr<ID3D11SamplerState> Sampler;

    private bool _isDisposed;

    public TextureArray(D3D11Context d3d, List<string> filePaths)
    {
        _d3d = d3d;
        using var firstStream = File.OpenRead(filePaths[0]);
        var firstImage = ImageResult.FromStream(firstStream, ColorComponents.RedGreenBlueAlpha);
        uint width = (uint)firstImage.Width;
        uint height = (uint)firstImage.Height;
        uint layers = (uint)filePaths.Count;

        Texture2DDesc desc = new Texture2DDesc
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = layers,
            Format = Silk.NET.DXGI.Format.FormatR8G8B8A8Unorm,
            SampleDesc = new Silk.NET.DXGI.SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };

        SubresourceData* subresources = stackalloc SubresourceData[(int)layers];
        List<byte[]> buffers = [];

        for (int i = 0; i < layers; i++)
        {
            using var stream = File.OpenRead(filePaths[i]);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            buffers.Add(image.Data);
        }

        for (int i = 0; i < layers; i++)
        {
            fixed (byte* ptr = buffers[i])
            {
                subresources[i] = new SubresourceData(ptr, width * 4, width * height * 4);
            }
        }

        fixed (ComPtr<ID3D11Texture2D>* texPtr = &Texture)
            _d3d.Device.CreateTexture2D(&desc, subresources, (ID3D11Texture2D**)texPtr);

        ShaderResourceViewDesc srvDesc = new ShaderResourceViewDesc
        {
            Format = desc.Format,
            ViewDimension = D3DSrvDimension.D3D101SrvDimensionTexture2Darray
        };
        srvDesc.Texture2DArray.ArraySize = layers;
        srvDesc.Texture2DArray.MipLevels = 1;

        fixed (ComPtr<ID3D11ShaderResourceView>* srvPtr = &SRV)
            _d3d.Device.CreateShaderResourceView((ID3D11Resource*)Texture.Handle, &srvDesc, (ID3D11ShaderResourceView**)srvPtr);

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
        if (_isDisposed) return;
        _isDisposed = true;

        Sampler.Dispose();
        SRV.Dispose();
        Texture.Dispose();
    }
}