using Minecraft.NET.Abstractions;
using Minecraft.NET.Services;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;
    private readonly IServiceProvider _container;
    private readonly Action<IServiceProvider> _setupAction;

    private IPerformanceMonitor _performanceMonitor = null!;

    private ILifecycleHandler[] _lifecycleHandlers = [];
    private IUpdatable[] _updatables = [];
    private IRenderable[] _renderables = [];
    private IWindowResizeHandler[] _resizeHandlers = [];
    private IDisposable[] _disposables = [];

    public Game(IWindow window, IServiceProvider container, Action<IServiceProvider> setupAction)
    {
        _window = window;
        _container = container;
        _setupAction = setupAction;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    private unsafe void OnLoad()
    {
        _setupAction(_container);

        var renderPipeline = _container.Resolve<IRenderPipeline>();
        var chunkManager = _container.Resolve<IChunkManager>();
        var chunkMesherService = _container.Resolve<IChunkMesherService>();
        var inputManager = _container.Resolve<IInputHandler>();
        var physicsService = _container.Resolve<IPhysicsService>();
        var gameStatsService = _container.Resolve<GameStatsService>();
        var world = _container.Resolve<IWorld>();
        var worldStorage = _container.Resolve<IWorldStorage>();
        _performanceMonitor = _container.Resolve<IPerformanceMonitor>();

        _lifecycleHandlers = [renderPipeline, chunkManager, chunkMesherService, (ILifecycleHandler)inputManager, world, worldStorage, _performanceMonitor];
        _updatables = [renderPipeline, chunkManager, chunkMesherService, (IUpdatable)inputManager, physicsService, gameStatsService];
        _renderables = [renderPipeline, gameStatsService];
        _resizeHandlers = [renderPipeline];
        _disposables = [(IDisposable)_container];

        foreach (var service in _lifecycleHandlers)
        {
            service.OnLoad();
        }

        OnFramebufferResize(_window.FramebufferSize);
    }

    public void Run() => _window.Run();

    private void OnUpdate(double deltaTime)
    {
        _performanceMonitor.BeginCpuFrame();
        foreach (var service in _updatables)
            service.OnUpdate(deltaTime);
    }

    private void OnRender(double deltaTime)
    {
        foreach (var service in _renderables)
            service.OnRender(deltaTime);
        _performanceMonitor.EndCpuFrame();
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        foreach (var service in _resizeHandlers)
            service.OnFramebufferResize(newSize);
    }

    private void OnClose()
    {
        foreach (var service in _lifecycleHandlers.Reverse())
            service.OnClose();
    }

    public void Dispose()
    {
        for (int i = _disposables.Length - 1; i >= 0; i--)
            _disposables[i].Dispose();

        _window.Dispose();
    }
}