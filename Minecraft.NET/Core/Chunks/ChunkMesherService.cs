using Minecraft.NET.Core.Environment;
using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Services;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Minecraft.NET.Core.Chunks;

public class ChunkMesherService(IChunkRenderer chunkRenderer) : IDisposable
{
    private World _world = null!;
    private ChunkManager _chunkManager = null!;

    private readonly Channel<(Vector2D<int> position, int sectionY)> _meshChannel =
        Channel.CreateUnbounded<(Vector2D<int>, int)>(new UnboundedChannelOptions { SingleReader = true });

    private readonly ConcurrentQueue<(ChunkColumn column, int sectionY, MeshData? meshData)> _generatedMeshes = new();
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    private static readonly Vector2D<int>[] NeighborOffsets = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    public void SetDependencies(World world, ChunkManager chunkManager)
    {
        _world = world;
        _chunkManager = chunkManager;
    }

    public void OnLoad()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => MesherLoop(_cts.Token));
    }

    private async Task MesherLoop(CancellationToken token)
    {
        try
        {
            await foreach (var (pos, sectionY) in _meshChannel.Reader.ReadAllAsync(token))
            {
                var column = _chunkManager?.GetColumn(pos);
                if (column is null || column.SectionStates[sectionY] != ChunkSectionState.Meshing)
                    continue;

                RemeshChunkSection(column, sectionY, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void OnUpdate(double _)
    {
        while (_generatedMeshes.TryDequeue(out var result))
        {
            var (column, sectionY, meshData) = result;
            if (_chunkManager.GetColumn(column.Position) is null || column.SectionStates[sectionY] != ChunkSectionState.Meshing)
            {
                meshData?.Dispose();
                continue;
            }

            var oldGeometry = column.MeshGeometries[sectionY];
            if (oldGeometry.IndexCount > 0)
            {
                chunkRenderer.FreeChunkMesh(oldGeometry);
                column.ActiveMask &= (ushort)~(1 << sectionY);
            }

            ChunkMeshGeometry newGeometry = meshData.HasValue
                ? chunkRenderer.UploadChunkMesh(meshData.Value)
                : default;

            column.MeshGeometries[sectionY] = newGeometry;
            if (newGeometry.IndexCount > 0)
                column.ActiveMask |= (ushort)(1 << sectionY);

            column.SectionStates[sectionY] = ChunkSectionState.Rendered;
            column.Version++;
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
        _generatedMeshes.Enqueue((column, sectionY, meshData.IndexCount > 0 ? meshData : null));
    }

    public void QueueForMeshing(ChunkColumn column, int sectionY)
    {
        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh && AreNeighborsGenerated(column.Position))
        {
            column.SectionStates[sectionY] = ChunkSectionState.Meshing;
            _meshChannel.Writer.TryWrite((column.Position, sectionY));
        }
    }

    private bool AreNeighborsGenerated(Vector2D<int> chunkPos)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighbor = _chunkManager.GetColumn(chunkPos + offset);
            if (neighbor is null || !neighbor.IsGenerated)
                return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}