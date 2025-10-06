using System.Collections.Concurrent;

namespace Minecraft.NET.Graphics.Meshes;

public static class MeshBufferPool
{
    private static readonly ConcurrentQueue<MeshBuffer> _pool = new();

    public static MeshBuffer Get()
    {
        if (_pool.TryDequeue(out var buffer)) return buffer;
        return new MeshBuffer();
    }

    public static void Return(MeshBuffer buffer)
    {
        buffer.Clear();
        _pool.Enqueue(buffer);
    }
}