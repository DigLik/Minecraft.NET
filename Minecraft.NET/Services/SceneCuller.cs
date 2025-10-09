using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Models;

namespace Minecraft.NET.Services;

public class VisibleScene
{
    public IReadOnlyList<Mesh> Meshes { get; internal set; } = [];
    public IReadOnlyList<Matrix4x4> ModelMatrices { get; internal set; } = [];
    public int VisibleSectionCount => Meshes.Count;
}

public class SceneCuller(Player player, ChunkManager chunkProvider)
{
    private readonly Frustum _frustum = new();
    private readonly List<Matrix4x4> _modelMatrices = new(MaxVisibleSections);
    private readonly List<Mesh> _visibleMeshes = new(MaxVisibleSections);

    public VisibleScene Result { get; } = new();

    public void Cull(in Matrix4x4 projectionMatrix, in Matrix4x4 relativeViewMatrix)
    {
        _frustum.Update(relativeViewMatrix * projectionMatrix);

        _visibleMeshes.Clear();
        _modelMatrices.Clear();

        var cameraOrigin = new Vector3d(Math.Floor(player.Position.X), Math.Floor(player.Position.Y), Math.Floor(player.Position.Z));

        var loadedChunks = chunkProvider.GetLoadedChunks();

        foreach (var column in loadedChunks)
        {
            var chunkPosDouble = new Vector3d(column.Position.X, 0, column.Position.Y);
            var chunkWorldPosBase = chunkPosDouble * ChunkSize;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                var mesh = column.Meshes[y];
                if (mesh == null || mesh.IndexCount == 0)
                    continue;

                int worldOffsetY = (y - VerticalChunkOffset) * ChunkSize;
                var chunkWorldPos = chunkWorldPosBase + new Vector3d(0, worldOffsetY, 0);
                var relativeChunkPosDouble = chunkWorldPos - cameraOrigin;
                var relativeChunkPos = (Vector3)relativeChunkPosDouble;

                var box = new BoundingBox(relativeChunkPos, relativeChunkPos + new Vector3(ChunkSize));
                if (!_frustum.Intersects(box))
                    continue;

                if (_visibleMeshes.Count >= MaxVisibleSections)
                    break;

                _visibleMeshes.Add(mesh);
                _modelMatrices.Add(Matrix4x4.CreateTranslation(relativeChunkPos));
            }
        }

        Result.Meshes = _visibleMeshes;
        Result.ModelMatrices = _modelMatrices;
    }
}