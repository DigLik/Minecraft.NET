using System.Runtime.CompilerServices;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

namespace Minecraft.NET.Graphics.Rendering;

public readonly record struct ChunkMeshGeometry(nint Vbo, nint Ebo, uint VertexCount, uint IndexCount);

public sealed unsafe class ChunkRenderer(D3D12Context d3d) : IChunkRenderer
{
    public void Initialize()
    {
    }

    public ChunkMeshGeometry UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0)
        {
            meshData.Dispose();
            return default;
        }

        ID3D12Device* device = (ID3D12Device*)d3d.Device.Handle;
        var uuid = SilkMarshal.GuidPtrOf<ID3D12Resource>();

        HeapProperties heapProps = new HeapProperties
        {
            Type = HeapType.Upload,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 1,
            VisibleNodeMask = 1
        };

        ulong vboSize = (ulong)(meshData.VertexCount * ChunkVertex.Stride);
        ResourceDesc vboDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = vboSize,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = ResourceFlags.None
        };

        ID3D12Resource* vbo;
        device->CreateCommittedResource(
            &heapProps,
            HeapFlags.None,
            &vboDesc,
            ResourceStates.GenericRead,
            null,
            uuid,
            (void**)&vbo
        );

        void* mappedVbo;
        vbo->Map(0, null, &mappedVbo);
        Unsafe.CopyBlock(mappedVbo, (void*)meshData.Vertices, (uint)vboSize);
        vbo->Unmap(0, null);

        ulong eboSize = (ulong)(meshData.IndexCount * sizeof(uint));
        ResourceDesc eboDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = eboSize,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = ResourceFlags.None
        };

        ID3D12Resource* ebo;
        device->CreateCommittedResource(
            &heapProps,
            HeapFlags.None,
            &eboDesc,
            ResourceStates.GenericRead,
            null,
            uuid,
            (void**)&ebo
        );

        void* mappedEbo;
        ebo->Map(0, null, &mappedEbo);
        Unsafe.CopyBlock(mappedEbo, meshData.Indices, (uint)eboSize);
        ebo->Unmap(0, null);

        uint vCount = (uint)meshData.VertexCount;
        uint iCount = (uint)meshData.IndexCount;

        meshData.Dispose();

        return new ChunkMeshGeometry((nint)vbo, (nint)ebo, vCount, iCount);
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        if (geometry.Vbo != 0) ((ID3D12Resource*)geometry.Vbo)->Release();
        if (geometry.Ebo != 0) ((ID3D12Resource*)geometry.Ebo)->Release();
    }

    public void DrawChunk(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount == 0) return;

        ID3D12GraphicsCommandList* cmdList = (ID3D12GraphicsCommandList*)d3d.CommandList.Handle;
        ID3D12Resource* vbo = (ID3D12Resource*)geometry.Vbo;
        ID3D12Resource* ebo = (ID3D12Resource*)geometry.Ebo;

        VertexBufferView vbv = new VertexBufferView
        {
            BufferLocation = vbo->GetGPUVirtualAddress(),
            StrideInBytes = ChunkVertex.Stride,
            SizeInBytes = (uint)(geometry.VertexCount * ChunkVertex.Stride)
        };

        IndexBufferView ibv = new IndexBufferView
        {
            BufferLocation = ebo->GetGPUVirtualAddress(),
            SizeInBytes = (uint)(geometry.IndexCount * sizeof(uint)),
            Format = Format.FormatR32Uint
        };

        cmdList->IASetVertexBuffers(0, 1, &vbv);
        cmdList->IASetIndexBuffer(&ibv);
        cmdList->DrawIndexedInstanced(geometry.IndexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
    }
}