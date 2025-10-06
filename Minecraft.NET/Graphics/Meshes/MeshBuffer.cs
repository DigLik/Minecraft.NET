using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Meshes;

public unsafe class MeshBuffer : IDisposable
{
    public float* VerticesPtr { get; private set; }
    public uint* IndicesPtr { get; private set; }

    public int VerticesCount { get; set; }
    public int IndicesCount { get; set; }

    private int _verticesCapacity;
    private int _indicesCapacity;

    public Vector2 Position;

    public MeshBuffer()
    {
        _verticesCapacity = 4096 * 5;
        _indicesCapacity = 4096 * 6;

        VerticesPtr = (float*)NativeMemory.Alloc((nuint)_verticesCapacity, (nuint)sizeof(float));
        IndicesPtr = (uint*)NativeMemory.Alloc((nuint)_indicesCapacity, (nuint)sizeof(uint));
    }

    public void EnsureVertexCapacity(int required)
    {
        if (required > _verticesCapacity)
        {
            _verticesCapacity = Math.Max(required, _verticesCapacity * 2);
            VerticesPtr = (float*)NativeMemory.Realloc(VerticesPtr, (nuint)_verticesCapacity * (nuint)sizeof(float));
        }
    }

    public void EnsureIndexCapacity(int required)
    {
        if (required > _indicesCapacity)
        {
            _indicesCapacity = Math.Max(required, _indicesCapacity * 2);
            IndicesPtr = (uint*)NativeMemory.Realloc(IndicesPtr, (nuint)_indicesCapacity * (nuint)sizeof(uint));
        }
    }

    public Span<float> GetVertexSpan() => new(VerticesPtr, _verticesCapacity);
    public Span<uint> GetIndexSpan() => new(IndicesPtr, _indicesCapacity);

    public ReadOnlySpan<float> GetVertices() => new(VerticesPtr, VerticesCount);
    public ReadOnlySpan<uint> GetIndices() => new(IndicesPtr, IndicesCount);

    public void Clear()
    {
        VerticesCount = 0;
        IndicesCount = 0;
        Position = default;
    }

    public void Dispose()
    {
        NativeMemory.Free(VerticesPtr);
        NativeMemory.Free(IndicesPtr);
        VerticesPtr = null;
        IndicesPtr = null;
        GC.SuppressFinalize(this);
    }
}