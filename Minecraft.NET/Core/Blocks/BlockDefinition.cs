namespace Minecraft.NET.Core.Blocks;

public readonly record struct BlockFaceTextures(Vector2 Top, Vector2 Bottom, Vector2 Side);

public readonly record struct BlockDefinition(ushort ID, string Name, BlockFaceTextures Textures);