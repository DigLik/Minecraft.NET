using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class WorldStorage(string worldName) : IDisposable
{
    private readonly string _savePath = Path.Combine(AppContext.BaseDirectory, "saves", worldName, "chunks.bin");
    private readonly Dictionary<Vector2<int>, Dictionary<int, BlockId>> _modifications = [];
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
                    var chunkPos = new Vector2<int>(cx, cy);

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

    public void ApplyModificationsToChunk(ChunkSection column)
    {
    }

    public void RecordModification(Vector3<int> position, BlockId blockId)
    {
    }

    public void OnClose() => Save();
    public void Dispose() => OnClose();
}