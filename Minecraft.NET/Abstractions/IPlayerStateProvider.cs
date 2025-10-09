using Minecraft.NET.Core.Common;

namespace Minecraft.NET.Abstractions;

public interface IPlayerStateProvider
{
    Vector3d Position { get; }
}