using Silk.NET.Input;

namespace Minecraft.NET.Abstractions;

public interface IInputHandler
{
    bool IsKeyPressed(Key key);
    bool IsMouseButtonPressed(MouseButton button);
}