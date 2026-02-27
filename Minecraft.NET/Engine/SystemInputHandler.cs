using Minecraft.NET.Character;
using Minecraft.NET.Engine.Abstractions;

using Silk.NET.GLFW;

namespace Minecraft.NET.Engine;

public class SystemInputHandler(
    IInputManager inputManager,
    GameModeManager gameModeManager)
{
    public void HandleKeyDown(Keys key)
    {
        if (key == Keys.Escape)
            inputManager.CloseWindow();
        else if (key == Keys.F1)
            gameModeManager.ToggleGameMode();
        else if (key == Keys.Tab)
            inputManager.ToggleMouseCapture();
    }
}