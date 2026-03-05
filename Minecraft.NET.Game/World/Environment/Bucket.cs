using System.Buffers;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public readonly record struct PooledChunkData(byte[] Buffer, int Length);

public class Bucket
{
    private readonly string _filePath;
    private readonly Lock _ioLock = new();
    private readonly Lock _dataLock = new();

    private readonly Dictionary<Vector3<int>, PooledChunkData> _chunkData = [];

    public bool IsDirty { get; private set; }

    public Bucket(string directory, Vector3<int> bucketPos)
    {
        _filePath = Path.Combine(directory, $"bucket.{bucketPos.X}.{bucketPos.Y}.{bucketPos.Z}.bin");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        lock (_ioLock)
        {
            try
            {
                using var fs = File.OpenRead(_filePath);
                using var reader = new BinaryReader(fs);

                int chunkCount = reader.ReadInt32();
                lock (_dataLock)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        int z = reader.ReadInt32();

                        int dataLength = reader.ReadInt32();
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(dataLength);
                        reader.Read(buffer, 0, dataLength);
                        _chunkData[new Vector3<int>(x, y, z)] = new PooledChunkData(buffer, dataLength);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading bucket {_filePath}: {ex.Message}");
            }
        }
    }

    public void Save()
    {
        if (!IsDirty) return;
        lock (_ioLock)
        {
            try
            {
                using var fs = File.Create(_filePath);
                using var writer = new BinaryWriter(fs);

                lock (_dataLock)
                {
                    writer.Write(_chunkData.Count);
                    foreach (var kvp in _chunkData)
                    {
                        writer.Write(kvp.Key.X);
                        writer.Write(kvp.Key.Y);
                        writer.Write(kvp.Key.Z);

                        writer.Write(kvp.Value.Length);
                        writer.Write(kvp.Value.Buffer, 0, kvp.Value.Length);
                    }
                    IsDirty = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving bucket {_filePath}: {ex.Message}");
            }
        }
    }

    public ReadOnlySpan<byte> GetChunkData(Vector3<int> localChunkPos)
    {
        lock (_dataLock)
        {
            if (_chunkData.TryGetValue(localChunkPos, out var data))
                return new ReadOnlySpan<byte>(data.Buffer, 0, data.Length);
            return default;
        }
    }

    public void SetChunkData(Vector3<int> localChunkPos, PooledChunkData data)
    {
        lock (_dataLock)
        {
            if (_chunkData.TryGetValue(localChunkPos, out var oldData))
                ArrayPool<byte>.Shared.Return(oldData.Buffer);

            _chunkData[localChunkPos] = data;
            IsDirty = true;
        }
    }

    public void FreeMemory()
    {
        lock (_dataLock)
        {
            foreach (var kvp in _chunkData)
                ArrayPool<byte>.Shared.Return(kvp.Value.Buffer);
            _chunkData.Clear();
        }
    }
}