using Silk.NET.GLFW;

namespace Minecraft.NET.Engine.Abstractions;

public interface IInputManager
{
    Vector2 MousePosition { get; }
    bool IsMouseCaptured { get; }

    void OnUpdate(double deltaTime);

    bool IsKeyPressed(Keys key);
    bool IsMouseButtonPressed(MouseButton button);

    void ToggleMouseCapture();
    void CloseWindow();
}