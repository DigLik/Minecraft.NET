using Minecraft.NET.Character;

namespace Minecraft.NET.Services;

public class PhysicsService(Player player, GameModeManager gameModeManager)
{
    public void OnUpdate(double deltaTime)
        => gameModeManager.CurrentPhysicsStrategy.Update(player, gameModeManager.World, deltaTime);
}