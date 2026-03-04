using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

public class ChunkMesher(ChunkManager chunkManager)
{
    private static readonly Vector3<int>[] FaceOffsets = [
        new(0, 0, 1), new(0, 0, -1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0)
    ];

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

    public ChunkMesh GenerateMesh(Vector3<int> chunkPos)
    {
        if (!chunkManager.TryGetChunk(chunkPos, out ChunkSection centerChunk) || centerChunk.IsEmpty)
            return new ChunkMesh { Vertices = [], Indices = [] };

        ChunkSection[] neighbors = new ChunkSection[6];
        for (int i = 0; i < 6; i++)
            chunkManager.TryGetChunk(chunkPos + FaceOffsets[i], out neighbors[i]);

        List<ChunkVertex> vertices = new(2048);
        List<uint> indices = new(3072);

        int grassOverlayId = BlockRegistry.TextureFiles.FindIndex(f => f.Contains("grass_side_overlay"));

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    BlockId currentId = centerChunk.GetBlock(new Vector3<int>(x, y, z));
                    if (currentId == BlockId.Air) continue;

                    var currentDef = BlockRegistry.Definitions[(int)currentId];

                    for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                    {
                        var offset = FaceOffsets[faceIndex];
                        int nx = x + offset.X;
                        int ny = y + offset.Y;
                        int nz = z + offset.Z;

                        BlockId neighborId;

                        if (nx >= 0 && nx < ChunkSize && ny >= 0 && ny < ChunkSize && nz >= 0 && nz < ChunkSize)
                        {
                            neighborId = centerChunk.GetBlock(new Vector3<int>(nx, ny, nz));
                        }
                        else
                        {
                            ref ChunkSection neighborChunk = ref neighbors[faceIndex];
                            if (neighborChunk.IsAllocated || neighborChunk.UniformId != BlockId.Air)
                            {
                                int wrapX = (nx + ChunkSize) % ChunkSize;
                                int wrapY = (ny + ChunkSize) % ChunkSize;
                                int wrapZ = (nz + ChunkSize) % ChunkSize;
                                neighborId = neighborChunk.GetBlock(new Vector3<int>(wrapX, wrapY, wrapZ));
                            }
                            else
                            {
                                neighborId = BlockId.Air;
                            }
                        }

                        var neighborDef = BlockRegistry.Definitions[(int)neighborId];

                        if (ShouldRenderFace(currentDef, neighborDef))
                        {
                            int textureId = GetTextureId(currentDef.Textures, faceIndex);
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

                            uint indexOffset = (uint)vertices.Count;

                            for (int v = 0; v < 4; v++)
                            {
                                Vector3<float> pos = new Vector3<float>(x, y, z) + FaceVertices[faceIndex][v];
                                vertices.Add(new ChunkVertex(pos, textureId, FaceUVs[v], overlayTexId, color, overlayColor));
                            }

                            indices.Add(indexOffset + 0);
                            indices.Add(indexOffset + 1);
                            indices.Add(indexOffset + 2);
                            indices.Add(indexOffset + 2);
                            indices.Add(indexOffset + 3);
                            indices.Add(indexOffset + 0);
                        }
                    }
                }
            }
        }

        if (vertices.Count > 0)
            return new ChunkMesh { Vertices = [.. vertices], Indices = [.. indices] };
        else
            return new ChunkMesh { Vertices = [], Indices = [] };
    }

    private static bool ShouldRenderFace(BlockDefinition current, BlockDefinition neighbor)
    {
        if (neighbor.Id == BlockId.Air) return true;
        if (current.Id == BlockId.OakLeaves && neighbor.Id == BlockId.OakLeaves) return true;
        if (current.Transparency == BlockTransparency.Foliage) return true;
        if (neighbor.Transparency == BlockTransparency.Foliage) return true;
        if (current.Transparency == BlockTransparency.Transparent)
        {
            if (neighbor.Transparency == BlockTransparency.Opaque) return false;
            return true;
        }
        if (current.Transparency == BlockTransparency.Opaque)
        {
            if (neighbor.Transparency == BlockTransparency.Opaque) return false;
            return true;
        }
        return true;
    }

    private static int GetTextureId(BlockFaceTextures textures, int faceIndex) => faceIndex switch
    {
        0 => textures.Top,
        1 => textures.Bottom,
        _ => textures.Side
    };
}