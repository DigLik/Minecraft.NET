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
using Minecraft.NET.Utils.Math;

var services = new ServiceCollection();

services.AddSingleton<IWindow>(_ => new GlfwWindow("Minecraft.NET Engine", 1280, 720));
services.AddSingleton<IInputManager, GlfwInputManager>();
services.AddSingleton<IRenderPipeline, VulkanRenderPipeline>();

services.AddSingleton<EngineApp>();

services.AddSingleton(_ => new WorldStorage("World1"));
services.AddSingleton<IWorldGenerator, TerrainWorldGenerator>();
services.AddSingleton<World>();

services.AddSingleton<PlayerInputSystem>();
services.AddSingleton<PhysicsSystem>();
services.AddSingleton<PlayerInteractionSystem>();
services.AddSingleton<CameraSystem>();
services.AddSingleton<ChunkRenderSystem>();

var provider = services.BuildServiceProvider();

var engine = provider.GetRequiredService<EngineApp>();
var world = provider.GetRequiredService<World>();

await World.InitializeAsync();

unsafe
{
    var renderPipeline = provider.GetRequiredService<IRenderPipeline>();
    renderPipeline.Initialize(
    [
        new(0, VertexFormat.Float4, 0),
        new(1, VertexFormat.Float2, 16),
        new(2, VertexFormat.Int, 24),
        new(3, VertexFormat.Float4, 28),
        new(4, VertexFormat.Int, 44),
        new(5, VertexFormat.Float4, 48)
    ], (uint)sizeof(ChunkVertex));
}

engine.AddSystem(provider.GetRequiredService<PlayerInputSystem>());
engine.AddSystem(provider.GetRequiredService<PhysicsSystem>());
engine.AddSystem(provider.GetRequiredService<PlayerInteractionSystem>());
engine.AddSystem(provider.GetRequiredService<CameraSystem>());
engine.AddSystem(provider.GetRequiredService<ChunkRenderSystem>());

engine.Registry.Create()
    .With(new TransformComponent { Position = new Vector3<float>(8, 8, 200) })
    .With(new VelocityComponent { IsOnGround = false })
    .With(new PlayerControlledComponent { IsCreativeMode = false });

engine.Run();
await provider.DisposeAsync();