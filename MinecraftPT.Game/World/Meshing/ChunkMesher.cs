using System.Numerics;
using System.Runtime.CompilerServices;

using MinecraftPT.Game.World.Blocks;
using MinecraftPT.Game.World.Blocks.Services;
using MinecraftPT.Game.World.Chunks;
using MinecraftPT.Game.World.Environment;
using MinecraftPT.Utils.Collections;
using MinecraftPT.Utils.Math;

namespace MinecraftPT.Game.World.Meshing;

public unsafe class ChunkMesher(ChunkManager chunkManager, IBlockService blockService, IResourceService resourceService)
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
            return new ChunkMesh { Vertices = default, Indices = default, OpaqueIndexCount = 0 };

        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 0, 1), out ChunkSection nPosZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 0, -1), out ChunkSection nNegZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(-1, 0, 0), out ChunkSection nNegX);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(1, 0, 0), out ChunkSection nPosX);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, 1, 0), out ChunkSection nPosY);
        chunkManager.TryGetChunk(chunkPos + new Vector3Int(0, -1, 0), out ChunkSection nNegY);

        NativeList<ChunkVertex> vertices = new(4096);
        NativeList<ushort> opaqueIndices = new(6144);
        NativeList<ushort> transparentIndices = new(1536);

        int grassOverlayId = resourceService.GetSpecialMaterialIndex(SpecialMaterialId.GrassSideOverlay);
        ReadOnlySpan<BlockDefinition> defs = blockService.GetDefinitionsFast();

        ref NativeList<BlockId> blocks = ref centerChunk.Blocks;
        BlockId uniformId = centerChunk.UniformId;
        bool isUniform = !blocks.IsCreated;
        bool isBottomChunk = chunkPos.Z == 0;

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

                    ref readonly BlockDefinition currentDef = ref defs[(int)currentId];
                    bool xMin = x == 0;
                    bool xMax = x == 15;

                    if (zMax)
                    {
                        var neighbor = defs[(int)GetNeighborBlock(ref nPosZ, x | (y << 8))];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(0)))
                        {
                            int textureId = currentDef.Textures.Top;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 0, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 0, currentId, in currentDef, grassOverlayId);
                        }
                    }
                    else
                    {
                        var neighbor = defs[(int)(isUniform ? uniformId : blocks[index + 16])];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(0)))
                        {
                            int textureId = currentDef.Textures.Top;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 0, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 0, currentId, in currentDef, grassOverlayId);
                        }
                    }

                    // Оптимизация: не рендерим нижнюю грань, если это самый нижний слой мира
                    if (!(isBottomChunk && zMin))
                    {
                        if (zMin)
                        {
                            var neighbor = defs[(int)GetNeighborBlock(ref nNegZ, x | 240 | (y << 8))];
                            if (ShouldRenderFace(in neighbor, GetOppositeFace(1)))
                            {
                                int textureId = currentDef.Textures.Bottom;
                                if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 1, currentId, in currentDef, grassOverlayId);
                                else BuildFace(ref vertices, ref transparentIndices, x, y, z, 1, currentId, in currentDef, grassOverlayId);
                            }
                        }
                        else
                        {
                            var neighbor = defs[(int)(isUniform ? uniformId : blocks[index - 16])];
                            if (ShouldRenderFace(in neighbor, GetOppositeFace(1)))
                            {
                                int textureId = currentDef.Textures.Bottom;
                                if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 1, currentId, in currentDef, grassOverlayId);
                                else BuildFace(ref vertices, ref transparentIndices, x, y, z, 1, currentId, in currentDef, grassOverlayId);
                            }
                        }
                    }

                    if (xMin)
                    {
                        var neighbor = defs[(int)GetNeighborBlock(ref nNegX, 15 | (z << 4) | (y << 8))];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(2)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 2, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 2, currentId, in currentDef, grassOverlayId);
                        }
                    }
                    else
                    {
                        var neighbor = defs[(int)(isUniform ? uniformId : blocks[index - 1])];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(2)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 2, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 2, currentId, in currentDef, grassOverlayId);
                        }
                    }

                    if (xMax)
                    {
                        var neighbor = defs[(int)GetNeighborBlock(ref nPosX, (z << 4) | (y << 8))];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(3)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 3, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 3, currentId, in currentDef, grassOverlayId);
                        }
                    }
                    else
                    {
                        var neighbor = defs[(int)(isUniform ? uniformId : blocks[index + 1])];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(3)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 3, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 3, currentId, in currentDef, grassOverlayId);
                        }
                    }

                    if (yMax)
                    {
                        var neighbor = defs[(int)GetNeighborBlock(ref nPosY, x | (z << 4))];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(4)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 4, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 4, currentId, in currentDef, grassOverlayId);
                        }
                    }
                    else
                    {
                        var neighbor = defs[(int)(isUniform ? uniformId : blocks[index + 256])];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(4)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 4, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 4, currentId, in currentDef, grassOverlayId);
                        }
                    }

                    if (yMin)
                    {
                        var neighbor = defs[(int)GetNeighborBlock(ref nNegY, x | (z << 4) | 3840)];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(5)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 5, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 5, currentId, in currentDef, grassOverlayId);
                        }
                    }
                    else
                    {
                        var neighbor = defs[(int)(isUniform ? uniformId : blocks[index - 256])];
                        if (ShouldRenderFace(in neighbor, GetOppositeFace(5)))
                        {
                            int textureId = currentDef.Textures.Side;
                            if (resourceService.IsMaterialOpaque(textureId)) BuildFace(ref vertices, ref opaqueIndices, x, y, z, 5, currentId, in currentDef, grassOverlayId);
                            else BuildFace(ref vertices, ref transparentIndices, x, y, z, 5, currentId, in currentDef, grassOverlayId);
                        }
                    }
                }
            }
        }

        uint opaqueCount = (uint)opaqueIndices.Count;
        if (transparentIndices.Count > 0)
        {
            opaqueIndices.Resize((int)opaqueCount + transparentIndices.Count);
            System.Buffer.MemoryCopy(transparentIndices.Data, opaqueIndices.Data + opaqueCount, transparentIndices.Count * sizeof(ushort), transparentIndices.Count * sizeof(ushort));
        }
        transparentIndices.Dispose();

        if (vertices.Count > 0)
        {
            return new ChunkMesh { Vertices = vertices, Indices = opaqueIndices, OpaqueIndexCount = opaqueCount };
        }
        else
        {
            vertices.Dispose();
            opaqueIndices.Dispose();
            return new ChunkMesh { Vertices = default, Indices = default, OpaqueIndexCount = 0 };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOppositeFace(int faceIndex) => faceIndex switch
    {
        0 => 1,
        1 => 0,
        2 => 3,
        3 => 2,
        4 => 5,
        5 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(faceIndex))
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BlockId GetNeighborBlock(ref ChunkSection chunk, int index)
    {
        if (chunk.Blocks.IsCreated) return chunk.Blocks[index];
        return chunk.UniformId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldRenderFace(in BlockDefinition neighbor, int neighborFaceIndex)
    {
        if (neighbor.Id == BlockId.Air) return true;

        int textureId = neighborFaceIndex switch
        {
            0 => neighbor.Textures.Top,
            1 => neighbor.Textures.Bottom,
            _ => neighbor.Textures.Side
        };

        return !resourceService.IsMaterialOpaque(textureId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildFace(
        ref NativeList<ChunkVertex> vertices, ref NativeList<ushort> indices,
        int x, int y, int z, int faceIndex,
        BlockId currentId, ref readonly BlockDefinition currentDef, int grassOverlayId)
    {
        int textureId = faceIndex switch
        {
            0 => currentDef.Textures.Top,
            1 => currentDef.Textures.Bottom,
            _ => currentDef.Textures.Side
        };

        uint tintType = 0;
        int overlayTexId = -1;

        if (currentId == BlockId.Grass)
        {
            if (faceIndex == 0) tintType = 1;
            else if (faceIndex >= 2 && grassOverlayId >= 0)
            {
                overlayTexId = grassOverlayId;
                tintType = 2;
            }
        }
        else if (currentId == BlockId.OakLeaves)
        {
            tintType = 3;
        }

        uint overlay = overlayTexId >= 0 ? (uint)overlayTexId : 0xFFF;

        uint basePacked = (uint)textureId | (overlay << 12) | (tintType << 26);

        uint indexOffset = (uint)vertices.Count;
        var fVerts = FaceVertices[faceIndex];

        vertices.Add(new ChunkVertex(x + fVerts[0].X, y + fVerts[0].Y, z + fVerts[0].Z, basePacked | (0u << 24)));
        vertices.Add(new ChunkVertex(x + fVerts[1].X, y + fVerts[1].Y, z + fVerts[1].Z, basePacked | (1u << 24)));
        vertices.Add(new ChunkVertex(x + fVerts[2].X, y + fVerts[2].Y, z + fVerts[2].Z, basePacked | (2u << 24)));
        vertices.Add(new ChunkVertex(x + fVerts[3].X, y + fVerts[3].Y, z + fVerts[3].Z, basePacked | (3u << 24)));

        indices.Add((ushort)(indexOffset + 0));
        indices.Add((ushort)(indexOffset + 1));
        indices.Add((ushort)(indexOffset + 2));
        indices.Add((ushort)(indexOffset + 2));
        indices.Add((ushort)(indexOffset + 3));
        indices.Add((ushort)(indexOffset + 0));
    }
}