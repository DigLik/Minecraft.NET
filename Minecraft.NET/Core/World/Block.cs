namespace Minecraft.NET.Core.World;

public readonly record struct ModelID(ushort ID);

public readonly record struct Block(ushort ID, ModelID Model);
