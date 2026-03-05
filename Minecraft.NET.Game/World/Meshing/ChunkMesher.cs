using System.Buffers;
using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

public unsafe class ChunkMesher(ChunkManager chunkManager)
{
    private static readonly float[] FaceShades = [1.0f, 0.5f, 0.8f, 0.8f, 0.6f, 0.6f];

    private static readonly Vector3<float>[][] FaceVertices = [
        [new(0, 1, 1), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1)],
        [new(1, 1, 0), new(1, 0, 0), new(0, 0, 0), new(0, 1, 0)],
        [new(0, 1, 1), new(0, 1, 0), new(0, 0, 0), new(0, 0, 1)],
        [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)],
        [new(1, 1, 1), new(1, 1, 0), new(0, 1, 0), new(0, 1, 1)],
        [new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1)]
    ];

    private static readonly Vector2<float>[] FaceUVs = [
        new(0, 0), new(0, 1), new(1, 1), new(1, 0)
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCapacity<T>(ref T[] array, int count, int required)
    {
        if (count + required > array.Length)
        {
            var newArray = ArrayPool<T>.Shared.Rent(Math.Max(array.Length * 2, count + required));
            Array.Copy(array, newArray, count);
            ArrayPool<T>.Shared.Return(array);
            array = newArray;
        }
    }

    public ChunkMesh GenerateMesh(Vector3<int> chunkPos)
    {
        if (!chunkManager.TryGetChunk(chunkPos, out ChunkSection centerChunk) || centerChunk.IsEmpty)
            return new ChunkMesh { Vertices = null!, Indices = null! };

        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(0, 0, 1), out ChunkSection nPosZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(0, 0, -1), out ChunkSection nNegZ);
        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(-1, 0, 0), out ChunkSection nNegX);
        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(1, 0, 0), out ChunkSection nPosX);
        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(0, 1, 0), out ChunkSection nPosY);
        chunkManager.TryGetChunk(chunkPos + new Vector3<int>(0, -1, 0), out ChunkSection nNegY);

        ChunkVertex[] vertices = ArrayPool<ChunkVertex>.Shared.Rent(4096);
        uint[] indices = ArrayPool<uint>.Shared.Rent(6144);
        int vertexCount = 0;
        int indexCount = 0;

        int grassOverlayId = BlockRegistry.TextureFiles.FindIndex(f => f.Contains("grass_side_overlay"));
        BlockDefinition[] defs = BlockRegistry.Definitions;

        BlockId* blocks = centerChunk.Blocks;
        BlockId uniformId = centerChunk.UniformId;
        bool isUniform = blocks == null;

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
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 0, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 16])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 0, currentId, ref currentDef, grassOverlayId);
                    }

                    if (zMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegZ, x | 240 | (y << 8))]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 1, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 16])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 1, currentId, ref currentDef, grassOverlayId);
                    }

                    if (xMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegX, 15 | (z << 4) | (y << 8))]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 2, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 1])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 2, currentId, ref currentDef, grassOverlayId);
                    }

                    if (xMax)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nPosX, (z << 4) | (y << 8))]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 3, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 1])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 3, currentId, ref currentDef, grassOverlayId);
                    }

                    if (yMax)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nPosY, x | (z << 4))]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 4, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index + 256])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 4, currentId, ref currentDef, grassOverlayId);
                    }

                    if (yMin)
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)GetNeighborBlock(ref nNegY, x | (z << 4) | 3840)]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 5, currentId, ref currentDef, grassOverlayId);
                    }
                    else
                    {
                        if (ShouldRenderFace(in currentDef, in defs[(int)(isUniform ? uniformId : blocks[index - 256])]))
                            BuildFace(ref vertices, ref vertexCount, ref indices, ref indexCount, x, y, z, 5, currentId, ref currentDef, grassOverlayId);
                    }
                }
            }
        }

        if (vertexCount > 0)
        {
            return new ChunkMesh { Vertices = vertices, VertexCount = vertexCount, Indices = indices, IndexCount = indexCount };
        }
        else
        {
            ArrayPool<ChunkVertex>.Shared.Return(vertices);
            ArrayPool<uint>.Shared.Return(indices);
            return new ChunkMesh { Vertices = null!, Indices = null! };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BlockId GetNeighborBlock(ref ChunkSection chunk, int index)
    {
        if (chunk.Blocks != null) return chunk.Blocks[index];
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
        ref ChunkVertex[] vertices, ref int vertexCount, ref uint[] indices, ref int indexCount,
        int x, int y, int z, int faceIndex,
        BlockId currentId, ref BlockDefinition currentDef, int grassOverlayId)
    {
        EnsureCapacity(ref vertices, vertexCount, 4);
        EnsureCapacity(ref indices, indexCount, 6);

        int textureId = faceIndex switch
        {
            0 => currentDef.Textures.Top,
            1 => currentDef.Textures.Bottom,
            _ => currentDef.Textures.Side
        };

        float shade = FaceShades[faceIndex];
        Vector4<float> color = new Vector4<float>(shade, shade, shade, 1.0f);
        int overlayTexId = -1;
        Vector4<float> overlayColor = Vector4<float>.Zero;

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
                overlayColor = new Vector4<float>(145.0f / 255.0f * shade, 189.0f / 255.0f * shade, 89.0f / 255.0f * shade, 1.0f);
            }
        }
        else if (currentId == BlockId.OakLeaves)
        {
            color.X = 72.0f / 255.0f * shade;
            color.Y = 181.0f / 255.0f * shade;
            color.Z = 72.0f / 255.0f * shade;
        }

        uint indexOffset = (uint)vertexCount;
        var fVerts = FaceVertices[faceIndex];

        vertices[vertexCount++] = new ChunkVertex(new Vector3<float>(x + fVerts[0].X, y + fVerts[0].Y, z + fVerts[0].Z), textureId, FaceUVs[0], overlayTexId, color, overlayColor);
        vertices[vertexCount++] = new ChunkVertex(new Vector3<float>(x + fVerts[1].X, y + fVerts[1].Y, z + fVerts[1].Z), textureId, FaceUVs[1], overlayTexId, color, overlayColor);
        vertices[vertexCount++] = new ChunkVertex(new Vector3<float>(x + fVerts[2].X, y + fVerts[2].Y, z + fVerts[2].Z), textureId, FaceUVs[2], overlayTexId, color, overlayColor);
        vertices[vertexCount++] = new ChunkVertex(new Vector3<float>(x + fVerts[3].X, y + fVerts[3].Y, z + fVerts[3].Z), textureId, FaceUVs[3], overlayTexId, color, overlayColor);

        indices[indexCount++] = indexOffset + 0;
        indices[indexCount++] = indexOffset + 1;
        indices[indexCount++] = indexOffset + 2;
        indices[indexCount++] = indexOffset + 2;
        indices[indexCount++] = indexOffset + 3;
        indices[indexCount++] = indexOffset + 0;
    }
}