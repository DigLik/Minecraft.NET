using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Services;

public class PhysicsService(IPlayer player, IGameModeManager gameModeManager) : IPhysicsService
{
    public void OnUpdate(double deltaTime)
    {
        gameModeManager.CurrentPhysicsStrategy.Update(player, gameModeManager.World, deltaTime);
    }
}