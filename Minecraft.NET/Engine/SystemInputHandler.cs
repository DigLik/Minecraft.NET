using Minecraft.NET.Character;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public class SystemInputHandler(IWindow window, GameModeManager gameModeManager, IMouse mouse)
{
    private readonly IWindow _window = window;

    public bool IsMouseCaptured { get; private set; }

    public void HandleKeyDown(Key key)
    {
        if (key == Key.Escape)
        {
            _window.Close();
        }
        else if (key == Key.F1)
        {
            gameModeManager.ToggleGameMode();
        }
        else if (key == Key.Tab)
        {
            IsMouseCaptured = !IsMouseCaptured;
            mouse.Cursor.CursorMode = IsMouseCaptured ? CursorMode.Disabled : CursorMode.Normal;

            if (IsMouseCaptured)
            {
                var center = new Vector2(_window.Size.X / 2f, _window.Size.Y / 2f);
                mouse.Position = center;
            }
        }
    }
}