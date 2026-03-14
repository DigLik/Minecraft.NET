using System.Numerics;
using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

public unsafe class ChunkMesher(ChunkManager chunkManager)
{
    private static readonly float[] FaceShades = [1.0f, 0.5f, 0.8f, 0.8f, 0.6f, 0.6f];

    private static readonly Vector3[][] FaceVertices = [
        [new(0, 1, 1), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1)],
        [new(1, 1, 0), new(1, 0, 0), new(0, 0, 0), new(0, 1, 0)],
        [new(0, 1, 1), new(0, 1, 0), new(0, 0, 0), new(0, 0, 1)],
        [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)],
        [new(1, 1, 1), new(1, 1, 0), new(0, 1, 0), new(0, 1, 1)],
        [new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1)]
    ];

    private static readonly Vector2[] FaceUVs = [
        new(0, 0), new(0, 1), new(1, 1), new(1, 0)
    ];

    public ChunkMesh GenerateMesh(Vector3Int chunkPos)
    {
        if (!chunkManager.TryGetChunk(chunkPos, out ChunkSection centerChunk) || centerChunk.IsEmpty)
            return new ChunkMesh { Vertices = default, Indices = default };

        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 0, 1), out ChunkSection nPosZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 0, -1), out ChunkSection nNegZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(-1, 0, 0), out ChunkSection nNegX);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(1, 0, 0), out ChunkSection nPosX);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 1, 0), out ChunkSection nPosY);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, -1, 0), out ChunkSection nNegY);

        NativeList<ChunkVertex> vertices = new(4096);
        NativeList<uint> indices = new(6144);

        int grassOverlayId = BlockRegistry.TextureFiles.FindIndex(f => f.Contains("grass_side_overlay"));
        BlockDefinition[] defs = BlockRegistry.Definitions;

        ref NativeList<BlockId> blocks = ref centerChunk.Blocks;
        BlockId uniformId = centerChunk.UniformId;
        bool isUniform = !blocks.IsCreated;

        int index = 0;

        for (int y = 0; y < ChunkSize; y++)
        {
            bool yMin = y == 0;
            bool yMax = y == 15;
            for (int z = 0; z < ChunkSize; z++)
            {
                bool zMin = z == 0;
                bool zMax = z == 15;
                for (int x = 0; x < ChunkSize; x++, index++)
                {
                    BlockId currentId = isUniform ? uniformId : blocks[index];
                    if (currentId == BlockId.Air) continue;

                    ref BlockDefinition currentDef = ref defs[(int)currentId];
                    bool xMin = x == 0;
                    bool xMax = x == 15;

                    if (zMax)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nPosZ, x | (y << 8))]))
                            BuildFace(ref vertices, ref indices, x, y, z, 0, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 16])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 0, currentId, ref currentDef, grassOverlayId);
                    }

                    if (zMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegZ, x | 240 | (y << 8))]))
                            BuildFace(ref vertices, ref indices, x, y, z, 1, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 16])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 1, currentId, ref currentDef, grassOverlayId);
                    }

                    if (xMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegX, 15 | (z << 4) | (y << 8))]))
                            BuildFace(ref vertices, ref indices, x, y, z, 2, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 1])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 2, currentId, ref currentDef, grassOverlayId);
                    }

                    if (xMax)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nPosX, (z << 4) | (y << 8))]))
                            BuildFace(ref vertices, ref indices, x, y, z, 3, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 1])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 3, currentId, ref currentDef, grassOverlayId);
                    }

                    if (yMax)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nPosY, x | (z << 4))]))
                            BuildFace(ref vertices, ref indices, x, y, z, 4, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 256])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 4, currentId, ref currentDef, grassOverlayId);
                    }

                    if (yMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegY, x | (z << 4) | 3840)]))
                            BuildFace(ref vertices, ref indices, x, y, z, 5, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 256])]))
                            BuildFace(ref vertices, ref indices, x, y, z, 5, currentId, ref currentDef, grassOverlayId);
                    }
                }
            }
        }

        if (vertices.Count > 0)
        {
            return new ChunkMesh { Vertices = vertices, Indices = indices };
        }
        else
        {
            vertices.Dispose();
            indices.Dispose();
            return new ChunkMesh { Vertices = default, Indices = default };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BlockId GetNeighborBlock(ref ChunkSection chunk, int index)
    {
        if (chunk.Blocks.IsCreated) return chunk.Blocks[index];
        return chunk.UniformId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldRenderFace(in BlockDefinition current, in BlockDefinition neighbor)
    {
        if (current.Transparency == BlockTransparency.Foliage || neighbor.Transparency == BlockTransparency.Foliage) return true;
        return neighbor.Transparency != BlockTransparency.Opaque;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildFace(
        ref NativeList<ChunkVertex> vertices, ref NativeList<uint> indices,
        int x, int y, int z, int faceIndex,
        BlockId currentId, ref BlockDefinition currentDef, int grassOverlayId)
    {
        int textureId = faceIndex switch
        {
            0 => currentDef.Textures.Top,
            1 => currentDef.Textures.Bottom,
            _ => currentDef.Textures.Side
        };

        float shade = FaceShades[faceIndex];
        Vector4 color = new Vector4(shade, shade, shade, 1.0f);
        int overlayTexId = -1;
        Vector4 overlayColor = Vector4.Zero;

        if (currentId == BlockId.Grass)
        {
            if (faceIndex == 0)
            {
                color.X = (145.0f / 255.0f) * shade;
                color.Y = (189.0f / 255.0f) * shade;
                color.Z = (89.0f / 255.0f) * shade;
            }
            else if (faceIndex >= 2 && grassOverlayId >= 0)
            {
                overlayTexId = grassOverlayId;
                overlayColor = new Vector4(145.0f / 255.0f * shade, 189.0f / 255.0f * shade, 89.0f / 255.0f * shade, 1.0f);
            }
        }
        else if (currentId == BlockId.OakLeaves)
        {
            color.X = 72.0f / 255.0f * shade;
            color.Y = 181.0f / 255.0f * shade;
            color.Z = 72.0f / 255.0f * shade;
        }

        uint indexOffset = (uint)vertices.Count;
        var fVerts = FaceVertices[faceIndex];

        vertices.Add(new ChunkVertex(new Vector4(x + fVerts[0].X, y + fVerts[0].Y, z + fVerts[0].Z, 1.0f), textureId, FaceUVs[0], overlayTexId, color, overlayColor));
        vertices.Add(new ChunkVertex(new Vector4(x + fVerts[1].X, y + fVerts[1].Y, z + fVerts[1].Z, 1.0f), textureId, FaceUVs[1], overlayTexId, color, overlayColor));
        vertices.Add(new ChunkVertex(new Vector4(x + fVerts[2].X, y + fVerts[2].Y, z + fVerts[2].Z, 1.0f), textureId, FaceUVs[2], overlayTexId, color, overlayColor));
        vertices.Add(new ChunkVertex(new Vector4(x + fVerts[3].X, y + fVerts[3].Y, z + fVerts[3].Z, 1.0f), textureId, FaceUVs[3], overlayTexId, color, overlayColor));

        indices.Add(indexOffset + 0);
        indices.Add(indexOffset + 1);
        indices.Add(indexOffset + 2);
        indices.Add(indexOffset + 2);
        indices.Add(indexOffset + 3);
        indices.Add(indexOffset + 0);
    }
}