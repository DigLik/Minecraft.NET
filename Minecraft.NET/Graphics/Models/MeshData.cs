using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

public unsafe readonly struct MeshData(nint vertices, int vertexCount, ushort* indices, int indexCount) : IDisposable
{
    public readonly nint Vertices = vertices;
    public readonly ushort* Indices = indices;
    public readonly int VertexCount = vertexCount;
    public readonly int IndexCount = indexCount;

    public void Dispose()
    {
        if (Vertices != 0)
            NativeMemory.Free((void*)Vertices);

        if (Indices != null)
            NativeMemory.Free(Indices);
    }
}