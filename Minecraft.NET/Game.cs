using Minecraft.NET.Core;
using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.World;
using Minecraft.NET.Diagnostics;
using Minecraft.NET.GameObjects;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;
using Minecraft.NET.Graphics.Scene;
using Minecraft.NET.Graphics.Shaders;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Collections.Concurrent;
using Texture = Minecraft.NET.Graphics.Texture;

namespace Minecraft.NET;

public sealed class Game : IDisposable
{
    private GL _gl = null!;
    private readonly IWindow _window;
    private readonly Renderer _renderer;
    private readonly IWorldManager _worldManager;
    private readonly PerformanceMonitor _performanceMonitor;

    private readonly Camera _camera;
    private readonly InputManager _inputManager;
    private readonly GraphicsSettings _graphicsSettings;
    private readonly GameSettings _gameSettings;

    private readonly List<IRenderable> _renderables = [];
    private readonly List<IUpdateable> _updateables = [];

    private BasicShader _basicShader = null!;
    private Texture _atlasTexture = null!;

    private readonly Dictionary<Vector2, ChunkRenderObject> _chunkRenderObjects = [];

    private Vector2 _lastPlayerChunkPosition = new(float.MaxValue);

    private readonly ConcurrentQueue<Vector2> _chunksToBuildQueue = new();
    private readonly ConcurrentQueue<MeshBuffer> _chunksToRenderQueue = new();
    private readonly List<Thread> _chunkWorkers = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public Game(
        IWindow window,
        Renderer renderer,
        IWorldManager worldManager,
        PerformanceMonitor performanceMonitor,
        Camera camera,
        GraphicsSettings graphicsSettings,
        GameSettings gameSettings
    )
    {
        _window = window;
        _renderer = renderer;
        _worldManager = worldManager;
        _performanceMonitor = performanceMonitor;
        _camera = camera;
        _graphicsSettings = graphicsSettings;
        _gameSettings = gameSettings;

        _inputManager = new InputManager(window, camera);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        _window.FramebufferResize += OnFramebufferResize;

        for (int i = 0; i < _gameSettings.MaxBackgroundThreads; i++)
        {
            var worker = new ChunkWorker(
                _worldManager, _gameSettings, _chunksToBuildQueue, _chunksToRenderQueue, _cancellationTokenSource.Token
            );
            var thread = new Thread(worker.Run) { Name = $"ChunkWorker-{i}", IsBackground = true };
            _chunkWorkers.Add(thread);
            thread.Start();
        }
    }

    public void Run() => _window.Run();

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _renderer.Load(_window, _camera, _graphicsSettings, _gameSettings);

        BlockManager.Initialize();

        LoadResources();
        CreateScene();

        _inputManager.Initialize();
    }

    private void LoadResources()
    {
        _basicShader = new BasicShader(_gl);
        _atlasTexture = new Texture(_gl, "Assets/Textures/atlas.png");
    }
    private void CreateScene()
    {
        _camera.Position = new Vector3(8, 170.0f, 8);
        _camera.Pitch = -30.0f;
        _camera.UpdateVectors();
    }

    private void OnUpdate(double delta)
    {
        _inputManager.HandleInput((float)delta);

        UpdateVisibleChunks();

        ProcessRenderQueue();
        
        foreach (var updateable in _updateables)
            updateable.Update((float)delta);
    }
    private void UpdateVisibleChunks()
    {
        var playerPos = _camera.Position;
        var currentChunkPos = new Vector2(
            MathF.Floor(playerPos.X / _gameSettings.ChunkSize),
            MathF.Floor(playerPos.Z / _gameSettings.ChunkSize)
        );

        if (currentChunkPos == _lastPlayerChunkPosition) return;
        _lastPlayerChunkPosition = currentChunkPos;

        for (int x = -_graphicsSettings.RenderDistance; x <= _graphicsSettings.RenderDistance; x++)
            for (int z = -_graphicsSettings.RenderDistance; z <= _graphicsSettings.RenderDistance; z++)
            {
                var chunkPos = new Vector2(currentChunkPos.X + x, currentChunkPos.Y + z);
                if (!_chunkRenderObjects.ContainsKey(chunkPos))
                {
                    _chunksToBuildQueue.Enqueue(chunkPos);
                    _chunkRenderObjects.Add(chunkPos, null!);
                }
            }

        var chunksToUnload = new List<Vector2>();
        foreach (var (pos, chunk) in _chunkRenderObjects)
        {
            float distance = Vector2.Distance(pos, currentChunkPos);
            if (distance > _graphicsSettings.RenderDistance + 2)
            {
                chunksToUnload.Add(pos);
                if (chunk is not null)
                {
                    _renderables.Remove(chunk);
                    chunk.Dispose();
                }
                _worldManager.UnloadChunkColumn(pos);
            }
        }

        foreach (var pos in chunksToUnload)
            _chunkRenderObjects.Remove(pos);
    }
    private void ProcessRenderQueue()
    {
        int processedCount = 0;
        const int maxMeshesPerFrame = 8;

        while (processedCount < maxMeshesPerFrame && _chunksToRenderQueue.TryDequeue(out var meshBuffer))
        {
            if (_chunkRenderObjects.ContainsKey(meshBuffer.Position))
            {
                var worldMaterial = new BasicMaterial(_basicShader) { Texture = _atlasTexture };
                var chunkRenderObject = new ChunkRenderObject(_gl, worldMaterial, meshBuffer);

                _chunkRenderObjects[meshBuffer.Position] = chunkRenderObject;
                _renderables.Add(chunkRenderObject);
            }
            else
            {
                MeshBufferPool.Return(meshBuffer);
            }
            processedCount++;
        }
    }

    private void OnRender(double delta)
    {
        _performanceMonitor.StartFrame();

        _renderer.Render(_renderables);

        if (_performanceMonitor.EndFrame(delta))
            _window.Title = _performanceMonitor.GetTitleAndReset();
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        _renderer.OnResize(newSize);
    }

    private void OnClose()
    {
        _cancellationTokenSource.Cancel();
        foreach (var thread in _chunkWorkers)
            thread.Join();

        foreach (var chunk in _chunkRenderObjects.Values)
            chunk?.Dispose();

        _basicShader.Dispose();
        _atlasTexture.Dispose();
        _renderer.Dispose();
    }
    public void Dispose()
    {
        _performanceMonitor.Dispose();
        _window.Dispose();
    }
}