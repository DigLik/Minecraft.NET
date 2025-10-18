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

    private readonly MemoryAllocator _vertexAllocator = null!;
    private readonly MemoryAllocator _indexAllocator = null!;

    private readonly nuint VertexElementSize = (nuint)sizeof(Half);
    private readonly nuint ComponentsPerVertex = (nuint)(sizeof(ChunkVertex) / sizeof(Half));

    private const nuint VertexCapacity = (nuint)MaxVisibleSections * 12000;
    private const nuint IndexCapacity = (nuint)MaxVisibleSections * 20000;

    public ChunkRenderer(GL gl, uint instanceVbo)
    {
        _gl = gl;

        // Инициализация VBO
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, VertexCapacity * VertexElementSize, null, BufferUsageARB.DynamicDraw);

        // Инициализация EBO
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, unchecked(IndexCapacity * sizeof(uint)), null, BufferUsageARB.DynamicDraw);

        // Инициализация Indirect Buffer
        _indirectBuffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        _gl.BufferData(BufferTargetARB.DrawIndirectBuffer, (nuint)(MaxVisibleSections * sizeof(DrawElementsIndirectCommand)), null, BufferUsageARB.StreamDraw);

        // Инициализация аллокаторов
        _vertexAllocator = new MemoryAllocator(VertexCapacity);
        _indexAllocator = new MemoryAllocator(IndexCapacity);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        ChunkVertex.SetVertexAttribPointers(_gl);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceVbo);

        uint matrixSize = (uint)sizeof(Matrix4x4);
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, matrixSize, 0);
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, matrixSize, sizeof(Vector4));
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, matrixSize, 2 * sizeof(Vector4));
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, matrixSize, 3 * sizeof(Vector4));

        _gl.VertexAttribDivisor(3, 1);
        _gl.VertexAttribDivisor(4, 1);
        _gl.VertexAttribDivisor(5, 1);
        _gl.VertexAttribDivisor(6, 1);

        _gl.BindVertexArray(0);
    }

    public ChunkMeshGeometry? UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0) { meshData.Dispose(); return null; }

        nuint numComponentsToAllocate = (nuint)meshData.VertexCount * ComponentsPerVertex;
        nuint vertexDataSizeInBytes = (nuint)meshData.VertexCount * (nuint)sizeof(ChunkVertex);
        nuint indexDataSizeInBytes = (nuint)meshData.IndexCount * sizeof(uint);

        bool vertexAllocated = _vertexAllocator.TryAllocate(numComponentsToAllocate, out nuint componentOffset);

        if (!vertexAllocated)
        {
            Console.WriteLine("Chunk renderer out of vertex memory!");
            meshData.Dispose();
            return null;
        }

        if (!_indexAllocator.TryAllocate((nuint)meshData.IndexCount, out nuint indexOffset))
        {
            Console.WriteLine("Chunk renderer out of index memory (or fragmented)!");
            _vertexAllocator.Free(componentOffset);
            meshData.Dispose();
            return null;
        }

        int baseVertex = (int)(componentOffset / ComponentsPerVertex);
        uint firstIndex = (uint)indexOffset;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(componentOffset * (nuint)sizeof(Half)), vertexDataSizeInBytes, (void*)meshData.Vertices);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(indexOffset * sizeof(uint)), indexDataSizeInBytes, meshData.Indices);

        var geometry = new ChunkMeshGeometry((uint)meshData.IndexCount, firstIndex, baseVertex);

        meshData.Dispose();
        return geometry;
    }

    public void UploadIndirectCommands(List<DrawElementsIndirectCommand> commands)
    {
        if (commands.Count == 0) return;

        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        fixed (DrawElementsIndirectCommand* ptr = CollectionsMarshal.AsSpan(commands))
            _gl.BufferSubData(BufferTargetARB.DrawIndirectBuffer, 0, (nuint)(commands.Count * sizeof(DrawElementsIndirectCommand)), ptr);
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        _vertexAllocator.Free((nuint)geometry.BaseVertex * ComponentsPerVertex);
        _indexAllocator.Free(geometry.FirstIndex);
    }

    public void Bind() => _gl.BindVertexArray(_vao);

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