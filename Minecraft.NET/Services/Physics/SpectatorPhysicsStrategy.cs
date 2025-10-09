using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Services.Physics;

public class SpectatorPhysicsStrategy : IPhysicsStrategy
{
    public void Update(IPlayer player, IWorld world, double deltaTime)
    {
        player.Position += player.Velocity * deltaTime;
    }
}