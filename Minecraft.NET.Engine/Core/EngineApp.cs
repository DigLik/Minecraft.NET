using System.Numerics;
using System.Runtime.InteropServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Core;

public sealed class EngineApp : IDisposable
{
    private readonly IWindow _window;
    private readonly IInputManager _inputManager;
    private readonly IRenderPipeline _renderPipeline;

    private readonly List<ISystem> _systems = [];
    private double _totalTime;
    private bool _isDisposed;

    public Registry Registry { get; } = new();
    public Matrix4x4 CameraMatrix { get; set; } = Matrix4x4.Identity;

    public EngineApp(IWindow window, IInputManager inputManager, IRenderPipeline renderPipeline)
    {
        _window = window;
        _inputManager = inputManager;
        _renderPipeline = renderPipeline;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClose;
    }

    public void AddSystem(ISystem system) => _systems.Add(system);

    public void Run() => _window.Run();

    private void OnLoad()
        => OnFramebufferResize(_window.FramebufferSize);

    private void OnUpdate(double deltaTime)
    {
        _totalTime += deltaTime;
        var time = new Time { DeltaTime = deltaTime, TotalTime = _totalTime };

        _inputManager.OnUpdate(deltaTime);

        _renderPipeline.ClearDraws();

        foreach (var system in CollectionsMarshal.AsSpan(_systems))
            system.Update(Registry, in time);
    }

    private double _timeAccumulator = 0;
    private int _frameCounter = 0;

    private void OnRender(double deltaTime)
    {
        _timeAccumulator += deltaTime;
        _frameCounter++;

        if (_timeAccumulator >= 1)
        {
            Console.WriteLine($"FPS: {_frameCounter / _timeAccumulator}");
            _timeAccumulator -= 1;
            _frameCounter = 0;
        }

        _renderPipeline.RenderFrame(CameraMatrix);
    }

    private void OnFramebufferResize(Vector2<int> newSize)
        => _renderPipeline.OnFramebufferResize(newSize);

    private void OnClose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _window.Load -= OnLoad;
        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClose;
    }

    public void Dispose() => OnClose();
}