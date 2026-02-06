using Minecraft.NET.Engine;
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

public sealed unsafe class ChunkRenderer : IChunkRenderer
{
    private GL _gl = null!;

    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    private MemoryAllocator _vertexAllocator = null!;
    private MemoryAllocator _indexAllocator = null!;

    private nuint _currentVertexCapacity = 1024 * 256;
    private nuint _currentIndexCapacity = 1024 * 512;

    public ChunkRenderer() { }

    public void Initialize(GL gl)
    {
        _gl = gl;

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, _currentVertexCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, _currentIndexCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        _vertexAllocator = new MemoryAllocator(_currentVertexCapacity);
        _indexAllocator = new MemoryAllocator(_currentIndexCapacity);

        SetupVao();
    }

    private void SetupVao()
    {
        if (_vao != 0)
            _gl.DeleteVertexArray(_vao);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        ChunkVertex.SetVertexAttribPointers(_gl);

        uint loc = 4;
        _gl.EnableVertexAttribArray(loc);
        _gl.VertexAttribDivisor(loc, 1);

        _gl.BindVertexArray(0);
    }

    public ChunkMeshGeometry UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0)
        { meshData.Dispose(); return default; }

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
        nuint indexSizeInBytes = (nuint)meshData.IndexCount * sizeof(ushort);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(vertexOffset * ChunkVertex.Stride), vertexSizeInBytes, (void*)meshData.Vertices);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(indexOffset * sizeof(ushort)), indexSizeInBytes, meshData.Indices);

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
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _ebo);
        _gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentIndexCapacity * sizeof(ushort));

        _gl.DeleteBuffer(_ebo);
        _ebo = newEbo;

        _indexAllocator.Grow(newCapacity);
        _currentIndexCapacity = newCapacity;

        SetupVao();
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        _vertexAllocator.Free((nuint)geometry.BaseVertex);
        _indexAllocator.Free(geometry.FirstIndex);
    }

    public void Bind() => _gl.BindVertexArray(_vao);

    public void DrawGPUIndirectCount(uint indirectBuffer, uint instanceBuffer, uint countBuffer, int maxDrawCount)
    {
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceBuffer);
        _gl.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.VertexAttribDivisor(4, 1);

        _gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, indirectBuffer);
        _gl.BindBuffer(BufferTargetARB.ParameterBuffer, countBuffer);

        _gl.MultiDrawElementsIndirectCount(PrimitiveType.Triangles, DrawElementsType.UnsignedShort, (void*)0, 0, (uint)maxDrawCount, 0);

        _gl.BindBuffer(BufferTargetARB.ParameterBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}