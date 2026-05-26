using MinecraftPT.Game.World.Blocks;
using MinecraftPT.Utils.Math;

namespace MinecraftPT.Game.Events;

public readonly record struct BlockChangedEvent(
    Vector3Int GlobalPosition,
    BlockId OldBlock,
    BlockId NewBlock
);