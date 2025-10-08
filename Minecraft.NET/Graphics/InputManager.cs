using Minecraft.NET.Core;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Graphics;

public class InputManager(IWindow window, Camera camera, World world)
{
    private readonly IWindow _window = window;
    private IKeyboard _keyboard = null!;
    private IMouse _mouse = null!;

    private bool _isMouseCaptured = false;
    private Vector2 _lastMousePosition;

    public void Initialize()
    {
        var input = _window.CreateInput();
        _keyboard = input.Keyboards[0];
        _mouse = input.Mice[0];

        _mouse.Cursor.CursorMode = CursorMode.Normal;
        _mouse.MouseMove += OnMouseMove;
        _keyboard.KeyDown += OnKeyDown;
        _mouse.MouseDown += OnMouseDown;
    }

    public void Update(float dt)
    {
        float cameraSpeed = 50.0f;

        if (_keyboard.IsKeyPressed(Key.ControlLeft)) cameraSpeed *= 2;

        if (_keyboard.IsKeyPressed(Key.W)) camera.Position += camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.S)) camera.Position -= camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.A)) camera.Position -= camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.D)) camera.Position += camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.Space)) camera.Position += Vector3.UnitY * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.ShiftLeft)) camera.Position -= Vector3.UnitY * cameraSpeed * dt;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_isMouseCaptured) return;

        if (button == MouseButton.Left)
        {
            world.BreakBlock(camera.Position, camera.Front);
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_isMouseCaptured)
        {
            _lastMousePosition = position;
            return;
        }

        const float sensitivity = 0.1f;
        var offset = new Vector2(position.X - _lastMousePosition.X, _lastMousePosition.Y - position.Y);
        _lastMousePosition = position;

        offset *= sensitivity;

        camera.Yaw += offset.X;
        camera.Pitch += offset.Y;
        camera.UpdateVectors();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int _)
    {
        if (key == Key.Escape) _window.Close();
        else if (key == Key.Tab)
        {
            _isMouseCaptured = !_isMouseCaptured;
            _mouse.Cursor.CursorMode = _isMouseCaptured ? CursorMode.Disabled : CursorMode.Normal;

            if (_isMouseCaptured)
            {
                _lastMousePosition = new Vector2(_window.Size.X / 2f, _window.Size.Y / 2f);
                _mouse.Position = _lastMousePosition;
            }
        }
    }
}