using Minecraft.NET.Core.Abstractions;
using System.Collections.Concurrent;

namespace Minecraft.NET.Core.World;

public class WorldManager(IWorldGenerator worldGenerator) : IWorldManager
{
    private readonly IWorldGenerator _worldGenerator = worldGenerator;
    private readonly ConcurrentDictionary<Vector2, ChunkColumn> _chunkColumns = [];
    private readonly ConcurrentDictionary<Vector2, Task<ChunkColumn>> _generationTasks = [];

    public ChunkColumn? GetChunkColumn(Vector2 position)
    {
        _chunkColumns.TryGetValue(position, out var chunkColumn);
        return chunkColumn;
    }

    public Task<ChunkColumn?> RequestChunkColumnAsync(Vector2 position)
    {
        var task = _generationTasks.GetOrAdd(position, pos => Task.Run(() =>
        {
            var column = _worldGenerator.GenerateChunkColumn(pos);
            _chunkColumns.TryAdd(pos, column);
            return column;
        }));

        return task!;
    }

    public void UnloadChunkColumn(Vector2 position)
    {
        _chunkColumns.TryRemove(position, out _);
        _generationTasks.TryRemove(position, out _);
    }
}