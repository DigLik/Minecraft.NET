using Silk.NET.Direct3D11;

using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

namespace Minecraft.NET.Graphics.Rendering;

public readonly record struct ChunkMeshGeometry(nint Vbo, nint Ebo, uint IndexCount);

public sealed unsafe class ChunkRenderer(D3D11Context d3d) : IChunkRenderer
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

        BufferDesc vbd = new BufferDesc
        {
            ByteWidth = (uint)(meshData.VertexCount * ChunkVertex.Stride),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.VertexBuffer
        };
        SubresourceData vData = new SubresourceData { PSysMem = (void*)meshData.Vertices };
        ID3D11Buffer* vbo;
        d3d.Device.CreateBuffer(&vbd, &vData, &vbo);

        BufferDesc ibd = new BufferDesc
        {
            ByteWidth = (uint)(meshData.IndexCount * sizeof(uint)),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.IndexBuffer
        };
        SubresourceData iData = new SubresourceData { PSysMem = meshData.Indices };
        ID3D11Buffer* ebo;
        d3d.Device.CreateBuffer(&ibd, &iData, &ebo);

        meshData.Dispose();
        return new ChunkMeshGeometry((nint)vbo, (nint)ebo, (uint)meshData.IndexCount);
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        if (geometry.Vbo != 0) ((ID3D11Buffer*)geometry.Vbo)->Release();
        if (geometry.Ebo != 0) ((ID3D11Buffer*)geometry.Ebo)->Release();
    }

    public void DrawChunk(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount == 0) return;

        uint stride = ChunkVertex.Stride;
        uint offset = 0;
        ID3D11Buffer* vbo = (ID3D11Buffer*)geometry.Vbo;

        d3d.Context.IASetVertexBuffers(0, 1, &vbo, &stride, &offset);
        d3d.Context.IASetIndexBuffer((ID3D11Buffer*)geometry.Ebo, Silk.NET.DXGI.Format.FormatR32Uint, 0);
        d3d.Context.DrawIndexed(geometry.IndexCount, 0, 0);
    }

    public void Dispose()
    {
    }
}