using Microsoft.Extensions.DependencyInjection;
using Minecraft.NET.Character;
using Minecraft.NET.Character.Controllers;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Engine;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Rendering;
using Minecraft.NET.Graphics.Rendering.Passes;
using Minecraft.NET.Services;
using Minecraft.NET.Services.Physics;
using Minecraft.NET.UI;
using Silk.NET.Windowing;

var services = new ServiceCollection();

services.AddSingleton(Window.Create(WindowOptions.Default with
{
    Title = "Minecraft.NET",
    Size = new(1200, 800),
    VSync = false,
    API = new GraphicsAPI(ContextAPI.OpenGL, new(4, 6)),
}));

services.AddSingleton(new Player(new(16, 80, 16)));
services.AddSingleton(_ => new WorldStorage("world"));
services.AddSingleton<IWorldGenerator, TerrainWorldGenerator>();
services.AddSingleton<FrameContext>();
services.AddSingleton<RenderSettings>();
services.AddSingleton<GameModeManager>();
services.AddSingleton<RenderResources>();

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
    new Dictionary<GameMode, IPhysicsStrategy>
        {
            { GameMode.Creative, new CreativePhysicsStrategy() },
            { GameMode.Spectator, new SpectatorPhysicsStrategy() }
        });

services.AddSingleton<IReadOnlyDictionary<GameMode, IPlayerController>>(provider =>
    new Dictionary<GameMode, IPlayerController>
        {
            { GameMode.Creative, provider.GetRequiredService<CreativePlayerController>() },
            { GameMode.Spectator, provider.GetRequiredService<SpectatorPlayerController>() }
        });

services.AddSingleton<IGlContextAccessor, GlContextAccessor>();

services.AddSingleton<FontService>();
services.AddSingleton<UiContext>();
services.AddSingleton<IRenderPipeline, RenderPipeline>();

services.AddSingleton<IRenderPass, GBufferPass>();
services.AddSingleton<IRenderPass, LightingPass>();
services.AddSingleton<IRenderPass, SmaaPass>();
services.AddSingleton<IRenderPass, UiRenderPass>();

services.AddSingleton<SceneCuller>();
services.AddSingleton<Game>();

var serviceProvider = services.BuildServiceProvider();
var window = serviceProvider.GetRequiredService<IWindow>();
var game = serviceProvider.GetRequiredService<Game>();
game.Run();

serviceProvider.Dispose();
window.Dispose();