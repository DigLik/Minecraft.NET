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

public sealed unsafe class ChunkRenderer(IGlContextAccessor glAccessor) : IChunkRenderer
{
    private GL Gl => glAccessor.Gl;
    private uint _vao, _vbo, _ebo;

    private MemoryAllocator _vertexAllocator = null!;
    private MemoryAllocator _indexAllocator = null!;

    private nuint _currentVertexCapacity = 1024 * 1024 * 8;
    private nuint _currentIndexCapacity = 1024 * 1024 * 12;

    public void Initialize()
    {
        _vbo = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _vbo);
        Gl.BufferData(BufferTargetARB.ShaderStorageBuffer, _currentVertexCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        _ebo = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        Gl.BufferData(BufferTargetARB.ElementArrayBuffer, _currentIndexCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        _vertexAllocator = new MemoryAllocator(_currentVertexCapacity);
        _indexAllocator = new MemoryAllocator(_currentIndexCapacity);

        SetupVao();
    }

    private void SetupVao()
    {
        if (_vao != 0) Gl.DeleteVertexArray(_vao);
        _vao = Gl.GenVertexArray();
        Gl.BindVertexArray(_vao);
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        Gl.BindVertexArray(0);
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

        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        Gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(vertexOffset * ChunkVertex.Stride), vertexSizeInBytes, (void*)meshData.Vertices);

        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        Gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, (nint)(indexOffset * sizeof(ushort)), indexSizeInBytes, meshData.Indices);

        var geometry = new ChunkMeshGeometry((uint)meshData.IndexCount, firstIndex, baseVertex);

        meshData.Dispose();
        return geometry;
    }

    private void ResizeVertexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentVertexCapacity * 2, _currentVertexCapacity + requiredExtraSize + 1024);
        uint newVbo = Gl.GenBuffer();

        Gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newVbo);
        Gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * ChunkVertex.Stride, null, BufferUsageARB.DynamicDraw);

        Gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _vbo);
        Gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentVertexCapacity * ChunkVertex.Stride);

        Gl.DeleteBuffer(_vbo);
        _vbo = newVbo;

        _vertexAllocator.Grow(newCapacity);
        _currentVertexCapacity = newCapacity;

        SetupVao();
    }

    private void ResizeIndexBuffer(nuint requiredExtraSize)
    {
        nuint newCapacity = Math.Max(_currentIndexCapacity * 2, _currentIndexCapacity + requiredExtraSize + 1024);

        uint newEbo = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, newEbo);
        Gl.BufferData(BufferTargetARB.CopyWriteBuffer, newCapacity * sizeof(ushort), null, BufferUsageARB.DynamicDraw);

        Gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _ebo);
        Gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer, 0, 0, _currentIndexCapacity * sizeof(ushort));

        Gl.DeleteBuffer(_ebo);
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

    public void Bind() => Gl.BindVertexArray(_vao);

    public void DrawGPUIndirectCount(uint indirectBuffer, uint instanceBuffer, uint countBuffer, int maxDrawCount)
    {
        Gl.BindVertexArray(_vao);

        Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _vbo);

        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceBuffer);

        uint locInstance = 4;
        Gl.EnableVertexAttribArray(locInstance);
        Gl.VertexAttribPointer(locInstance, 3, VertexAttribPointerType.Float, false, 16, (void*)0);
        Gl.VertexAttribDivisor(locInstance, 1);

        Gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, indirectBuffer);
        Gl.BindBuffer(BufferTargetARB.ParameterBuffer, countBuffer);

        Gl.MultiDrawElementsIndirectCount(PrimitiveType.Triangles, DrawElementsType.UnsignedShort, (void*)0, 0, (uint)maxDrawCount, 0);

        Gl.BindBuffer(BufferTargetARB.ParameterBuffer, 0);
        Gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        Gl.DeleteVertexArray(_vao);
        Gl.DeleteBuffer(_vbo);
        Gl.DeleteBuffer(_ebo);
    }
}