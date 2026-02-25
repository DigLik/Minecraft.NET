using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

namespace Minecraft.NET.Graphics.Rendering;

public readonly record struct ChunkMeshGeometry(uint Vao, uint Vbo, uint Ebo, uint IndexCount);

public sealed unsafe class ChunkRenderer(GL gl) : IChunkRenderer
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

        uint vao = gl.GenVertexArray();
        uint vbo = gl.GenBuffer();
        uint ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(meshData.VertexCount * ChunkVertex.Stride), (void*)meshData.Vertices, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(meshData.IndexCount * sizeof(ushort)), meshData.Indices, BufferUsageARB.StaticDraw);

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.UnsignedByte, false, ChunkVertex.Stride, (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 1, VertexAttribPointerType.UnsignedShort, false, ChunkVertex.Stride, (void*)3);

        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.UnsignedByte, false, ChunkVertex.Stride, (void*)5);

        gl.BindVertexArray(0);

        var geometry = new ChunkMeshGeometry(vao, vbo, ebo, (uint)meshData.IndexCount);

        meshData.Dispose();
        return geometry;
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        if (geometry.Vao != 0) gl.DeleteVertexArray(geometry.Vao);
        if (geometry.Vbo != 0) gl.DeleteBuffer(geometry.Vbo);
        if (geometry.Ebo != 0) gl.DeleteBuffer(geometry.Ebo);
    }

    public void DrawChunk(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount == 0) return;
        gl.BindVertexArray(geometry.Vao);
        gl.DrawElements(PrimitiveType.Triangles, geometry.IndexCount, DrawElementsType.UnsignedShort, (void*)0);
        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
    }
}