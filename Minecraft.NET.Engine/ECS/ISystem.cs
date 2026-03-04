using Minecraft.NET.Engine.Core;

namespace Minecraft.NET.Engine.ECS;

public interface ISystem
{
    void Update(Registry registry, in Time time);
}