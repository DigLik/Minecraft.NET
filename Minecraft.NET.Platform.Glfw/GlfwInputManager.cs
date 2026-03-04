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

    private readonly bool[] _currentKeys = new bool[256];
    private readonly bool[] _previousKeys = new bool[256];

    private readonly bool[] _currentButtons = new bool[8];
    private readonly bool[] _previousButtons = new bool[8];

    private static readonly EngineKey[] _allKeys = Enum.GetValues<EngineKey>();
    private static readonly EngineMouseButton[] _allButtons = Enum.GetValues<EngineMouseButton>();

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

        Array.Copy(_currentKeys, _previousKeys, _currentKeys.Length);
        foreach (var key in _allKeys)
        {
            var glfwKey = MapKey(key);
            if (glfwKey != Keys.Unknown)
                _currentKeys[(int)key] = _glfw.GetKey(_windowHandle, glfwKey) == (int)InputAction.Press;
        }

        Array.Copy(_currentButtons, _previousButtons, _currentButtons.Length);
        foreach (var button in _allButtons)
        {
            var glfwButton = MapMouseButton(button);
            _currentButtons[(int)button] = _glfw.GetMouseButton(_windowHandle, (int)glfwButton) == (int)InputAction.Press;
        }
    }

    public bool IsKeyDown(EngineKey key) => _currentKeys[(int)key] && !_previousKeys[(int)key];
    public bool IsKey(EngineKey key) => _currentKeys[(int)key];
    public bool IsKeyUp(EngineKey key) => !_currentKeys[(int)key] && _previousKeys[(int)key];

    public bool IsMouseButtonDown(EngineMouseButton button) => _currentButtons[(int)button] && !_previousButtons[(int)button];
    public bool IsMouseButton(EngineMouseButton button) => _currentButtons[(int)button];
    public bool IsMouseButtonUp(EngineMouseButton button) => !_currentButtons[(int)button] && _previousButtons[(int)button];

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
        EngineKey.ShiftLeft => Keys.ShiftLeft,
        EngineKey.ControlLeft => Keys.ControlLeft,
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