using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Common;

namespace Minecraft.NET.Abstractions;

public interface IWorld : ILifecycleHandler
{
    BlockId GetBlock(Vector3d worldPosition);
    void SetBlock(Vector3d worldPosition, BlockId id);
    ChunkColumn? GetColumn(Vector2D<int> position);
}