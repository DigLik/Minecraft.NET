using Minecraft.NET.Core;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics;

file readonly record struct GeneratedFace(
    float[] FaceVertices,
    Vector3 BlockPos,
    Vector2 TexCoords,
    float[] Occlusion,
    uint[] Indices
);

public static class ChunkMesher
{
    private static readonly float[] FrontFace = [-0.5f, -0.5f, 0.5f, 0, 1, 0.5f, -0.5f, 0.5f, 1, 1, 0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, 0.5f, 0, 0];
    private static readonly float[] BackFace = [0.5f, -0.5f, -0.5f, 0, 1, -0.5f, -0.5f, -0.5f, 1, 1, -0.5f, 0.5f, -0.5f, 1, 0, 0.5f, 0.5f, -0.5f, 0, 0];
    private static readonly float[] TopFace = [-0.5f, 0.5f, 0.5f, 0, 1, 0.5f, 0.5f, 0.5f, 1, 1, 0.5f, 0.5f, -0.5f, 1, 0, -0.5f, 0.5f, -0.5f, 0, 0];
    private static readonly float[] BottomFace = [-0.5f, -0.5f, -0.5f, 0, 1, 0.5f, -0.5f, -0.5f, 1, 1, 0.5f, -0.5f, 0.5f, 1, 0, -0.5f, -0.5f, 0.5f, 0, 0];
    private static readonly float[] RightFace = [0.5f, -0.5f, 0.5f, 0, 1, 0.5f, -0.5f, -0.5f, 1, 1, 0.5f, 0.5f, -0.5f, 1, 0, 0.5f, 0.5f, 0.5f, 0, 0];
    private static readonly float[] LeftFace = [-0.5f, -0.5f, -0.5f, 0, 1, -0.5f, -0.5f, 0.5f, 1, 1, -0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, -0.5f, 0, 0];

    private static readonly uint[] FaceIndices = [0, 1, 2, 2, 3, 0];
    private static readonly uint[] FlippedFaceIndices = [1, 2, 3, 3, 0, 1];

    private const float MinLight = 0.4f;

    public static void Initialize() { }

    private static float CalculateAO(bool side1, bool side2, bool corner)
    {
        if (side1 && side2) return MinLight;
        int obstructions = (side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0);
        return 1.0f - obstructions * (1.0f - MinLight) / 3.0f;
    }

