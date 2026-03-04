using Minecraft.NET.Engine.Input;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IInputManager
{
    Vector2<float> MousePosition { get; }
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