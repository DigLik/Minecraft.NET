using Minecraft.NET.Core.World;
using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;
using Minecraft.NET.Graphics.Scene;
using Silk.NET.OpenGL;

namespace Minecraft.NET.GameObjects;

public sealed class ChunkRenderObject : IRenderable, IDisposable
{
    private readonly GL _gl;
    public ChunkColumn Column { get; }

    public Mesh Mesh { get; private set; } = null!;
    public Material Material { get; }
    public Transform Transform { get; }

    public ChunkRenderObject(
        GL gl,
        ChunkColumn column,
        Material material,
        Vector2 position
    )
    {
        _gl = gl;
        Column = column;
        Material = material;

        Transform = new Transform { Position = Vector3.Zero };

        RebuildMesh(position);
    }

    public void RebuildMesh(Vector2 position)
    {
        Mesh?.Dispose();

        var meshData = ChunkMesher.GenerateMesh(Column, position);

        if (meshData.Indices.Length > 0)
            Mesh = new Mesh(_gl, meshData.Vertices, meshData.Indices);
        else
            Mesh = new Mesh(_gl, [0], [0]);

        Column.Chunks.ToList().ForEach(c => c.IsDirty = false);
    }

    public void Dispose()
    {
        Mesh?.Dispose();
    }
}