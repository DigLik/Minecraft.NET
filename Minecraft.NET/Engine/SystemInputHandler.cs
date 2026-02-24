using Minecraft.NET.Character;
using Minecraft.NET.Graphics;

namespace Minecraft.NET.Engine;

public class SystemInputHandler(
    IInputManager inputManager,
    GameModeManager gameModeManager,
    RenderSettings renderSettings)
{
    public void HandleKeyDown(Keys key)
    {
        if (key == Keys.Escape)
            inputManager.CloseWindow();
        else if (key == Keys.F1)
            gameModeManager.ToggleGameMode();
        else if (key == Keys.F3)
            renderSettings.IsWireframeEnabled = !renderSettings.IsWireframeEnabled;
        else if (key == Keys.Tab)
            inputManager.ToggleMouseCapture();
    }
}