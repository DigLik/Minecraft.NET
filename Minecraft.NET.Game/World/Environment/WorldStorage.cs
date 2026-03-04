using System.Collections.Concurrent;

using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Serialization;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public class WorldStorage : IAsyncDisposable
{
    private readonly string _worldDirectory;
    private readonly ConcurrentDictionary<Vector3<int>, Bucket> _buckets = new();

    public WorldStorage(string worldName)
    {
        _worldDirectory = Path.Combine(AppContext.BaseDirectory, "saves", worldName, "buckets");
        Directory.CreateDirectory(_worldDirectory);
    }

    public bool TryLoadChunk(ref ChunkSection chunk)
    {
        var bucketPos = GetBucketPosition(chunk.Position);
        var bucket = GetOrLoadBucket(bucketPos);

        var localChunkPos = GetLocalChunkPosition(chunk.Position);
        ReadOnlySpan<byte> data = bucket.GetChunkData(localChunkPos);

        if (data.Length != 0)
        {
            ChunkSerializer.Deserialize(data, ref chunk);
            return true;
        }
        return false;
    }

    public void SaveChunk(ref ChunkSection chunk)
    {
        if (!chunk.IsModified) return;

        var bucketPos = GetBucketPosition(chunk.Position);
        var bucket = GetOrLoadBucket(bucketPos);

        var localChunkPos = GetLocalChunkPosition(chunk.Position);
        PooledChunkData data = ChunkSerializer.Serialize(ref chunk);

        bucket.SetChunkData(localChunkPos, data);

        chunk.IsModified = false;
    }

    private Bucket GetOrLoadBucket(Vector3<int> bucketPos)
        => _buckets.GetOrAdd(bucketPos, static (pos, dir) => new Bucket(dir, pos), _worldDirectory);

    public async Task SaveAllAsync()
    {
        await Task.Factory.StartNew(static state =>
        {
            var buckets = (ConcurrentDictionary<Vector3<int>, Bucket>)state!;
            foreach (var kvp in buckets)
                if (kvp.Value.IsDirty)
                    kvp.Value.Save();
        }, _buckets, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
    }

    private static Vector3<int> GetBucketPosition(Vector3<int> chunkPos) => new(
        chunkPos.X >> 4,
        chunkPos.Y >> 4,
        chunkPos.Z >> 4
    );

    private static Vector3<int> GetLocalChunkPosition(Vector3<int> chunkPos) => new(
        chunkPos.X & 15,
        chunkPos.Y & 15,
        chunkPos.Z & 15
    );

    public async ValueTask DisposeAsync() => await SaveAllAsync();
}