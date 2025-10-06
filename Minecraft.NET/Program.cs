using Minecraft.NET;
using Minecraft.NET.Core;
using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Core.World;
using Minecraft.NET.Diagnostics;
using Minecraft.NET.Graphics;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default with
{
    Title = "Minecraft.NET",
    Size = new(1280, 720),
    VSync = false,
    API = new GraphicsAPI(ContextAPI.OpenGL, new(4, 6))
};

var window = Window.Create(windowOptions);

var graphicsSettings = new GraphicsSettings { RenderDistance = 32 };
var gameSettings = new GameSettings();

var performanceMonitor = new PerformanceMonitor(100);
IWorldGenerator worldGenerator = new FlatWorldGenerator(gameSettings);
IWorldManager worldManager = new WorldManager(worldGenerator);
var renderer = new Renderer();
var camera = new Camera();

using var game = new Game(window, renderer, worldManager, performanceMonitor, camera, graphicsSettings, gameSettings);
game.Run();