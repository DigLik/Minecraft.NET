using Minecraft.NET.Character;
using Minecraft.NET.Core.Environment;

namespace Minecraft.NET.Services.Physics;

public class SpectatorPhysicsStrategy : IPhysicsStrategy
{
    public void Update(Player player, World world, double deltaTime)
    {
        player.Position += player.Velocity * deltaTime;
    }
}