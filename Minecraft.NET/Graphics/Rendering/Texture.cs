using System;
using System.IO;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class Texture : IDisposable
{
    private readonly D3D12Context _d3d;

    public ComPtr<ID3D12Resource> Resource;
    public ResourceDesc Desc;
    public byte[] ImageData;

    private bool _isDisposed;

    public Texture(D3D12Context d3d, string path)
    {
        _d3d = d3d;

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        ImageData = image.Data;

        Desc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = (ulong)image.Width,
            Height = (uint)image.Height,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = ResourceFlags.None
        };
    }

    public void Bind(uint slot = 0)
    {
        if (_isDisposed || _d3d == null || slot > 999) return;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Resource.Dispose();
    }
}