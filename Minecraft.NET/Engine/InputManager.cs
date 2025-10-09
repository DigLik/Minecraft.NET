using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Services;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public class InputManager
{
    private readonly Player _player;
    private readonly WorldInteractionService _worldInteractionService;
    private readonly Dictionary<GameMode, IPlayerController> _controllers;
    private readonly SystemInputHandler _systemInputHandler;

    private readonly IKeyboard _keyboard = null!;
    private readonly IMouse _mouse = null!;
    private Vector2 _lastMousePosition;

    private const float BreakBlockCooldown = 0.25f;
    private const float PlaceBlockCooldown = 0.2f;
    private float _breakCooldownTimer = BreakBlockCooldown;
    private float _placeCooldownTimer = PlaceBlockCooldown;

    public InputManager(
        IWindow window,
        Player player,
        WorldInteractionService worldInteraction,
        GameModeManager gameModeManager,
        CreativePlayerController creative,
        SpectatorPlayerController spectator
    )
    {
        _player = player;
        _worldInteractionService = worldInteraction;
        _controllers = new Dictionary<GameMode, IPlayerController>
        {
            { GameMode.Creative, creative },
            { GameMode.Spectator, spectator }
        };

        var input = window.CreateInput();
        _keyboard = input.Keyboards[0];
        _mouse = input.Mice[0];

        _systemInputHandler = new SystemInputHandler(window, gameModeManager, _mouse);
    }

    public bool IsKeyPressed(Key key) => _keyboard.IsKeyPressed(key);
    public bool IsMouseButtonPressed(MouseButton button) => _mouse.IsButtonPressed(button);

    public void OnLoad()
    {
        _mouse.Cursor.CursorMode = CursorMode.Normal;
        _mouse.MouseMove += OnMouseMove;
        _keyboard.KeyDown += OnKeyDown;
    }

    public void OnUpdate(double deltaTime)
    {
        _breakCooldownTimer += (float)deltaTime;
        _placeCooldownTimer += (float)deltaTime;

        if (_controllers.TryGetValue(_player.CurrentGameMode, out var controller))
        {
            controller.HandleInput(this, _player);
        }

        if (_systemInputHandler.IsMouseCaptured)
        {
            if (IsMouseButtonPressed(MouseButton.Left) && _breakCooldownTimer >= BreakBlockCooldown)
            {
                _worldInteractionService.BreakBlock();
                _breakCooldownTimer = 0f;
            }

            if (IsMouseButtonPressed(MouseButton.Right) && _placeCooldownTimer >= PlaceBlockCooldown)
            {
                _worldInteractionService.PlaceBlock();
                _placeCooldownTimer = 0f;
            }
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_systemInputHandler.IsMouseCaptured)
        {
            _lastMousePosition = position;
            return;
        }

        const float sensitivity = 0.1f;
        var offset = new Vector2(position.X - _lastMousePosition.X, _lastMousePosition.Y - position.Y);
        _lastMousePosition = position;

        offset *= sensitivity;

        _player.Camera.Yaw += offset.X;
        _player.Camera.Pitch += offset.Y;
        _player.Camera.UpdateVectors();

        _lastMousePosition = position;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int _)
        => _systemInputHandler.HandleKeyDown(key);

    public void OnClose()
    {
        _mouse.MouseMove -= OnMouseMove;
        _keyboard.KeyDown -= OnKeyDown;
    }
}