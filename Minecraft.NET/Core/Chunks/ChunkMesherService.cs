using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics.Models;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Services;
using System.Collections.Concurrent;

namespace Minecraft.NET.Core.Chunks;

public class ChunkMesherService(GL gl) : IDisposable
{
    private World _world = null!;
    private ChunkManager _chunkManager = null!;
    private RenderPipeline? _resourceProvider;

    private readonly ConcurrentQueue<(Vector2D<int> position, int sectionY)> _chunksToMesh = new();
    private readonly ConcurrentQueue<(ChunkColumn column, int sectionY, MeshData meshData)> _generatedMeshes = new();

    private Task _mesherTask = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    private bool _isDisposed = false;

    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    public void SetDependencies(World world, ChunkManager chunkManager)
    {
        _world = world;
        _chunkManager = chunkManager;
    }

    public void SetRenderPipeline(RenderPipeline renderPipeline)
    {
        _resourceProvider = renderPipeline;
    }

    public void OnLoad()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _mesherTask = Task.Run(() => MesherLoop(_cancellationTokenSource.Token));
    }

    private void MesherLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_chunksToMesh.TryDequeue(out var item))
            {
                if (token.IsCancellationRequested) break;

                var column = _chunkManager?.GetColumn(item.position);

                if (column is null || column.SectionStates[item.sectionY] != ChunkSectionState.Meshing)
                    continue;

                RemeshChunkSection(column, item.sectionY, token);
            }
            else
            {
                try { Task.Delay(10, token).Wait(token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public void OnUpdate(double _)
    {
        if (_resourceProvider is null)
            return;

        while (_generatedMeshes.TryDequeue(out var result))
        {
            var (column, sectionY, meshData) = result;

            if (_chunkManager.GetColumn(column.Position) is null || column.SectionStates[sectionY] != ChunkSectionState.Meshing)
            {
                meshData.Dispose();
                continue;
            }

            Mesh? newMesh = null;
            if (meshData.IndexCount > 0)
            {
                newMesh = new Mesh(meshData);
                newMesh.UploadToGpu(gl, _resourceProvider.InstanceVbo);
            }
            else
            {
                meshData.Dispose();
            }

            var oldMesh = column.Meshes[sectionY];
            column.Meshes[sectionY] = newMesh;
            oldMesh?.Dispose();

            column.SectionStates[sectionY] = ChunkSectionState.Rendered;
        }
    }

    private void RemeshChunkSection(ChunkColumn column, int sectionY, CancellationToken token)
    {
        var meshData = ChunkMesher.GenerateMesh(column, sectionY, _world, token);

        if (token.IsCancellationRequested)
        {
            meshData.Dispose();
            return;
        }

        _generatedMeshes.Enqueue((column, sectionY, meshData));
    }

    public void QueueForMeshing(ChunkColumn column, int sectionY)
    {
        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh && AreNeighborsGenerated(column.Position))
        {
            column.SectionStates[sectionY] = ChunkSectionState.Meshing;
            _chunksToMesh.Enqueue((column.Position, sectionY));
        }
    }

    private bool AreNeighborsGenerated(Vector2D<int> chunkPos)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighborPos = chunkPos + offset;
            var neighbor = _chunkManager.GetColumn(neighborPos);
            if (neighbor is null || !neighbor.IsGenerated)
                return false;
        }
        return true;
    }

    public void OnClose()
    {
        if (_isDisposed) return;
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cancellationTokenSource?.Cancel();
        _mesherTask?.Wait();
        _cancellationTokenSource?.Dispose();

        GC.SuppressFinalize(this);
    }
}