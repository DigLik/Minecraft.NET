using Minecraft.NET.Utils.Math;
using Minecraft.NET.Engine.Input;

namespace Minecraft.NET.Engine.Abstractions;

public interface IInputManager
{
    Vector2<float> MousePosition { get; }
    bool IsMouseCaptured { get; }

    void OnUpdate(double deltaTime);

    bool IsKeyPressed(Key key);
    bool IsMouseButtonPressed(MouseButton button);

    void ToggleMouseCapture();
    void CloseWindow();
}