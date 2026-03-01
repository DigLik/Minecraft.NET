using Microsoft.Extensions.DependencyInjection;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.Physics;
using Minecraft.NET.Game.World.Environment;
using Minecraft.NET.Graphics.D3D12;
using Minecraft.NET.Platform.Glfw;
using Minecraft.NET.Utils.Math;

var services = new ServiceCollection();

services.AddSingleton<IWindow>(_ => new GlfwWindow("Minecraft.NET Engine", 1280, 720));
services.AddSingleton<IInputManager, GlfwInputManager>();

services.AddSingleton<IRenderPipeline, D3D12RenderPipeline>();

services.AddSingleton<EngineApp>();

services.AddSingleton(_ => new WorldStorage("World1"));
services.AddSingleton<IWorldGenerator, FlatWorldGenerator>();
services.AddSingleton<World>();

services.AddSingleton<PlayerInputSystem>();
services.AddSingleton<PhysicsSystem>();
services.AddSingleton<PlayerInteractionSystem>();

var provider = services.BuildServiceProvider();

var engine = provider.GetRequiredService<EngineApp>();
var world = provider.GetRequiredService<World>();

await world.InitializeAsync();

engine.AddSystem(provider.GetRequiredService<PlayerInputSystem>());
engine.AddSystem(provider.GetRequiredService<PhysicsSystem>());
engine.AddSystem(provider.GetRequiredService<PlayerInteractionSystem>());

engine.Registry.Create()
    .With(new TransformComponent { Position = new Vector3<float>(8, 100, 8) })
    .With(new VelocityComponent { IsOnGround = false })
    .With(new PlayerControlledComponent { IsCreativeMode = true });

engine.Run();
await provider.DisposeAsync();