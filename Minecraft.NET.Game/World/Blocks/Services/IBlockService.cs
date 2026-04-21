namespace Minecraft.NET.Game.World.Blocks.Services;

public interface IBlockService
{
    ReadOnlySpan<BlockDefinition> GetDefinitionsFast();
    ref readonly BlockDefinition GetBlock(BlockId id);
    void SetBlockFaceTexture(BlockId id, BlockFace face, int textureIndex);
}