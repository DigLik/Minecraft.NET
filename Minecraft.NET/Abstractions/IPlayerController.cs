using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Player.Controllers;

public interface IPlayerController
{
    void HandleInput(IInputHandler inputHandler, IPlayer player);
}