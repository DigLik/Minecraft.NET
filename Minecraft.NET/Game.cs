using Minecraft.NET.Core;
using Minecraft.NET.Graphics;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Minecraft.NET;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;
    private GL _gl = null!;

    private readonly Camera _camera;
    private readonly InputManager _inputManager;
    private readonly World _world;
    private readonly Renderer _renderer;

    private readonly FPSCounter _fpsCounter = new();

    public Game(IWindow window)
    {
        _window = window;
        _camera = new Camera(new Vector3(8, 40, 8));
        _world = new World();
        _renderer = new Renderer();
        _inputManager = new InputManager(_window, _camera);

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
        _inputManager.Initialize();
        _renderer.Load(_gl);
        _world.Initialize();

        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnUpdate(double deltaTime)
    {
        _inputManager.Update((float)deltaTime);
        _world.Update(_camera.Position);

        while (_world.TryDequeueGeneratedMesh(out var result))
        {
            var (chunk, meshData, isNewChunk) = result;

            chunk.Mesh?.Dispose();
            chunk.Mesh = null;

            if (meshData.IndexCount > 0)
            {
                var newMesh = new Mesh(meshData);
                newMesh.UploadToGpu(_gl, _renderer.InstanceVbo);
                chunk.Mesh = newMesh;
            }
            else
            {
                meshData.Dispose();
            }

            chunk.State = ChunkState.Rendered;
            if (isNewChunk)
            {
                _world.AddRenderableChunk(chunk);
            }
        }

        _window.Title = $"FPS: {_fpsCounter.FPS:F0} | Chunks: {_world.GetLoadedChunkCount()} | Renderables: {_world.GetRenderableChunks().Count} | Position: {_camera.Position}";
    }

    private void OnRender(double deltaTime)
    {
        _fpsCounter.Update(deltaTime);
        _renderer.Render(_world.GetRenderableChunks(), _camera);
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        _renderer.OnFramebufferResize(newSize);
    }

    private void OnClose() => _world.Dispose();

    public void Dispose() => _window.Dispose();

    private class FPSCounter(double updateInterval = 1.0f)
    {
        private int _frameCount;
        private double _elapsedTime;

        public readonly double UpdateInterval = updateInterval;

        public double FPS { get; private set; }

        public void Update(double deltaTime)
        {
            _frameCount++;
            _elapsedTime += deltaTime;
            if (_elapsedTime >= UpdateInterval)
            {
                FPS = _frameCount / _elapsedTime;
                _elapsedTime -= UpdateInterval;
                _frameCount = 0;
            }
        }
    }
}