using Minecraft.NET.Core.World;

namespace Minecraft.NET.Core.Abstractions;

public interface IWorldGenerator
{
    ChunkColumn GenerateChunkColumn(Vector2 position);
}