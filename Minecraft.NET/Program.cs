using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.World;
using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Player;
using Minecraft.NET.Player.Controllers;
using Minecraft.NET.Services;
using Minecraft.NET.Services.Physics;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default with
{
    Title = "Minecraft.NET",
    Size = new(1600, 900),
    VSync = false,
    API = new GraphicsAPI(ContextAPI.OpenGL, new(4, 6))
};

using var window = Window.Create(windowOptions);
var container = new DiContainer();

container.RegisterSingleton(_ => window);
container.RegisterSingleton(sp => sp.Resolve<IWindow>().CreateOpenGL());

container.RegisterSingleton(sp => new Player(new(16, 80, 16)));
container.RegisterSingleton<IPlayer>(sp => sp.Resolve<Player>());
container.RegisterSingleton<IPlayerStateProvider>(sp => sp.Resolve<Player>());
container.RegisterSingleton(_ => new CreativePlayerController());
container.RegisterSingleton(_ => new SpectatorPlayerController());
container.RegisterSingleton(_ => new CreativePhysicsStrategy());
container.RegisterSingleton(_ => new SpectatorPhysicsStrategy());

container.RegisterSingleton<IWorldStorage>(_ => new WorldStorage("world"));

container.RegisterSingleton(sp => new ChunkMesherService(sp.Resolve<GL>(), sp));
container.RegisterSingleton<IChunkMesherService>(sp => sp.Resolve<ChunkMesherService>());

container.RegisterSingleton(sp =>
{
    var mesher = sp.Resolve<IChunkMesherService>();
    ChunkMeshRequestHandler meshRequestHandler = mesher.QueueForMeshing;
    return new ChunkManager(
        sp.Resolve<IPlayerStateProvider>(),
        sp.Resolve<IWorldStorage>(),
        meshRequestHandler
    );
});
container.RegisterSingleton<IChunkManager>(sp => sp.Resolve<ChunkManager>());
container.RegisterSingleton<IChunkProvider>(sp => sp.Resolve<ChunkManager>());

container.RegisterSingleton(sp => new SceneCuller(sp.Resolve<IPlayer>(), sp.Resolve<IChunkProvider>()));

container.RegisterSingleton(sp => new RenderPipeline(sp.Resolve<GL>(), sp.Resolve<IPlayer>(), sp.Resolve<SceneCuller>(), sp.Resolve<IPerformanceMonitor>()));
container.RegisterSingleton<IRenderPipeline>(sp => sp.Resolve<RenderPipeline>());
container.RegisterSingleton<IChunkResourceProvider>(sp => sp.Resolve<RenderPipeline>());

container.RegisterSingleton<IWorld>(sp => new World(
    sp.Resolve<IChunkManager>(),
    sp.Resolve<IWorldStorage>()
));

container.RegisterSingleton<IGameModeManager>(sp =>
{
    var strategies = new Dictionary<GameMode, IPhysicsStrategy>
    {
        { GameMode.Creative, sp.Resolve<CreativePhysicsStrategy>() },
        { GameMode.Spectator, sp.Resolve<SpectatorPhysicsStrategy>() }
    };

    return new GameModeManager(
        sp.Resolve<IPlayer>(),
        sp.Resolve<IWorld>(),
        strategies);
});

container.RegisterSingleton<IPhysicsService>(sp => new PhysicsService(
    sp.Resolve<IPlayer>(),
    sp.Resolve<IGameModeManager>()
));

container.RegisterSingleton<IWorldInteractionService>(sp => new WorldInteractionService(sp.Resolve<IPlayer>(), sp.Resolve<IWorld>()));

container.RegisterSingleton<IPerformanceMonitor>(sp => new PerformanceMonitor(sp.Resolve<GL>()));

container.RegisterSingleton(sp => new GameStatsService(
    sp.Resolve<IWindow>(),
    sp.Resolve<IPlayer>(),
    sp.Resolve<IChunkManager>(),
    sp.Resolve<IRenderPipeline>(),
    sp.Resolve<IPerformanceMonitor>()
));

container.RegisterSingleton<IInputHandler>(sp => new InputManager(
    sp.Resolve<IWindow>(),
    sp.Resolve<IPlayer>(),
    sp.Resolve<IWorldInteractionService>(),
    sp.Resolve<IGameModeManager>(),
    sp.Resolve<CreativePlayerController>(),
    sp.Resolve<SpectatorPlayerController>()
));

Action<IServiceProvider> setupAction = sp =>
{
    var chunkMesherService = sp.Resolve<IChunkMesherService>();
    var world = sp.Resolve<IWorld>();
    var chunkManager = sp.Resolve<IChunkManager>();
    chunkMesherService.SetDependencies(world, chunkManager);
};

using var game = new Game(window, container, setupAction);
game.Run();