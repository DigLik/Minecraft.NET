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

    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private readonly uint _indirectBuffer;
    private readonly uint _instanceVbo;

    private readonly MemoryAllocator _vertexAllocator;
    private readonly MemoryAllocator _indexAllocator;

    // 2 МБ вершин и 4 МБ индексов для старта.
    private nuint _currentVertexCapacity = 1024 * 256;
    private nuint _currentIndexCapacity = 1024 * 512;

    public ChunkRenderer(GL gl, uint instanceVbo)
    {
        _gl = gl;
        _instanceVbo = instanceVbo;

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, _currentVertexCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, _currentIndexCapacity * sizeof(uint), null, BufferUsageARB.DynamicDraw);

        _indirectBuffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _indirectBuffer);
        _gl.BufferData(BufferTargetARB.DrawIndirectBuffer, (nuint)(MaxVisibleSections * sizeof(DrawElementsIndirectCommand)), null, BufferUsageARB.StreamDraw);

        _vertexAllocator = new MemoryAllocator(_currentVertexCapacity);
        _indexAllocator = new MemoryAllocator(_currentIndexCapacity);

        SetupVao();
    }

    private void SetupVao()
    {
        if (_vao != 0) _gl.DeleteVertexArray(_vao);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        ChunkVertex.SetVertexAttribPointers(_gl);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);

        uint matrixSize = (uint)sizeof(Matrix4x4);
        for (uint i = 0; i < 4; i++)
        {
            uint loc = 4 + i;
            _gl.EnableVertexAttribArray(loc);
            _gl.VertexAttribPointer(loc, 4, VertexAttribPointerType.Float, false, matrixSize, (void*)(i * sizeof(Vector4)));
            _gl.VertexAttribDivisor(loc, 1);
        }

        _gl.BindVertexArray(0);
    }

    public ChunkMeshGeometry? UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0) { meshData.Dispose(); return null; }

        if (!_vertexAllocator.TryAllocate((nuint)meshData.VertexCount, out nuint vertexOffset))
        {
            ResizeVertexBuffer((nuint)meshData.VertexCount);
            _vertexAllocator.TryAllocate((nuint)meshData.VertexCount, out vertexOffset);
        }

        if (!_indexAllocator.TryAllocate((nuint)meshData.IndexCount, out nuint indexOffset))
        {
            ResizeIndexBuffer((nuint)meshData.IndexCount);
            _indexAllocator.TryAllocate((nuint)meshData.IndexCount, out indexOffset);
        }

        int baseVertex = (int)vertexOffset;
        uint firstIndex = (uint)indexOffset;

        nuint vertexSizeInBytes = (nuint)meshData.VertexCount * ChunkVertex.Stride;
        nuint indexSizeInBytes = (nuint)meshData.IndexCount * sizeof(uint);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(vertexOffset * ChunkVertex.Stride), vertexSizeInBytes, (void*)meshData.Vertices);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(indexOffset * sizeof(uint)), indexSizeInBytes, meshData.Indices);

        var geometry = new ChunkMeshGeometry((uint)meshData.IndexCount, firstIndex, baseVertex);

        meshData.Dispose();
        return geometry;
    }

    private void ResizeVertexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentVertexCapacity * 2, _currentVertexCapacity + requiredExtraSize + 1024);

        uint newVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newVbo);
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _vbo);
        _gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentVertexCapacity * ChunkVertex.Stride);

        _gl.DeleteBuffer(_vbo);
        _vbo = newVbo;

        _vertexAllocator.Grow(newCapacity);
        _currentVertexCapacity = newCapacity;

        SetupVao();
    }

    private void ResizeIndexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentIndexCapacity * 2, _currentIndexCapacity + requiredExtraSize + 1024);

        uint newEbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newEbo);
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * sizeof(uint), null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _ebo);
        _gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentIndexCapacity * sizeof(uint));

        _gl.DeleteBuffer(_ebo);
        _ebo = newEbo;

        _indexAllocator.Grow(newCapacity);
        _currentIndexCapacity = newCapacity;

        SetupVao();
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
        _vertexAllocator.Free((nuint)geometry.BaseVertex);
        _indexAllocator.Free(geometry.FirstIndex);
    }

    public void Bind() => _gl.BindVertexArray(_vao);

    public void Draw(int commandCount)
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