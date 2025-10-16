using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

public unsafe sealed class MeshData(nint vertices, int vertexCount, uint* indices, int indexCount) : IDisposable
{
    public nint Vertices { get; } = vertices;
    public uint* Indices { get; } = indices;
    public int VertexCount { get; } = vertexCount;
    public int IndexCount { get; } = indexCount;

    private bool _isDisposed = false;

    public void Dispose()
    {
        if (_isDisposed) return;

        if (Vertices != 0) NativeMemory.Free((void*)Vertices);
        if (Indices != null) NativeMemory.Free(Indices);

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}