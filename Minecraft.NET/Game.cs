using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Diagnostics;
using Minecraft.NET.GameObjects;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;
using Minecraft.NET.Graphics.Scene;
using Minecraft.NET.Graphics.Shaders;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Minecraft.NET;

public sealed class Game : IDisposable
{
    private GL _gl = null!;
    private readonly IWindow _window;
    private readonly Renderer _renderer;
    private readonly IWorldManager _worldManager;
    private readonly PerformanceMonitor _performanceMonitor;

    private readonly Camera _camera;
    private IKeyboard _keyboard = null!;
    private IMouse _mouse = null!;

    private bool _isMouseCaptured = false;
    private Vector2 _lastMousePosition;

    private readonly List<IRenderable> _renderables = [];
    private readonly List<IUpdateable> _updateables = [];

    private BasicShader _basicShader = null!;
    private Mesh _cubeMesh = null!;

    private readonly List<ChunkRenderObject> _chunkRenderObjects = [];

    public Game(
        IWindow window,
        Renderer renderer,
        IWorldManager worldManager,
        PerformanceMonitor performanceMonitor,
        Camera camera
    )
    {
        _window = window;
        _renderer = renderer;
        _worldManager = worldManager;
        _performanceMonitor = performanceMonitor;
        _camera = camera;

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        _window.FramebufferResize += OnFramebufferResize;
    }

    public void Run() => _window.Run();

    private void OnLoad()
    {
        _renderer.Load(_window, _camera);
        _gl = _window.CreateOpenGL();

        _renderer.Load(_window, _camera);
        _gl = _window.CreateOpenGL();

        LoadResources();
        CreateScene();

        var input = _window.CreateInput();
        _keyboard = input.Keyboards[0];
        _mouse = input.Mice[0];

        _mouse.Cursor.CursorMode = CursorMode.Normal;
        _mouse.MouseMove += OnMouseMove;
        _keyboard.KeyDown += OnKeyDown;
    }

    private void LoadResources()
    {
        _basicShader = new BasicShader(_gl);
        _cubeMesh = null!;
    }
    private void CreateScene()
    {
        var orangeMaterial = new BasicMaterial(_basicShader);

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                var columnPosition = new Vector2(x, z);

                var column = _worldManager.GetChunkColumn(columnPosition);

                if (column != null)
                {
                    var chunkRenderObject = new ChunkRenderObject(
                        _gl,
                        column,
                        orangeMaterial,
                        columnPosition
                    );

                    _renderables.Add(chunkRenderObject);
                    _chunkRenderObjects.Add(chunkRenderObject);
                }
            }
        }

        _camera.Position = new Vector3(0, 70.0f, 5.0f);
        _camera.Pitch = -15.0f;
        _camera.UpdateVectors();
    }

    private void OnUpdate(double delta)
    {
        HandleInput((float)delta);

        foreach (var updateable in _updateables)
            updateable.Update((float)delta);
    }
    private void HandleInput(float dt)
    {
        const float cameraSpeed = 5.0f;

        if (_keyboard.IsKeyPressed(Key.W))
            _camera.Position += _camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.S))
            _camera.Position -= _camera.Front * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.A))
            _camera.Position -= _camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.D))
            _camera.Position += _camera.Right * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.Space))
            _camera.Position += Vector3.UnitY * cameraSpeed * dt;
        if (_keyboard.IsKeyPressed(Key.ShiftLeft))
            _camera.Position -= Vector3.UnitY * cameraSpeed * dt;
    }
    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_isMouseCaptured)
        {
            _lastMousePosition = position;
            return;
        }

        const float sensitivity = 0.1f;
        var offset = new Vector2(position.X - _lastMousePosition.X, _lastMousePosition.Y - position.Y);
        _lastMousePosition = new Vector2(position.X, position.Y);

        offset *= sensitivity;

        _camera.Yaw += offset.X;
        _camera.Pitch += offset.Y;

        _camera.UpdateVectors();
    }
    private void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Escape)
        {
            _window.Close();
        }
        else if (key == Key.Tab)
        {
            _isMouseCaptured = !_isMouseCaptured;
            _mouse.Cursor.CursorMode = _isMouseCaptured ? CursorMode.Disabled : CursorMode.Normal;
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
        foreach (var obj in _chunkRenderObjects)
            obj.Dispose();

        _basicShader.Dispose();
        _renderer.Dispose();
    }
    public void Dispose()
    {
        _performanceMonitor.Dispose();
        _window.Dispose();
    }
}