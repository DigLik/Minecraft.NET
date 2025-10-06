using Minecraft.NET.Core.World;
using System.Numerics;

namespace Minecraft.NET.Core.Abstractions;

public interface IWorldManager
{
    ChunkColumn? GetChunkColumn(Vector2 position);
}