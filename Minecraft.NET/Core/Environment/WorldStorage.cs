using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class WorldStorage(string worldName) : IDisposable
{
    private readonly string _savePath = Path.Combine(AppContext.BaseDirectory, "saves", worldName, "chunks.bin");
    private readonly Dictionary<Vector2D<int>, Dictionary<int, BlockId>> _modifications = [];
    private readonly Lock _lock = new();

    private static int GetIndex(int x, int y, int z) => x + z * ChunkSize + y * ChunkSize * ChunkSize;

    public void OnLoad()
    {
        if (!File.Exists(_savePath)) return;

        try
        {
            using var fs = File.OpenRead(_savePath);
            using var reader = new BinaryReader(fs);

            int chunkCount = reader.ReadInt32();
            lock (_lock)
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    int cx = reader.ReadInt32();
                    int cy = reader.ReadInt32();
                    var chunkPos = new Vector2D<int>(cx, cy);

                    int modCount = reader.ReadInt32();
                    var mods = new Dictionary<int, BlockId>(modCount);

                    for (int j = 0; j < modCount; j++)
                    {
                        int index = reader.ReadInt32();
                        BlockId id = (BlockId)reader.ReadByte();
                        mods[index] = id;
                    }
                    _modifications[chunkPos] = mods;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading world: {ex.Message}");
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_savePath);
                if (dir != null) Directory.CreateDirectory(dir);

                using var fs = File.Create(_savePath);
                using var writer = new BinaryWriter(fs);

                writer.Write(_modifications.Count);
                foreach (var chunkKvp in _modifications)
                {
                    writer.Write(chunkKvp.Key.X);
                    writer.Write(chunkKvp.Key.Y);

                    writer.Write(chunkKvp.Value.Count);
                    foreach (var mod in chunkKvp.Value)
                    {
                        writer.Write(mod.Key);
                        writer.Write((byte)mod.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving world: {ex.Message}");
            }
        }
    }

    public void ApplyModificationsToChunk(ChunkColumn column)
    {
        Dictionary<int, BlockId>? mods;
        lock (_lock)
        {
            _modifications.TryGetValue(column.Position, out mods);
        }

        if (mods != null)
        {
            foreach (var (index, blockId) in mods)
            {
                int y = index / (ChunkSize * ChunkSize);
                int rem = index % (ChunkSize * ChunkSize);
                int z = rem / ChunkSize;
                int x = rem % ChunkSize;
                column.SetBlock(x, y, z, blockId);
            }
        }
    }

    public void RecordModification(Vector2D<int> chunkPos, int x, int y, int z, BlockId blockId)
    {
        lock (_lock)
        {
            if (!_modifications.TryGetValue(chunkPos, out var chunkMods))
            {
                chunkMods = [];
                _modifications[chunkPos] = chunkMods;
            }
            chunkMods[GetIndex(x, y, z)] = blockId;
        }
    }

    public void OnClose() => Save();
    public void Dispose() => OnClose();
}