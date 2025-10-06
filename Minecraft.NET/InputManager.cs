using Minecraft.NET.Graphics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET;

public class InputManager(IWindow window, Camera camera)
{
    private readonly IWindow _window = window;
    private readonly Camera _camera = camera;
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
    }

    public void HandleInput(float dt)
    {
        const float cameraSpeed = 15.0f;

        if (_keyboard.IsKeyPressed(Key.W))
            _camera.Position += _camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.S))
            _camera.Position -= _camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.A))
            _camera.Position -= _camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.D))
            _camera.Position += _camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.Space))
            _camera.Position += Vector3.UnitY * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.ShiftLeft))
            _camera.Position -= Vector3.UnitY * cameraSpeed * dt;
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
        _lastMousePosition = new Vector2(position.X, position.Y);

        offset *= sensitivity;

        _camera.Yaw += offset.X;
        _camera.Pitch += offset.Y;

        _camera.UpdateVectors();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Escape)
        {
            _window.Close();
        }
        else if (key == Key.Tab)
        {
            _isMouseCaptured = !_isMouseCaptured;
            _mouse.Cursor.CursorMode = _isMouseCaptured ? CursorMode.Disabled : CursorMode.Normal;
        }
    }
}