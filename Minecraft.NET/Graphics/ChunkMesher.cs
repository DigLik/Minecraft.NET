using Minecraft.NET.Core;

namespace Minecraft.NET.Graphics;

public static class ChunkMesher
{
    public static unsafe MeshData GenerateMesh(ChunkSection chunk, World world)
    {
        if (chunk.Blocks == null)
            return new MeshData(null, 0, null, 0);

        using var builder = new MeshBuilder(initialVertexCapacity: 32768, initialIndexCapacity: 49152);
        uint vertexOffset = 0;

        for (int axis = 0; axis < 3; axis++)
        {
            for (int dir = 0; dir < 2; dir++)
            {
                int d = axis;
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                if (axis == 1) (u, v) = (v, u);

                var x = new int[3];
                var q = new int[3];
                q[d] = 1;

                var mask = new BlockId[ChunkSize * ChunkSize];

                for (x[d] = -1; x[d] < ChunkSize;)
                {
                    int n = 0;
                    for (x[v] = 0; x[v] < ChunkSize; x[v]++)
                    {
                        for (x[u] = 0; x[u] < ChunkSize; x[u]++)
                        {
                            var blockCurrent = GetBlock(chunk, world, x[0], x[1], x[2]);
                            var blockNeighbor = GetBlock(chunk, world, x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                            bool isCurrentSolid = blockCurrent != BlockId.Air;
                            bool isNeighborSolid = blockNeighbor != BlockId.Air;

                            if (isCurrentSolid == isNeighborSolid)
                                mask[n] = BlockId.Air;
                            else
                                mask[n] = isCurrentSolid ? blockCurrent : blockNeighbor;
                            n++;
                        }
                    }

                    x[d]++;
                    n = 0;

                    for (int j = 0; j < ChunkSize; j++)
                    {
                        for (int i = 0; i < ChunkSize;)
                        {
                            if (mask[n] != BlockId.Air)
                            {
                                var blockId = mask[n];
                                int w, h;
                                for (w = 1; i + w < ChunkSize && mask[n + w] == blockId; w++) ;
                                bool done = false;
                                for (h = 1; j + h < ChunkSize; h++)
                                {
                                    for (int k = 0; k < w; k++)
                                    {
                                        if (mask[n + k + h * ChunkSize] != blockId) { done = true; break; }
                                    }
                                    if (done) break;
                                }

                                x[u] = i; x[v] = j;
                                var du = new int[3]; du[u] = w;
                                var dv = new int[3]; dv[v] = h;

                                var blockDef = BlockRegistry.Definitions[blockId];
                                Vector2 texCoords;
                                if (axis == 0) texCoords = blockDef.Textures.Side;
                                else if (axis == 1) texCoords = (dir == 0) ? blockDef.Textures.Bottom : blockDef.Textures.Top;
                                else texCoords = blockDef.Textures.Side;

                                var v1 = new Vector3(x[0], x[1], x[2]);
                                var v2 = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                                var v3 = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);
                                var v4 = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);

                                bool reversed = dir == 0;

                                AddQuad(builder, v1, v2, v3, v4, reversed, texCoords, ref vertexOffset);

                                vertexOffset += 4;
                                for (int l = 0; l < h; l++) for (int k = 0; k < w; k++) mask[n + k + l * ChunkSize] = BlockId.Air;
                                i += w; n += w;
                            }
                            else { i++; n++; }
                        }
                    }
                }
            }
        }
        return builder.Build();
    }

    private static void AddQuad(
        MeshBuilder builder,
        Vector3 v_bl, Vector3 v_br, Vector3 v_tl, Vector3 v_tr,
        bool reversed, Vector2 texCoords,
        ref uint vertexOffset)
    {
        uint baseIndex = vertexOffset;
        float Tx = texCoords.X, Ty = texCoords.Y;

        builder.AddVertex(v_bl.X, v_bl.Y, v_bl.Z, Tx, Ty);
        builder.AddVertex(v_br.X, v_br.Y, v_br.Z, Tx, Ty);
        builder.AddVertex(v_tr.X, v_tr.Y, v_tr.Z, Tx, Ty);
        builder.AddVertex(v_tl.X, v_tl.Y, v_tl.Z, Tx, Ty);

        if (reversed)
        {
            builder.AddIndices(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            builder.AddIndices(baseIndex + 0, baseIndex + 2, baseIndex + 3);
        }
        else
        {
            builder.AddIndices(baseIndex + 0, baseIndex + 2, baseIndex + 1);
            builder.AddIndices(baseIndex + 0, baseIndex + 3, baseIndex + 2);
        }
    }

    private static BlockId GetBlock(ChunkSection chunk, World world, int x, int y, int z)
    {
        if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize)
            return chunk.GetBlock(x, y, z);
        var worldPos = chunk.Position * ChunkSize + new Vector3(x, y, z);
        var neighborChunkPos = new Vector3(MathF.Floor(worldPos.X / ChunkSize), MathF.Floor(worldPos.Y / ChunkSize), MathF.Floor(worldPos.Z / ChunkSize));
        var neighborChunk = world.GetChunk(neighborChunkPos);
        if (neighborChunk == null) return BlockId.Air;
        int localX = (int)worldPos.X % ChunkSize; if (localX < 0) localX += ChunkSize;
        int localY = (int)worldPos.Y % ChunkSize; if (localY < 0) localY += ChunkSize;
        int localZ = (int)worldPos.Z % ChunkSize; if (localZ < 0) localZ += ChunkSize;
        return neighborChunk.GetBlock(localX, localY, localZ);
    }
}