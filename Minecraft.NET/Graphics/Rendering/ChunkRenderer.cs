using Minecraft.NET.Graphics.Models;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Rendering;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct DrawElementsIndirectCommand(
    uint Count,
    uint InstanceCount,
    uint FirstIndex,
    int BaseVertex,
    uint BaseInstance
);

public readonly record struct ChunkMeshGeometry(
    uint IndexCount,
    uint FirstIndex,
    int BaseVertex
);

public sealed unsafe class ChunkRenderer : IDisposable
{
    private readonly GL _gl;

    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly uint _indirectBuffer;

    private nuint _vertexCount;
    private nuint _indexCount;

    private const nuint VertexCapacity = MaxVisibleSections * 12000;
    private const nuint IndexCapacity = MaxVisibleSections * 20000;

    public ChunkRenderer(GL gl, uint instanceVbo)
    {
        _gl = gl;

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, VertexCapacity * sizeof(float), null, BufferUsageARB.DynamicDraw);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, IndexCapacity * sizeof(uint), null, BufferUsageARB.DynamicDraw);

        _indirectBuffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        _gl.BufferData(BufferTargetARB.DrawIndirectBuffer, (nuint)(MaxVisibleSections * sizeof(DrawElementsIndirectCommand)), null, BufferUsageARB.StreamDraw);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        const int stride = 7 * sizeof(float);

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceVbo);

        nuint matrixSize = (nuint)sizeof(Matrix4x4);
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, 0);
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, sizeof(Vector4));
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, (nint)(2 * sizeof(Vector4)));
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, (nint)(3 * sizeof(Vector4)));

        _gl.VertexAttribDivisor(3, 1);
        _gl.VertexAttribDivisor(4, 1);
        _gl.VertexAttribDivisor(5, 1);
        _gl.VertexAttribDivisor(6, 1);

        _gl.BindVertexArray(0);
    }

    public ChunkMeshGeometry? UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0)
        {
            meshData.Dispose();
            return null;
        }

        nuint vertexDataSize = (nuint)meshData.VertexCount * sizeof(float);
        nuint indexDataSize = (nuint)meshData.IndexCount * sizeof(uint);

        if (_vertexCount + (nuint)meshData.VertexCount > VertexCapacity || _indexCount + (nuint)meshData.IndexCount > IndexCapacity)
        {
            Console.WriteLine("Chunk renderer out of memory!");
            meshData.Dispose();
            return null;
        }

        int baseVertex = (int)(_vertexCount / 7);
        uint firstIndex = (uint)_indexCount;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(_vertexCount * sizeof(float)), vertexDataSize, meshData.Vertices);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(_indexCount * sizeof(uint)), indexDataSize, meshData.Indices);

        var geometry = new ChunkMeshGeometry((uint)meshData.IndexCount, firstIndex, baseVertex);

        _vertexCount += (nuint)meshData.VertexCount;
        _indexCount += (nuint)meshData.IndexCount;

        meshData.Dispose();

        return geometry;
    }

    public void UploadIndirectCommands(List<DrawElementsIndirectCommand> commands)
    {
        if (commands.Count == 0) return;

        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        fixed (DrawElementsIndirectCommand* ptr = CollectionsMarshal.AsSpan(commands))
        {
            _gl.BufferSubData(BufferTargetARB.DrawIndirectBuffer, 0, (nuint)(commands.Count * sizeof(DrawElementsIndirectCommand)), ptr);
        }
    }

    public void Bind()
    {
        _gl.BindVertexArray(_vao);
    }

    public unsafe void Draw(int commandCount)
    {
        if (commandCount == 0) return;

        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        _gl.MultiDrawElementsIndirect(PrimitiveType.Triangles, DrawElementsType.UnsignedInt, null, (uint)commandCount, 0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteBuffer(_indirectBuffer);
    }
}