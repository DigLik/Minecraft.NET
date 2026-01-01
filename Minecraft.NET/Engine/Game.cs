using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Services;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;

    private readonly IRenderPipeline _renderPipeline;
    private readonly IInputManager _inputManager;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IGameStatsService _gameStatsService;

    private readonly ChunkManager _chunkManager;
    private readonly ChunkMesherService _chunkMesherService;
    private readonly PhysicsService _physicsService;
    private readonly World _world;

    private GL _gl = null!;

    public Game(
        IWindow window,
        IRenderPipeline renderPipeline,
        IInputManager inputManager,
        IPerformanceMonitor performanceMonitor,
        IGameStatsService gameStatsService,
        ChunkManager chunkManager,
        ChunkMesherService chunkMesherService,
        PhysicsService physicsService,
        World world
        )
    {
        _window = window;

        _renderPipeline = renderPipeline;
        _inputManager = inputManager;
        _performanceMonitor = performanceMonitor;
        _gameStatsService = gameStatsService;
        _chunkManager = chunkManager;
        _chunkMesherService = chunkMesherService;
        _physicsService = physicsService;
        _world = world;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    public void Run() => _window.Run();

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        var inputContext = _window.CreateInput();

        _performanceMonitor.Initialize(_gl);
        _renderPipeline.Initialize(_gl);
        _inputManager.Initialize(inputContext);

        _world.OnLoad();

        _chunkMesherService.SetDependencies(_world, _chunkManager);

        _chunkManager.SetHandlers(
            _chunkMesherService.QueueForMeshing,
            _renderPipeline.ChunkRenderer.FreeChunkMesh
        );

        _chunkMesherService.OnLoad();

        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnUpdate(double deltaTime)
    {
        _performanceMonitor.BeginCpuFrame();

        _chunkManager.OnUpdate(deltaTime);
        _chunkMesherService.OnUpdate(deltaTime);
        _inputManager.OnUpdate(deltaTime);
        _physicsService.OnUpdate(deltaTime);
        _gameStatsService.OnUpdate(deltaTime);
    }

    private void OnRender(double deltaTime)
    {
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
        _performanceMonitor.Dispose();
        _world.Dispose();
        _inputManager.Dispose();
        _chunkMesherService.Dispose();
        _renderPipeline.Dispose();
    }

    public void Dispose()
    {
        OnClose();
        _window.Dispose();
        _gl?.Dispose();
    }
}