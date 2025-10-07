using Minecraft.NET;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default with
{
    Title = "Minecraft.NET",
    Size = new(1600, 900),
    VSync = false,
    API = new GraphicsAPI(ContextAPI.OpenGL, new(4, 6))
};

using var window = Window.Create(windowOptions);
using var game = new Game(window);

game.Run();