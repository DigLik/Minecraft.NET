using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Abstractions;

public interface IChunkProvider
{
    IReadOnlyCollection<ChunkColumn> GetLoadedChunks();
}