using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Common;

namespace Minecraft.NET.Player;

public class GameModeManager : IGameModeManager
{
    private readonly IPlayer _player;
    private readonly IReadOnlyDictionary<GameMode, IPhysicsStrategy> _strategies;
    private readonly List<GameMode> _availableModes;
    private int _currentIndex = 0;

    public IPhysicsStrategy CurrentPhysicsStrategy { get; private set; }
    public IWorld World { get; }

    public GameModeManager(IPlayer player, IWorld world, IReadOnlyDictionary<GameMode, IPhysicsStrategy> strategies)
    {
        _player = player;
        World = world;
        _strategies = strategies;
        _availableModes = [.. _strategies.Keys];

        if (_availableModes.Count == 0)
            throw new InvalidOperationException("No game modes registered.");

        UpdateGameMode();
    }

    public void ToggleGameMode()
    {
        _currentIndex = (_currentIndex + 1) % _availableModes.Count;
        UpdateGameMode();
    }

    private void UpdateGameMode()
    {
        var newMode = _availableModes[_currentIndex];
        _player.CurrentGameMode = newMode;
        CurrentPhysicsStrategy = _strategies[newMode];

        if (_player.CurrentGameMode == GameMode.Spectator)
        {
            _player.Velocity = Vector3d.Zero;
        }
    }
}