using Minecraft.NET.Core;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Graphics;

public class InputManager(Game game, IWindow window, Camera camera, World world)
{
    private readonly Game _game = game;
    private readonly IWindow _window = window;
    private readonly Camera _camera = camera;
    private readonly World _world = world;

    private IKeyboard _keyboard = null!;
    private IMouse _mouse = null!;

    private bool _isMouseCaptured = false;
    private Vector2 _lastMousePosition;

    private const float BreakBlockCooldown = 0.25f;
    private const float PlaceBlockCooldown = 0.2f;
    private float _breakCooldownTimer = BreakBlockCooldown;
    private float _placeCooldownTimer = PlaceBlockCooldown;

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
        _breakCooldownTimer += dt;
        _placeCooldownTimer += dt;

        if (_game.CurrentGameMode == GameMode.Spectator)
            HandleSpectatorMovement(dt);
        else
            HandleCreativeMovement();

        _world.UpdatePlayerPosition(_game, _camera, dt);

        if (_isMouseCaptured)
        {
            if (_mouse.IsButtonPressed(MouseButton.Left) && _breakCooldownTimer >= BreakBlockCooldown)
            {
                world.BreakBlock(camera.Position, camera.Front);
                _breakCooldownTimer = 0f;
            }

            if (_mouse.IsButtonPressed(MouseButton.Right) && _placeCooldownTimer >= PlaceBlockCooldown)
            {
                world.PlaceBlock(camera);
                _placeCooldownTimer = 0f;
            }
        }
    }

    private void HandleSpectatorMovement(float dt)
    {
        double cameraSpeed = 50.0;

        if (_keyboard.IsKeyPressed(Key.ControlLeft)) cameraSpeed *= 2;

        var moveDir = Vector3d.Zero;
        if (_keyboard.IsKeyPressed(Key.W)) moveDir += (Vector3d)_camera.Front;
        if (_keyboard.IsKeyPressed(Key.S)) moveDir -= (Vector3d)_camera.Front;
        if (_keyboard.IsKeyPressed(Key.A)) moveDir -= (Vector3d)_camera.Right;
        if (_keyboard.IsKeyPressed(Key.D)) moveDir += (Vector3d)_camera.Right;
        if (_keyboard.IsKeyPressed(Key.Space)) moveDir += Vector3d.UnitY;
        if (_keyboard.IsKeyPressed(Key.ShiftLeft)) moveDir -= Vector3d.UnitY;

        if (moveDir.LengthSquared() > 0)
        {
            _camera.Position += Vector3d.Normalize(moveDir) * cameraSpeed * dt;
        }
    }

    private void HandleCreativeMovement()
    {
        var wishDir = Vector3d.Zero;
        var forward = Vector3d.Normalize(new Vector3d(_camera.Front.X, 0, _camera.Front.Z));
        var right = Vector3d.Normalize(new Vector3d(_camera.Right.X, 0, _camera.Right.Z));

        if (_keyboard.IsKeyPressed(Key.W)) wishDir += forward;
        if (_keyboard.IsKeyPressed(Key.S)) wishDir -= forward;
        if (_keyboard.IsKeyPressed(Key.A)) wishDir -= right;
        if (_keyboard.IsKeyPressed(Key.D)) wishDir += right;

        if (wishDir.LengthSquared() > 0)
            wishDir = Vector3d.Normalize(wishDir);

        var currentYVelocity = _camera.Velocity.Y;
        var horizontalVelocity = wishDir * MaxSpeed;

        _camera.Velocity = new Vector3d(horizontalVelocity.X, currentYVelocity, horizontalVelocity.Z);

        if (_keyboard.IsKeyPressed(Key.Space) && _camera.IsOnGround)
            _camera.Velocity = _camera.Velocity with { Y = JumpForce };
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
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
        else if (key == Key.F1)
        {
            _game.ToggleGameMode();
        }
    }
}