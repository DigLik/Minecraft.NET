using Minecraft.NET.Core;
using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.World;
using Minecraft.NET.Shared.Structs;
using System.Collections.Concurrent;

namespace Minecraft.NET.Graphics.Meshes;

public readonly record struct ChunkMeshData(Vector2 Position, float[] Vertices, uint[] Indices);
file readonly record struct GeneratedFace(float[] FaceVertices, Vector3 BlockPos, Vector2 TexCoords);

public static class ChunkMesher
{
    #region Cube Face Definitions
    private static readonly float[] FrontFace = [-0.5f, -0.5f, 0.5f, 0, 1, 0.5f, -0.5f, 0.5f, 1, 1, 0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, 0.5f, 0, 0];
    private static readonly float[] BackFace = [0.5f, -0.5f, -0.5f, 0, 1, -0.5f, -0.5f, -0.5f, 1, 1, -0.5f, 0.5f, -0.5f, 1, 0, 0.5f, 0.5f, -0.5f, 0, 0];
    private static readonly float[] TopFace = [-0.5f, 0.5f, 0.5f, 0, 1, 0.5f, 0.5f, 0.5f, 1, 1, 0.5f, 0.5f, -0.5f, 1, 0, -0.5f, 0.5f, -0.5f, 0, 0];
    private static readonly float[] BottomFace = [-0.5f, -0.5f, -0.5f, 0, 1, 0.5f, -0.5f, -0.5f, 1, 1, 0.5f, -0.5f, 0.5f, 1, 0, -0.5f, -0.5f, 0.5f, 0, 0];
    private static readonly float[] RightFace = [0.5f, -0.5f, 0.5f, 0, 1, 0.5f, -0.5f, -0.5f, 1, 1, 0.5f, 0.5f, -0.5f, 1, 0, 0.5f, 0.5f, 0.5f, 0, 0];
    private static readonly float[] LeftFace = [-0.5f, -0.5f, -0.5f, 0, 1, -0.5f, -0.5f, 0.5f, 1, 1, -0.5f, 0.5f, 0.5f, 1, 0, -0.5f, 0.5f, -0.5f, 0, 0];

    private static readonly uint[] FaceIndices = [0, 1, 2, 2, 3, 0];
    #endregion

