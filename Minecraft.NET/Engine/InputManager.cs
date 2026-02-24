using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Graphics;
using Minecraft.NET.Services;
using Minecraft.NET.Windowing;

namespace Minecraft.NET.Engine;

public unsafe class InputManager : IInputManager
{
    private readonly Player _player;
    private readonly WorldInteractionService _worldInteraction;
    private readonly Dictionary<GameMode, IPlayerController> _controllers;
    private readonly SystemInputHandler _systemInputHandler;

    private readonly Glfw _glfw = null!;
    private readonly WindowHandle* _windowHandle;

    private readonly GlfwCallbacks.CursorPosCallback _cursorPosCallback = null!;
    private readonly GlfwCallbacks.KeyCallback _keyCallback = null!;

    private Vector2 _lastMousePosition;
    private float _breakCooldownTimer = 0.2f;
    private float _placeCooldownTimer = 0.2f;

    public Vector2 MousePosition { get; private set; }
    public bool IsMouseCaptured { get; private set; }

    public InputManager(
        Player player,
        WorldInteractionService worldInteraction,
        GameModeManager gameModeManager,
        RenderSettings renderSettings,
        IReadOnlyDictionary<GameMode, IPlayerController> controllers,
        IWindow window)
    {
        _player = player;
        _worldInteraction = worldInteraction;
        _controllers = new(controllers);
        _systemInputHandler = new SystemInputHandler(this, gameModeManager, renderSettings);

        _glfw = Glfw.GetApi();
        _windowHandle = (WindowHandle*)window.Handle;

        _cursorPosCallback = OnCursorPos;
        _keyCallback = OnKey;

        _glfw.SetCursorPosCallback(_windowHandle, _cursorPosCallback);
        _glfw.SetKeyCallback(_windowHandle, _keyCallback);

        SetMouseCapture(false);
    }

    public bool IsKeyPressed(Keys key)
    {
        var state = _glfw.GetKey(_windowHandle, key);
        return state is ((int)InputAction.Press) or ((int)InputAction.Repeat);
    }

    public bool IsMouseButtonPressed(MouseButton button)
        => _glfw.GetMouseButton(_windowHandle, (int)button) == (int)InputAction.Press;

    public void ToggleMouseCapture() => SetMouseCapture(!IsMouseCaptured);
    public void CloseWindow() => _glfw.SetWindowShouldClose(_windowHandle, true);

    private void SetMouseCapture(bool captured)
    {
        IsMouseCaptured = captured;
        _glfw.SetInputMode(_windowHandle, CursorStateAttribute.Cursor,
            captured ? CursorModeValue.CursorDisabled : CursorModeValue.CursorNormal);

        if (captured)
        {
            _glfw.GetWindowSize(_windowHandle, out int w, out int h);
            _lastMousePosition = new Vector2(w / 2f, h / 2f);
            _glfw.SetCursorPos(_windowHandle, _lastMousePosition.X, _lastMousePosition.Y);
        }
    }

    public void OnUpdate(double deltaTime)
    {
        if (_windowHandle == null) return;

        _glfw.GetCursorPos(_windowHandle, out double mx, out double my);
        MousePosition = new Vector2((float)mx, (float)my);

        _breakCooldownTimer += (float)deltaTime;
        _placeCooldownTimer += (float)deltaTime;

        if (_controllers.TryGetValue(_player.CurrentGameMode, out var controller))
            controller.HandleInput(this, _player);

        if (IsMouseCaptured)
        {
            if (IsMouseButtonPressed(MouseButton.Left) && _breakCooldownTimer >= 0.2f)
            {
                _worldInteraction.BreakBlock();
                _breakCooldownTimer = 0f;
            }

            if (IsMouseButtonPressed(MouseButton.Right) && _placeCooldownTimer >= 0.2f)
            {
                _worldInteraction.PlaceBlock();
                _placeCooldownTimer = 0f;
            }
        }
    }

    private void OnCursorPos(WindowHandle* window, double xpos, double ypos)
    {
        var currentPos = new Vector2((float)xpos, (float)ypos);

        if (!IsMouseCaptured)
        {
            _lastMousePosition = currentPos;
            return;
        }

        const float sensitivity = 0.1f;
        var offset = new Vector2(currentPos.X - _lastMousePosition.X, _lastMousePosition.Y - currentPos.Y);
        _lastMousePosition = currentPos;

        offset *= sensitivity;

        _player.Camera.Yaw += offset.X;
        _player.Camera.Pitch += offset.Y;
        _player.Camera.UpdateVectors();
    }

    private void OnKey(WindowHandle* window, Keys key, int scancode, InputAction action, KeyModifiers mods)
    {
        if (action == InputAction.Press)
            _systemInputHandler.HandleKeyDown(key);
    }
}