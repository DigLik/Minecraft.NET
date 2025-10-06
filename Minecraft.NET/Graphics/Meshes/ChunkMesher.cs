using Minecraft.NET.Core.World;

namespace Minecraft.NET.Graphics.Meshes;

public static class ChunkMesher
{
    private const int CHUNK_SIZE = 16;
    private const int CHUNK_HEIGHT_BLOCKS = 256;

    private static readonly float[] CubeVertices =
    [
        -0.5f, -0.5f,  0.5f,
         0.5f, -0.5f,  0.5f,
         0.5f,  0.5f,  0.5f,
        -0.5f,  0.5f,  0.5f,

        -0.5f, -0.5f, -0.5f,
         0.5f, -0.5f, -0.5f,
         0.5f,  0.5f, -0.5f,
        -0.5f,  0.5f, -0.5f,

        -0.5f,  0.5f,  0.5f,
         0.5f,  0.5f,  0.5f,
         0.5f,  0.5f, -0.5f,
        -0.5f,  0.5f, -0.5f,

        -0.5f, -0.5f,  0.5f,
         0.5f, -0.5f,  0.5f,
         0.5f, -0.5f, -0.5f,
        -0.5f, -0.5f, -0.5f,

         0.5f, -0.5f,  0.5f,
         0.5f, -0.5f, -0.5f,
         0.5f,  0.5f, -0.5f,
         0.5f,  0.5f,  0.5f,

        -0.5f, -0.5f,  0.5f,
        -0.5f, -0.5f, -0.5f,
        -0.5f,  0.5f, -0.5f,
        -0.5f,  0.5f,  0.5f,
    ];
    private static readonly uint[] CubeIndices =
    [
        0, 1, 2, 2, 3, 0,
        4, 5, 6, 6, 7, 4,
        8, 9, 10, 10, 11, 8,
        12, 13, 14, 14, 15, 12,
        16, 17, 18, 18, 19, 16,
        20, 21, 22, 22, 23, 20
    ];

    public struct MeshData(float[] vertices, uint[] indices)
    {
        public float[] Vertices = vertices;
        public uint[] Indices = indices;
    }

    public static MeshData GenerateMesh(ChunkColumn column, Vector2 columnPosition)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        uint currentVertexOffset = 0;

        float offsetX = columnPosition.X * CHUNK_SIZE;
        float offsetZ = columnPosition.Y * CHUNK_SIZE;

        for (int chunkY = 0; chunkY < column.Chunks.Length; chunkY++)
        {
            var chunk = column.Chunks[chunkY];
            if (chunk == null) continue;

            float offsetY = chunkY * CHUNK_SIZE;

            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    for (int z = 0; z < CHUNK_SIZE; z++)
                    {
                        var block = chunk.BlockIDs[x, y, z];

                        if (block.ID == 0) continue;

                        float blockX = offsetX + x;
                        float blockY = offsetY + y;
                        float blockZ = offsetZ + z;

                        for (int i = 0; i < CubeVertices.Length; i += 3)
                        {
                            vertices.Add(CubeVertices[i + 0] + blockX);
                            vertices.Add(CubeVertices[i + 1] + blockY);
                            vertices.Add(CubeVertices[i + 2] + blockZ);
                        }

                        foreach (var index in CubeIndices)
                            indices.Add(index + currentVertexOffset);

                        currentVertexOffset += 24;
                    }
                }
            }
        }

        return new MeshData(vertices.ToArray(), indices.ToArray());
    }
}