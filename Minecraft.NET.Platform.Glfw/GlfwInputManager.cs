using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Utils.Math;

using Silk.NET.GLFW;

using EngineKey = Minecraft.NET.Engine.Input.Key;
using EngineMouseButton = Minecraft.NET.Engine.Input.MouseButton;
using GlfwApi = Silk.NET.GLFW.Glfw;

namespace Minecraft.NET.Platform.Glfw;

public unsafe class GlfwInputManager : IInputManager
{
    private readonly GlfwApi _glfw;
    private readonly WindowHandle* _windowHandle;

    public Vector2<float> MousePosition { get; private set; }
    public bool IsMouseCaptured { get; private set; }

    public GlfwInputManager(IWindow window)
    {
        _glfw = GlfwApi.GetApi();
        _windowHandle = (WindowHandle*)window.Handle;
        SetMouseCapture(false);
    }

    public void OnUpdate(double deltaTime)
    {
        if (_windowHandle == null) return;

        _glfw.GetCursorPos(_windowHandle, out double mx, out double my);
        MousePosition = new Vector2<float>((float)mx, (float)my);
    }

    public bool IsKeyPressed(EngineKey key)
    {
        var glfwKey = MapKey(key);
        if (glfwKey == Keys.Unknown) return false;

        var state = _glfw.GetKey(_windowHandle, glfwKey);
        return state is ((int)InputAction.Press) or ((int)InputAction.Repeat);
    }

    public bool IsMouseButtonPressed(EngineMouseButton button)
    {
        var glfwButton = MapMouseButton(button);
        return _glfw.GetMouseButton(_windowHandle, (int)glfwButton) == (int)InputAction.Press;
    }

    public void ToggleMouseCapture() => SetMouseCapture(!IsMouseCaptured);

    public void CloseWindow() => _glfw.SetWindowShouldClose(_windowHandle, true);

    private void SetMouseCapture(bool captured)
    {
        IsMouseCaptured = captured;
        _glfw.SetInputMode(_windowHandle, CursorStateAttribute.Cursor,
            captured ? CursorModeValue.CursorDisabled : CursorModeValue.CursorNormal);
    }

    private static Keys MapKey(EngineKey key) => key switch
    {
        EngineKey.Space => Keys.Space,
        EngineKey.Escape => Keys.Escape,
        EngineKey.Enter => Keys.Enter,
        EngineKey.Tab => Keys.Tab,
        EngineKey.W => Keys.W,
        EngineKey.A => Keys.A,
        EngineKey.S => Keys.S,
        EngineKey.D => Keys.D,
        EngineKey.F1 => Keys.F1,
        _ => Keys.Unknown
    };

    private static MouseButton MapMouseButton(EngineMouseButton button) => button switch
    {
        EngineMouseButton.Left => MouseButton.Left,
        EngineMouseButton.Right => MouseButton.Right,
        EngineMouseButton.Middle => MouseButton.Middle,
        _ => MouseButton.Left
    };
}