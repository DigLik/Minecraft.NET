using System.Runtime.CompilerServices;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class TextureArray : IDisposable
{
    private readonly D3D12Context _d3d;

    public ComPtr<ID3D12Resource> Resource;
    public ComPtr<ID3D12Resource> UploadHeap;
    public ComPtr<ID3D12DescriptorHeap> SrvHeap;

    private bool _isDisposed;

    public TextureArray(D3D12Context d3d, List<string> filePaths)
    {
        _d3d = d3d;
        ref var device = ref _d3d.Device.Get();

        if (filePaths == null || filePaths.Count == 0)
            throw new ArgumentException("No texture files provided.");

        using var firstStream = File.OpenRead(filePaths[0]);
        var firstImage = ImageResult.FromStream(firstStream, ColorComponents.RedGreenBlueAlpha);

        uint width = (uint)firstImage.Width;
        uint height = (uint)firstImage.Height;
        uint layers = (uint)filePaths.Count;

        var desc = new ResourceDesc
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

        var defaultHeapProps = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);
        var uploadHeapProps = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);

        var riidResource = SilkMarshal.GuidPtrOf<ID3D12Resource>();
        void* resPtr;
        int hr = device.CreateCommittedResource(&defaultHeapProps, HeapFlags.None, &desc, ResourceStates.CopyDest, null, riidResource, &resPtr);
        if (hr < 0) throw new Exception("Failed to create default heap for TextureArray.");

        Resource = new ComPtr<ID3D12Resource>((ID3D12Resource*)resPtr);

        PlacedSubresourceFootprint* layouts = stackalloc PlacedSubresourceFootprint[(int)layers];
        ulong uploadBufferSize;
        device.GetCopyableFootprints(&desc, 0, layers, 0, layouts, null, null, &uploadBufferSize);

        var uploadDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer, Width = uploadBufferSize, Height = 1, DepthOrArraySize = 1,
            MipLevels = 1, Format = Format.FormatUnknown, SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor, Flags = ResourceFlags.None
        };

        void* uploadPtr;
        hr = device.CreateCommittedResource(&uploadHeapProps, HeapFlags.None, &uploadDesc, ResourceStates.GenericRead, null, riidResource, &uploadPtr);
        if (hr < 0) throw new Exception("Failed to create upload heap for TextureArray.");

        UploadHeap = new ComPtr<ID3D12Resource>((ID3D12Resource*)uploadPtr);

        void* mappedData;
        hr = UploadHeap.Get().Map(0, null, &mappedData);
        if (hr < 0) throw new Exception("Failed to map upload heap.");

        byte* pData = (byte*)mappedData;

        for (int i = 0; i < layers; i++)
        {
            using var stream = File.OpenRead(filePaths[i]);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            byte* destSlice = pData + layouts[i].Offset;
            uint rowPitch = layouts[i].Footprint.RowPitch;

            for (int y = 0; y < height; y++)
            {
                fixed (byte* src = &image.Data[y * width * 4])
                {
                    Unsafe.CopyBlock(destSlice + y * rowPitch, src, width * 4);
                }
            }
        }
        UploadHeap.Get().Unmap(0, null);

        var cmdAllocator = _d3d.CommandAllocators[0].Handle;
        var cmdList = _d3d.CommandList.Handle;

        cmdAllocator->Reset();
        cmdList->Reset(cmdAllocator, null);

        for (uint i = 0; i < layers; i++)
        {
            var destLoc = new TextureCopyLocation(pResource: Resource.Handle, type: TextureCopyType.SubresourceIndex, subresourceIndex: i);
            var srcLoc = new TextureCopyLocation(pResource: UploadHeap.Handle, type: TextureCopyType.PlacedFootprint, placedFootprint: layouts[i]);
            cmdList->CopyTextureRegion(&destLoc, 0, 0, 0, &srcLoc, null);
        }

        var barrier = new ResourceBarrier
        {
            Type = ResourceBarrierType.Transition, Flags = ResourceBarrierFlags.None,
            Transition = new ResourceTransitionBarrier { PResource = Resource.Handle, StateBefore = ResourceStates.CopyDest, StateAfter = ResourceStates.PixelShaderResource, Subresource = uint.MaxValue }
        };

        cmdList->ResourceBarrier(1, &barrier);
        cmdList->Close();

        ID3D12CommandList* ppCommandList = (ID3D12CommandList*)cmdList;
        _d3d.CommandQueue.Get().ExecuteCommandLists(1, &ppCommandList);
        _d3d.WaitForGpu();

        var srvHeapDesc = new DescriptorHeapDesc { NumDescriptors = 1, Type = DescriptorHeapType.CbvSrvUav, Flags = DescriptorHeapFlags.ShaderVisible };
        var riidHeap = SilkMarshal.GuidPtrOf<ID3D12DescriptorHeap>();
        void* srvHeapPtr;
        hr = device.CreateDescriptorHeap(&srvHeapDesc, riidHeap, &srvHeapPtr);
        if (hr < 0) throw new Exception("Failed to create SRV Heap.");

        SrvHeap = new ComPtr<ID3D12DescriptorHeap>((ID3D12DescriptorHeap*)srvHeapPtr);
        device.CreateShaderResourceView(Resource.Handle, null, SrvHeap.Get().GetCPUDescriptorHandleForHeapStart());
    }

    public void Bind(ID3D12GraphicsCommandList* cmdList, uint rootParameterIndex)
    {
        if (_isDisposed || _d3d == null) return;
        ID3D12DescriptorHeap* ppHeaps = SrvHeap.Handle;
        cmdList->SetDescriptorHeaps(1, &ppHeaps);
        cmdList->SetGraphicsRootDescriptorTable(rootParameterIndex, SrvHeap.Get().GetGPUDescriptorHandleForHeapStart());
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (SrvHeap.Handle != null) SrvHeap.Dispose();
        if (UploadHeap.Handle != null) UploadHeap.Dispose();
        if (Resource.Handle != null) Resource.Dispose();
    }
}