namespace Minecraft.NET.Abstractions;

public interface IPhysicsStrategy
{
    void Update(IPlayer player, IWorld world, double deltaTime);
}