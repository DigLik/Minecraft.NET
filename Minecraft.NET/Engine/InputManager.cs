using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Services;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public class InputManager(
    IWindow window,
    Player player,
    WorldInteractionService worldInteraction,
    GameModeManager gameModeManager,
    IReadOnlyDictionary<GameMode, IPlayerController> controllers
    ) : IInputManager
{
    private readonly Dictionary<GameMode, IPlayerController> _controllers = new(controllers);
    private readonly SystemInputHandler _systemInputHandler = new(window, gameModeManager);

    private IInputContext _inputContext = null!;
    private IKeyboard _keyboard = null!;
    private IMouse _mouse = null!;

    private Vector2 _lastMousePosition;

    private const float BreakBlockCooldown = 0.25f;
    private const float PlaceBlockCooldown = 0.2f;
    private float _breakCooldownTimer = BreakBlockCooldown;
    private float _placeCooldownTimer = PlaceBlockCooldown;

    public void Initialize(IInputContext inputContext)
    {
        _inputContext = inputContext;
        _keyboard = _inputContext.Keyboards[0];
        _mouse = _inputContext.Mice[0];
        _systemInputHandler.SetMouse(_mouse);
        _mouse.Cursor.CursorMode = CursorMode.Normal;
        _mouse.MouseMove += OnMouseMove;
        _keyboard.KeyDown += OnKeyDown;
    }

    public bool IsKeyPressed(Key key) => _keyboard.IsKeyPressed(key);
    public bool IsMouseButtonPressed(MouseButton button) => _mouse.IsButtonPressed(button);

    public void OnUpdate(double deltaTime)
    {
        if (_inputContext == null) return;

        _breakCooldownTimer += (float)deltaTime;
        _placeCooldownTimer += (float)deltaTime;

        if (_controllers.TryGetValue(player.CurrentGameMode, out var controller))
        {
            controller.HandleInput(this, player);
        }

        if (_systemInputHandler.IsMouseCaptured)
        {
            if (IsMouseButtonPressed(MouseButton.Left) && _breakCooldownTimer >= BreakBlockCooldown)
            {
                worldInteraction.BreakBlock();
                _breakCooldownTimer = 0f;
            }

            if (IsMouseButtonPressed(MouseButton.Right) && _placeCooldownTimer >= PlaceBlockCooldown)
            {
                worldInteraction.PlaceBlock();
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

        player.Camera.Yaw += offset.X;
        player.Camera.Pitch += offset.Y;
        player.Camera.UpdateVectors();

        _lastMousePosition = position;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int _)
        => _systemInputHandler.HandleKeyDown(key);

    public void Dispose()
    {
        if (_mouse != null) _mouse.MouseMove -= OnMouseMove;
        if (_keyboard != null) _keyboard.KeyDown -= OnKeyDown;
    }
}