    public static unsafe MeshData GenerateMesh(ChunkSection chunk, World world)
    {
        if (chunk.Blocks == null)
            return new MeshData(null, 0, null, 0);

        var visibleFaces = new ConcurrentBag<GeneratedFace>();

        Parallel.For(0, BlocksInChunk, i =>
        {
            int x = i % ChunkSize;
            int y = (i / ChunkSize) % ChunkSize;
            int z = i / (ChunkSize * ChunkSize);

            var blockId = chunk.Blocks[i];
            if (blockId == BlockId.Air) return;

            var def = BlockRegistry.Definitions[blockId];
            var blockPos = new Vector3(x, y, z);

            if (GetNeighbor(chunk, world, x, y + 1, z) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x - 1, y + 1, z), IsSolid(chunk, world, x - 1, y + 1, z - 1),
                    IsSolid(chunk, world, x, y + 1, z - 1), IsSolid(chunk, world, x + 1, y + 1, z - 1),
                    IsSolid(chunk, world, x + 1, y + 1, z), IsSolid(chunk, world, x + 1, y + 1, z + 1),
                    IsSolid(chunk, world, x, y + 1, z + 1), IsSolid(chunk, world, x - 1, y + 1, z + 1)
                };
                var ao = new[] { CalculateAO(n[0], n[6], n[7]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[0], n[2], n[1]) };
                visibleFaces.Add(new GeneratedFace(TopFace, blockPos, def.Textures.Top, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }

            if (GetNeighbor(chunk, world, x, y - 1, z) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x - 1, y - 1, z), IsSolid(chunk, world, x - 1, y - 1, z - 1),
                    IsSolid(chunk, world, x, y - 1, z - 1), IsSolid(chunk, world, x + 1, y - 1, z - 1),
                    IsSolid(chunk, world, x + 1, y - 1, z), IsSolid(chunk, world, x + 1, y - 1, z + 1),
                    IsSolid(chunk, world, x, y - 1, z + 1), IsSolid(chunk, world, x - 1, y - 1, z + 1)
                };
                var ao = new[] { CalculateAO(n[0], n[2], n[1]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[0], n[6], n[7]) };
                visibleFaces.Add(new GeneratedFace(BottomFace, blockPos, def.Textures.Bottom, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }

            if (GetNeighbor(chunk, world, x, y, z + 1) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x - 1, y, z + 1), IsSolid(chunk, world, x - 1, y - 1, z + 1),
                    IsSolid(chunk, world, x, y - 1, z + 1), IsSolid(chunk, world, x + 1, y - 1, z + 1),
                    IsSolid(chunk, world, x + 1, y, z + 1), IsSolid(chunk, world, x + 1, y + 1, z + 1),
                    IsSolid(chunk, world, x, y + 1, z + 1), IsSolid(chunk, world, x - 1, y + 1, z + 1)
                };
                var ao = new[] { CalculateAO(n[0], n[2], n[1]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[0], n[6], n[7]) };
                visibleFaces.Add(new GeneratedFace(FrontFace, blockPos, def.Textures.Side, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }

            if (GetNeighbor(chunk, world, x, y, z - 1) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x + 1, y, z - 1), IsSolid(chunk, world, x + 1, y - 1, z - 1),
                    IsSolid(chunk, world, x, y - 1, z - 1), IsSolid(chunk, world, x - 1, y - 1, z - 1),
                    IsSolid(chunk, world, x - 1, y, z - 1), IsSolid(chunk, world, x - 1, y + 1, z - 1),
                    IsSolid(chunk, world, x, y + 1, z - 1), IsSolid(chunk, world, x + 1, y + 1, z - 1)
                };
                var ao = new[] { CalculateAO(n[0], n[2], n[1]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[0], n[6], n[7]) };
                visibleFaces.Add(new GeneratedFace(BackFace, blockPos, def.Textures.Side, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }

            if (GetNeighbor(chunk, world, x + 1, y, z) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x + 1, y, z + 1), IsSolid(chunk, world, x + 1, y - 1, z + 1),
                    IsSolid(chunk, world, x + 1, y - 1, z), IsSolid(chunk, world, x + 1, y - 1, z - 1),
                    IsSolid(chunk, world, x + 1, y, z - 1), IsSolid(chunk, world, x + 1, y + 1, z - 1),
                    IsSolid(chunk, world, x + 1, y + 1, z), IsSolid(chunk, world, x + 1, y + 1, z + 1)
                };
                var ao = new[] { CalculateAO(n[0], n[2], n[1]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[0], n[6], n[7]) };
                visibleFaces.Add(new GeneratedFace(RightFace, blockPos, def.Textures.Side, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }

            if (GetNeighbor(chunk, world, x - 1, y, z) == BlockId.Air)
            {
                var n = new[] {
                    IsSolid(chunk, world, x - 1, y, z - 1), IsSolid(chunk, world, x - 1, y - 1, z - 1),
                    IsSolid(chunk, world, x - 1, y - 1, z), IsSolid(chunk, world, x - 1, y - 1, z + 1),
                    IsSolid(chunk, world, x - 1, y, z + 1), IsSolid(chunk, world, x - 1, y + 1, z + 1),
                    IsSolid(chunk, world, x - 1, y + 1, z), IsSolid(chunk, world, x - 1, y + 1, z - 1)
                };
                var ao = new[] { CalculateAO(n[0], n[2], n[1]), CalculateAO(n[4], n[2], n[3]), CalculateAO(n[4], n[6], n[5]), CalculateAO(n[0], n[6], n[7]) };
                visibleFaces.Add(new GeneratedFace(LeftFace, blockPos, def.Textures.Side, ao, ao[0] + ao[2] > ao[1] + ao[3] ? FlippedFaceIndices : FaceIndices));
            }
        });

        if (visibleFaces.IsEmpty)
            return new MeshData(null, 0, null, 0);

        int vertexCount = visibleFaces.Count * 4 * 6;
        int indexCount = visibleFaces.Count * 6;
        var verticesPtr = (float*)NativeMemory.Alloc((nuint)vertexCount, sizeof(float));
        var indicesPtr = (uint*)NativeMemory.Alloc((nuint)indexCount, sizeof(uint));

        int vertexI = 0;
        int indexI = 0;
        uint vertexOffset = 0;

        const float pixelPadding = 0.1f;

        foreach (var face in visibleFaces)
        {
            float startX = face.TexCoords.X * TileSize;
            float startY = face.TexCoords.Y * TileSize;
            float endX = startX + TileSize;
            float endY = startY + TileSize;

            startX += pixelPadding;
            startY += pixelPadding;
            endX -= pixelPadding;
            endY -= pixelPadding;

            float u0 = startX / AtlasWidth;
            float v0 = startY / AtlasHeight;
            float u1 = endX / AtlasWidth;
            float v1 = endY / AtlasHeight;

            for (int i = 0; i < 4; i++)
            {
                int baseIndex = i * 5;
                verticesPtr[vertexI++] = face.FaceVertices[baseIndex + 0] + face.BlockPos.X;
                verticesPtr[vertexI++] = face.FaceVertices[baseIndex + 1] + face.BlockPos.Y;
                verticesPtr[vertexI++] = face.FaceVertices[baseIndex + 2] + face.BlockPos.Z;
                verticesPtr[vertexI++] = face.FaceVertices[baseIndex + 3] == 0 ? u0 : u1;
                verticesPtr[vertexI++] = face.FaceVertices[baseIndex + 4] == 0 ? v0 : v1;
                verticesPtr[vertexI++] = face.Occlusion[i];
            }

            for (int i = 0; i < face.Indices.Length; i++)
                indicesPtr[indexI++] = vertexOffset + face.Indices[i];

            vertexOffset += 4;
        }

        return new MeshData(verticesPtr, vertexCount, indicesPtr, indexCount);
    }

    private static bool IsSolid(ChunkSection chunk, World world, int x, int y, int z)
    {
        return GetNeighbor(chunk, world, x, y, z) != BlockId.Air;
    }

    private static BlockId GetNeighbor(ChunkSection chunk, World world, int x, int y, int z)
    {
        if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize)
            return chunk.GetBlock(x, y, z);

        var worldPos = chunk.Position * ChunkSize + new Vector3(x, y, z);

        var neighborChunkPos = new Vector3(
            MathF.Floor(worldPos.X / ChunkSize),
            MathF.Floor(worldPos.Y / ChunkSize),
            MathF.Floor(worldPos.Z / ChunkSize)
        );

        var neighborChunk = world.GetChunk(neighborChunkPos);
        if (neighborChunk == null) return BlockId.Air;

        int localX = (int)worldPos.X % ChunkSize;
        if (localX < 0) localX += ChunkSize;

        int localY = (int)worldPos.Y % ChunkSize;
        if (localY < 0) localY += ChunkSize;

        int localZ = (int)worldPos.Z % ChunkSize;
        if (localZ < 0) localZ += ChunkSize;

        return neighborChunk.GetBlock(localX, localY, localZ);
    }
}