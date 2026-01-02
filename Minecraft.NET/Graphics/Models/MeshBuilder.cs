using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

public unsafe class MeshBuilder
{
    private ChunkVertex* _vertices;
    private ushort* _indices;

    private int _vertexCapacity;
    private int _indexCapacity;

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }

    public MeshBuilder(int initialVertexCapacity = 8192, int initialIndexCapacity = 12288)
    {
        _vertexCapacity = initialVertexCapacity;
        _indexCapacity = initialIndexCapacity;
        _vertices = (ChunkVertex*)NativeMemory.Alloc((nuint)_vertexCapacity, (nuint)sizeof(ChunkVertex));
        _indices = (ushort*)NativeMemory.Alloc((nuint)_indexCapacity, sizeof(ushort));
    }

    public void Reset()
    {
        VertexCount = 0;
        IndexCount = 0;
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

    public void AddIndices(ushort i1, ushort i2, ushort i3)
    {
        if (IndexCount + 3 > _indexCapacity)
        {
            _indexCapacity *= 2;
            _indices = (ushort*)NativeMemory.Realloc(_indices, (nuint)_indexCapacity * sizeof(ushort));
        }

        int currentOffset = IndexCount;
        _indices[currentOffset + 0] = i1;
        _indices[currentOffset + 1] = i2;
        _indices[currentOffset + 2] = i3;
        IndexCount += 3;
    }

    public MeshData BuildToData()
    {
        if (IndexCount == 0) return default;

        ChunkVertex* outVertices = (ChunkVertex*)NativeMemory.Alloc((nuint)VertexCount, (nuint)sizeof(ChunkVertex));
        ushort* outIndices = (ushort*)NativeMemory.Alloc((nuint)IndexCount, sizeof(ushort));

        Unsafe.CopyBlock(outVertices, _vertices, (uint)(VertexCount * sizeof(ChunkVertex)));
        Unsafe.CopyBlock(outIndices, _indices, (uint)(IndexCount * sizeof(ushort)));

        return new MeshData((nint)outVertices, VertexCount, outIndices, IndexCount);
    }
}