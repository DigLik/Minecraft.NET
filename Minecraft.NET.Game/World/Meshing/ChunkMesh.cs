using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Collections;

namespace Minecraft.NET.Game.World.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct ChunkVertex(
    float X, float Y, float Z,
    uint PackedData
);

public record struct ChunkMesh(NativeList<ChunkVertex> Vertices = default, NativeList<uint> Indices = default)
{
    public readonly bool IsEmpty => !Vertices.IsCreated || !Indices.IsCreated || Vertices.Count == 0 || Indices.Count == 0;
}