using Minecraft.NET.Character;
using Minecraft.NET.Graphics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public class SystemInputHandler(IWindow window, GameModeManager gameModeManager, RenderSettings renderSettings)
{
    private IMouse _mouse = null!;

    public void SetMouse(IMouse mouse) => _mouse = mouse;

    public bool IsMouseCaptured { get; private set; }

    public void HandleKeyDown(Key key)
    {
        if (_mouse == null) return;

        if (key == Key.Escape)
        {
            window.Close();
        }
        else if (key == Key.F1)
        {
            gameModeManager.ToggleGameMode();
        }
        else if (key == Key.F3)
        {
            renderSettings.IsWireframeEnabled = !renderSettings.IsWireframeEnabled;
        }
        else if (key == Key.Tab)
        {
            IsMouseCaptured = !IsMouseCaptured;
            _mouse.Cursor.CursorMode = IsMouseCaptured ? CursorMode.Disabled : CursorMode.Normal;

            if (IsMouseCaptured)
            {
                var center = new Vector2(window.Size.X / 2f, window.Size.Y / 2f);
                _mouse.Position = center;
            }
        }
    }
}