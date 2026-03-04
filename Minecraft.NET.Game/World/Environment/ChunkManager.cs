using HighPerformanceBus;

using Minecraft.NET.Game.Events;
using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public class ChunkManager(WorldStorage storage, IWorldGenerator generator) : IDisposable
{
    private readonly ToroidalChunkVolume _volume = new(RenderDistance, WorldHeightInChunks);

    public BlockId GetBlock(Vector3<int> globalPos)
    {
        var chunkPos = GetChunkPosition(globalPos);

        if (_volume.TryGetChunk(chunkPos, out var chunk))
        {
            var localPos = GetLocalPosition(globalPos);
            return chunk.GetBlock(localPos);
        }

        return BlockId.Air;
    }

    public void SetBlock(Vector3<int> globalPos, BlockId id)
    {
        var chunkPos = GetChunkPosition(globalPos);

        _volume.UpdateChunk(chunkPos, (ref chunk) =>
        {
            var localPos = GetLocalPosition(globalPos);
            BlockId oldId = chunk.GetBlock(localPos);

            if (oldId == id) return;

            chunk.SetBlock(localPos, id);

            storage.SaveChunk(ref chunk);
            EventBus.Publish(new BlockChangedEvent(globalPos, oldId, id));
        });
    }

    public bool TryGetChunk(Vector3<int> chunkPos, out ChunkSection chunk)
        => _volume.TryGetChunk(chunkPos, out chunk);

    public void LoadChunk(Vector3<int> chunkPos)
    {
        if (_volume.TryGetChunk(chunkPos, out _)) return;

        var chunk = new ChunkSection { Position = chunkPos };

        if (!storage.TryLoadChunk(ref chunk))
        {
            generator.Generate(ref chunk);
            chunk.IsModified = false;
        }

        var evictedChunk = _volume.SetChunk(chunkPos, chunk);

        if (evictedChunk.HasValue)
        {
            var oldChunk = evictedChunk.Value;
            storage.SaveChunk(ref oldChunk);
            oldChunk.Free();
        }
    }

    public void UnloadChunk(Vector3<int> chunkPos)
    {
        _volume.RemoveChunk(chunkPos, out var chunk);

        if (chunk.IsAllocated || chunk.UniformId != BlockId.Air)
        {
            storage.SaveChunk(ref chunk);
            chunk.Free();
        }
    }

    private static Vector3<int> GetChunkPosition(Vector3<int> globalPos) => new(
        globalPos.X >> 4,
        globalPos.Y >> 4,
        globalPos.Z >> 4
    );

    private static Vector3<int> GetLocalPosition(Vector3<int> globalPos) => new(
        globalPos.X & 15,
        globalPos.Y & 15,
        globalPos.Z & 15
    );

    public void Dispose()
    {
        foreach (var chunk in _volume.GetAllValidChunks())
        {
            var mutableChunk = chunk;
            storage.SaveChunk(ref mutableChunk);
            mutableChunk.Free();
        }
    }
}