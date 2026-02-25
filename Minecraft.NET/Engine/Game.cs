using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Services;
using Minecraft.NET.Windowing;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;
    private readonly IRenderPipeline _renderPipeline;
    private readonly IInputManager _inputManager;
    private readonly ChunkManager _chunkManager;
    private readonly ChunkMesherService _chunkMesherService;
    private readonly PhysicsService _physicsService;
    private readonly World _world;
    private readonly GL _gl;

    public Game(
        IWindow window,
        IRenderPipeline renderPipeline,
        IInputManager inputManager,
        ChunkManager chunkManager,
        ChunkMesherService chunkMesherService,
        PhysicsService physicsService,
        World world,
        GL gl)
    {
        _window = window;
        _renderPipeline = renderPipeline;
        _inputManager = inputManager;
        _chunkManager = chunkManager;
        _chunkMesherService = chunkMesherService;
        _physicsService = physicsService;
        _world = world;
        _gl = gl;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    public void Run() => _window.Run();

    private unsafe void OnLoad()
    {
        _world.OnLoad();
        _renderPipeline.Initialize();
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
        _chunkManager.OnUpdate(deltaTime);
        _chunkMesherService.OnUpdate(deltaTime);
        _inputManager.OnUpdate(deltaTime);
        _physicsService.OnUpdate(deltaTime);
    }

    private void OnRender(double deltaTime)
    {
        _renderPipeline.OnRender(deltaTime);
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
        => _renderPipeline?.OnFramebufferResize(newSize);

    private void OnClose()
    {
        _window.Load -= OnLoad;
        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClose;

        _world.Dispose();
        _chunkMesherService.Dispose();
        _renderPipeline.Dispose();
    }

    public void Dispose() => OnClose();
}