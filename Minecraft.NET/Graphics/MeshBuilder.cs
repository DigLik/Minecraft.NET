using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics;

public unsafe class MeshBuilder : IDisposable
{
    private float* _vertices;
    private uint* _indices;

    private int _vertexCapacity;
    private int _indexCapacity;

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }

    public MeshBuilder(int initialVertexCapacity = 8192, int initialIndexCapacity = 12288)
    {
        _vertexCapacity = initialVertexCapacity;
        _indexCapacity = initialIndexCapacity;
        _vertices = (float*)NativeMemory.Alloc((nuint)_vertexCapacity, sizeof(float));
        _indices = (uint*)NativeMemory.Alloc((nuint)_indexCapacity, sizeof(uint));
    }

    public void AddVertex(float x, float y, float z, float tx, float ty, float u, float v)
    {
        const int vertexSize = 7;
        if (VertexCount + vertexSize > _vertexCapacity)
        {
            _vertexCapacity *= 2;
            _vertices = (float*)NativeMemory.Realloc(_vertices, (nuint)_vertexCapacity * sizeof(float));
        }

        int currentOffset = VertexCount;
        _vertices[currentOffset + 0] = x;
        _vertices[currentOffset + 1] = y;
        _vertices[currentOffset + 2] = z;
        _vertices[currentOffset + 3] = tx;
        _vertices[currentOffset + 4] = ty;
        _vertices[currentOffset + 5] = u;
        _vertices[currentOffset + 6] = v;
        VertexCount += vertexSize;
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
        var data = new MeshData(_vertices, VertexCount, _indices, IndexCount);
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