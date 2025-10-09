using Minecraft.NET.Character;
using Minecraft.NET.Core.Environment;

namespace Minecraft.NET.Services.Physics;

public interface IPhysicsStrategy
{
    void Update(Player player, World world, double deltaTime);
}