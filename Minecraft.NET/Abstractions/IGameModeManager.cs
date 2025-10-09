namespace Minecraft.NET.Abstractions;

public interface IGameModeManager
{
    IPhysicsStrategy CurrentPhysicsStrategy { get; }
    IWorld World { get; }
    void ToggleGameMode();
}