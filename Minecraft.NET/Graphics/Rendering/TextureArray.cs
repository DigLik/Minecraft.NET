using System;
using System.Collections.Generic;
using System.IO;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class TextureArray : IDisposable
{
    private readonly D3D12Context _d3d;

    public ComPtr<ID3D12Resource> Resource;
    public ResourceDesc Desc;
    public List<byte[]> Buffers;

    private bool _isDisposed;

    public TextureArray(D3D12Context d3d, List<string> filePaths)
    {
        _d3d = d3d;

        using var firstStream = File.OpenRead(filePaths[0]);
        var firstImage = ImageResult.FromStream(firstStream, ColorComponents.RedGreenBlueAlpha);

        uint width = (uint)firstImage.Width;
        uint height = (uint)firstImage.Height;
        uint layers = (uint)filePaths.Count;

        Buffers = new List<byte[]>((int)layers);

        for (int i = 0; i < layers; i++)
        {
            using var stream = File.OpenRead(filePaths[i]);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            Buffers.Add(image.Data);
        }

        Desc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = width,
            Height = height,
            DepthOrArraySize = (ushort)layers,
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