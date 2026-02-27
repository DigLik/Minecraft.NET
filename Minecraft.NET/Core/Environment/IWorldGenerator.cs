using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public interface IWorldGenerator
{
    void Generate(ChunkSection column);
}