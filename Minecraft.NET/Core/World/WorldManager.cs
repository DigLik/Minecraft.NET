using Minecraft.NET.Core.Abstractions;

namespace Minecraft.NET.Core.World;

public class WorldManager(IWorldGenerator worldGenerator) : IWorldManager
{
    private readonly IWorldGenerator _worldGenerator = worldGenerator;
    private readonly Dictionary<Vector2, ChunkColumn> _chunkColumns = [];
    private readonly Lock _chunkColumnsLock = new();

    public ChunkColumn? GetChunkColumn(Vector2 position)
    {
        lock (_chunkColumnsLock)
        {
            if (_chunkColumns.TryGetValue(position, out var chunkColumn))
                return chunkColumn;

            chunkColumn = _worldGenerator.GenerateChunkColumn(position);
            _chunkColumns.Add(position, chunkColumn);
            return chunkColumn;
        }
    }
}