    public static MeshBuffer GenerateMesh(IWorldManager world, ChunkColumn column, Vector2 columnPosition, GameSettings gameSettings)
    {
        var buffer = MeshBufferPool.Get();
        buffer.Position = columnPosition;

        var visibleFaces = new ConcurrentBag<GeneratedFace>();

        var threadLocalCache = new ThreadLocal<Dictionary<Vector2, ChunkColumn?>>(() =>
            new Dictionary<Vector2, ChunkColumn?> { [columnPosition] = column }
        );

        Parallel.For(0, column.Chunks.Length, chunkY =>
        {
            var localChunkCache = threadLocalCache.Value!;
            var chunk = column.Chunks[chunkY];
            float offsetX = columnPosition.X * gameSettings.ChunkSize;
            float offsetZ = columnPosition.Y * gameSettings.ChunkSize;
            float offsetY = chunkY * gameSettings.ChunkSize;

            for (int x = 0; x < gameSettings.ChunkSize; x++)
                for (int y = 0; y < gameSettings.ChunkSize; y++)
                    for (int z = 0; z < gameSettings.ChunkSize; z++)
                    {
                        var blockId = chunk.GetBlock(x, y, z).ID;
                        if (blockId == BlockManager.Air.ID) continue;
                        if (!BlockManager.TryGetDefinition(blockId, out var def)) continue;

                        var blockPos = new Vector3(offsetX + x, offsetY + y, offsetZ + z);

                        if (IsTransparent(world, localChunkCache, columnPosition, x + 1, y, z, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(RightFace, blockPos, def.Textures.Side));
                        if (IsTransparent(world, localChunkCache, columnPosition, x - 1, y, z, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(LeftFace, blockPos, def.Textures.Side));
                        if (IsTransparent(world, localChunkCache, columnPosition, x, y + 1, z, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(TopFace, blockPos, def.Textures.Top));
                        if (IsTransparent(world, localChunkCache, columnPosition, x, y - 1, z, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(BottomFace, blockPos, def.Textures.Bottom));
                        if (IsTransparent(world, localChunkCache, columnPosition, x, y, z + 1, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(FrontFace, blockPos, def.Textures.Side));
                        if (IsTransparent(world, localChunkCache, columnPosition, x, y, z - 1, chunkY, gameSettings))
                            visibleFaces.Add(new GeneratedFace(BackFace, blockPos, def.Textures.Side));
                    }
        });

        buffer.EnsureVertexCapacity(visibleFaces.Count * 4 * 5);
        buffer.EnsureIndexCapacity(visibleFaces.Count * 6);

        var verticesBuffer = new ResizableBuffer<float>(buffer.GetVertexSpan());
        var indicesBuffer = new ResizableBuffer<uint>(buffer.GetIndexSpan());

        uint vertexOffset = 0;
        foreach (var face in visibleFaces)
        {
            AddFace(ref verticesBuffer, ref indicesBuffer, ref vertexOffset, face.FaceVertices, face.BlockPos, face.TexCoords);
        }

        buffer.VerticesCount = verticesBuffer.Count;
        buffer.IndicesCount = indicesBuffer.Count;

        return buffer;
    }

    private static void AddFace(
        ref ResizableBuffer<float> vertices,
        ref ResizableBuffer<uint> indices,
        ref uint vertexOffset, float[] faceVertices,
        Vector3 blockPos, Vector2 texCoords)
    {
        float u0 = (texCoords.X * Constants.TileSize) / Constants.AtlasWidth;
        float v0 = (texCoords.Y * Constants.TileSize) / Constants.AtlasHeight;
        float u1 = u0 + (Constants.TileSize / Constants.AtlasWidth);
        float v1 = v0 + (Constants.TileSize / Constants.AtlasHeight);

        for (int i = 0; i < 4; i++)
        {
            int baseIndex = i * 5;
            vertices.Add(faceVertices[baseIndex + 0] + blockPos.X);
            vertices.Add(faceVertices[baseIndex + 1] + blockPos.Y);
            vertices.Add(faceVertices[baseIndex + 2] + blockPos.Z);
            vertices.Add(faceVertices[baseIndex + 3] == 0 ? u0 : u1);
            vertices.Add(faceVertices[baseIndex + 4] == 0 ? v0 : v1);
        }

        for (int i = 0; i < FaceIndices.Length; i++)
            indices.Add(vertexOffset + FaceIndices[i]);

        vertexOffset += 4;
    }

    private static ChunkColumn? GetNeighborColumn(IWorldManager world, Dictionary<Vector2, ChunkColumn?> cache, Vector2 position)
    {
        if (cache.TryGetValue(position, out var column)) return column;
        column = world.GetChunkColumn(position);
        cache[position] = column;
        return column;
    }

    private static bool IsTransparent(
        IWorldManager world,
        Dictionary<Vector2, ChunkColumn?> cache,
        Vector2 currentColumnPos,
        int x, int y, int z, int chunkY, GameSettings gameSettings)
    {
        int worldBlockY = chunkY * gameSettings.ChunkSize + y;
        if (worldBlockY < 0 || worldBlockY >= gameSettings.WorldHeightInBlocks) return true;

        Vector2 targetColumnPos = currentColumnPos;
        int localX = x;
        int localZ = z;

        if (x < 0) { localX = gameSettings.ChunkSize - 1; targetColumnPos.X--; }
        else if (x >= gameSettings.ChunkSize) { localX = 0; targetColumnPos.X++; }

        if (z < 0) { localZ = gameSettings.ChunkSize - 1; targetColumnPos.Y--; }
        else if (z >= gameSettings.ChunkSize) { localZ = 0; targetColumnPos.Y++; }

        var targetColumn = GetNeighborColumn(world, cache, targetColumnPos);
        if (targetColumn is null) return true;

        int targetChunkY = worldBlockY / gameSettings.ChunkSize;
        int localY = worldBlockY % gameSettings.ChunkSize;

        if (targetChunkY < 0 || targetChunkY >= targetColumn.Chunks.Length) return true;

        var blockId = targetColumn.Chunks[targetChunkY].GetBlock(localX, localY, localZ).ID;
        return blockId == BlockManager.Air.ID;
    }
}