using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

public unsafe class MeshBuilder : IDisposable
{
    private ChunkVertex* _vertices;
    private uint* _indices;

    private int _vertexCapacity;
    private int _indexCapacity;

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }

    public MeshBuilder(int initialVertexCapacity = 8192, int initialIndexCapacity = 12288)
    {
        _vertexCapacity = initialVertexCapacity;
        _indexCapacity = initialIndexCapacity;
        _vertices = (ChunkVertex*)NativeMemory.Alloc((nuint)_vertexCapacity, (nuint)sizeof(ChunkVertex));
        _indices = (uint*)NativeMemory.Alloc((nuint)_indexCapacity, sizeof(uint));
    }

    public void AddVertex(in ChunkVertex vertex)
    {
        if (VertexCount + 1 > _vertexCapacity)
        {
            _vertexCapacity *= 2;
            _vertices = (ChunkVertex*)NativeMemory.Realloc(_vertices, (nuint)_vertexCapacity * (nuint)sizeof(ChunkVertex));
        }

        _vertices[VertexCount++] = vertex;
    }

    public void AddIndices(uint i1, uint i2, uint i3)
    {
        if (IndexCount + 3 > _indexCapacity)
        {
            _indexCapacity *= 2;
            _indices = (uint*)NativeMemory.Realloc(_indices, (nuint)_indexCapacity * sizeof(uint));
        }

        int currentOffset = IndexCount;
        _indices[currentOffset + 0] = i1;
        _indices[currentOffset + 1] = i2;
        _indices[currentOffset + 2] = i3;
        IndexCount += 3;
    }

    public MeshData Build()
    {
        var data = new MeshData((nint)_vertices, VertexCount, _indices, IndexCount);
        _vertices = null;
        _indices = null;
        return data;
    }

    public void Dispose()
    {
        if (_vertices != null) NativeMemory.Free(_vertices);
        if (_indices != null) NativeMemory.Free(_indices);
        GC.SuppressFinalize(this);
    }
}