using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Rendering;

namespace Minecraft.NET.Services;

public class VisibleScene
{
    public List<ChunkMeshGeometry> VisibleGeometries { get; } = new(MaxVisibleSections);
    public List<Matrix4x4> ModelMatrices { get; } = new(MaxVisibleSections);
    public int VisibleSectionCount => VisibleGeometries.Count;
}

public class SceneCuller(Player player, ChunkManager chunkProvider)
{
    private readonly Frustum _frustum = new();

    public VisibleScene Result { get; } = new();

    public void Cull(in Matrix4x4 projectionMatrix, in Matrix4x4 relativeViewMatrix)
    {
        _frustum.Update(relativeViewMatrix * projectionMatrix);

        Result.VisibleGeometries.Clear();
        Result.ModelMatrices.Clear();

        var cameraOrigin = new Vector3d(Math.Floor(player.Position.X), Math.Floor(player.Position.Y), Math.Floor(player.Position.Z));

        var loadedChunks = chunkProvider.GetLoadedChunks();

        bool maxSectionsReached = false;
        foreach (var chunkPair in loadedChunks)
        {
            var column = chunkPair.Value;
            var chunkPosDouble = new Vector3d(column.Position.X, 0, column.Position.Y);
            var chunkWorldPosBase = chunkPosDouble * ChunkSize;

            var relativeChunkPosBase = (Vector3)(chunkWorldPosBase - new Vector3d(cameraOrigin.X, 0, cameraOrigin.Z));

            float columnMinY = (float)((0 - VerticalChunkOffset) * ChunkSize - cameraOrigin.Y);
            float columnMaxY = (float)((WorldHeightInChunks - VerticalChunkOffset) * ChunkSize - cameraOrigin.Y);

            var columnBox = new BoundingBox(
                new Vector3(relativeChunkPosBase.X, columnMinY, relativeChunkPosBase.Z),
                new Vector3(relativeChunkPosBase.X + ChunkSize, columnMaxY, relativeChunkPosBase.Z + ChunkSize)
            );

            if (!_frustum.Intersects(columnBox))
            {
                continue;
            }

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                var geometry = column.MeshGeometries[y];
                if (geometry is null)
                    continue;

                float sectionRelativeY = (float)((y - VerticalChunkOffset) * ChunkSize - cameraOrigin.Y);
                var relativeSectionPos = new Vector3(relativeChunkPosBase.X, sectionRelativeY, relativeChunkPosBase.Z);

                var sectionBox = new BoundingBox(relativeSectionPos, relativeSectionPos + new Vector3(ChunkSize));
                if (!_frustum.Intersects(sectionBox))
                    continue;

                if (Result.VisibleGeometries.Count >= MaxVisibleSections)
                {
                    maxSectionsReached = true;
                    break;
                }

                Result.VisibleGeometries.Add(geometry.Value);
                Result.ModelMatrices.Add(Matrix4x4.CreateTranslation(relativeSectionPos));
            }

            if (maxSectionsReached)
            {
                break;
            }
        }
    }
}