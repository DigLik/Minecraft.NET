using Microsoft.Extensions.DependencyInjection;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.Physics;
using Minecraft.NET.Game.World.Blocks.Services;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Game.World.Meshing;
using Minecraft.NET.Graphics.Vulkan;
using Minecraft.NET.Platform.Glfw;

var services = new ServiceCollection()
    .AddSingleton<IWindow>(_ => new GlfwWindow("Minecraft.NET Engine", 1280, 720))
    .AddSingleton<IInputManager, GlfwInputManager>()
    .AddSingleton<IRenderPipeline, VulkanRenderPipeline>()
    .AddSingleton<IBlockService, BlockService>()
    .AddSingleton<IResourceService, ResourceService>()
    .AddSingleton<EngineApp>()
    .AddSingleton(_ => new WorldStorage("World1"))
    .AddSingleton<IWorldGenerator, TerrainWorldGenerator>()
    .AddSingleton<World>()
    .AddSingleton<PlayerInputSystem>()
    .AddSingleton<PhysicsSystem>()
    .AddSingleton<PlayerInteractionSystem>()
    .AddSingleton<CameraSystem>()
    .AddSingleton<ChunkRenderSystem>();

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<EngineApp>();

unsafe
{
    provider.GetRequiredService<IRenderPipeline>().Initialize([
        new(0, VertexFormat.Float3, 0),   // Position (Vector3 - 12 байт)
        new(1, VertexFormat.UInt, 12)     // PackedData (uint - 4 байта)
    ], (uint)sizeof(ChunkVertex));        // Итоговый размер: 16 байт
}

engine.AddSystem(provider.GetRequiredService<PlayerInputSystem>());
engine.AddSystem(provider.GetRequiredService<PhysicsSystem>());
engine.AddSystem(provider.GetRequiredService<PlayerInteractionSystem>());
engine.AddSystem(provider.GetRequiredService<CameraSystem>());
engine.AddSystem(provider.GetRequiredService<ChunkRenderSystem>());

engine.Registry.Create()
    .With(new TransformComponent { ChunkPosition = new(0, 0, 12), LocalPosition = new(8, 8, 8) })
    .With(new VelocityComponent())
    .With(new PlayerControlledComponent());

engine.Run();
await provider.DisposeAsync();