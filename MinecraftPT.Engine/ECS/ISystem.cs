using MinecraftPT.Engine.Core;

namespace MinecraftPT.Engine.ECS;

public interface ISystem
{
    void Update(Registry registry, in Time time);
}