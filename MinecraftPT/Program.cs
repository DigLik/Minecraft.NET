using Microsoft.Extensions.Logging;

using MinecraftPT.Engine.Abstractions;
using MinecraftPT.Engine.Abstractions.Graphics;
using MinecraftPT.Engine.Core;
using MinecraftPT.Game.Entities;
using MinecraftPT.Game.Physics;
using MinecraftPT.Game.World.Blocks.Services;
using MinecraftPT.Game.World.Environment;
using MinecraftPT.Game.World.Meshing;
using MinecraftPT.Graphics.Vulkan;
using MinecraftPT.Platform.Glfw;

using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;

try
{
    Console.Write("");
}
catch (IOException)
{
    Console.SetOut(TextWriter.Null);
}

try
{
    Console.Error.Write("");
}
catch (IOException)
{
    Console.SetError(TextWriter.Null);
}

Streamline.StreamlineAPI.EarlyInitStreamline();
AppLauncher.Launch();

public static class AppLauncher
{
    [Conditional("DEBUG")]
    private static void CheckDebug(ref bool isDebug) => isDebug = true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Launch()
    {
        bool isDebug = false;
        CheckDebug(ref isDebug);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(isDebug ? LogLevel.Debug : LogLevel.Warning);
        });

        var window = new GlfwWindow("MinecraftPT Engine", 1280, 720);
        var inputManager = new GlfwInputManager(window);
        
        var pipelineLogger = loggerFactory.CreateLogger<VulkanRenderPipeline>();
        var renderPipeline = Streamline.StreamlineAPI.RunSilenced(() => new VulkanRenderPipeline(window, pipelineLogger));
        
        var blockService = new BlockService();
        var resourceService = new ResourceService(blockService);
        var worldStorage = new WorldStorage("World1");
        var worldGenerator = new TerrainWorldGenerator();
        var world = new World(worldStorage, worldGenerator);

        var playerInputSystem = new PlayerInputSystem(inputManager);
        var physicsSystem = new PhysicsSystem(world);
        var playerInteractionSystem = new PlayerInteractionSystem(inputManager, world);
        
        var engineLogger = loggerFactory.CreateLogger<EngineApp>();
        var engineApp = new EngineApp(window, inputManager, renderPipeline, engineLogger);

        var cameraSystem = new CameraSystem(engineApp, window);
        var chunkRenderSystem = new ChunkRenderSystem(engineApp, renderPipeline, world, resourceService, blockService);

        unsafe
        {
            renderPipeline.Initialize([
                new(0, VertexFormat.Float3, 0),   // Position (Vector3 - 12 байт)
                new(1, VertexFormat.UInt, 12)     // PackedData (uint - 4 байта)
            ], (uint)sizeof(ChunkVertex));        // Итоговый размер: 16 байт
        }

        engineApp.AddSystem(playerInputSystem);
        engineApp.AddSystem(physicsSystem);
        engineApp.AddSystem(playerInteractionSystem);
        engineApp.AddSystem(cameraSystem);
        engineApp.AddSystem(chunkRenderSystem);

        engineApp.Registry.Create()
            .With(new TransformComponent { ChunkPosition = new(0, 0, 12), LocalPosition = new(8, 8, 8) })
            .With(new VelocityComponent())
            .With(new PlayerControlledComponent());

        engineApp.Run();

        // Ручная утилизация ресурсов
        chunkRenderSystem.Dispose();
        renderPipeline.Dispose();
        world.DisposeAsync().GetAwaiter().GetResult();
        window.Dispose();
    }
}