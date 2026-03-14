using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Serialization;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public class WorldStorage : IAsyncDisposable
{
    private readonly string _worldDirectory;
    private readonly Dictionary<Vector3Int, Bucket> _buckets = [];
    private readonly Lock _bucketsLock = new();

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

    private Bucket GetOrLoadBucket(Vector3Int bucketPos)
    {
        lock (_bucketsLock)
        {
            if (!_buckets.TryGetValue(bucketPos, out var bucket))
            {
                bucket = new Bucket(_worldDirectory, bucketPos);
                _buckets[bucketPos] = bucket;
            }
            return bucket;
        }
    }

    public async Task SaveAllAsync()
    {
        List<Bucket> bucketsToSave;
        lock (_bucketsLock)
        {
            bucketsToSave = [.. _buckets.Values];
        }

        await Task.Factory.StartNew(static state =>
        {
            var buckets = (List<Bucket>)state!;
            foreach (var bucket in buckets)
                if (bucket.IsDirty)
                    bucket.Save();
        }, bucketsToSave, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
    }

    private static Vector3Int GetBucketPosition(Vector3Int chunkPos) => new(
        chunkPos.X >> 4,
        chunkPos.Y >> 4,
        chunkPos.Z >> 4
    );

    private static Vector3Int GetLocalChunkPosition(Vector3Int chunkPos) => new(
        chunkPos.X & 15,
        chunkPos.Y & 15,
        chunkPos.Z & 15
    );

    public async ValueTask DisposeAsync() => await SaveAllAsync();
}