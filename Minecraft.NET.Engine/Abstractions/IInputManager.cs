using System.Numerics;

using Minecraft.NET.Engine.Input;

namespace Minecraft.NET.Engine.Abstractions;

public interface IInputManager
{
    Vector2 MousePosition { get; }
    bool IsMouseCaptured { get; }

    void OnUpdate(double deltaTime);

    bool IsKeyDown(Key key);
    bool IsKey(Key key);
    bool IsKeyUp(Key key);

    bool IsMouseButtonDown(MouseButton button);
    bool IsMouseButton(MouseButton button);
    bool IsMouseButtonUp(MouseButton button);

    void ToggleMouseCapture();
    void CloseWindow();
}