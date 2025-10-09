using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Services;
using Minecraft.NET.Services.Physics;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;

    private RenderPipeline _renderPipeline = null!;
    private ChunkManager _chunkManager = null!;
    private ChunkMesherService _chunkMesherService = null!;
    private InputManager _inputManager = null!;
    private PhysicsService _physicsService = null!;
    private GameStatsService _gameStatsService = null!;
    private World _world = null!;
    private WorldStorage _worldStorage = null!;
    private PerformanceMonitor _performanceMonitor = null!;

    public Game(IWindow window)
    {
        _window = window;
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    private void OnLoad()
    {
        var gl = _window.CreateOpenGL();
        var player = new Player(new(16, 80, 16));
        _worldStorage = new WorldStorage("world");
        _performanceMonitor = new PerformanceMonitor(gl);

        var creativeController = new CreativePlayerController();
        var spectatorController = new SpectatorPlayerController();
        var creativeStrategy = new CreativePhysicsStrategy();
        var spectatorStrategy = new SpectatorPhysicsStrategy();

        var physicsStrategies = new Dictionary<GameMode, IPhysicsStrategy>
        {
            { GameMode.Creative, creativeStrategy },
            { GameMode.Spectator, spectatorStrategy }
        };

        _chunkMesherService = new ChunkMesherService();
        ChunkMeshRequestHandler meshRequestHandler = _chunkMesherService.QueueForMeshing;
        _chunkManager = new ChunkManager(player, _worldStorage, meshRequestHandler);
        _world = new World(_chunkManager, _worldStorage);

        _chunkMesherService.SetDependencies(_world, _chunkManager);

        var gameModeManager = new GameModeManager(player, _world, physicsStrategies);
        _physicsService = new PhysicsService(player, gameModeManager);
        var worldInteractionService = new WorldInteractionService(player, _world);
        var sceneCuller = new SceneCuller(player, _chunkManager);

        _renderPipeline = new RenderPipeline(gl, player, sceneCuller, _performanceMonitor);

        _gameStatsService = new GameStatsService(_window, player, _chunkManager, _renderPipeline, _performanceMonitor);

        _inputManager = new InputManager(
            _window,
            player,
            worldInteractionService,
            gameModeManager,
            creativeController,
            spectatorController
        );

        _renderPipeline.OnLoad();
        _chunkMesherService.SetChunkRenderer(_renderPipeline.ChunkRenderer);

        _chunkMesherService.OnLoad();
        _inputManager.OnLoad();
        _world.OnLoad();
        _worldStorage.OnLoad();
        _performanceMonitor.OnLoad();

        OnFramebufferResize(_window.FramebufferSize);
    }

    public void Run() => _window.Run();

    private void OnUpdate(double deltaTime)
    {
        if (_performanceMonitor is null) return;

        _performanceMonitor.BeginCpuFrame();
        _chunkManager.OnUpdate(deltaTime);
        _chunkMesherService.OnUpdate(deltaTime);
        _inputManager.OnUpdate(deltaTime);
        _physicsService.OnUpdate(deltaTime);
        _gameStatsService.OnUpdate(deltaTime);
    }

    private void OnRender(double deltaTime)
    {
        if (_renderPipeline is null) return;

        _renderPipeline.OnRender(deltaTime);
        _gameStatsService.OnRender(deltaTime);
        _performanceMonitor.EndCpuFrame();
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        _renderPipeline?.OnFramebufferResize(newSize);
    }

    private void OnClose()
    {
        if (_performanceMonitor is null) return;

        _performanceMonitor.OnClose();
        _worldStorage.OnClose();
        _world.OnClose();
        _inputManager.OnClose();
        _chunkMesherService.OnClose();
        _renderPipeline.OnClose();
    }

    public void Dispose()
    {
        _chunkMesherService?.Dispose();
        _world?.Dispose();
        _chunkManager?.Dispose();
        _performanceMonitor?.Dispose();
        _worldStorage?.Dispose();
        _window.Dispose();
    }
}