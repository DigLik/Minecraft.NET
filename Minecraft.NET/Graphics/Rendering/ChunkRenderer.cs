using System.Runtime.InteropServices;

using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

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

public sealed unsafe class ChunkRenderer(GL gl) : IChunkRenderer
{
    private uint _vao, _vbo, _ebo;

    private MemoryAllocator _vertexAllocator = null!;
    private MemoryAllocator _indexAllocator = null!;

    private nuint _currentVertexCapacity = 1024 * 1024 * 8;
    private nuint _currentIndexCapacity = 1024 * 1024 * 12;

    public void Initialize()
    {
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ShaderStorageBuffer, _currentVertexCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, _currentIndexCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        _vertexAllocator = new MemoryAllocator(_currentVertexCapacity);
        _indexAllocator = new MemoryAllocator(_currentIndexCapacity);

        SetupVao();
    }

    private void SetupVao()
    {
        if (_vao != 0) gl.DeleteVertexArray(_vao);
        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BindVertexArray(0);
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

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(vertexOffset * ChunkVertex.Stride), vertexSizeInBytes, (void*)meshData.Vertices);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(indexOffset * sizeof(ushort)), indexSizeInBytes, meshData.Indices);

        var geometry = new ChunkMeshGeometry((uint)meshData.IndexCount, firstIndex, baseVertex);

        meshData.Dispose();
        return geometry;
    }

    private void ResizeVertexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentVertexCapacity * 2, _currentVertexCapacity + requiredExtraSize + 1024);
        uint newVbo = gl.GenBuffer();

        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newVbo);
        gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _vbo);
        gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentVertexCapacity * ChunkVertex.Stride);

        gl.DeleteBuffer(_vbo);
        _vbo = newVbo;

        _vertexAllocator.Grow(newCapacity);
        _currentVertexCapacity = newCapacity;

        SetupVao();
    }

    private void ResizeIndexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentIndexCapacity * 2, _currentIndexCapacity + requiredExtraSize + 1024);

        uint newEbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newEbo);
        gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _ebo);
        gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentIndexCapacity * sizeof(ushort));

        gl.DeleteBuffer(_ebo);
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

    public void Bind() => gl.BindVertexArray(_vao);

    public void DrawGpuIndirectCount(uint indirectBuffer, uint instanceBuffer, uint countBuffer, int maxDrawCount)
    {
        gl.BindVertexArray(_vao);

        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _vbo);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceBuffer);

        uint locInstance = 4;
        gl.EnableVertexAttribArray(locInstance);
        gl.VertexAttribPointer(locInstance, 3, VertexAttribPointerType.Float, false, 16, (void*)0);
        gl.VertexAttribDivisor(locInstance, 1);

        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, indirectBuffer);
        gl.BindBuffer(BufferTargetARB.ParameterBuffer, countBuffer);

        gl.MultiDrawElementsIndirectCount(PrimitiveType.Triangles, DrawElementsType.UnsignedShort, (void*)0, 0, (uint)maxDrawCount, 0);

        gl.BindBuffer(BufferTargetARB.ParameterBuffer, 0);
        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        gl.DeleteVertexArray(_vao);
        gl.DeleteBuffer(_vbo);
        gl.DeleteBuffer(_ebo);
    }
}