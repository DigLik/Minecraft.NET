using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Environment;

public sealed class World(WorldStorage storage) : IDisposable
{
    public void OnLoad()
    {
        BlockRegistry.Initialize();
        storage.OnLoad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3<int> position, BlockId id)
        => storage.RecordModification(position, id);

    internal BlockId GetBlock(Vector3<float> blockPos)
        => throw new NotImplementedException();

    public void Dispose() => storage.OnClose();
}