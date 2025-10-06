using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;
using Minecraft.NET.Graphics.Scene;
using Silk.NET.OpenGL;

namespace Minecraft.NET.GameObjects;

public sealed class ChunkRenderObject : IRenderable, IDisposable
{
    private readonly GL _gl;
    public Mesh Mesh { get; private set; } = null!;
    public Material Material { get; }
    public Transform Transform { get; }

    public ChunkRenderObject(
        GL gl,
        Material material,
        MeshBuffer meshBuffer
    )
    {
        _gl = gl;
        Material = material;
        Transform = new Transform { Position = Vector3.Zero };

        if (meshBuffer.IndicesCount > 0)
            Mesh = new Mesh(_gl, meshBuffer.GetVertices(), meshBuffer.GetIndices());
        else
            Mesh = new Mesh(_gl, [], []);

        MeshBufferPool.Return(meshBuffer);
    }

    public void Dispose() => Mesh?.Dispose();
}