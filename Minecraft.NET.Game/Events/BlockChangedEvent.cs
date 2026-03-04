using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Events;

public readonly record struct BlockChangedEvent(
    Vector3<int> GlobalPosition,
    BlockId OldBlock,
    BlockId NewBlock
);