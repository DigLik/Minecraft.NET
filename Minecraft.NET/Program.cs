using Microsoft.Extensions.DependencyInjection;
using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Services;
using Minecraft.NET.Services.Physics;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default with
{
    Title = "Minecraft.NET",
    Size = new(1600, 900),
    VSync = false,
    API = new GraphicsAPI(ContextAPI.OpenGL, new(4, 6)),
};
var window = Window.Create(windowOptions);

var services = new ServiceCollection();

services.AddSingleton<IWindow>(window);

services.AddSingleton<Player>(new Player(new(16, 80, 16)));
services.AddSingleton<WorldStorage>(_ => new WorldStorage("world"));
services.AddSingleton<IWorldGenerator, TerrainWorldGenerator>(); // FlatWorldGenerator, TerrainWorldGenerator
services.AddSingleton<FrameContext>();
services.AddSingleton<GameModeManager>();

services.AddSingleton<PhysicsService>();
services.AddSingleton<WorldInteractionService>();
services.AddSingleton<ChunkManager>();
services.AddSingleton<World>();
services.AddSingleton<ChunkMesherService>();

services.AddSingleton<IInputManager, InputManager>();
services.AddSingleton<IChunkRenderer, ChunkRenderer>();
services.AddSingleton<IRenderPipeline, RenderPipeline>();
services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
services.AddSingleton<IGameStatsService, GameStatsService>();

services.AddSingleton<CreativePlayerController>();
services.AddSingleton<SpectatorPlayerController>();

services.AddSingleton<IReadOnlyDictionary<GameMode, IPhysicsStrategy>>(_ =>
{
    return new Dictionary<GameMode, IPhysicsStrategy>
    {
        { GameMode.Creative, new CreativePhysicsStrategy() },
        { GameMode.Spectator, new SpectatorPhysicsStrategy() }
    };
});
services.AddSingleton<IReadOnlyDictionary<GameMode, IPlayerController>>(provider =>
{
    return new Dictionary<GameMode, IPlayerController>
    {
        { GameMode.Creative, provider.GetRequiredService<CreativePlayerController>() },
        { GameMode.Spectator, provider.GetRequiredService<SpectatorPlayerController>() }
    };
});

services.AddSingleton<SceneCuller>();
services.AddSingleton<Game>();

using var serviceProvider = services.BuildServiceProvider();

var game = serviceProvider.GetRequiredService<Game>();
game.Run();