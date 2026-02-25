using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics.Rendering;
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
    private readonly D3D11Context _d3d;

    private bool _isDisposed;

    public Game(
        IWindow window,
        IRenderPipeline renderPipeline,
        IInputManager inputManager,
        ChunkManager chunkManager,
        ChunkMesherService chunkMesherService,
        PhysicsService physicsService,
        World world,
        D3D11Context d3d)
    {
        _window = window;
        _renderPipeline = renderPipeline;
        _inputManager = inputManager;
        _chunkManager = chunkManager;
        _chunkMesherService = chunkMesherService;
        _physicsService = physicsService;
        _world = world;
        _d3d = d3d;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    public void Run() => _window.Run();

    private void OnLoad()
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
    {
        _d3d.Resize(newSize);
        _renderPipeline?.OnFramebufferResize(newSize);
    }

    private void OnClose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _window.Load -= OnLoad;
        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClose;

        _chunkMesherService.Dispose();
        _chunkManager.Dispose();
        _world.Dispose();
        _renderPipeline.Dispose();
        _d3d.Dispose();
    }

    public void Dispose() => OnClose();
}