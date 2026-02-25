using System.Runtime.CompilerServices;

using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.Rendering;

public readonly record struct ChunkMeshGeometry(nint Buffer, uint VertexCount, uint IndexCount);

public sealed unsafe class ChunkRenderer(D3D12Context d3d) : IChunkRenderer
{
    public void Initialize() { }

    public ChunkMeshGeometry UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0)
        {
            meshData.Dispose();
            return default;
        }

        ID3D12Device* device = d3d.Device.Handle;
        var uuid = SilkMarshal.GuidPtrOf<ID3D12Resource>();

        HeapProperties heapProps = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);

        uint vboSize = (uint)(meshData.VertexCount * ChunkVertex.Stride);
        uint eboSize = (uint)(meshData.IndexCount * sizeof(uint));
        uint vboAlignedSize = (vboSize + 3u) & ~3u;
        ulong totalSize = vboAlignedSize + eboSize;

        ResourceDesc bufferDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer, Width = totalSize, Height = 1, DepthOrArraySize = 1,
            MipLevels = 1, Format = Format.FormatUnknown, SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor, Flags = ResourceFlags.None
        };

        ID3D12Resource* gpuBuffer;
        int hr = device->CreateCommittedResource(&heapProps, HeapFlags.None, &bufferDesc, ResourceStates.GenericRead, null, uuid, (void**)&gpuBuffer);

        if (hr < 0 || gpuBuffer == null)
        {
            Console.WriteLine($"[WARNING] Ошибка выделения памяти GPU для чанка (Лимит ОС или OutOfMemory). HRESULT: {hr:X}");
            meshData.Dispose();
            return default;
        }

        void* mappedData;
        hr = gpuBuffer->Map(0, null, &mappedData);
        if (hr < 0)
        {
            gpuBuffer->Release();
            meshData.Dispose();
            return default;
        }

        byte* pData = (byte*)mappedData;

        Unsafe.CopyBlock(pData, (void*)meshData.Vertices, vboSize);
        Unsafe.CopyBlock(pData + vboAlignedSize, meshData.Indices, eboSize);

        gpuBuffer->Unmap(0, null);

        uint vCount = (uint)meshData.VertexCount;
        uint iCount = (uint)meshData.IndexCount;
        meshData.Dispose();

        return new ChunkMeshGeometry((nint)gpuBuffer, vCount, iCount);
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        if (geometry.Buffer != 0)
            ((ID3D12Resource*)geometry.Buffer)->Release();
    }

    public void DrawChunk(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount == 0 || geometry.Buffer == 0) return;

        ID3D12GraphicsCommandList* cmdList = d3d.CommandList.Handle;
        ID3D12Resource* buffer = (ID3D12Resource*)geometry.Buffer;

        uint vboSize = geometry.VertexCount * ChunkVertex.Stride;
        uint vboAlignedSize = (vboSize + 3u) & ~3u;
        ulong gpuAddress = buffer->GetGPUVirtualAddress();

        VertexBufferView vbv = new VertexBufferView
        {
            BufferLocation = gpuAddress,
            StrideInBytes = ChunkVertex.Stride,
            SizeInBytes = vboSize
        };

        IndexBufferView ibv = new IndexBufferView
        {
            BufferLocation = gpuAddress + vboAlignedSize,
            SizeInBytes = geometry.IndexCount * sizeof(uint),
            Format = Format.FormatR32Uint
        };

        cmdList->IASetVertexBuffers(0, 1, &vbv);
        cmdList->IASetIndexBuffer(&ibv);
        cmdList->DrawIndexedInstanced(geometry.IndexCount, 1, 0, 0, 0);
    }

    public void Dispose() { }
}