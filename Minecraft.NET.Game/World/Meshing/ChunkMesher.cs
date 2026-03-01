using System.Numerics;

using Minecraft.NET.Game.World.Blocks;
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
        var vertices = new List<ChunkVertex>();
        var indices = new List<uint>();
        uint indexOffset = 0;

        Vector3<int> chunkGlobalOrigin = new(chunkPos.X * ChunkSize, chunkPos.Y * ChunkSize, chunkPos.Z * ChunkSize);
        int grassOverlayId = BlockRegistry.TextureFiles.FindIndex(f => f.Contains("grass_side_overlay"));

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    Vector3<int> globalPos = chunkGlobalOrigin + new Vector3<int>(x, y, z);
                    BlockId currentId = chunkManager.GetBlock(globalPos);

                    if (currentId == BlockId.Air) continue;

                    var currentDef = BlockRegistry.Definitions[(int)currentId];

                    for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                    {
                        Vector3<int> neighborPos = globalPos + FaceOffsets[faceIndex];
                        BlockId neighborId = chunkManager.GetBlock(neighborPos);
                        var neighborDef = BlockRegistry.Definitions[(int)neighborId];

                        if (ShouldRenderFace(currentDef, neighborDef))
                        {
                            int textureId = GetTextureId(currentDef.Textures, faceIndex);
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
                                    overlayColor = new Vector4((145.0f / 255.0f) * shade, (189.0f / 255.0f) * shade, (89.0f / 255.0f) * shade, 1.0f);
                                }
                            }
                            else if (currentId == BlockId.OakLeaves)
                            {
                                color.X = (72.0f / 255.0f) * shade;
                                color.Y = (181.0f / 255.0f) * shade;
                                color.Z = (72.0f / 255.0f) * shade;
                            }

                            for (int v = 0; v < 4; v++)
                            {
                                Vector3<float> pos = new Vector3<float>(x, y, z) + FaceVertices[faceIndex][v];
                                vertices.Add(new ChunkVertex(pos, FaceUVs[v], textureId, color, overlayTexId, overlayColor));
                            }

                            indices.Add(indexOffset + 0);
                            indices.Add(indexOffset + 1);
                            indices.Add(indexOffset + 2);
                            indices.Add(indexOffset + 2);
                            indices.Add(indexOffset + 3);
                            indices.Add(indexOffset + 0);

                            indexOffset += 4;
                        }
                    }
                }
            }
        }

        return new ChunkMesh { Vertices = [.. vertices], Indices = [.. indices] };
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