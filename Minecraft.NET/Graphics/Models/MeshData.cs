using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

public unsafe sealed class MeshData(float* vertices, int vertexCount, uint* indices, int indexCount) : IDisposable
{
    public float* Vertices { get; } = vertices;
    public uint* Indices { get; } = indices;
    public int VertexCount { get; } = vertexCount;
    public int IndexCount { get; } = indexCount;

    private bool _isDisposed = false;

    public void Dispose()
    {
        if (_isDisposed) return;

        if (Vertices != null) NativeMemory.Free(Vertices);
        if (Indices != null) NativeMemory.Free(Indices);

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}