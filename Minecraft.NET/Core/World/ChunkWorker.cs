using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Graphics.Meshes;
using System.Collections.Concurrent;

namespace Minecraft.NET.Core.World;

public class ChunkWorker(
    IWorldManager worldManager,
    GameSettings gameSettings,
    ConcurrentQueue<Vector2> buildQueue,
    ConcurrentQueue<ChunkMeshData> resultQueue,
    CancellationToken cancellationToken)
{
    private readonly IWorldManager _worldManager = worldManager;
    private readonly GameSettings _gameSettings = gameSettings;

    public void Run()
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (buildQueue.TryDequeue(out var position))
            {
                var column = _worldManager.GetChunkColumn(position);
                if (column != null)
                {
                    var meshData = ChunkMesher.GenerateMesh(_worldManager, column, position, _gameSettings);
                    resultQueue.Enqueue(meshData);
                }
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }
}