using Minecraft.NET.Core.Environment;
using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Services;
using Minecraft.NET.Windowing;

using Silk.NET.Maths;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;
    private readonly IInputManager _inputManager;
    private readonly PhysicsService _physicsService;
    private readonly World _world;

    private bool _isDisposed;

    public Game(
        IWindow window,
        IInputManager inputManager,
        PhysicsService physicsService,
        World world)
    {
        _window = window;
        _inputManager = inputManager;
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
        _world.OnLoad();
        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnUpdate(double deltaTime)
    {
        _inputManager.OnUpdate(deltaTime);
        _physicsService.OnUpdate(deltaTime);
    }

    private void OnRender(double deltaTime) { }

    private void OnFramebufferResize(Vector2D<int> newSize) { }

    private void OnClose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _window.Load -= OnLoad;
        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClose;

        _world.Dispose();
    }

    public void Dispose() => OnClose();
}