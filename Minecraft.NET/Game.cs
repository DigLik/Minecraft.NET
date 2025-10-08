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

    private readonly GameStatsUpdater _statsUpdater;

    public Game(IWindow window)
    {
        _window = window;
        _camera = new Camera(new Vector3d(0, 40, 0));
        _world = new World();
        _renderer = new Renderer();
        _inputManager = new InputManager(_window, _camera, _world);
        _statsUpdater = new GameStatsUpdater(_window, _camera, _world, _renderer);

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
        _statsUpdater.UpdateTitle(deltaTime);

        while (_world.TryDequeueGeneratedMesh(out var result))
        {
            var (chunk, meshData, isNewChunk) = result;

            Mesh? newMesh = null;
            if (meshData.IndexCount > 0)
            {
                newMesh = new Mesh(meshData);
                newMesh.UploadToGpu(_gl, _renderer.InstanceVbo);
            }
            else
            {
                meshData.Dispose();
            }

            var oldMesh = chunk.Mesh;
            chunk.Mesh = newMesh;
            oldMesh?.Dispose();

            chunk.State = ChunkState.Rendered;
            if (isNewChunk && newMesh != null)
                _world.AddRenderableChunk(chunk);
        }
    }

    private void OnRender(double deltaTime)
    {
        _statsUpdater.IncrementFrameCount();
        _renderer.Render(_world.GetRenderableChunksSnapshot(), _camera);
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
        => _renderer.OnFramebufferResize(newSize);

    private void OnClose() => _world.Dispose();

    public void Dispose() => _window.Dispose();

    private class GameStatsUpdater(IWindow window, Camera camera, World world, Renderer renderer, double updateInterval = 1.0)
    {
        private readonly IWindow _window = window;
        private int _frameCount;
        private double _titleUpdateTimer;
        private double _fps;

        public void IncrementFrameCount() => _frameCount++;

        public void UpdateTitle(double deltaTime)
        {
            _titleUpdateTimer += deltaTime;
            if (_titleUpdateTimer >= updateInterval)
            {
                _fps = _frameCount / _titleUpdateTimer;

                var pos = camera.Position;
                var posString = $"X: {pos.X:F1} Y: {pos.Y:F1} Z: {pos.Z:F1}";
                _window.Title =
                    $"Minecraft.NET " +
                    $"| FPS: {_fps:F0} | " +
                    $"Chunks (Visible/Meshed/Loaded): {renderer.VisibleChunkCount}/{world.GetRenderableChunkCount()}/{world.GetLoadedChunkCount()} | " +
                    $"{posString}";

                _titleUpdateTimer = 0;
                _frameCount = 0;
            }
        }
    }
}