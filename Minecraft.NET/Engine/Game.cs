using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Services;
using Minecraft.NET.UI;
using Minecraft.NET.UI.Elements;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Minecraft.NET.Engine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;

    private readonly IRenderPipeline _renderPipeline;
    private readonly IInputManager _inputManager;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IGameStatsService _gameStatsService;

    private readonly ChunkManager _chunkManager;
    private readonly ChunkMesherService _chunkMesherService;
    private readonly PhysicsService _physicsService;
    private readonly World _world;
    private readonly UiContext _uiContext;
    private readonly FontService _fontService;

    private GL _gl = null!;

    public Game(
        IWindow window,
        IRenderPipeline renderPipeline,
        IInputManager inputManager,
        IPerformanceMonitor performanceMonitor,
        IGameStatsService gameStatsService,
        ChunkManager chunkManager,
        ChunkMesherService chunkMesherService,
        PhysicsService physicsService,
        World world,
        UiContext uiContext,
        FontService fontService
    )
    {
        _window = window;

        _renderPipeline = renderPipeline;
        _inputManager = inputManager;
        _performanceMonitor = performanceMonitor;
        _gameStatsService = gameStatsService;
        _chunkManager = chunkManager;
        _chunkMesherService = chunkMesherService;
        _physicsService = physicsService;
        _world = world;
        _uiContext = uiContext;
        _fontService = fontService;

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
        var inputContext = _window.CreateInput();

        _fontService.Initialize(_gl);

        _performanceMonitor.Initialize(_gl);
        _renderPipeline.Initialize(_gl);
        _inputManager.Initialize(inputContext);

        _world.OnLoad();

        _chunkMesherService.SetDependencies(_world, _chunkManager);

        _chunkManager.SetHandlers(
            _chunkMesherService.QueueForMeshing,
            _renderPipeline.ChunkRenderer.FreeChunkMesh
        );

        _chunkMesherService.OnLoad();

        var fpsLabel = new Label("FPS: ...") { FontSize = 28 };
        var chunkLabel = new Label("Chunks: ...") { FontSize = 28 };
        var posLabel = new Label("XYZ: ...") { FontSize = 28 };
        var exitButton = new UI.Elements.Button
        {
            Style =
            {
                Color = new(0, 0, 1, 1),
                HoverColor = new(1, 0, 0, 1),
                BorderRadius = 10
            },
            Children = { new Label("Exit") },
            OnClick = _window.Close
        };

        if (_gameStatsService is GameStatsService stats)
        {
            stats.FpsLabel = fpsLabel;
            stats.ChunkLabel = chunkLabel;
            stats.PosLabel = posLabel;
        }

        var hud = new Panel
        {
            Style =
            {
                Width = float.NaN, Height = float.NaN,
                JustifyContent = Alignment.Start,
                AlignItems = Alignment.Start,
                Padding = new Vector4(10),
            },
            Children =
            {
                new Stack(LayoutDirection.Column, gap: 10)
                {
                    Style =
                    {
                        Color = new Vector4(0, 0, 0, 0.5f),
                        HoverColor = new Vector4(0, 0, 0, 0.5f),
                        BorderRadius = 10.0f,
                        Padding = new Vector4(10)
                    },
                    Children =
                    {
                        fpsLabel, chunkLabel, posLabel,
                        new Stack(LayoutDirection.Row, gap: 10)
                        {
                            Children = { exitButton }
                        }
                    }
                }
            }
        };
        _uiContext.SetRoot(hud);

        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnUpdate(double deltaTime)
    {
        _performanceMonitor.BeginCpuFrame();

        _chunkManager.OnUpdate(deltaTime);
        _chunkMesherService.OnUpdate(deltaTime);
        _inputManager.OnUpdate(deltaTime);
        _physicsService.OnUpdate(deltaTime);
        _gameStatsService.OnUpdate(deltaTime);
        _uiContext.Update(_inputManager.Mouse);
    }

    private void OnRender(double deltaTime)
    {
        _renderPipeline.OnRender(deltaTime);
        _gameStatsService.OnRender(deltaTime);
        _performanceMonitor.EndCpuFrame();
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
        => _renderPipeline?.OnFramebufferResize(newSize);

    private void OnClose()
    {
        _performanceMonitor.Dispose();
        _world.Dispose();
        _inputManager.Dispose();
        _chunkMesherService.Dispose();
        _renderPipeline.Dispose();
    }

    public void Dispose()
    {
        OnClose();
        _gl?.Dispose();
    }
}