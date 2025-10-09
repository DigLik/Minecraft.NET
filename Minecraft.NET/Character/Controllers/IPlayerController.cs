using Minecraft.NET.Engine;

namespace Minecraft.NET.Character.Controllers;

public interface IPlayerController
{
    void HandleInput(InputManager inputHandler, Player player);
}