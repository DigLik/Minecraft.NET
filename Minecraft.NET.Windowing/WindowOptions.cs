using Silk.NET.Maths;

namespace Minecraft.NET.Windowing;

public struct WindowOptions
{
    public string Title { get; set; }
    public Vector2D<int> Size { get; set; }
    public bool VSync { get; set; }
    public double TargetFPS { get; set; }
    public double TargetUPS { get; set; }

    public int ContextVersionMajor { get; set; }
    public int ContextVersionMinor { get; set; }

    public static WindowOptions Default => new()
    {
        Title = "Window",
        Size = new(600, 400),
        VSync = false,
        TargetFPS = 0,
        TargetUPS = 100.0
    };
}