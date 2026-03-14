using Microsoft.Extensions.DependencyInjection;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.Physics;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Game.World.Meshing;
using Minecraft.NET.Graphics.Vulkan;
using Minecraft.NET.Platform.Glfw;

var services = new ServiceCollection()
    .AddSingleton<IWindow>(_ => new GlfwWindow("Minecraft.NET Engine", 1280, 720))
    .AddSingleton<IInputManager, GlfwInputManager>()
    .AddSingleton<IRenderPipeline, VulkanRenderPipeline>()
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
await World.InitializeAsync();

unsafe
{
    provider.GetRequiredService<IRenderPipeline>().Initialize([
        new(0, VertexFormat.Float4, 0),   // Position (Vector4)
        new(1, VertexFormat.Int, 16),     // TextureIndex (int)
        new(2, VertexFormat.Float2, 20),  // UV (Vector2)
        new(3, VertexFormat.Int, 28),     // OverlayTextureIndex (int)
        new(4, VertexFormat.Float4, 32),  // Color (Vector4)
        new(5, VertexFormat.Float4, 48)   // OverlayColor (Vector4)
    ], (uint)sizeof(ChunkVertex));        // Итоговый размер: 64 байта
}

engine.AddSystem(provider.GetRequiredService<PlayerInputSystem>());
engine.AddSystem(provider.GetRequiredService<PhysicsSystem>());
engine.AddSystem(provider.GetRequiredService<PlayerInteractionSystem>());
engine.AddSystem(provider.GetRequiredService<CameraSystem>());
engine.AddSystem(provider.GetRequiredService<ChunkRenderSystem>());

engine.Registry.Create()
    .With(new TransformComponent { Position = new(8, 8, 200) })
    .With(new VelocityComponent())
    .With(new PlayerControlledComponent());

engine.Run();
await provider.DisposeAsync();