using Silk.NET.Maths;

namespace Minecraft.NET.Windowing;

public interface IWindow : IDisposable
{
    Vector2D<int> Size { get; }
    Vector2D<int> FramebufferSize { get; }
    string Title { get; set; }
    bool IsClosing { get; }

    event Action? Load;
    event Action<double>? Update;
    event Action<double>? Render;
    event Action<Vector2D<int>>? FramebufferResize;
    event Action? Closing;

    void Run();
    void Close();

    unsafe void* Handle { get; }
}