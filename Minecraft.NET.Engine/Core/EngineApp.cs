using System.Numerics;
using System.Runtime.InteropServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Core;

public sealed class EngineApp : IDisposable
{
    private readonly IWindow _window;
    private readonly IInputManager _inputManager;
    private readonly IRenderPipeline _renderPipeline;
    private readonly List<ISystem> _systems = [];

    private double _totalTime, _timeAccumulator;
    private int _frameCounter;
    private bool _isDisposed;

    public Registry Registry { get; } = new();
    public CameraData Camera { get; set; } = new()
    {
        ViewProjection = Matrix4x4.Identity,
        InverseViewProjection = Matrix4x4.Identity,
        ChunkPosition = Vector3Int.Zero,
        LocalPosition = Vector3.Zero,
        SunDirection = new(0, 0, 1, 0)
    };

    public EngineApp(IWindow window, IInputManager inputManager, IRenderPipeline renderPipeline)
    {
        _window = window;
        _inputManager = inputManager;
        _renderPipeline = renderPipeline;

        _window.Load += () => OnFramebufferResize(_window.FramebufferSize);
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += Dispose;
    }

    public void AddSystem(ISystem system) => _systems.Add(system);
    public void Run() => _window.Run();

    private void OnUpdate(double dt)
    {
        _inputManager.OnUpdate(dt);
        _renderPipeline.ClearDraws();

        var time = new Time { DeltaTime = dt, TotalTime = _totalTime += dt };

        foreach (var system in CollectionsMarshal.AsSpan(_systems)) system.Update(Registry, in time);
    }

    private void OnRender(double dt)
    {
        if ((_timeAccumulator += dt) >= 1)
        {
            Console.WriteLine($"FPS: {++_frameCounter / _timeAccumulator}");
            _timeAccumulator -= 1;
            _frameCounter = 0;
        }
        else _frameCounter++;

        _renderPipeline.RenderFrame(Camera);
    }

    private void OnFramebufferResize(Vector2Int newSize) => _renderPipeline.OnFramebufferResize(newSize);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= Dispose;
    }
}